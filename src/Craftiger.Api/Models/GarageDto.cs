namespace Craftiger.Api.Models;

/// <summary>The garage as the client stores it: a null machine tier means not owned, coils name the installed coil per heat-gated map.</summary>
public sealed record GarageDto(
    int DefaultTier,
    Dictionary<string, int?>? Machines,
    List<string>? BuiltMultiblocks,
    Dictionary<string, string>? Coils);
