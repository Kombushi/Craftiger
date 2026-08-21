using Craftiger.Api.Models;

namespace Craftiger.Api.Interfaces;

/// <summary>The byte form of a solved entry for the store: the table's arrays, the settings
/// that produced them and the craft-list order, stamped with the artifact they belong to.</summary>
public interface ISolveEntryCodec
{
    byte[] Encode(SolveEntry entry);

    /// <summary>The entry, or null when the bytes were written for another artifact build or
    /// another format and must be recomputed rather than trusted.</summary>
    SolveEntry? Decode(byte[] payload);
}
