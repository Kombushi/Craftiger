namespace Gtnh.Planner.Builder.Models;

public sealed record DumpItemOutput(string RecipeId, string ItemId, long Size, double Chance, long Slot);
