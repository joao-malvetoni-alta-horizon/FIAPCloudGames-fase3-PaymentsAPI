using FCG.Application.Messaging;
using FCG.Application.Payments.UseCases;
using FCG.Domain.Payments;
using FCG.Domain.Payments.Interfaces;
using FiapCloudGames.Contracts.Catalog;
using FiapCloudGames.Contracts.Payments;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FCG.UnitTests.Application.Payments.UseCases;

public class ProcessOrderPlacedUseCaseTests
{
    private readonly IPaymentApprovalPolicy _approvalPolicy = Substitute.For<IPaymentApprovalPolicy>();
    private readonly IPaymentUnitOfWork _unitOfWork = Substitute.For<IPaymentUnitOfWork>();
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IIntegrationEventPublisher _eventPublisher = Substitute.For<IIntegrationEventPublisher>();
    private readonly ILogger<ProcessOrderPlacedUseCase> _logger = Substitute.For<ILogger<ProcessOrderPlacedUseCase>>();
    private readonly ProcessOrderPlacedUseCase _useCase;

    public ProcessOrderPlacedUseCaseTests()
    {
        _unitOfWork.Payments.Returns(_paymentRepository);
        _paymentRepository.GetByEventIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        _useCase = new ProcessOrderPlacedUseCase(_approvalPolicy, _unitOfWork, _eventPublisher, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApproved_PublishesPaymentProcessedEventWithApprovedStatus()
    {
        // Arrange
        var orderPlaced = new OrderPlacedEvent(Guid.NewGuid(), Guid.NewGuid(), 99.90m);
        _approvalPolicy.IsApproved(orderPlaced.UserId, orderPlaced.GameId, orderPlaced.Price).Returns(true);

        // Act
        await _useCase.ExecuteAsync(orderPlaced, CancellationToken.None);

        // Assert
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<PaymentProcessedEvent>(e =>
                e.UserId == orderPlaced.UserId &&
                e.GameId == orderPlaced.GameId &&
                e.Status == PaymentStatus.Approved),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenApproved_PersistsPaymentAndCommits()
    {
        // Arrange
        var orderPlaced = new OrderPlacedEvent(Guid.NewGuid(), Guid.NewGuid(), 99.90m);
        _approvalPolicy.IsApproved(orderPlaced.UserId, orderPlaced.GameId, orderPlaced.Price).Returns(true);

        // Act
        await _useCase.ExecuteAsync(orderPlaced, CancellationToken.None);

        // Assert
        await _paymentRepository.Received(1).AddAsync(
            Arg.Is<Payment>(p =>
                p.EventId == orderPlaced.EventId &&
                p.UserId == orderPlaced.UserId &&
                p.GameId == orderPlaced.GameId &&
                p.Price == orderPlaced.Price &&
                p.Status == PaymentStatus.Approved),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRejected_PublishesPaymentProcessedEventWithRejectedStatus()
    {
        // Arrange
        var orderPlaced = new OrderPlacedEvent(Guid.NewGuid(), Guid.NewGuid(), 99.90m);
        _approvalPolicy.IsApproved(orderPlaced.UserId, orderPlaced.GameId, orderPlaced.Price).Returns(false);

        // Act
        await _useCase.ExecuteAsync(orderPlaced, CancellationToken.None);

        // Assert
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<PaymentProcessedEvent>(e => e.Status == PaymentStatus.Rejected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventAlreadyProcessed_DoesNotReRunApprovalPolicyButRepublishesStoredStatus()
    {
        // Arrange: OrderPlacedEvent reentregue pelo RabbitMQ (at-least-once) — já existe um
        // Payment persistido para este EventId.
        var orderPlaced = new OrderPlacedEvent(Guid.NewGuid(), Guid.NewGuid(), 99.90m);
        var existingPayment = Payment.Create(
            orderPlaced.EventId, orderPlaced.UserId, orderPlaced.GameId, orderPlaced.Price, PaymentStatus.Rejected);
        _paymentRepository.GetByEventIdAsync(orderPlaced.EventId, Arg.Any<CancellationToken>())
            .Returns(existingPayment);

        // Act
        await _useCase.ExecuteAsync(orderPlaced, CancellationToken.None);

        // Assert
        _approvalPolicy.DidNotReceive().IsApproved(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>());
        await _paymentRepository.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<PaymentProcessedEvent>(e => e.Status == PaymentStatus.Rejected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellationToken()
    {
        // Arrange
        var orderPlaced = new OrderPlacedEvent(Guid.NewGuid(), Guid.NewGuid(), 50m);
        using var cts = new CancellationTokenSource();

        // Act
        await _useCase.ExecuteAsync(orderPlaced, cts.Token);

        // Assert
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Any<PaymentProcessedEvent>(), cts.Token);
    }
}