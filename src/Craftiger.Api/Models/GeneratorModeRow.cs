namespace Craftiger.Api.Models;

/// <summary>A generator_modes row of planner.sqlite as read at load.</summary>
internal sealed record GeneratorModeRow(string ItemId, string Kind, string FluidId, double PerSecond, double Factor);
