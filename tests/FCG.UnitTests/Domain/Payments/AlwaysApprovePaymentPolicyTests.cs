using FCG.Domain.Payments;
using Shouldly;

namespace FCG.UnitTests.Domain.Payments;

public class AlwaysApprovePaymentPolicyTests
{
    [Fact]
    public void IsApproved_AlwaysReturnsTrue()
    {
        // Arrange
        var policy = new AlwaysApprovePaymentPolicy();

        // Act
        var result = policy.IsApproved(Guid.NewGuid(), Guid.NewGuid(), 199.90m);

        // Assert
        result.ShouldBeTrue();
    }
}
