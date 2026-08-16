namespace Craftiger.Api.Models;

public sealed record SolveResponse(string SolveId, int PricedItems, bool Converged);
