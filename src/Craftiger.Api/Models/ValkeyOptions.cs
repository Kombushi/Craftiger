namespace Craftiger.Api.Models;

/// <summary>The Valkey server that keeps solved tables outside the process (spec §8).</summary>
public sealed class ValkeyOptions
{
    /// <summary>GLIDE connection string, e.g. <c>localhost:6379</c>; required — the API refuses
    /// to start without it.</summary>
    public string? ConnectionString { get; init; }
}
