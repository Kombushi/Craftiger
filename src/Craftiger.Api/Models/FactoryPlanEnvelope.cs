using Craftiger.Solver.Models.Factory;

namespace Craftiger.Api.Models;

/// <summary>A stored factory plan wrapped in the artifact identity it was solved on; a mismatch on read means recompute, never serve.</summary>
public sealed record FactoryPlanEnvelope(int SchemaVersion, string PackVersion, string BuildId, FactoryPlan Plan);
