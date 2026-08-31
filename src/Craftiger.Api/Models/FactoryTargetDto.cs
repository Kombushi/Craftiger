namespace Craftiger.Api.Models;

/// <summary>One factory target as the client sends it: Kind is produce, consume or energy; Rate is units per second, or EU/t of net export; GeneratorTier is the minimum tier the exporting generators emit at.</summary>
public sealed record FactoryTargetDto(string Kind, string? ItemId, double Rate, int? GeneratorTier = null);
