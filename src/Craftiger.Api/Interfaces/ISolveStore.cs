namespace Craftiger.Api.Interfaces;

/// <summary>Solved entries kept outside the process, by solve id, as the bytes the codec
/// produces — so any replica can serve a solve another one computed, and a restart keeps
/// them. Errors reaching the store surface to the caller; nothing degrades silently.</summary>
public interface ISolveStore
{
    Task<byte[]?> GetAsync(string solveId);

    Task PutAsync(string solveId, byte[] payload);
}
