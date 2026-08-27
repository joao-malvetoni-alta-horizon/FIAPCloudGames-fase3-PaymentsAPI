using FCG.Domain.Payments;
using FCG.Domain.Payments.Interfaces;
using FCG.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Persistence.Repositories;

public class PaymentRepository(AppDbContext context) : RepositoryBase<Payment>(context), IPaymentRepository
{
    public async Task<Payment?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(p => p.EventId == eventId, cancellationToken);
    }
}
