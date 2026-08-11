using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Gtnh.Planner.Builder;

/// <summary>Writes planner.sqlite from the transformed model.</summary>
public static class PlannerWriter
{
    private static readonly string[] TierNames =
    [
        "Steam", "LV", "MV", "HV", "EV", "IV", "LuV", "ZPM",
        "UV", "UHV", "UEV", "UIV", "UMV", "UXV", "MAX"
    ];

    public static void Write(
        string path,
        Dump dump,
        UnifiedItems unified,
        List<PlannerRecipe> recipes,
        Dictionary<string, string> leafClasses,
        Dictionary<string, int> ingotTiers,
        BuilderConfig config,
        string packVersion)
    {
        File.Delete(path);
        using var db = new SqliteConnection($"Data Source={path}");
        db.Open();

        Execute(db, null, """
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

        var itemIds = CollectItemIds(recipes);
        var atlasIdx = 0;
        using (var insert = Prepare(db, tx, "INSERT INTO items VALUES ($id, $name, $oredict, $fluid, $leaf, $atlas)"))
        {
            foreach (var id in itemIds.Order(StringComparer.Ordinal))
            {
                var isFluid = dump.Fluids.ContainsKey(id);
                var name = isFluid ? dump.Fluids[id].Name : dump.Items.TryGetValue(id, out var item) ? item.Name : id;
                Bind(insert, "$id", id);
                Bind(insert, "$name", name);
                Bind(insert, "$oredict", (object?)unified.PrimaryOredictByCanonical.GetValueOrDefault(id) ?? DBNull.Value);
                Bind(insert, "$fluid", isFluid ? 1 : 0);
                Bind(insert, "$leaf", (object?)leafClasses.GetValueOrDefault(id) ?? DBNull.Value);
                Bind(insert, "$atlas", atlasIdx++);
                insert.ExecuteNonQuery();
            }
        }

        using (var insert = Prepare(db, tx, "INSERT INTO item_aliases VALUES ($id, $alias)"))
        {
            foreach (var id in itemIds)
            {
                foreach (var alias in unified.AliasesByCanonical.GetValueOrDefault(id) ?? [])
                {
                    Bind(insert, "$id", id);
                    Bind(insert, "$alias", alias);
                    insert.ExecuteNonQuery();
                }
            }
        }

        using (var insertRecipe = Prepare(db, tx, "INSERT INTO recipes VALUES ($id, $machine, $tier, $heat, $duration, $eut)"))
        using (var insertInput = Prepare(db, tx, "INSERT INTO recipe_inputs VALUES ($recipe, $item, $amount)"))
        using (var insertOutput = Prepare(db, tx, "INSERT INTO recipe_outputs VALUES ($recipe, $item, $amount, $chance)"))
        {
            foreach (var recipe in recipes)
            {
                Bind(insertRecipe, "$id", recipe.Id);
                Bind(insertRecipe, "$machine", recipe.Machine);
                Bind(insertRecipe, "$tier", recipe.Tier);
                Bind(insertRecipe, "$heat", (object?)recipe.Heat ?? DBNull.Value);
                Bind(insertRecipe, "$duration", recipe.DurationTicks);
                Bind(insertRecipe, "$eut", recipe.EuT);
                insertRecipe.ExecuteNonQuery();

                foreach (var (itemId, amount) in recipe.Inputs)
                {
                    Bind(insertInput, "$recipe", recipe.Id);
                    Bind(insertInput, "$item", itemId);
                    Bind(insertInput, "$amount", amount);
                    insertInput.ExecuteNonQuery();
                }

                foreach (var output in recipe.Outputs)
                {
                    Bind(insertOutput, "$recipe", recipe.Id);
                    Bind(insertOutput, "$item", output.ItemId);
                    Bind(insertOutput, "$amount", output.Amount);
                    Bind(insertOutput, "$chance", output.Chance);
                    insertOutput.ExecuteNonQuery();
                }
            }
        }

        using (var insert = Prepare(db, tx, "INSERT INTO item_tiers VALUES ($id, $tier)"))
        {
            foreach (var (id, tier) in ingotTiers)
            {
                Bind(insert, "$id", id);
                Bind(insert, "$tier", tier);
                insert.ExecuteNonQuery();
            }
        }

        var maxTier = recipes.Count == 0 ? 0 : recipes.Max(r => r.Tier);
        var meta = new Dictionary<string, string>
        {
            ["pack_version"] = packVersion,
            ["exporter_version"] = dump.ExporterVersion,
            ["dump_date"] = dump.ExportedAt.ToString("O"),
            ["tier_names"] = JsonSerializer.Serialize(TierNames[..Math.Min(maxTier + 1, TierNames.Length)]),
            ["coils"] = JsonSerializer.Serialize(config.Coils.Select(c => new { c.Name, c.MaxHeat, c.Tier }))
        };
        using (var insert = Prepare(db, tx, "INSERT INTO meta VALUES ($key, $value)"))
        {
            foreach (var (key, value) in meta)
            {
                Bind(insert, "$key", key);
                Bind(insert, "$value", value);
                insert.ExecuteNonQuery();
            }
        }

        Execute(db, tx, """
            CREATE INDEX idx_recipe_inputs_recipe ON recipe_inputs(recipe_id);
            CREATE INDEX idx_recipe_inputs_item ON recipe_inputs(item_id);
            CREATE INDEX idx_recipe_outputs_recipe ON recipe_outputs(recipe_id);
            CREATE INDEX idx_recipe_outputs_item ON recipe_outputs(item_id);
            CREATE INDEX idx_item_aliases_item ON item_aliases(item_id);
            CREATE INDEX idx_items_oredict ON items(oredict);
            """);

        tx.Commit();
    }

    public static HashSet<string> CollectItemIds(List<PlannerRecipe> recipes)
    {
        var ids = new HashSet<string>();
        foreach (var recipe in recipes)
        {
            ids.UnionWith(recipe.Inputs.Keys);
            foreach (var output in recipe.Outputs) ids.Add(output.ItemId);
        }
        return ids;
    }

    private static void Execute(SqliteConnection db, SqliteTransaction? tx, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static SqliteCommand Prepare(SqliteConnection db, SqliteTransaction tx, string sql)
    {
        var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return cmd;
    }

    private static void Bind(SqliteCommand cmd, string name, object value)
    {
        var existing = cmd.Parameters.Cast<SqliteParameter>().FirstOrDefault(p => p.ParameterName == name);
        if (existing is not null) existing.Value = value;
        else cmd.Parameters.AddWithValue(name, value);
    }
}