namespace Gtnh.Planner.Builder.Models;

public sealed record EraSolve(
    Dictionary<string, int> Tiers,
    Dictionary<string, int> Era,
    Dictionary<string, PlannerRecipe> BestRecipe,
    HashSet<string> Seeds);
