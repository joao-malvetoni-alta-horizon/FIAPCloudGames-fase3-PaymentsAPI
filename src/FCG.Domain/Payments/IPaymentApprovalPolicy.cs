namespace FCG.Domain.Payments;

/// <summary>
/// Decide se o pagamento de uma compra deve ser aprovado.
/// </summary>
public interface IPaymentApprovalPolicy
{
    bool IsApproved(Guid userId, Guid gameId, decimal price);
}
