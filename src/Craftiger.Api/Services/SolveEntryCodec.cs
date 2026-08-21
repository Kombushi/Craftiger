using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Craftiger.Solver.Models;

namespace Craftiger.Api.Services;

/// <summary>A compact little-endian layout: the magic and format version in the clear, then —
/// Brotli-compressed — the artifact identity (schema, pack, build), the garage and weights, the
/// table's arrays as raw bytes and the craft-list ranks.</summary>
public sealed class SolveEntryCodec(PlannerArtifact artifact) : ISolveEntryCodec
{
    private const int Magic = 0x45534643;
    private const int FormatVersion = 2;
    private const int HeaderLength = 2 * sizeof(int);

    /// <summary>Reads out of the decoder come in large chunks instead of per field.</summary>
    private const int BufferSize = 1 << 20;

    public byte[] Encode(SolveEntry entry)
    {
        using var stream = new MemoryStream();
        using (var header = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            header.Write(Magic);
            header.Write(FormatVersion);
        }
        // The encoder closes a block at every write it receives, which costs a third of the
        // ratio on split input, so the body is laid out in full first and compressed in one write.
        using var body = new MemoryStream(BodyCapacity(entry));
        using (var writer = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true))
        {
            WriteBody(writer, entry);
        }
        using (var compressed = new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        {
            compressed.Write(body.GetBuffer().AsSpan(0, (int)body.Length));
        }
        return stream.ToArray();
    }

    /// <summary>The arrays dominate; the rest is a few strings and counts.</summary>
    private static int BodyCapacity(SolveEntry entry) =>
        entry.Table.Costs.Length * (sizeof(double) + 2 * sizeof(int))
        + entry.Table.PickArray.Length * sizeof(ushort)
        + entry.Sorted.Count * sizeof(int)
        + 4096;

    private void WriteBody(BinaryWriter writer, SolveEntry entry)
    {
        writer.Write(PlannerArtifactRepository.SupportedSchemaVersion);
        writer.Write(artifact.PackVersion);
        writer.Write(artifact.BuildId);

        writer.Write(entry.Table.Converged);
        writer.Write(entry.ReachableCount);

        writer.Write(entry.Garage.DefaultTier);
        writer.Write(entry.Garage.MachineTiers.Count);
        foreach (var (machine, tier) in entry.Garage.MachineTiers)
        {
            writer.Write(machine);
            writer.Write(tier.HasValue);
            writer.Write(tier ?? 0);
        }
        writer.Write(entry.Garage.BuiltMultiblocks.Count);
        foreach (var machine in entry.Garage.BuiltMultiblocks)
        {
            writer.Write(machine);
        }
        writer.Write(entry.Garage.CoilHeat.Count);
        foreach (var (machine, heat) in entry.Garage.CoilHeat)
        {
            writer.Write(machine);
            writer.Write(heat);
        }

        writer.Write(entry.Weights.PriceBase);
        writer.Write(entry.Weights.ItemWeights.Count);
        foreach (var (itemId, weight) in entry.Weights.ItemWeights)
        {
            writer.Write(itemId);
            writer.Write(weight);
        }

        WriteArray(writer, entry.Table.Costs);
        WriteArray(writer, entry.Table.BestRecipes);
        WriteArray(writer, entry.Table.PickStarts);
        WriteArray(writer, entry.Table.PickArray);
        WriteArray(writer, entry.Sorted is int[] ranks ? ranks : [.. entry.Sorted]);
    }

    public SolveEntry? Decode(byte[] payload)
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
            using var buffered = new BufferedStream(decompressed, BufferSize);
            using var reader = new BinaryReader(buffered, Encoding.UTF8);
            if (reader.ReadInt32() != PlannerArtifactRepository.SupportedSchemaVersion
                || reader.ReadString() != artifact.PackVersion
                || reader.ReadString() != artifact.BuildId)
            {
                return null;
            }

            var converged = reader.ReadBoolean();
            var reachable = reader.ReadInt32();

            var defaultTier = reader.ReadInt32();
            var machines = new Dictionary<string, int?>();
            for (var count = reader.ReadInt32(); count > 0; count--)
            {
                var machine = reader.ReadString();
                var owned = reader.ReadBoolean();
                var tier = reader.ReadInt32();
                machines[machine] = owned ? tier : null;
            }
            var built = new HashSet<string>();
            for (var count = reader.ReadInt32(); count > 0; count--)
            {
                built.Add(reader.ReadString());
            }
            var coils = new Dictionary<string, int>();
            for (var count = reader.ReadInt32(); count > 0; count--)
            {
                coils[reader.ReadString()] = reader.ReadInt32();
            }

            var priceBase = reader.ReadDouble();
            var weights = new Dictionary<string, double>();
            for (var count = reader.ReadInt32(); count > 0; count--)
            {
                weights[reader.ReadString()] = reader.ReadDouble();
            }

            var index = artifact.Graph.Index;
            var cost = ReadArray<double>(reader);
            var best = ReadArray<int>(reader);
            var pickStart = ReadArray<int>(reader);
            var picks = ReadArray<ushort>(reader);
            var sorted = ReadArray<int>(reader);
            if (cost.Length != index.ItemCount || best.Length != index.ItemCount
                || pickStart.Length != index.ItemCount || sorted.Length != artifact.CraftListOrder.Count)
            {
                return null;
            }

            return new SolveEntry(
                new CostTable(index, cost, best, pickStart, picks, converged),
                new Garage(defaultTier, machines, built, coils),
                new WeightSettings(priceBase, weights),
                sorted,
                reachable);
        }
        // The Brotli decoder reports damaged input as InvalidOperationException; a damaged
        // cache value is recomputed, never a failed request.
        catch (Exception e) when (e is EndOfStreamException or IOException or FormatException or InvalidDataException or InvalidOperationException)
        {
            return null;
        }
    }

    private static void WriteArray<T>(BinaryWriter writer, ReadOnlySpan<T> values) where T : unmanaged
    {
        writer.Write(values.Length);
        writer.Write(MemoryMarshal.AsBytes(values));
    }

    /// <summary>A decompressing stream hands out what it has, so the read loops until the
    /// array is full or the stream ends short.</summary>
    private static T[] ReadArray<T>(BinaryReader reader) where T : unmanaged
    {
        var values = new T[reader.ReadInt32()];
        var bytes = MemoryMarshal.AsBytes(values.AsSpan());
        while (bytes.Length > 0)
        {
            var read = reader.Read(bytes);
            if (read <= 0)
            {
                throw new EndOfStreamException();
            }
            bytes = bytes[read..];
        }
        return values;
    }
}
