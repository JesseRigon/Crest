namespace Crest.Workflows.Common.Services;

/// <inheritdoc />
public class SystemClock : ISystemClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}