using Dapper;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.Repositories;

public sealed class PlannerRepository : IPlannerRepository
{
    /// <summary>Version of the artifact contract, bumped on any schema change so a reader
    /// can refuse an artifact written for a contract it does not know.</summary>
    public const int SchemaVersion = 9;

    public void Write(string path, PlannerData data)
    {
        // An interrupted earlier run can leave journal sidecars that would replay into the
        // fresh file the moment it is opened.
        foreach (var stale in new[] { path, path + "-journal", path + "-wal", path + "-shm" })
        {
            File.Delete(stale);
        }
        using var db = new SqliteConnection($"Data Source={path}");
        db.Open();

        // Written once and shipped read-only; a crash mid-build costs a rebuild, nothing more.
        db.Execute("""
            PRAGMA page_size = 8192;
            PRAGMA journal_mode = OFF;
            PRAGMA synchronous = OFF;
            """);

        db.Execute("""
            CREATE TABLE items(
                id TEXT PRIMARY KEY,
                name_en TEXT NOT NULL,
                oredict TEXT,
                is_fluid INTEGER NOT NULL,
                leaf_class TEXT,
                atlas_idx INTEGER NOT NULL UNIQUE,
                max_stack INTEGER);
            CREATE TABLE item_aliases(
                item_id TEXT NOT NULL,
                alias TEXT NOT NULL,
                UNIQUE(item_id, alias));
            CREATE TABLE recipes(
                id TEXT PRIMARY KEY,
                machine TEXT NOT NULL,
                tier INTEGER NOT NULL,
                multi_tier INTEGER,
                heat INTEGER,
                duration_ticks INTEGER NOT NULL,
                eu_t INTEGER NOT NULL,
                amps INTEGER NOT NULL,
                cleanroom INTEGER NOT NULL,
                low_gravity INTEGER NOT NULL);
            CREATE TABLE recipe_inputs(
                recipe_id TEXT NOT NULL,
                item_id TEXT NOT NULL,
                amount INTEGER NOT NULL,
                slot INTEGER NOT NULL,
                catalyst INTEGER NOT NULL,
                tool INTEGER NOT NULL,
                UNIQUE(recipe_id, slot, item_id));
            CREATE TABLE recipe_outputs(recipe_id TEXT NOT NULL, item_id TEXT NOT NULL, amount INTEGER NOT NULL, chance REAL NOT NULL);
            CREATE TABLE recipe_grid(
                recipe_id TEXT NOT NULL,
                cell INTEGER NOT NULL,
                slot INTEGER NOT NULL,
                UNIQUE(recipe_id, cell));
            CREATE TABLE item_tiers(item_id TEXT PRIMARY KEY, tier INTEGER NOT NULL);
            CREATE TABLE item_parents(
                item_id TEXT PRIMARY KEY,
                parent_item_id TEXT NOT NULL,
                divisor REAL NOT NULL);
            CREATE TABLE item_weights(item_id TEXT PRIMARY KEY, weight REAL NOT NULL);
            CREATE TABLE machine_eras(machine TEXT PRIMARY KEY, era INTEGER, multiblock INTEGER NOT NULL);
            CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE VIRTUAL TABLE item_search USING fts5(item_id UNINDEXED, text, tokenize = 'trigram case_sensitive 1');
            """);

        using var tx = db.BeginTransaction();

        db.Execute("INSERT INTO items VALUES (@Id, @Name, @Oredict, @IsFluid, @LeafClass, @AtlasIdx, @MaxStack)",
            data.OrderedItemIds.Select((id, index) => new
            {
                Id = id,
                Name = data.Dump.NameOf(id),
                Oredict = data.Unified.PrimaryOredictByCanonical.GetValueOrDefault(id),
                IsFluid = data.Dump.Fluids.ContainsKey(id) ? 1 : 0,
                LeafClass = data.LeafClasses.GetValueOrDefault(id),
                AtlasIdx = index,
                MaxStack = data.Dump.Items.TryGetValue(id, out var item) ? item.MaxStackSize : (long?)null,
            }), tx);

        db.Execute("INSERT INTO item_aliases VALUES (@Id, @Alias)",
            data.OrderedItemIds.SelectMany(id =>
                (data.Unified.AliasesByCanonical.GetValueOrDefault(id) ?? [])
                .Select(alias => new { Id = id, Alias = alias })), tx);

        // Search text is folded here with .NET's invariant Unicode lowercasing, and the reader
        // folds its query the same way: SQLite's own LIKE only folds ASCII, the trigram index
        // is told the text is already case-folded, and both paths then agree on every script.
        db.Execute("INSERT INTO item_search (item_id, text) VALUES (@Id, @Text)",
            data.OrderedItemIds.SelectMany(id =>
                (data.Unified.AliasesByCanonical.GetValueOrDefault(id) ?? [])
                .Prepend(data.Dump.NameOf(id))
                .Select(text => new { Id = id, Text = text.ToLowerInvariant() })), tx);

        db.Execute(
            "INSERT INTO recipes VALUES (@Id, @Machine, @Tier, @MultiTier, @Heat, @DurationTicks, @EuT, @Amps, @Cleanroom, @LowGravity)",
            data.Recipes.Select(r => new
            {
                r.Id, r.Machine, Tier = r.SingleBlockTier, MultiTier = r.MultiblockTier,
                r.Heat, r.DurationTicks, r.EuT, r.Amps,
                Cleanroom = r.RequiresCleanroom ? 1 : 0,
                LowGravity = r.RequiresLowGravity ? 1 : 0,
            }), tx);

        // Rows sharing a slot are alternatives; the solver takes the cheapest of them.
        // Catalyst rows never price: the solver reads only whether a slot holds a wearing tool.
        db.Execute("INSERT INTO recipe_inputs VALUES (@RecipeId, @ItemId, @Amount, @Slot, @Catalyst, @Tool)",
            data.Recipes.SelectMany(r =>
                r.Inputs
                    .Select((i, slot) => new
                    {
                        RecipeId = r.Id, ItemId = i.Key, Amount = i.Value, Slot = slot, Catalyst = 0, Tool = 0
                    })
                    .Concat(r.Choices.SelectMany((choice, index) => choice.Alternatives.Select(a => new
                    {
                        RecipeId = r.Id, a.ItemId, a.Amount, Slot = r.Inputs.Count + index, Catalyst = 0, Tool = 0
                    })))
                    .Concat(r.Catalysts.SelectMany((slot, index) => slot.Alternatives.Select(a => new
                    {
                        RecipeId = r.Id, a.ItemId, a.Amount,
                        Slot = r.Inputs.Count + r.Choices.Count + index, Catalyst = 1, Tool = a.Tool ? 1 : 0
                    })))), tx);

        db.Execute("INSERT INTO recipe_outputs VALUES (@RecipeId, @ItemId, @Amount, @Chance)",
            data.Recipes.SelectMany(r =>
                r.Outputs.Select(o => new { RecipeId = r.Id, o.ItemId, o.Amount, o.Chance })), tx);

        // The shape of a shaped crafting recipe: which input slot each filled grid cell holds.
        db.Execute("INSERT INTO recipe_grid VALUES (@RecipeId, @Cell, @Slot)",
            data.Recipes.SelectMany(r =>
                (r.Grid ?? []).Select(g => new { RecipeId = r.Id, g.Cell, g.Slot })), tx);

        db.Execute("INSERT INTO item_tiers VALUES (@Key, @Value)",
            data.MaterialTiers.Select(t => new { t.Key, t.Value }), tx);

        db.Execute("INSERT INTO item_parents VALUES (@Id, @ParentItemId, @Divisor)",
            data.ItemParents.Select(p => new { Id = p.Key, p.Value.ParentItemId, p.Value.Divisor }), tx);

        db.Execute("INSERT INTO item_weights VALUES (@Key, @Value)",
            data.LeafWeights
                .Where(w => data.LeafClasses.ContainsKey(w.Key))
                .Select(w => new { w.Key, w.Value }), tx);

        db.Execute("INSERT INTO machine_eras VALUES (@Key, @Value, @Multiblock)",
            data.MachineEras.Select(m => new
            {
                m.Key,
                m.Value,
                Multiblock = data.MultiblockMachines.Contains(m.Key) ? 1 : 0,
            }), tx);

        db.Execute("INSERT INTO meta VALUES (@Key, @Value)",
            data.Meta.Select(m => new { m.Key, m.Value }), tx);
        db.Execute("INSERT INTO meta VALUES ('schema_version', @Version)",
            new { Version = SchemaVersion.ToString() }, tx);
        // Every build is its own artifact even at the same pack and schema: a reader that keeps
        // solved tables outside the process keys them by this, never by the pack alone.
        db.Execute("INSERT INTO meta VALUES ('build_id', @BuildId)",
            new { BuildId = Guid.NewGuid().ToString("N") }, tx);

        db.Execute("""
            CREATE INDEX idx_recipe_inputs_recipe ON recipe_inputs(recipe_id);
            CREATE INDEX idx_recipe_inputs_item ON recipe_inputs(item_id);
            CREATE INDEX idx_recipe_outputs_recipe ON recipe_outputs(recipe_id);
            CREATE INDEX idx_recipe_outputs_item ON recipe_outputs(item_id);
            CREATE INDEX idx_recipe_grid_recipe ON recipe_grid(recipe_id);
            CREATE INDEX idx_item_aliases_item ON item_aliases(item_id);
            CREATE INDEX idx_items_oredict ON items(oredict);
            """, transaction: tx);

        tx.Commit();

        // Statistics let a reader's query planner see the real row counts.
        db.Execute("ANALYZE main");
    }
}
