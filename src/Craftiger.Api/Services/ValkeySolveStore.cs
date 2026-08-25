using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Craftiger.Api.Services;

/// <summary>The solve store on Valkey: zero-copy byte values, keys carrying the artifact's schema, pack and build so a rebuild never reads another build's tables, no expiry (the server evicts by LRU), and a startup connection so an unreachable server refuses the process.</summary>
public sealed class ValkeySolveStore : ISolveStore
{
    private readonly IDatabase _database;
    private readonly string _prefix;

    public ValkeySolveStore(PlannerArtifact artifact, IOptions<ApiOptions> options, ILogger<ValkeySolveStore> logger)
    {
        var connectionString = options.Value.Valkey.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ApiOptions:Valkey:ConnectionString is required");
        }
        var multiplexer = ConnectionMultiplexer.Connect(connectionString);
        _database = multiplexer.GetDatabase();
        _prefix = $"craftiger:{PlannerArtifactRepository.SupportedSchemaVersion}:{artifact.PackVersion}:{artifact.BuildId}:";
        logger.LogInformation("solve store on Valkey {ConnectionString}, keys {Prefix}*", connectionString, _prefix);
    }

    public async Task<byte[]?> GetAsync(string solveId)
    {
        var value = await _database.StringGetAsync(_prefix + solveId);
        return value.IsNull ? null : (byte[]?)value;
    }

    public Task PutAsync(string solveId, byte[] payload) => _database.StringSetAsync(_prefix + solveId, payload);
}
