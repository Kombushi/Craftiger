namespace Craftiger.Api.Models;

/// <summary>The meta environment entry as serialized by the builder.</summary>
internal sealed record EnvironmentMeta(string CleanroomItemId, int CleanroomEra, int LowGravityEra);
