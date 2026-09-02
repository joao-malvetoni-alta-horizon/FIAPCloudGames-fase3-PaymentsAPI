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
    /// <remarks>
    /// O <c>[Transaction]</c> é do agente APM do New Relic: o consumo da fila roda fora de
    /// qualquer requisição HTTP, então sem ele o processamento do pagamento não apareceria
    /// como transação nenhuma. Com ele, cada mensagem vira uma transação não-web —
    /// é o que dá latência/throughput/erro do fluxo de compra no dashboard. Quando o agente
    /// não está carregado (testes, `dotnet run` sem as variáveis CORECLR_*), vira no-op.
    /// </remarks>
    [Transaction]
    public async Task<MessageProcessingResult> ProcessAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        if (!TryDeserialize(body, out OrderPlacedEvent? orderPlaced))
        {
            return MessageProcessingResult.PoisonMessage;
        }

        AdicionarAtributosDeRastreio(orderPlaced);

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
    /// Anota a transação do New Relic com os identificadores do pedido, para que o trace do
    /// fluxo de "Compra de Jogo" seja localizável por evento/jogo/usuário. Só identificadores
    /// opacos (GUIDs), os mesmos que já vão para o log — nada de dado pessoal.
    /// </summary>
    private static void AdicionarAtributosDeRastreio(OrderPlacedEvent orderPlaced)
    {
        ITransaction transacao = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;

        transacao.AddCustomAttribute("fcg.orderEventId", orderPlaced.EventId.ToString());
        transacao.AddCustomAttribute("fcg.userId", orderPlaced.UserId.ToString());
        transacao.AddCustomAttribute("fcg.gameId", orderPlaced.GameId.ToString());
    }

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
