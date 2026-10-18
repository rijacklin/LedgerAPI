namespace LedgerApi.Domain
{
    /// <summary>
    /// A business rule that a request failed. Returned by <see cref="LedgerRules"/> checks;
    /// <c>null</c> from a check means no violation.
    /// </summary>
    public record RuleViolation(string Code, string Message);
}
