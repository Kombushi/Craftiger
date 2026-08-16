namespace Craftiger.Api.Models;

public sealed record MetaResponse(
    string PackVersion,
    IReadOnlyList<string> TierNames,
    IReadOnlyList<CoilDto> Coils,
    IReadOnlyList<MachineDto> Machines,
    AtlasDto? Atlas);
