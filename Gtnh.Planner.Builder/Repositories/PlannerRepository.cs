using Dapper;
using Gtnh.Planner.Builder.Interfaces;
using Gtnh.Planner.Builder.Models;
using Microsoft.Data.Sqlite;

namespace Gtnh.Planner.Builder.Repositories;

public sealed class PlannerRepository : IPlannerRepository
{
    public void Write(string path, PlannerData data)
    {
        File.Delete(path);
        using var db = new SqliteConnection($"Data Source={path}");
        db.Open();

        db.Execute("""
            CREATE TABLE items(
                id TEXT PRIMARY KEY,
                name_en TEXT NOT NULL,
                oredict TEXT,
                is_fluid INTEGER NOT NULL,
                leaf_class TEXT,
                atlas_idx INTEGER NOT NULL);
            CREATE TABLE item_aliases(item_id TEXT NOT NULL, alias TEXT NOT NULL);
            CREATE TABLE recipes(
                id TEXT PRIMARY KEY,
                machine TEXT NOT NULL,
                tier INTEGER NOT NULL,
                heat INTEGER,
                duration_ticks INTEGER NOT NULL,
                eu_t INTEGER NOT NULL);
            CREATE TABLE recipe_inputs(recipe_id TEXT NOT NULL, item_id TEXT NOT NULL, amount INTEGER NOT NULL);
            CREATE TABLE recipe_outputs(recipe_id TEXT NOT NULL, item_id TEXT NOT NULL, amount INTEGER NOT NULL, chance REAL NOT NULL);
            CREATE TABLE item_tiers(item_id TEXT PRIMARY KEY, tier INTEGER NOT NULL);
            CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            """);

        using var tx = db.BeginTransaction();

        db.Execute("INSERT INTO items VALUES (@Id, @Name, @Oredict, @IsFluid, @LeafClass, @AtlasIdx)",
            data.OrderedItemIds.Select((id, index) => new
            {
                Id = id,
                Name = data.Dump.NameOf(id),
                Oredict = data.Unified.PrimaryOredictByCanonical.GetValueOrDefault(id),
                IsFluid = data.Dump.Fluids.ContainsKey(id) ? 1 : 0,
                LeafClass = data.LeafClasses.GetValueOrDefault(id),
                AtlasIdx = index
            }), tx);

        db.Execute("INSERT INTO item_aliases VALUES (@Id, @Alias)",
            data.OrderedItemIds.SelectMany(id =>
                (data.Unified.AliasesByCanonical.GetValueOrDefault(id) ?? [])
                .Select(alias => new { Id = id, Alias = alias })), tx);

        db.Execute("INSERT INTO recipes VALUES (@Id, @Machine, @Tier, @Heat, @DurationTicks, @EuT)",
            data.Recipes.Select(r => new { r.Id, r.Machine, r.Tier, r.Heat, r.DurationTicks, r.EuT }), tx);

        db.Execute("INSERT INTO recipe_inputs VALUES (@RecipeId, @ItemId, @Amount)",
            data.Recipes.SelectMany(r =>
                r.Inputs.Select(i => new { RecipeId = r.Id, ItemId = i.Key, Amount = i.Value })), tx);

        db.Execute("INSERT INTO recipe_outputs VALUES (@RecipeId, @ItemId, @Amount, @Chance)",
            data.Recipes.SelectMany(r =>
                r.Outputs.Select(o => new { RecipeId = r.Id, o.ItemId, o.Amount, o.Chance })), tx);

        db.Execute("INSERT INTO item_tiers VALUES (@Key, @Value)",
            data.IngotTiers.Select(t => new { t.Key, t.Value }), tx);

        db.Execute("INSERT INTO meta VALUES (@Key, @Value)",
            data.Meta.Select(m => new { m.Key, m.Value }), tx);

        db.Execute("""
            CREATE INDEX idx_recipe_inputs_recipe ON recipe_inputs(recipe_id);
            CREATE INDEX idx_recipe_inputs_item ON recipe_inputs(item_id);
            CREATE INDEX idx_recipe_outputs_recipe ON recipe_outputs(recipe_id);
            CREATE INDEX idx_recipe_outputs_item ON recipe_outputs(item_id);
            CREATE INDEX idx_item_aliases_item ON item_aliases(item_id);
            CREATE INDEX idx_items_oredict ON items(oredict);
            """, transaction: tx);

        tx.Commit();
    }
}
