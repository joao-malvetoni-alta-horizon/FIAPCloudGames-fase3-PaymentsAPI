using FCG.Domain.Payments;

namespace FCG.API.Controllers;

/// <summary>
/// Dados para disparar manualmente o processamento de um pagamento — a mesma informação de um
/// <c>OrderPlacedEvent</c>, sem o <c>EventId</c> (gerado internamente a cada disparo).
/// </summary>
public sealed record ProcessPaymentRequest(Guid UserId, Guid GameId, decimal Price);

/// <summary>Representação de um pagamento processado retornada pela API REST.</summary>
public sealed record PaymentResponse(
    Guid Id,
    Guid EventId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static PaymentResponse From(Payment payment) => new(
        payment.Id,
        payment.EventId,
        payment.UserId,
        payment.GameId,
        payment.Price,
        payment.Status.ToString(),
        payment.CreatedAt,
        payment.UpdatedAt);
}
