using Microsoft.Data.Sqlite;

namespace Gtnh.Planner.Builder;

/// <summary>Loads the converted NESQL dump (SQLite) into memory.</summary>
public static class DumpReader
{
    public static Dump Read(string dumpPath)
    {
        using var db = new SqliteConnection($"Data Source={dumpPath};Mode=ReadOnly");
        db.Open();

        var items = new Dictionary<string, DumpItem>();
        foreach (var r in Rows(db, """SELECT ID, LOCALIZED_NAME, MOD_ID, INTERNAL_NAME, IMAGE_FILE_PATH FROM ITEM"""))
            items[r.GetString(0)] = new DumpItem(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4));

        var fluids = new Dictionary<string, DumpFluid>();
        foreach (var r in Rows(db, """SELECT ID, LOCALIZED_NAME, MOD_ID, INTERNAL_NAME, IMAGE_FILE_PATH FROM FLUID"""))
            fluids[r.GetString(0)] = new DumpFluid(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4));

        var recipes = new List<DumpRecipe>();
        foreach (var r in Rows(db, """
            SELECT r.ID, rt.TYPE, rt.CATEGORY
            FROM RECIPE r JOIN RECIPE_TYPE rt ON rt.ID = r.RECIPE_TYPE_ID
            """))
            recipes.Add(new DumpRecipe(r.GetString(0), r.GetString(1), r.GetString(2)));

        // coil_heat metadata is authoritative; RECIPE_SPECIAL_VALUE holds the same number for EBF maps.
        var heat = new Dictionary<string, int>();
        foreach (var r in Rows(db, """
            SELECT g.RECIPE_ID, m.METADATA_VALUE
            FROM GREG_TECH_RECIPE_METADATA m
            JOIN GREG_TECH_RECIPE g ON g.ID = m.GREG_TECH_RECIPE_ID
            WHERE m.METADATA_KEY = 'coil_heat'
            """))
            heat[r.GetString(0)] = r.GetInt32(1);

        var gt = new Dictionary<string, DumpGtData>();
        foreach (var r in Rows(db, """SELECT RECIPE_ID, VOLTAGE, AMPERAGE, DURATION FROM GREG_TECH_RECIPE"""))
        {
            var id = r.GetString(0);
            gt[id] = new DumpGtData(id, r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), heat.TryGetValue(id, out var h) ? h : null);
        }

        var groupStacks = new Dictionary<string, List<DumpItemStack>>();
        foreach (var r in Rows(db, """SELECT ITEM_GROUP_ID, ITEM_STACKS_ITEM_ID, ITEM_STACKS_STACK_SIZE FROM ITEM_GROUP_ITEM_STACKS"""))
            Add(groupStacks, r.GetString(0), new DumpItemStack(r.GetString(1), r.GetInt64(2)));

        var oredict = new List<(string, string)>();
        foreach (var r in Rows(db, """SELECT NAME, ITEM_GROUP_ID FROM ORE_DICTIONARY"""))
            oredict.Add((r.GetString(0), r.GetString(1)));

        var itemInputs = new Dictionary<string, List<(long, string)>>();
        foreach (var r in Rows(db, """SELECT RECIPE_ID, ITEM_INPUTS_KEY, ITEM_INPUTS_ID FROM RECIPE_ITEM_GROUP"""))
            Add(itemInputs, r.GetString(0), (r.GetInt64(1), r.GetString(2)));

        var itemOutputs = new Dictionary<string, List<DumpItemOutput>>();
        foreach (var r in Rows(db, """
            SELECT RECIPE_ID, ITEM_OUTPUTS_VALUE_ITEM_ID, ITEM_OUTPUTS_VALUE_STACK_SIZE, ITEM_OUTPUTS_VALUE_PROBABILITY
            FROM RECIPE_ITEM_OUTPUTS WHERE ITEM_OUTPUTS_VALUE_ITEM_ID IS NOT NULL
            """))
            Add(itemOutputs, r.GetString(0),
                new DumpItemOutput(r.GetString(0), r.GetString(1), r.GetInt64(2), r.IsDBNull(3) ? 1.0 : r.GetDouble(3)));

        // Fluid input groups are single-stack in practice; excess stacks would be alternatives and are ignored.
        var fluidGroupStack = new Dictionary<string, (string FluidId, long Amount)>();
        foreach (var r in Rows(db, """SELECT FLUID_GROUP_ID, FLUID_STACKS_FLUID_ID, FLUID_STACKS_AMOUNT FROM FLUID_GROUP_FLUID_STACKS"""))
            fluidGroupStack.TryAdd(r.GetString(0), (r.GetString(1), r.GetInt64(2)));

        var fluidInputs = new Dictionary<string, List<DumpFluidInput>>();
        foreach (var r in Rows(db, """SELECT RECIPE_ID, FLUID_INPUTS_ID FROM RECIPE_FLUID_GROUP"""))
        {
            if (fluidGroupStack.TryGetValue(r.GetString(1), out var s))
                Add(fluidInputs, r.GetString(0), new DumpFluidInput(r.GetString(0), s.FluidId, s.Amount));
        }

        var fluidOutputs = new Dictionary<string, List<DumpFluidOutput>>();
        foreach (var r in Rows(db, """
            SELECT RECIPE_ID, FLUID_OUTPUTS_VALUE_FLUID_ID, FLUID_OUTPUTS_VALUE_AMOUNT, FLUID_OUTPUTS_VALUE_PROBABILITY
            FROM RECIPE_FLUID_OUTPUTS WHERE FLUID_OUTPUTS_VALUE_FLUID_ID IS NOT NULL
            """))
            Add(fluidOutputs, r.GetString(0),
                new DumpFluidOutput(r.GetString(0), r.GetString(1), r.GetInt64(2), r.IsDBNull(3) ? 1.0 : r.GetDouble(3)));

        var containers = new Dictionary<string, DumpContainer>();
        foreach (var r in Rows(db, """
            SELECT CONTAINER_ID, FLUID_STACK_FLUID_ID, FLUID_STACK_AMOUNT, EMPTY_CONTAINER_ID
            FROM FLUID_CONTAINER
            WHERE CONTAINER_ID IS NOT NULL AND FLUID_STACK_FLUID_ID IS NOT NULL AND EMPTY_CONTAINER_ID IS NOT NULL
            """))
            containers.TryAdd(r.GetString(0), new DumpContainer(r.GetString(1), r.GetInt64(2), r.GetString(3)));

        string version = "unknown";
        long createdMillis = 0;
        foreach (var r in Rows(db, """SELECT VERSION, CREATION_TIME_MILLIS FROM METADATA LIMIT 1"""))
        {
            version = r.GetString(0);
            createdMillis = r.GetInt64(1);
        }

        return new Dump
        {
            Items = items,
            Fluids = fluids,
            Recipes = recipes,
            GtByRecipeId = gt,
            GroupStacks = groupStacks,
            Oredict = oredict,
            ItemInputsByRecipe = itemInputs,
            ItemOutputsByRecipe = itemOutputs,
            FluidInputsByRecipe = fluidInputs,
            FluidOutputsByRecipe = fluidOutputs,
            ContainersByItemId = containers,
            ExporterVersion = version,
            ExportedAt = DateTimeOffset.FromUnixTimeMilliseconds(createdMillis)
        };
    }

    private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = [];
        list.Add(value);
    }

    private static IEnumerable<SqliteDataReader> Rows(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) yield return reader;
    }
}
