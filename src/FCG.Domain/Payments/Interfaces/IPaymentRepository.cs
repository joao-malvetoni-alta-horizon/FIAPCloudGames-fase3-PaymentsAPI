using FCG.Domain.Shared;

namespace FCG.Domain.Payments.Interfaces;

public interface IPaymentRepository : IRepository<Payment>
{
    /// <summary>Usado para idempotência: evita reprocessar um <c>OrderPlacedEvent</c> reentregue.</summary>
    Task<Payment?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
}
