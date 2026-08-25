using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Services;

public sealed class UnificationService : IUnificationService
{
    private static readonly string[] _leafPrefixes = ["ingot", "dustSmall", "dustTiny", "dust", "gem", "logWood", "block"];

    public UnifiedItems Run(Dump dump)
    {
        var parent = new Dictionary<string, string>();
        var oredictsByItem = new Dictionary<string, List<string>>();
        var membersByOredict = new Dictionary<string, List<string>>();

        foreach (var (name, groupId) in dump.Oredict)
        {
            if (!dump.GroupStacks.TryGetValue(groupId, out var stacks))
            {
                continue;
            }
            // Only names GT itself unifies merge identities; every other oredict only classifies and searches.
            var unifies = dump.UnifiedOredictTargets.ContainsKey(name);
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
                if (!unifies || dump.UnificationBlacklist.Contains(stack.ItemId))
                {
                    continue;
                }
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

        // GT's substitution targets, gathered per class so the canonical is GT's own pick.
        var targetsByRoot = new Dictionary<string, List<string>>();
        foreach (var target in dump.UnifiedOredictTargets.Values)
        {
            if (!oredictsByItem.ContainsKey(target))
            {
                continue;
            }
            var root = Find(parent, target);
            if (!targetsByRoot.TryGetValue(root, out var targets))
            {
                targetsByRoot[root] = targets = [];
            }
            targets.Add(target);
        }

        var canonicalByRawId = new Dictionary<string, string>();
        var primaryOredict = new Dictionary<string, string>();
        var aliases = new Dictionary<string, IReadOnlySet<string>>();

        foreach (var (root, members) in classMembers)
        {
            var canonical = (targetsByRoot.GetValueOrDefault(root) ?? members)
                .MinBy(id => SortKey(dump.Items[id]))!;
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
            primaryOredict[canonical] = PickPrimary(oredicts, dump.UnifiedOredictTargets);
        }

        var canonicalByOredict = new Dictionary<string, string>();
        var oredictsByCanonical = new Dictionary<string, IReadOnlySet<string>>();
        foreach (var (name, members) in membersByOredict)
        {
            if (members.Count > 0)
            {
                var representative = dump.UnifiedOredictTargets.GetValueOrDefault(name, members[0]);
                canonicalByOredict[name] = canonicalByRawId.GetValueOrDefault(representative, representative);
            }
            foreach (var member in members)
            {
                var canonical = canonicalByRawId.GetValueOrDefault(member, member);
                if (!oredictsByCanonical.TryGetValue(canonical, out var set))
                {
                    oredictsByCanonical[canonical] = set = new HashSet<string>();
                }
                ((HashSet<string>)set).Add(name);
            }
        }

        return new UnifiedItems
        {
            CanonicalByRawId = canonicalByRawId,
            PrimaryOredictByCanonical = primaryOredict,
            AliasesByCanonical = aliases,
            MembersByOredict = membersByOredict.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value),
            OredictsByCanonical = oredictsByCanonical,
            CanonicalByOredict = canonicalByOredict
        };
    }

    /// <summary>GT-unified names outrank convention ones, so a wildcard like ingotAnyIron never shadows the real material name.</summary>
    private static string PickPrimary(
        SortedSet<string> oredicts, IReadOnlyDictionary<string, string> unifiedNames)
    {
        foreach (var unifiedOnly in new[] { true, false })
        {
            foreach (var prefix in _leafPrefixes)
            {
                foreach (var name in oredicts)
                {
                    if ((!unifiedOnly || unifiedNames.ContainsKey(name))
                        && name.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return name;
                    }
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
