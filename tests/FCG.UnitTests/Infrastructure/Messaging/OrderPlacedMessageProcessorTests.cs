using System.Text;
using System.Text.Json;
using FCG.Application.Payments.Interfaces;
using FCG.Infrastructure.Messaging;
using FiapCloudGames.Contracts.Catalog;
using FiapCloudGames.RabbitMq.Consumers;
using FiapCloudGames.RabbitMq.Processing;
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
        MessageProcessingResult result = await _processor.ProcessAsync(body, MessageHeaders.Empty, CancellationToken.None);

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
        MessageProcessingResult result = await _processor.ProcessAsync(body, MessageHeaders.Empty, CancellationToken.None);

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
        MessageProcessingResult result = await _processor.ProcessAsync(body, MessageHeaders.Empty, CancellationToken.None);

        // Assert
        result.ShouldBe(MessageProcessingResult.TransientFailure);
    }

    [Fact]
    public async Task ProcessAsync_WithDistributedTraceHeaders_AcceptsThemAndStillDispatches()
    {
        // Arrange: headers como o consumidor do pacote entrega — já normalizados para string.
        var orderPlaced = new OrderPlacedEvent(Guid.NewGuid(), Guid.NewGuid(), 120m);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(orderPlaced);

        IReadOnlyDictionary<string, string?> headers = MessageHeaders.Normalize(new Dictionary<string, object?>
        {
            ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            ["newrelic"] = "eyJ2IjpbMCwxXX0="
        });

        // Act
        MessageProcessingResult result = await _processor.ProcessAsync(body, headers, CancellationToken.None);

        // Assert: a aceitação do trace é no-op sem agente anexado, mas não pode atrapalhar
        // o processamento nem lançar.
        result.ShouldBe(MessageProcessingResult.Success);
        await _useCase.Received(1).ExecuteAsync(Arg.Any<OrderPlacedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithMalformedJsonAndTraceHeaders_StillReturnsPoisonMessage()
    {
        // Arrange: aceitar o trace acontece antes da desserialização; uma mensagem corrompida
        // continua sendo descartada como poison message.
        byte[] body = Encoding.UTF8.GetBytes("{ isso não é json válido");

        IReadOnlyDictionary<string, string?> headers = MessageHeaders.Normalize(new Dictionary<string, object?>
        {
            ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
        });

        // Act
        MessageProcessingResult result = await _processor.ProcessAsync(body, headers, CancellationToken.None);

        // Assert
        result.ShouldBe(MessageProcessingResult.PoisonMessage);
    }

    [Fact]
    public void GetHeaderValues_WithPresentHeader_ReturnsSingleValue()
    {
        // Arrange
        IReadOnlyDictionary<string, string?> carrier = MessageHeaders.Normalize(new Dictionary<string, object?>
        {
            ["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
        });

        // Act
        IEnumerable<string> values = OrderPlacedMessageProcessor.GetHeaderValues(carrier, "traceparent");

        // Assert
        values.ShouldBe(["00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"]);
    }

    [Fact]
    public void GetHeaderValues_IsCaseInsensitive()
    {
        // Arrange: o dicionário do pacote compara chaves sem diferenciar maiúsculas.
        IReadOnlyDictionary<string, string?> carrier = MessageHeaders.Normalize(new Dictionary<string, object?>
        {
            ["TraceParent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
        });

        // Act
        IEnumerable<string> values = OrderPlacedMessageProcessor.GetHeaderValues(carrier, "traceparent");

        // Assert
        values.Count().ShouldBe(1);
    }

    [Fact]
    public void GetHeaderValues_WithMissingHeader_ReturnsEmpty()
    {
        // Act
        IEnumerable<string> values = OrderPlacedMessageProcessor.GetHeaderValues(MessageHeaders.Empty, "traceparent");

        // Assert: coleção vazia é como o agente entende "sem contexto de trace" e inicia um novo.
        values.ShouldBeEmpty();
    }

    [Fact]
    public void GetHeaderValues_WithNullHeaderValue_ReturnsEmpty()
    {
        // Arrange
        IReadOnlyDictionary<string, string?> carrier = MessageHeaders.Normalize(new Dictionary<string, object?>
        {
            ["traceparent"] = null
        });

        // Act
        IEnumerable<string> values = OrderPlacedMessageProcessor.GetHeaderValues(carrier, "traceparent");

        // Assert
        values.ShouldBeEmpty();
    }
}
