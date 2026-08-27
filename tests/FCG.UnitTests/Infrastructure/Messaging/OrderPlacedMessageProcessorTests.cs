using System.Text;
using System.Text.Json;
using FCG.Application.Payments.Interfaces;
using FCG.Infrastructure.Messaging;
using FiapCloudGames.Contracts.Catalog;
using FiapCloudGames.RabbitMq.Consumers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace FCG.UnitTests.Infrastructure.Messaging;

public class OrderPlacedMessageProcessorTests
{
    private readonly IProcessOrderPlacedUseCase _useCase = Substitute.For<IProcessOrderPlacedUseCase>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();

    private readonly ILogger<OrderPlacedMessageProcessor>
        _logger = Substitute.For<ILogger<OrderPlacedMessageProcessor>>();

    private readonly OrderPlacedMessageProcessor _processor;

    public OrderPlacedMessageProcessorTests()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IProcessOrderPlacedUseCase)).Returns(_useCase);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory.CreateScope().Returns(scope);

        _processor = new OrderPlacedMessageProcessor(_scopeFactory, _logger);
    }

    [Fact]
    public async Task ProcessAsync_WithValidMessage_DispatchesToUseCaseAndReturnsSuccess()
    {
        // Arrange
        var orderPlaced = new OrderPlacedEvent(Guid.NewGuid(), Guid.NewGuid(), 120m);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(orderPlaced);

        // Act
        MessageProcessingResult result = await _processor.ProcessAsync(body, CancellationToken.None);

        // Assert
        result.ShouldBe(MessageProcessingResult.Success);
        await _useCase.Received(1).ExecuteAsync(
            Arg.Is<OrderPlacedEvent>(e => e.UserId == orderPlaced.UserId && e.GameId == orderPlaced.GameId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithMalformedJson_ReturnsPoisonMessageAndDoesNotCallUseCase()
    {
        // Arrange
        byte[] body = Encoding.UTF8.GetBytes("{ isso não é json válido");

        // Act
        MessageProcessingResult result = await _processor.ProcessAsync(body, CancellationToken.None);

        // Assert
        result.ShouldBe(MessageProcessingResult.PoisonMessage);
        await _useCase.DidNotReceive().ExecuteAsync(Arg.Any<OrderPlacedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WhenUseCaseThrows_ReturnsTransientFailure()
    {
        // Arrange
        var orderPlaced = new OrderPlacedEvent(Guid.NewGuid(), Guid.NewGuid(), 120m);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(orderPlaced);

        _useCase.ExecuteAsync(Arg.Any<OrderPlacedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TimeoutException("broker indisponível")));

        // Act
        MessageProcessingResult result = await _processor.ProcessAsync(body, CancellationToken.None);

        // Assert
        result.ShouldBe(MessageProcessingResult.TransientFailure);
    }
}
