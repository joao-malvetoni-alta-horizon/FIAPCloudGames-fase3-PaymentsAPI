using FCG.Domain.Shared;

namespace FCG.Domain.Payments.Interfaces;

public interface IPaymentUnitOfWork : IUnitOfWork
{
    IPaymentRepository Payments { get; }
}
