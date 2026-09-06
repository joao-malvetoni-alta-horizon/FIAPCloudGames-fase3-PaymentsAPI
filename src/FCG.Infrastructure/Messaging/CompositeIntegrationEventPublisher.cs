using FCG.Application.Messaging;
using FiapCloudGames.Contracts;

namespace FCG.Infrastructure.Messaging;

/// <summary>
/// O PaymentProcessedEvent tem dois consumidores com transportes diferentes: o
/// CatalogAPI (via RabbitMQ — adiciona o jogo à biblioteca se aprovado) e a Lambda do
/// NotificationsAPI (via SNS — envia o email de confirmação). Este publisher fan-out
/// publica nos dois; cada implementação concreta já trata sua própria falha
/// internamente (loga um warning, sem propagar — ver RabbitMqIntegrationEventPublisher
/// e SnsIntegrationEventPublisher), então uma falha em um transporte não impede a
/// publicação no outro.
/// </summary>
public sealed class CompositeIntegrationEventPublisher(
    RabbitMqIntegrationEventPublisher rabbitMqPublisher,
    SnsIntegrationEventPublisher snsPublisher) : IIntegrationEventPublisher
{
    public async Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        await rabbitMqPublisher.PublishAsync(integrationEvent, cancellationToken);
        await snsPublisher.PublishAsync(integrationEvent, cancellationToken);
    }
}
