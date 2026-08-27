using FCG.Domain.Payments;
using Shouldly;

namespace FCG.UnitTests.Domain.Payments;

public class RandomApprovalPolicyTests
{
    [Fact]
    public void IsApproved_WithHundredPercent_AlwaysApproves()
    {
        var policy = new RandomApprovalPolicy(100);

        for (var i = 0; i < 1000; i++)
        {
            policy.IsApproved(Guid.NewGuid(), Guid.NewGuid(), 199.90m).ShouldBeTrue();
        }
    }

    [Fact]
    public void IsApproved_WithZeroPercent_AlwaysRejects()
    {
        var policy = new RandomApprovalPolicy(0);

        for (var i = 0; i < 1000; i++)
        {
            policy.IsApproved(Guid.NewGuid(), Guid.NewGuid(), 199.90m).ShouldBeFalse();
        }
    }
}
