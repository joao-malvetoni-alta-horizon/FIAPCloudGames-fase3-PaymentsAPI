using FCG.Infrastructure.Messaging;
using FiapCloudGames.Contracts.Payments;
using FiapCloudGames.RabbitMq.Publishers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace FCG.UnitTests.Infrastructure.Messaging;

public class RabbitMqIntegrationEventPublisherTests
{
    private readonly IRabbitMqPublisher _rabbitMqPublisher = Substitute.For<IRabbitMqPublisher>();

    private readonly ILogger<RabbitMqIntegrationEventPublisher> _logger =
        Substitute.For<ILogger<RabbitMqIntegrationEventPublisher>>();

    private readonly RabbitMqIntegrationEventPublisher _publisher;

    public RabbitMqIntegrationEventPublisherTests()
    {
        _publisher = new RabbitMqIntegrationEventPublisher(_rabbitMqPublisher, _logger);
    }

    private static PaymentProcessedEvent NewEvent() =>
        new(Guid.NewGuid(), Guid.NewGuid(), PaymentStatus.Approved);

    [Fact]
    public async Task PublishAsync_UsesTheOverloadThatCarriesHeaders()
    {
        // Arrange
        PaymentProcessedEvent paymentProcessed = NewEvent();

        // Act
        await _publisher.PublishAsync(paymentProcessed, CancellationToken.None);

        // Assert: é a sobrecarga com headers que leva o contexto de trace distribuído adiante;
        // a antiga (sem headers) não pode mais ser usada neste caminho.
        await _rabbitMqPublisher.Received(1).PublishAsync(
            PaymentsMessaging.Exchange,
            PaymentsMessaging.RoutingKeys.Status,
            paymentProcessed,
            Arg.Any<IDictionary<string, object?>>(),
            Arg.Any<CancellationToken>());

        await _rabbitMqPublisher.DidNotReceive().PublishAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<PaymentProcessedEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WithoutActiveTransaction_StillPublishesWithNonNullHeaders()
    {
        // Arrange: sem agente New Relic anexado, a injeção vira no-op e o dicionário fica vazio.
        PaymentProcessedEvent paymentProcessed = NewEvent();
        IDictionary<string, object?>? captured = null;

        await _rabbitMqPublisher.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<PaymentProcessedEvent>(),
            Arg.Do<IDictionary<string, object?>>(h => captured = h),
            Arg.Any<CancellationToken>());

        // Act
        await _publisher.PublishAsync(paymentProcessed, CancellationToken.None);

        // Assert: publicação segue normal, apenas sem header — não é erro.
        captured.ShouldNotBeNull();
        captured.ShouldBeEmpty();
    }

    [Fact]
    public void CaptureDistributedTraceHeaders_WithoutActiveTransaction_ReturnsEmptyDictionary()
    {
        // Act
        Dictionary<string, object?> headers =
            RabbitMqIntegrationEventPublisher.CaptureDistributedTraceHeaders();

        // Assert: NoOpTransaction não injeta nada, mas o dicionário nunca é nulo.
        headers.ShouldNotBeNull();
        headers.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WhenBrokerFails_SwallowsTheExceptionInsteadOfBreakingTheFlow()
    {
        // Arrange: sem Outbox, uma falha de publish não pode derrubar o processamento do evento.
        PaymentProcessedEvent paymentProcessed = NewEvent();

        _rabbitMqPublisher.PublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<PaymentProcessedEvent>(),
                Arg.Any<IDictionary<string, object?>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("broker indisponível"));

        // Act + Assert
        await Should.NotThrowAsync(() => _publisher.PublishAsync(paymentProcessed, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_WithNullEvent_Throws()
    {
        // Act + Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _publisher.PublishAsync<PaymentProcessedEvent>(null!, CancellationToken.None));
    }
}
