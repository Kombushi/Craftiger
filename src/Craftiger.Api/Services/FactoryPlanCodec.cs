using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Craftiger.Solver.Models.Factory;

namespace Craftiger.Api.Services;

/// <summary>Factory plans for the store: magic and format version in the clear, then the Brotli-compressed JSON envelope naming the artifact build, so a plan never outlives the tables it was solved on.</summary>
public sealed class FactoryPlanCodec(PlannerArtifact artifact) : IFactoryPlanCodec
{
    private const int Magic = 0x50434643;
    private const int FormatVersion = 2;
    private const int HeaderLength = 2 * sizeof(int);

    public byte[] Encode(FactoryPlan plan)
    {
        using var stream = new MemoryStream();
        using (var header = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            header.Write(Magic);
            header.Write(FormatVersion);
        }
        using (var compressed = new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        {
            JsonSerializer.Serialize(compressed, new FactoryPlanEnvelope(
                PlannerArtifactRepository.SupportedSchemaVersion, artifact.PackVersion, artifact.BuildId, plan));
        }
        return stream.ToArray();
    }

    public FactoryPlan? Decode(byte[] payload)
    {
        try
        {
            if (payload.Length < HeaderLength
                || BitConverter.ToInt32(payload, 0) != Magic
                || BitConverter.ToInt32(payload, sizeof(int)) != FormatVersion)
            {
                return null;
            }
            using var decompressed = new BrotliStream(
                new MemoryStream(payload, HeaderLength, payload.Length - HeaderLength, writable: false),
                CompressionMode.Decompress);
            var envelope = JsonSerializer.Deserialize<FactoryPlanEnvelope>(decompressed);
            return envelope is not null
                && envelope.SchemaVersion == PlannerArtifactRepository.SupportedSchemaVersion
                && envelope.PackVersion == artifact.PackVersion
                && envelope.BuildId == artifact.BuildId
                    ? envelope.Plan
                    : null;
        }
        // The Brotli decoder reports damaged input as InvalidOperationException; a damaged value is recomputed, never a failed request.
        catch (Exception e) when (e is JsonException or EndOfStreamException or IOException or InvalidDataException or InvalidOperationException)
        {
            return null;
        }
    }
}
