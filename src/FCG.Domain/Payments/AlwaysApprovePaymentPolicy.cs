namespace FCG.Domain.Payments;

/// <summary>
/// Simulação de processamento de pagamento que sempre aprova. A Fase 2 do Tech Challenge
/// pede apenas a simulação do pagamento, sem integração com um gateway real.
/// </summary>
public sealed class AlwaysApprovePaymentPolicy : IPaymentApprovalPolicy
{
    public bool IsApproved(Guid userId, Guid gameId, decimal price) => true;
}
