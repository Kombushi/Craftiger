namespace Craftiger.Builder.Models.Dump;

public sealed record DumpFluidOutput(string RecipeId, string FluidId, long Amount, double Chance);
