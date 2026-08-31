using Craftiger.Api.Interfaces;
using Craftiger.Api.Repositories;
using Craftiger.Solver.Models.Options;
using Craftiger.Solver.Services.Costs;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Craftiger.Api.UnitTests;

/// <summary>Boots the API once over a hand-written artifact at the current schema.</summary>
public sealed class ApiFixture : IDisposable
{
    public string Dir { get; }

    public HttpClient Client { get; }

    private readonly WebApplicationFactory<Program> _factory;

    public ApiFixture()
    {
        Dir = Path.Combine(Path.GetTempPath(), "craftiger-api-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
        WriteArtifact(
            Path.Combine(Dir, "planner.sqlite"),
            schemaVersion: PlannerArtifactRepository.SupportedSchemaVersion);
        File.WriteAllText(Path.Combine(Dir, "atlas-offsets.json"), "{}");
        _factory = Create(Dir);
        Client = _factory.CreateClient();
    }

    /// <summary>A cost solver at the API's default options over the given garage rules.</summary>
    public static CostSolverService CostSolver(IOptions<GarageRules> rules)
    {
        var options = Options.Create(new CostSolverOptions());
        return new(
            new LeafWeightService(),
            new GarageLegalityService(rules),
            new RoutePreferenceService(Options.Create(new SolverPreferences()), options),
            options);
    }

    /// <summary>Boots the API over the given artifacts with an in-memory solve store in place of Valkey; the connection string only has to be present for startup validation.</summary>
    public static WebApplicationFactory<Program> Create(string artifactsDir, FakeSolveStore? store = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ApiOptions:ArtifactsDir", artifactsDir);
            builder.UseSetting("ApiOptions:Valkey:ConnectionString", "test");
            builder.ConfigureTestServices(services => services.AddSingleton<ISolveStore>(store ?? new FakeSolveStore()));
        });

    /// <summary>The fixture graph: two tiered ingots, a nugget fraction, a wiremill recipe
    /// with a catalyst saw, a heat-1900 EBF recipe, an MV extruder recipe, an LV recipe on a
    /// late-era machine, and a frame made two ways at one price — by hand with a wearing saw,
    /// or assembled with a card that never wears.</summary>
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
                atlas_idx INTEGER NOT NULL UNIQUE,
                max_stack INTEGER);
            CREATE TABLE item_aliases(item_id TEXT NOT NULL, alias TEXT NOT NULL, UNIQUE(item_id, alias));
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
                scope TEXT,
                low_gravity INTEGER NOT NULL,
                overclock TEXT);
            CREATE TABLE recipe_inputs(
                recipe_id TEXT NOT NULL,
                item_id TEXT NOT NULL,
                amount INTEGER NOT NULL,
                slot INTEGER NOT NULL,
                catalyst INTEGER NOT NULL,
                tool INTEGER NOT NULL,
                UNIQUE(recipe_id, slot, item_id));
            CREATE TABLE recipe_outputs(recipe_id TEXT NOT NULL, item_id TEXT NOT NULL, amount INTEGER NOT NULL, chance REAL NOT NULL);
            CREATE TABLE recipe_grid(recipe_id TEXT NOT NULL, cell INTEGER NOT NULL, slot INTEGER NOT NULL, UNIQUE(recipe_id, cell));
            CREATE TABLE item_tiers(item_id TEXT PRIMARY KEY, tier INTEGER NOT NULL);
            CREATE TABLE item_parents(item_id TEXT PRIMARY KEY, parent_item_id TEXT NOT NULL, divisor REAL NOT NULL);
            CREATE TABLE item_weights(item_id TEXT PRIMARY KEY, weight REAL NOT NULL);
            CREATE TABLE machine_eras(machine TEXT PRIMARY KEY, era INTEGER, multiblock INTEGER NOT NULL);
            CREATE TABLE fuels(map TEXT NOT NULL, item_id TEXT NOT NULL, amount INTEGER NOT NULL, eu_per_unit REAL, eu_t REAL, duration_ticks INTEGER, return_item_id TEXT, return_amount INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE generator_modes(item_id TEXT NOT NULL, kind TEXT NOT NULL, fluid_id TEXT NOT NULL, per_second REAL NOT NULL, factor REAL NOT NULL);
            CREATE TABLE machine_items(map TEXT NOT NULL, item_id TEXT NOT NULL, tier INTEGER, multiblock INTEGER NOT NULL, steam INTEGER NOT NULL, era INTEGER);
            CREATE TABLE machine_props(item_id TEXT PRIMARY KEY, era INTEGER, generator_efficiency REAL, generator_eu_t INTEGER, generator_amps INTEGER, dynamo_eu_t INTEGER, dynamo_amps INTEGER, max_parallel INTEGER, boiler_eu_t INTEGER, rotor_fuel TEXT);
            CREATE TABLE machine_bonuses(item_id TEXT NOT NULL, kind TEXT NOT NULL, bonus REAL NOT NULL, multiplicative INTEGER NOT NULL, tier_axis TEXT);
            CREATE TABLE rotor_fuel_stats(item_id TEXT NOT NULL, fuel TEXT NOT NULL, efficiency REAL NOT NULL, loose_efficiency REAL NOT NULL, optimal_flow REAL NOT NULL, loose_optimal_flow REAL NOT NULL, optimal_eut REAL NOT NULL, loose_optimal_eut REAL NOT NULL);
            CREATE TABLE renewable_seeds(item_id TEXT PRIMARY KEY, kind TEXT NOT NULL);
            CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE VIRTUAL TABLE item_search USING fts5(item_id UNINDEXED, text, tokenize = 'trigram case_sensitive 1');
            """);

        db.Execute("""
            INSERT INTO items VALUES
                ('ing', 'Iron Ingot', 'ingotIronium', 0, 'ingot', 0, 64),
                ('nug', 'Iron Nugget', 'nuggetIronium', 0, 'nugget', 1, 64),
                ('wire', 'Iron Wire', NULL, 0, NULL, 2, 64),
                ('hot', 'Hot Thing', NULL, 0, NULL, 3, 64),
                ('sil', 'Silver Ingot', 'ingotSilverium', 0, 'ingot', 4, 64),
                ('rod', 'Extruded Rod', NULL, 0, NULL, 5, 64),
                ('chip', 'Late Chip', NULL, 0, NULL, 6, 64),
                ('saw', 'Test Saw', NULL, 0, NULL, 7, 1),
                ('frame', 'Frame Box', NULL, 0, NULL, 8, 64),
                ('card', 'Logic Card', NULL, 0, NULL, 9, 64);
            INSERT INTO item_aliases VALUES ('ing', 'ingotIronium'), ('ing', 'Ferrum Ingot'), ('sil', 'silberlötzinn');
            INSERT INTO item_search (item_id, text) VALUES
                ('ing', 'iron ingot'), ('ing', 'ingotironium'), ('ing', 'ferrum ingot'),
                ('nug', 'iron nugget'), ('wire', 'iron wire'), ('hot', 'hot thing'),
                ('sil', 'silver ingot'), ('sil', 'silberlötzinn'), ('rod', 'extruded rod'),
                ('chip', 'late chip'), ('saw', 'test saw'), ('frame', 'frame box'), ('card', 'logic card');
            INSERT INTO recipes VALUES
                ('r_wire', 'Wiremill', 1, NULL, NULL, 100, 32, 1, 0, NULL, 0, NULL),
                ('r_ebf', 'Electric Blast Furnace', 1, NULL, 1900, 200, 120, 1, 0, NULL, 0, NULL),
                ('r_rod', 'Extruder', 2, NULL, NULL, 100, 96, 2, 0, NULL, 0, NULL),
                ('r_chip', 'Circuit Assembly Line', 1, NULL, NULL, 100, 32, 1, 1, NULL, 0, NULL),
                ('r_frame_hand', 'Crafting Table', 0, NULL, NULL, 0, 0, 1, 0, NULL, 0, NULL),
                ('r_frame_asm', 'Assembler', 1, NULL, NULL, 64, 7, 1, 0, NULL, 0, NULL),
                ('r_farm_wire', 'Wiremill', 1, NULL, NULL, 100, 0, 1, 0, 'FACTORY', 0, 'FIXED');
            INSERT INTO recipe_inputs VALUES
                ('r_wire', 'ing', 1, 0, 0, 0),
                ('r_wire', 'saw', 1, 1, 1, 1),
                ('r_ebf', 'ing', 1, 0, 0, 0),
                ('r_rod', 'ing', 1, 0, 0, 0),
                ('r_chip', 'ing', 1, 0, 0, 0),
                ('r_frame_hand', 'ing', 2, 0, 0, 0),
                ('r_frame_hand', 'saw', 1, 1, 1, 1),
                ('r_frame_asm', 'ing', 1, 0, 0, 0),
                ('r_frame_asm', 'card', 1, 1, 1, 0);
            INSERT INTO recipe_outputs VALUES
                ('r_wire', 'wire', 2, 1.0),
                ('r_farm_wire', 'wire', 8, 1.0),
                ('r_ebf', 'hot', 1, 1.0),
                ('r_rod', 'rod', 1, 1.0),
                ('r_chip', 'chip', 1, 1.0),
                ('r_frame_hand', 'frame', 2, 1.0),
                ('r_frame_asm', 'frame', 1, 1.0);
            INSERT INTO recipe_grid VALUES
                ('r_frame_hand', 0, 0),
                ('r_frame_hand', 3, 0),
                ('r_frame_hand', 4, 1);
            INSERT INTO item_tiers VALUES ('ing', 0), ('sil', 1);
            INSERT INTO item_parents VALUES ('nug', 'ing', 9.0);
            INSERT INTO machine_eras VALUES
                ('Wiremill', 0, 0),
                ('Electric Blast Furnace', 1, 1),
                ('Extruder', NULL, 0),
                ('Circuit Assembly Line', 3, 0),
                ('Assembler', 1, 0);
            INSERT INTO machine_items VALUES ('Wiremill', 'wiremill-lv', 1, 0, 0, 0);
            INSERT INTO machine_props VALUES ('wiremill-lv', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
            INSERT INTO renewable_seeds VALUES ('ing', 'WORLD');
            """);
        db.Execute(
            "INSERT INTO meta VALUES ('schema_version', @Version), " +
            "('pack_version', 'test-pack'), ('build_id', 'test-build'), " +
            "('tier_names', '[\"Steam\",\"LV\",\"MV\",\"HV\"]'), " +
            "('tier_voltages', '[0,32,128,512]'), " +
            "('coils', '[{\"Name\":\"Cupronickel\",\"MaxHeat\":1800,\"Tier\":1},{\"Name\":\"Kanthal\",\"MaxHeat\":2700,\"Tier\":2}]'), " +
            "('atlas_width', '192'), ('atlas_height', '32'), ('atlas_cell', '32'), " +
            "('steam', '{\"SteamFluidIds\":[\"f~IC2~ic2steam\"],\"DistilledWaterId\":null,\"EuPerLiter\":0.5,\"WaterPerSteam\":160}'), " +
            "('environment', '{\"CleanroomItemId\":\"clean\",\"CleanroomEra\":3,\"LowGravityEra\":3}')",
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
