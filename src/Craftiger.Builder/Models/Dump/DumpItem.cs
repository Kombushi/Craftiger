namespace Craftiger.Builder.Models.Dump;

public sealed record DumpItem(
    string Id, string Name, string ModId, string InternalName, string ImagePath, long MaxDamage, long MaxStackSize);
