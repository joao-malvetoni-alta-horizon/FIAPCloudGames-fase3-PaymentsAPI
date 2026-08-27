namespace FCG.Domain.Payments;

/// <summary>
/// Simulação de processamento de pagamento que aprova uma porcentagem configurável das
/// tentativas (padrão 90%). A Fase 2 do Tech Challenge pede apenas a simulação do pagamento,
/// sem integração com um gateway real; a aleatoriedade existe para exercitar também o caminho
/// de rejeição (<see cref="FiapCloudGames.Contracts.Payments.PaymentStatus.Rejected"/>).
/// </summary>
public sealed class RandomApprovalPolicy(int approvalPercentage = 90) : IPaymentApprovalPolicy
{
    // Random.Shared é thread-safe: o consumidor pode processar mensagens concorrentemente.
    public bool IsApproved(Guid userId, Guid gameId, decimal price)
        => Random.Shared.Next(1, 101) <= approvalPercentage;
}
