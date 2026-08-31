using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Services.Factory;

public sealed class AutoInfiniteService(IGarageLegalityService legality) : IAutoInfiniteService
{
    /// <summary>A worklist over the garage-legal recipes; catalysts and EU count as free because the index carries neither as a slot, so a zero-slot recipe qualifies outright.</summary>
    public AutoInfiniteItems Reach(FactoryContext context, IReadOnlySet<int> seedItems, FactoryRequest request)
    {
        var index = context.Index;
        var infinite = new bool[index.ItemCount];
        var remaining = new int[index.RecipeCount];
        var satisfied = new bool[index.SlotStart[index.RecipeCount]];
        var queue = new Queue<int>();

        void Reach(int item)
        {
            if (!infinite[item])
            {
                infinite[item] = true;
                queue.Enqueue(item);
            }
        }

        void Qualify(int recipe)
        {
            for (var o = index.OutputStart[recipe]; o < index.OutputStart[recipe + 1]; o++)
            {
                Reach(index.OutputItem[o]);
            }
        }

        for (var recipe = 0; recipe < index.RecipeCount; recipe++)
        {
            if (!legality.IsLegal(index, recipe, context.Garage)
                || !request.Admits(index.ScopeOf(recipe)))
            {
                remaining[recipe] = -1;
                continue;
            }
            remaining[recipe] = index.SlotCount(recipe);
            if (remaining[recipe] == 0)
            {
                Qualify(recipe);
            }
        }
        foreach (var seed in seedItems)
        {
            Reach(seed);
        }

        while (queue.TryDequeue(out var item))
        {
            for (var c = index.ConsumerStart[item]; c < index.ConsumerStart[item + 1]; c++)
            {
                var recipe = index.ConsumerRecipe[c];
                if (remaining[recipe] <= 0)
                {
                    continue;
                }
                for (var slot = 0; slot < index.SlotCount(recipe) && remaining[recipe] > 0; slot++)
                {
                    var position = index.SlotStart[recipe] + slot;
                    if (satisfied[position] || !index.SlotHolds(recipe, slot, item))
                    {
                        continue;
                    }
                    satisfied[position] = true;
                    if (--remaining[recipe] == 0)
                    {
                        Qualify(recipe);
                    }
                }
            }
        }
        return new AutoInfiniteItems([.. infinite]);
    }
}
