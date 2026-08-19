namespace Craftiger.Builder.Models;

public sealed record DumpItem(
    string Id, string Name, string ModId, string InternalName, string ImagePath, long MaxDamage);
