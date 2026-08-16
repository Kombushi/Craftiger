namespace Craftiger.Api.Models;

public sealed record BomRequest(
    string SolveId,
    List<BomTargetDto> Targets,
    Dictionary<string, string>? Pins);
