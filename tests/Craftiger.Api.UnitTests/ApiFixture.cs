using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Craftiger.Api.UnitTests;

/// <summary>Boots the API once over a hand-written schema-v4 artifact.</summary>
public sealed class ApiFixture : IDisposable
{
    public string Dir { get; }

    public HttpClient Client { get; }

    private readonly WebApplicationFactory<Program> _factory;

    public ApiFixture()
    {
        Dir = Path.Combine(Path.GetTempPath(), "craftiger-api-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
        WriteArtifact(Path.Combine(Dir, "planner.sqlite"), schemaVersion: 6);
        File.WriteAllText(Path.Combine(Dir, "atlas-offsets.json"), "{}");
        _factory = Create(Dir);
        Client = _factory.CreateClient();
    }

    public static WebApplicationFactory<Program> Create(string artifactsDir) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ApiOptions:ArtifactsDir", artifactsDir));

    /// <summary>The fixture graph: two tiered ingots, a nugget fraction, a wiremill recipe
    /// with a catalyst saw, a heat-1900 EBF recipe, an MV extruder recipe, and an LV recipe
    /// on a late-era machine.</summary>
    public static void WriteArtifact(string path, int schemaVersion)
    {
        using var db = new SqliteConnection($"Data Source={path}");
        db.Open();
        db.Execute("""
            CREATE TABLE items(
                id TEXT PRIMARY KEY,
                name_en TEXT NOT NULL,
                oredict TEXT,
                is_fluid INTEGER NOT NULL,
                leaf_class TEXT,
                atlas_idx INTEGER NOT NULL UNIQUE);
            CREATE TABLE item_aliases(item_id TEXT NOT NULL, alias TEXT NOT NULL, UNIQUE(item_id, alias));
            CREATE TABLE recipes(
                id TEXT PRIMARY KEY,
                machine TEXT NOT NULL,
                tier INTEGER NOT NULL,
                multi_tier INTEGER,
                heat INTEGER,
                duration_ticks INTEGER NOT NULL,
                eu_t INTEGER NOT NULL);
            CREATE TABLE recipe_inputs(
                recipe_id TEXT NOT NULL,
                item_id TEXT NOT NULL,
                amount INTEGER NOT NULL,
                slot INTEGER NOT NULL,
                catalyst INTEGER NOT NULL,
                UNIQUE(recipe_id, slot, item_id));
            CREATE TABLE recipe_outputs(recipe_id TEXT NOT NULL, item_id TEXT NOT NULL, amount INTEGER NOT NULL, chance REAL NOT NULL);
            CREATE TABLE item_tiers(item_id TEXT PRIMARY KEY, tier INTEGER NOT NULL);
            CREATE TABLE item_parents(item_id TEXT PRIMARY KEY, parent_item_id TEXT NOT NULL, divisor REAL NOT NULL);
            CREATE TABLE item_weights(item_id TEXT PRIMARY KEY, weight REAL NOT NULL);
            CREATE TABLE machine_eras(machine TEXT PRIMARY KEY, era INTEGER, multiblock INTEGER NOT NULL);
            CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE VIRTUAL TABLE item_search USING fts5(item_id UNINDEXED, text, tokenize = 'trigram case_sensitive 1');
            """);

        db.Execute("""
            INSERT INTO items VALUES
                ('ing', 'Iron Ingot', 'ingotIronium', 0, 'ingot', 0),
                ('nug', 'Iron Nugget', 'nuggetIronium', 0, 'nugget', 1),
                ('wire', 'Iron Wire', NULL, 0, NULL, 2),
                ('hot', 'Hot Thing', NULL, 0, NULL, 3),
                ('sil', 'Silver Ingot', 'ingotSilverium', 0, 'ingot', 4),
                ('rod', 'Extruded Rod', NULL, 0, NULL, 5),
                ('chip', 'Late Chip', NULL, 0, NULL, 6),
                ('saw', 'Test Saw', NULL, 0, NULL, 7);
            INSERT INTO item_aliases VALUES ('ing', 'ingotIronium'), ('ing', 'Ferrum Ingot'), ('sil', 'silberlötzinn');
            INSERT INTO item_search (item_id, text) VALUES
                ('ing', 'iron ingot'), ('ing', 'ingotironium'), ('ing', 'ferrum ingot'),
                ('nug', 'iron nugget'), ('wire', 'iron wire'), ('hot', 'hot thing'),
                ('sil', 'silver ingot'), ('sil', 'silberlötzinn'), ('rod', 'extruded rod'),
                ('chip', 'late chip'), ('saw', 'test saw');
            INSERT INTO recipes VALUES
                ('r_wire', 'Wiremill', 1, NULL, NULL, 100, 32),
                ('r_ebf', 'Electric Blast Furnace', 1, NULL, 1900, 200, 120),
                ('r_rod', 'Extruder', 2, NULL, NULL, 100, 96),
                ('r_chip', 'Circuit Assembly Line', 1, NULL, NULL, 100, 32);
            INSERT INTO recipe_inputs VALUES
                ('r_wire', 'ing', 1, 0, 0),
                ('r_wire', 'saw', 1, 1, 1),
                ('r_ebf', 'ing', 1, 0, 0),
                ('r_rod', 'ing', 1, 0, 0),
                ('r_chip', 'ing', 1, 0, 0);
            INSERT INTO recipe_outputs VALUES
                ('r_wire', 'wire', 2, 1.0),
                ('r_ebf', 'hot', 1, 1.0),
                ('r_rod', 'rod', 1, 1.0),
                ('r_chip', 'chip', 1, 1.0);
            INSERT INTO item_tiers VALUES ('ing', 0), ('sil', 1);
            INSERT INTO item_parents VALUES ('nug', 'ing', 9.0);
            INSERT INTO machine_eras VALUES
                ('Wiremill', 0, 0),
                ('Electric Blast Furnace', 1, 1),
                ('Extruder', NULL, 0),
                ('Circuit Assembly Line', 3, 0);
            """);
        db.Execute(
            "INSERT INTO meta VALUES ('schema_version', @Version), " +
            "('pack_version', 'test-pack'), " +
            "('tier_names', '[\"Steam\",\"LV\",\"MV\",\"HV\"]'), " +
            "('coils', '[{\"Name\":\"Cupronickel\",\"MaxHeat\":1800,\"Tier\":1},{\"Name\":\"Kanthal\",\"MaxHeat\":2700,\"Tier\":2}]'), " +
            "('atlas_width', '192'), ('atlas_height', '32'), ('atlas_cell', '32')",
            new { Version = schemaVersion.ToString() });
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
