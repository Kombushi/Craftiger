namespace Craftiger.Builder.Models;

public sealed record DumpFluidOutput(string RecipeId, string FluidId, long Amount, double Chance);
