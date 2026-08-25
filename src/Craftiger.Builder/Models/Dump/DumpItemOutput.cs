namespace Craftiger.Builder.Models.Dump;

public sealed record DumpItemOutput(string RecipeId, string ItemId, long Size, double Chance, long Slot);
