using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FCG.Application.Payments.Interfaces;
using FiapCloudGames.Contracts.Catalog;
using FiapCloudGames.RabbitMq.Consumers;
using FiapCloudGames.RabbitMq.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    public async Task<MessageProcessingResult> ProcessAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
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
