namespace Craftiger.Api.Models;

/// <summary>The Valkey server that keeps solved tables outside the process.</summary>
public sealed record ValkeyOptions
{
    /// <summary>StackExchange.Redis configuration string; required — the API refuses to start without it.</summary>
    public string? ConnectionString { get; init; }
}
