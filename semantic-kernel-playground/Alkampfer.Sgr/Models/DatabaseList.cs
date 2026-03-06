namespace Alkampfer.Sgr.Models;

/// <summary>
/// Represents a cached list of SQL Server database names retrieved from the instance.
/// </summary>
public sealed class DatabaseList
{
    public IReadOnlyList<string> Databases { get; set; } = Array.Empty<string>();
    public DateTime RetrievedAtUtc { get; set; } = DateTime.UtcNow;
}
