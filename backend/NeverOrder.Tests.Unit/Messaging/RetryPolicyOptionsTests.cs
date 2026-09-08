using NeverOrder.Application.Messaging;

namespace NeverOrder.Tests.Unit.Messaging;

public class RetryPolicyOptionsTests
{
    private static readonly RetryPolicyOptions Policy = new()
    {
        MaxAttempts = 4,
        BackoffSeconds = new[] { 1, 2, 4, 8 }
    };

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    public void DelayForAttempt_grows_exponentially(int attempt, int expectedSeconds) =>
        Policy.DelayForAttempt(attempt).Should().Be(TimeSpan.FromSeconds(expectedSeconds));

    [Fact]
    public void The_final_attempt_has_no_delay_because_it_is_dead_lettered()
    {
        // With MaxAttempts = 4, attempt 4 is the last try; there is no fifth delivery to schedule.
        Policy.DelayForAttempt(4).Should().BeNull();
        Policy.DelayForAttempt(5).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_attempts_are_rejected(int attempt) =>
        Policy.DelayForAttempt(attempt).Should().BeNull();

    [Fact]
    public void A_short_ladder_repeats_its_last_step()
    {
        var policy = new RetryPolicyOptions { MaxAttempts = 5, BackoffSeconds = new[] { 1, 2 } };

        policy.DelayForAttempt(1).Should().Be(TimeSpan.FromSeconds(1));
        policy.DelayForAttempt(2).Should().Be(TimeSpan.FromSeconds(2));
        policy.DelayForAttempt(3).Should().Be(TimeSpan.FromSeconds(2));
        policy.DelayForAttempt(4).Should().Be(TimeSpan.FromSeconds(2));
        policy.DelayForAttempt(5).Should().BeNull();
    }

    [Fact]
    public void An_empty_ladder_disables_retries()
    {
        var policy = new RetryPolicyOptions { MaxAttempts = 3, BackoffSeconds = Array.Empty<int>() };

        policy.DelayForAttempt(1).Should().BeNull();
    }

    [Fact]
    public void DeclaredDelays_are_deduplicated_and_ordered_so_each_maps_to_one_ttl_queue()
    {
        var policy = new RetryPolicyOptions { BackoffSeconds = new[] { 8, 2, 2, 1 } };

        policy.DeclaredDelays.Should().Equal(1, 2, 8);
    }

    [Fact]
    public void Every_retryable_attempt_maps_to_a_declared_queue()
    {
        var declared = Policy.DeclaredDelays;

        for (var attempt = 1; attempt < Policy.MaxAttempts; attempt++)
        {
            var delay = Policy.DelayForAttempt(attempt);
            delay.Should().NotBeNull();
            declared.Should().Contain((int)delay!.Value.TotalSeconds,
                $"attempt {attempt} needs a TTL queue that the topology actually declares");
        }
    }
}
