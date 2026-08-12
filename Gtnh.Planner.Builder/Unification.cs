namespace Gtnh.Planner.Builder;

/// <summary>Result of oredict unification: raw item id to canonical id, plus names.</summary>
public sealed class UnifiedItems
{
    public required Dictionary<string, string> CanonicalByRawId { get; init; }
    public required Dictionary<string, string> PrimaryOredictByCanonical { get; init; }
    public required Dictionary<string, HashSet<string>> AliasesByCanonical { get; init; }
    public required Dictionary<string, List<string>> MembersByOredict { get; init; }

    public string Canonical(string rawId) => CanonicalByRawId.GetValueOrDefault(rawId, rawId);
}

/// <summary>Collapses oredict-equivalent items into one canonical item per class.</summary>
public static class Unification
{
    public static UnifiedItems Run(Dump dump, BuilderConfig config)
    {
        var parent = new Dictionary<string, string>();
        var oredictsByItem = new Dictionary<string, List<string>>();
        var membersByOredict = new Dictionary<string, List<string>>();

        foreach (var (name, groupId) in dump.Oredict)
        {
            if (IsGrouping(name, config)) continue;
            if (!dump.GroupStacks.TryGetValue(groupId, out var stacks)) continue;
            string? first = null;
            foreach (var stack in stacks)
            {
                if (!dump.Items.ContainsKey(stack.ItemId)) continue;
                if (!membersByOredict.TryGetValue(name, out var members)) membersByOredict[name] = members = [];
                members.Add(stack.ItemId);
                if (!oredictsByItem.TryGetValue(stack.ItemId, out var names)) oredictsByItem[stack.ItemId] = names = [];
                names.Add(name);
                if (first is null) first = stack.ItemId;
                else Union(parent, first, stack.ItemId);
            }
        }

        var classMembers = new Dictionary<string, List<string>>();
        foreach (var itemId in oredictsByItem.Keys)
        {
            var root = Find(parent, itemId);
            if (!classMembers.TryGetValue(root, out var members)) classMembers[root] = members = [];
            members.Add(itemId);
        }

        var canonicalByRawId = new Dictionary<string, string>();
        var primaryOredict = new Dictionary<string, string>();
        var aliases = new Dictionary<string, HashSet<string>>();

        foreach (var members in classMembers.Values)
        {
            var canonical = members.MinBy(id => SortKey(dump.Items[id]))!;
            var names = new HashSet<string>();
            var oredicts = new SortedSet<string>();
            foreach (var id in members)
            {
                canonicalByRawId[id] = canonical;
                names.Add(dump.Items[id].Name);
                foreach (var o in oredictsByItem[id]) oredicts.Add(o);
            }
            names.Remove(dump.Items[canonical].Name);
            names.UnionWith(oredicts);
            aliases[canonical] = names;
            primaryOredict[canonical] = PickPrimary(oredicts);
        }

        return new UnifiedItems
        {
            CanonicalByRawId = canonicalByRawId,
            PrimaryOredictByCanonical = primaryOredict,
            AliasesByCanonical = aliases,
            MembersByOredict = membersByOredict
        };
    }

    private static bool IsGrouping(string name, BuilderConfig config) =>
        config.GroupingOredictNames.Contains(name) ||
        config.GroupingOredictPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)) ||
        config.GroupingOredictInfixes.Any(i => name.Contains(i, StringComparison.Ordinal));

    private static readonly string[] LeafPrefixes = ["ingot", "dustSmall", "dustTiny", "dust", "gem", "logWood", "block"];

    private static string PickPrimary(SortedSet<string> oredicts)
    {
        foreach (var prefix in LeafPrefixes)
        {
            foreach (var name in oredicts)
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return name;
        }
        return oredicts.MinBy(n => (n.Length, n))!;
    }

    private static (int, string) SortKey(DumpItem item) => (item.ModId switch
    {
        "minecraft" => 0,
        "gregtech" => 1,
        _ => 2
    }, item.Id);

    private static string Find(Dictionary<string, string> parent, string x)
    {
        var root = x;
        while (parent.TryGetValue(root, out var p) && p != root) root = p;
        while (parent.TryGetValue(x, out var p) && p != root) { parent[x] = root; x = p; }
        return root;
    }

    private static void Union(Dictionary<string, string> parent, string a, string b)
    {
        var ra = Find(parent, a);
        var rb = Find(parent, b);
        if (ra != rb) parent[rb] = ra;
    }
}
