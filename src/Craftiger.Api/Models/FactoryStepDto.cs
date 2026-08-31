namespace Craftiger.Api.Models;

/// <summary>One pipeline step as the client sends it: a recipe or generator line id, optionally pinned to one machine block and overclock level.</summary>
public sealed record FactoryStepDto(string Id, string? MachineItemId = null, int? OcSteps = null);
