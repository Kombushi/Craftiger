namespace Craftiger.Api.Interfaces;

/// <summary>Solved entries kept outside the process by solve id, so any replica serves a solve another computed and a restart keeps them; store errors surface, nothing degrades silently.</summary>
public interface ISolveStore
{
    Task<byte[]?> GetAsync(string solveId);

    Task PutAsync(string solveId, byte[] payload);
}
