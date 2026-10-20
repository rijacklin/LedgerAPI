using LedgerApi.Services;

namespace LedgerApi.Tests;

/// <summary>
/// Unit tests for the backoff delay: after the nth failure the wait is K × unit, K random in {0 ... 2^n - 1}.
/// </summary>
public class BackoffTests
{
    private const int Samples = 2_000;

    /// <summary>
    /// Every delay is a whole multiple of the unit, between 0 and (2^n - 1) × unit.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Delay_stays_within_window_for_nth_failure(int failures)
    {
        var rng = new Random(42);
        int maxMs = ((1 << failures) - 1) * Backoff.UnitMs;

        for (int i = 0; i < Samples; i++)
        {
            double ms = Backoff.Delay(failures, rng).TotalMilliseconds;
            Assert.InRange(ms, 0, maxMs);
            Assert.Equal(0, ms % Backoff.UnitMs);
        }
    }

    /// <summary>
    /// The window doubles with each failure: the whole range 0 to (2^n - 1) × unit is reachable, not just part of it.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Delay_window_grows_exponentially(int failures)
    {
        var rng = new Random(42);
        var delays = Enumerable
            .Range(0, Samples)
            .Select(_ => Backoff.Delay(failures, rng).TotalMilliseconds)
            .ToList();

        Assert.Equal(0, delays.Min());
        Assert.Equal(((1 << failures) - 1) * Backoff.UnitMs, delays.Max());
    }
}
