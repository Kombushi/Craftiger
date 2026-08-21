using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Craftiger.Api.Services;

/// <summary>The solve store on Valkey through StackExchange.Redis, whose values wrap a byte
/// array without copying it — a solved entry is a couple of megabytes and must not be
/// re-encoded on the way in or out. Keys carry the schema, pack and build of the artifact, so
/// a rebuilt artifact never reads another build's tables; values carry no expiry — the server
/// evicts by LRU. Connecting happens here, at startup, so an unreachable server refuses the
/// process rather than the first request.</summary>
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
