namespace LedgerApi.Services
{
    /// <summary>
    /// Waiting rule for retrying a write that collided with another request.
    /// </summary>
    public static class Backoff
    {
        /// <summary>Total tries (the first plus retries) before the request gives up as busy.</summary>
        public const int MaxAttempts = 5;

        /// <summary>The fixed time unit that the random multiplier K scales, in milliseconds.</summary>
        public const int UnitMs = 50;

        /// <summary>
        /// Binary exponential backoff: after the nth failure, wait K × UnitMs, where K is random in
        /// {0, 1, ..., 2^n - 1}. The random choice stops colliding clients from retrying in lockstep.
        /// Inspired by 'Computer Networking: A Top-Down Approach', Kurose & Ross.
        /// </summary>
        /// <param name="failures">How many failures so far; at least 1.</param>
        public static TimeSpan Delay(int failures, Random rng) =>
            TimeSpan.FromMilliseconds(rng.Next(0, 1 << failures) * UnitMs);
    }
}
