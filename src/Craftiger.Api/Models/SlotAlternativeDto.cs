namespace Craftiger.Api.Models;

/// <summary>One alternative of a recipe input slot, priced under the current solve.</summary>
public sealed record SlotAlternativeDto(string ItemId, long Amount, double? Cost);
