using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FCG.Application.Payments.Interfaces;
using FiapCloudGames.Contracts.Catalog;
using FiapCloudGames.RabbitMq.Consumers;
using FiapCloudGames.RabbitMq.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NewRelic.Api.Agent;

namespace FCG.Infrastructure.Messaging;

/// <summary>
/// Desserializa e despacha mensagens de <see cref="OrderPlacedEvent"/> (publicadas pelo
/// CatalogAPI) para o <see cref="IProcessOrderPlacedUseCase"/>, resolvido num escopo de DI
/// por mensagem — o único ponto deste serviço que conhece o contêiner.
/// </summary>
public sealed class OrderPlacedMessageProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<OrderPlacedMessageProcessor> logger) : IMessageProcessor
{
    /// <summary>
    /// Processa uma mensagem de <see cref="OrderPlacedEvent"/>.
    /// </summary>
    /// <remarks>
    /// O <c>[Transaction]</c> é o que faz este trabalho existir no APM: o agente New Relic só
    /// instrumenta o <c>RabbitMQ.Client</c> até a 6.8.1 e aqui usamos a 7.2.1, então o consumo
    /// não abre transação sozinho — sem o atributo, todo o processamento de pagamento fica
    /// invisível (não entra em throughput nem em taxa de erro).
    ///
    /// Com a transação aberta, o <c>AcceptDistributedTraceHeaders</c> liga esta transação ao
    /// trace que veio do CatalogAPI pelos headers da mensagem. Precisa vir antes de qualquer
    /// trabalho, e com <see cref="TransportType.Queue"/> — é esse valor que faz o New Relic
    /// desenhar o salto como fila em vez de chamada HTTP.
    /// </remarks>
    [Transaction]
    public async Task<MessageProcessingResult> ProcessAsync(
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string?> headers,
        CancellationToken cancellationToken)
    {
        AcceptDistributedTrace(headers);

        if (!TryDeserialize(body, out OrderPlacedEvent? orderPlaced))
        {
            return MessageProcessingResult.PoisonMessage;
        }

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IProcessOrderPlacedUseCase>();
            await useCase.ExecuteAsync(orderPlaced, cancellationToken);
            return MessageProcessingResult.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Falha ao processar OrderPlacedEvent para usuário {UserId}, jogo {GameId}",
                orderPlaced.UserId,
                orderPlaced.GameId);
            return MessageProcessingResult.TransientFailure;
        }
    }

    /// <summary>
    /// Lê um header do carrier no formato que o New Relic espera (uma coleção de valores).
    /// Header ausente ou com valor nulo devolve coleção vazia — é assim que o agente entende
    /// "essa mensagem não trouxe contexto de trace" e começa um trace novo.
    /// </summary>
    internal static IEnumerable<string> GetHeaderValues(IReadOnlyDictionary<string, string?> carrier, string key) =>
        carrier.TryGetValue(key, out string? value) && value is not null ? [value] : [];

    // Sem agente New Relic anexado (testes, execução local), GetAgent() devolve um no-op e
    // a chamada não faz nada — não é erro, o processamento segue normalmente.
    private static void AcceptDistributedTrace(IReadOnlyDictionary<string, string?> headers) =>
        NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction
            .AcceptDistributedTraceHeaders(headers, GetHeaderValues, TransportType.Queue);

    private bool TryDeserialize(ReadOnlyMemory<byte> body, [NotNullWhen(true)] out OrderPlacedEvent? orderPlaced)
    {
        try
        {
            orderPlaced = JsonSerializer.Deserialize<OrderPlacedEvent>(body.Span);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Mensagem OrderPlacedEvent malformada recebida, descartando");
            orderPlaced = null;
            return false;
        }

        if (orderPlaced is not null)
        {
            return true;
        }

        logger.LogWarning("Mensagem OrderPlacedEvent vazia recebida, descartando");
        return false;
    }
}
