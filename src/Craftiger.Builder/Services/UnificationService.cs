using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;

namespace Craftiger.Builder.Services;

public sealed class UnificationService(BuilderConfig config) : IUnificationService
{
    private static readonly string[] LeafPrefixes = ["ingot", "dustSmall", "dustTiny", "dust", "gem", "logWood", "block"];

    public UnifiedItems Run(Dump dump)
    {
        var parent = new Dictionary<string, string>();
        var oredictsByItem = new Dictionary<string, List<string>>();
        var membersByOredict = new Dictionary<string, List<string>>();

        foreach (var (name, groupId) in dump.Oredict)
        {
            if (IsGrouping(name))
            {
                continue;
            }
            if (!dump.GroupStacks.TryGetValue(groupId, out var stacks))
            {
                continue;
            }
            string? first = null;
            foreach (var stack in stacks)
            {
                if (!dump.Items.ContainsKey(stack.ItemId))
                {
                    continue;
                }
                if (!membersByOredict.TryGetValue(name, out var members))
                {
                    membersByOredict[name] = members = [];
                }
                members.Add(stack.ItemId);
                if (!oredictsByItem.TryGetValue(stack.ItemId, out var names))
                {
                    oredictsByItem[stack.ItemId] = names = [];
                }
                names.Add(name);
                if (first is null)
                {
                    first = stack.ItemId;
                }
                else
                {
                    Union(parent, first, stack.ItemId);
                }
            }
        }

        var classMembers = new Dictionary<string, List<string>>();
        foreach (var itemId in oredictsByItem.Keys)
        {
            var root = Find(parent, itemId);
            if (!classMembers.TryGetValue(root, out var members))
            {
                classMembers[root] = members = [];
            }
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
                foreach (var o in oredictsByItem[id])
                {
                    oredicts.Add(o);
                }
            }
            names.Remove(dump.Items[canonical].Name);
            names.UnionWith(oredicts);
            aliases[canonical] = names;
            primaryOredict[canonical] = PickPrimary(oredicts);
        }

        var canonicalByOredict = new Dictionary<string, string>();
        foreach (var (name, members) in membersByOredict)
        {
            if (members.Count > 0)
            {
                canonicalByOredict[name] = canonicalByRawId.GetValueOrDefault(members[0], members[0]);
            }
        }

        return new UnifiedItems
        {
            CanonicalByRawId = canonicalByRawId,
            PrimaryOredictByCanonical = primaryOredict,
            AliasesByCanonical = aliases,
            MembersByOredict = membersByOredict,
            CanonicalByOredict = canonicalByOredict
        };
    }

    private bool IsGrouping(string name) =>
        config.GroupingOredictNames.Contains(name) ||
        config.GroupingOredictPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)) ||
        config.GroupingOredictInfixes.Any(i => name.Contains(i, StringComparison.Ordinal));

    private static string PickPrimary(SortedSet<string> oredicts)
    {
        foreach (var prefix in LeafPrefixes)
        {
            foreach (var name in oredicts)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return name;
                }
            }
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
        while (parent.TryGetValue(root, out var p) && p != root)
        {
            root = p;
        }
        while (parent.TryGetValue(x, out var p) && p != root)
        {
            parent[x] = root;
            x = p;
        }
        return root;
    }

    private static void Union(Dictionary<string, string> parent, string a, string b)
    {
        var ra = Find(parent, a);
        var rb = Find(parent, b);
        if (ra != rb)
        {
            parent[rb] = ra;
        }
    }
}
