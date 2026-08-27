using FCG.Domain.Payments;
using FCG.Infrastructure.Persistence.Context;
using FCG.Infrastructure.Persistence.Repositories;
using FiapCloudGames.Contracts.Payments;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FCG.UnitTests.Infrastructure.Persistence.Repositories;

public class PaymentRepositoryTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetByEventIdAsync_WhenPaymentExists_ReturnsIt()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new PaymentRepository(context);
        var eventId = Guid.NewGuid();
        var payment = Payment.Create(eventId, Guid.NewGuid(), Guid.NewGuid(), 99.90m, PaymentStatus.Approved);

        await repository.AddAsync(payment);
        await context.SaveChangesAsync();

        // Act
        var found = await repository.GetByEventIdAsync(eventId);

        // Assert
        found.ShouldNotBeNull();
        found.Id.ShouldBe(payment.Id);
        found.Status.ShouldBe(PaymentStatus.Approved);
    }

    [Fact]
    public async Task GetByEventIdAsync_WhenPaymentDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext();
        var repository = new PaymentRepository(context);

        // Act
        var found = await repository.GetByEventIdAsync(Guid.NewGuid());

        // Assert
        found.ShouldBeNull();
    }
}
