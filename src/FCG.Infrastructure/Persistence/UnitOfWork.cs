using FCG.Domain.Payments.Interfaces;
using FCG.Infrastructure.Persistence.Context;

namespace FCG.Infrastructure.Persistence;

public class UnitOfWork(
    AppDbContext context,
    IPaymentRepository payments) : IPaymentUnitOfWork
{
    public IPaymentRepository Payments { get; } = payments;

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }
}
