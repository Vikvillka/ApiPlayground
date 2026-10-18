using Xunit;

namespace ApiPlayground.Tests;

public sealed class HarnessSelfTest
{
    [Fact]
    public void Controlled_target_is_ready_without_dispatching_a_request()
    {
        using var target = new InProcessControlledTarget("local-controlled-test-target");

        Assert.True(target.IsReady, "The in-process controlled target must be available before the harness self-test.");
        Assert.Equal(0, target.DispatchCount);
    }

    [Fact]
    public void Controlled_target_without_configuration_is_not_ready()
    {
        using var target = new InProcessControlledTarget();

        Assert.False(target.IsReady);
        Assert.Equal(0, target.DispatchCount);
    }
}