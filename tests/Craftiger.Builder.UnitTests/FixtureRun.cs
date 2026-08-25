using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Services;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Craftiger.Builder.UnitTests;

/// <summary>One builder run over the fixture dump, against the settings the builder ships.</summary>
public sealed class FixtureRun : IDisposable
{
    public string PlannerPath { get; }
    private readonly string _directory;

    public FixtureRun(params KeyValuePair<string, string?>[] overrides)
    {
        _directory = Directory.CreateTempSubdirectory("craftiger-tests").FullName;
        var dumpPath = FixtureDump.Create(_directory);

        var settings = new Dictionary<string, string?>
        {
            ["BuilderOptions:DumpPath"] = dumpPath,
            ["BuilderOptions:OutputDir"] = _directory,
            ["BuilderOptions:PackVersion"] = "fixture-pack",
            ["BuilderOptions:ImagesPath"] = Path.Combine(_directory, "image.zip")
        };
        foreach (var (key, value) in overrides)
        {
            settings[key] = value;
        }

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddInMemoryCollection(settings)
            .Build();

        using var services = new ServiceCollection()
            .AddBuilderServices(configuration)
            .BuildServiceProvider();
        if (services.GetRequiredService<IBuilderPipeline>().Run() != 0)
        {
            throw new InvalidOperationException("builder pipeline failed; see its log output");
        }

        PlannerPath = Path.Combine(_directory, "planner.sqlite");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    public T Scalar<T>(string sql)
    {
        using var db = new SqliteConnection($"Data Source={PlannerPath};Mode=ReadOnly");
        return db.ExecuteScalar<T>(sql)!;
    }
}
