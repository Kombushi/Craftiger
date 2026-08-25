namespace Craftiger.Builder.Models.Dump;

/// <summary>What one block drops when broken, without silk touch or fortune.</summary>
public sealed record DumpBlockDrop(string Id, string BlockItemId, string DropItemId, int Quantity);
