namespace Craftiger.Builder.Models.Dump;

/// <summary>A dynamo hatch's output capacity; dynamos have no conversion loss.</summary>
public sealed record DumpDynamo(string ItemId, long MaxEuOutput, long AmpsOut);
