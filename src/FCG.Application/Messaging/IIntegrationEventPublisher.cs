using FiapCloudGames.Contracts;

namespace FCG.Application.Messaging;

/// <summary>
/// Publica eventos de integração em um broker de mensageria.
/// A rota de transporte (exchange e routing key) é resolvida a partir do próprio
/// evento — via <c>[IntegrationEventRoute]</c> no pacote FiapCloudGames.Contracts —
/// para que o caso de uso não conheça detalhes de infraestrutura de mensageria.
/// A implementação concreta (RabbitMQ) fica na camada de Infrastructure.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
