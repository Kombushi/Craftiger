using System.Runtime.InteropServices;
using System.Text;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Craftiger.Solver.Models;

namespace Craftiger.Api.Services;

/// <summary>A compact little-endian layout: a header naming the format and the artifact
/// (schema, pack, build), then the garage and weights, then the table's arrays as raw bytes
/// and the craft-list ranks. About two megabytes for a full solve.</summary>
public sealed class SolveEntryCodec(PlannerArtifact artifact) : ISolveEntryCodec
{
    private const int Magic = 0x45534643;
    private const int FormatVersion = 1;

    public byte[] Encode(SolveEntry entry)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(FormatVersion);
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
        writer.Flush();
        return stream.ToArray();
    }

    public SolveEntry? Decode(byte[] payload)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(payload, writable: false), Encoding.UTF8);
            if (reader.ReadInt32() != Magic
                || reader.ReadInt32() != FormatVersion
                || reader.ReadInt32() != PlannerArtifactRepository.SupportedSchemaVersion
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
        catch (Exception e) when (e is EndOfStreamException or IOException or FormatException)
        {
            return null;
        }
    }

    private static void WriteArray<T>(BinaryWriter writer, ReadOnlySpan<T> values) where T : unmanaged
    {
        writer.Write(values.Length);
        writer.Write(MemoryMarshal.AsBytes(values));
    }

    private static T[] ReadArray<T>(BinaryReader reader) where T : unmanaged
    {
        var values = new T[reader.ReadInt32()];
        var bytes = MemoryMarshal.AsBytes(values.AsSpan());
        if (reader.Read(bytes) != bytes.Length)
        {
            throw new EndOfStreamException();
        }
        return values;
    }
}
