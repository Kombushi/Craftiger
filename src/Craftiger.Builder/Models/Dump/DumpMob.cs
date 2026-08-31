namespace Craftiger.Builder.Models.Dump;

/// <summary>A mob the dump knows drops for; only soul-vial-capturable mobs can sit in a powered spawner.</summary>
public sealed record DumpMob(
    string MobId,
    double Health,
    bool SoulVialUsable,
    bool AlwaysInfernal,
    IReadOnlyList<DumpMobDrop> Drops);
