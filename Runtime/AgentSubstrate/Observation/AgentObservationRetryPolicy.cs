namespace Pitech.XR.AgentSubstrate.Observation
{
    /// <summary>
    /// Pure exponential-backoff schedule per plan §2.9. Wait 2^attempt seconds,
    /// capped at 64s, max 3 retries, then drop with a warning log.
    /// Attempt index is 1-based for the first retry.
    /// </summary>
    public static class AgentObservationRetryPolicy
    {
        public const int MaxRetries = 3;
        public const int MaxBackoffSeconds = 64;

        /// <summary>Delay in seconds for the given 1-based retry attempt.</summary>
        public static int DelaySeconds(int retryAttempt)
        {
            if (retryAttempt < 1) retryAttempt = 1;
            long exp = 1;
            for (int i = 0; i < retryAttempt && exp < MaxBackoffSeconds; i++)
            {
                exp *= 2;
            }
            if (exp > MaxBackoffSeconds) exp = MaxBackoffSeconds;
            return (int)exp;
        }
    }
}
