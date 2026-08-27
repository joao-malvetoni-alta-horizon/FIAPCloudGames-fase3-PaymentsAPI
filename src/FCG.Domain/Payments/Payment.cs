using FCG.Domain.Shared;
using FiapCloudGames.Contracts.Payments;

namespace FCG.Domain.Payments;

/// <summary>
/// Registro de um pagamento processado a partir de um <c>OrderPlacedEvent</c>. Persistido
/// principalmente para permitir idempotência: o RabbitMQ entrega mensagens no mínimo uma vez,
/// então <see cref="EventId"/> (o EventId do evento de origem) é usado para detectar reentregas.
/// </summary>
public class Payment : Entity
{
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public decimal Price { get; private set; }
    public PaymentStatus Status { get; private set; }

    protected Payment()
    {
    }

    private Payment(Guid eventId, Guid userId, Guid gameId, decimal price, PaymentStatus status)
    {
        EventId = eventId;
        UserId = userId;
        GameId = gameId;
        Price = price;
        Status = status;
    }

    public static Payment Create(Guid eventId, Guid userId, Guid gameId, decimal price, PaymentStatus status)
    {
        return new Payment(eventId, userId, gameId, price, status);
    }
}
