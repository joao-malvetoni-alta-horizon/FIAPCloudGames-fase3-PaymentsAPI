using FCG.Application.Messaging;
using FCG.Application.Payments.Interfaces;
using FCG.Domain.Payments;
using FCG.Domain.Payments.Interfaces;
using FiapCloudGames.Contracts.Catalog;
using FiapCloudGames.Contracts.Payments;
using Microsoft.Extensions.Logging;

namespace FCG.Application.Payments.UseCases;

public partial class ProcessOrderPlacedUseCase(
    IPaymentApprovalPolicy approvalPolicy,
    IPaymentUnitOfWork unitOfWork,
    IIntegrationEventPublisher eventPublisher,
    ILogger<ProcessOrderPlacedUseCase> logger) : IProcessOrderPlacedUseCase
{
    public async Task ExecuteAsync(OrderPlacedEvent orderPlaced, CancellationToken cancellationToken = default)
    {
        var existingPayment = await unitOfWork.Payments.GetByEventIdAsync(orderPlaced.EventId, cancellationToken);

        PaymentStatus status;
        if (existingPayment is not null)
        {
            status = existingPayment.Status;
            LogEventoJaProcessado(orderPlaced.EventId, orderPlaced.UserId, orderPlaced.GameId);
        }
        else
        {
            var approved = approvalPolicy.IsApproved(orderPlaced.UserId, orderPlaced.GameId, orderPlaced.Price);
            status = approved ? PaymentStatus.Approved : PaymentStatus.Rejected;

            var payment = Payment.Create(orderPlaced.EventId, orderPlaced.UserId, orderPlaced.GameId, orderPlaced.Price,
                status);
            await unitOfWork.Payments.AddAsync(payment, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            LogPagamentoProcessadoParaUsuario(orderPlaced.UserId, orderPlaced.GameId, status);
        }

        var paymentProcessed = new PaymentProcessedEvent(orderPlaced.UserId, orderPlaced.GameId, status);

        await eventPublisher.PublishAsync(paymentProcessed, cancellationToken);

        LogPagamentoProcessadoParaUsuario(orderPlaced.UserId, orderPlaced.GameId, status);
    }

    [LoggerMessage(LogLevel.Information, "Pagamento processado para usuário {UserId}, jogo {GameId}: {Status}")]
    partial void LogPagamentoProcessadoParaUsuario(Guid userId, Guid gameId, PaymentStatus status);

    [LoggerMessage(LogLevel.Information,
        "OrderPlacedEvent {OrderEventId} (usuário {UserId}, jogo {GameId}) já processado, republicando resultado")]
    partial void LogEventoJaProcessado(Guid orderEventId, Guid userId, Guid gameId);
}