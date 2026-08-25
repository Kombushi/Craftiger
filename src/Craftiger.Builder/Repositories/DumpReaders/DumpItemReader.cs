using System.Text.RegularExpressions;
using Craftiger.Builder.Interfaces.DumpReaders;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.Repositories.DumpReaders;

public sealed partial class DumpItemReader : IDumpItemReader
{
    [GeneratedRegex("§.")]
    private static partial Regex Formatting();

    [GeneratedRegex(@"Voltage IN:\s*([\d,]+)")]
    private static partial Regex VoltageIn();

    public DumpItemSet Read(SqliteConnection db)
    {
        var items = db.Query<DumpItem>("""
            SELECT ID AS Id, LOCALIZED_NAME AS Name, MOD_ID AS ModId,
                INTERNAL_NAME AS InternalName, IMAGE_FILE_PATH AS ImagePath,
                MAX_DAMAGE AS MaxDamage, MAX_STACK_SIZE AS MaxStackSize
            FROM ITEM
            """).ToDictionary(i => i.Id);

        var fluids = db.Query<DumpFluid>("""
            SELECT ID AS Id, LOCALIZED_NAME AS Name, MOD_ID AS ModId,
                INTERNAL_NAME AS InternalName, IMAGE_FILE_PATH AS ImagePath
            FROM FLUID
            """).ToDictionary(f => f.Id);

        var machineVoltageTiers = new Dictionary<string, int>();
        foreach (var (itemId, tooltip) in db.Query<(string, string)>(
            """SELECT ITEM_ID, TOOLTIP FROM ITEM_TOOLTIP WHERE TOOLTIP LIKE '%Voltage IN:%'"""))
        {
            var match = VoltageIn().Match(Formatting().Replace(tooltip, ""));
            if (match.Success && long.TryParse(match.Groups[1].Value.Replace(",", ""), out var voltage) && voltage > 0)
            {
                machineVoltageTiers[itemId] = TierLadder.VoltageTier(voltage);
            }
        }

        return new DumpItemSet(items, fluids, machineVoltageTiers, ReadDeprecatedItems(db));
    }

    /// <summary>GT's deprecation banner is a rigid tooltip line, not prose; every match is a superseded controller.</summary>
    private static HashSet<string> ReadDeprecatedItems(SqliteConnection db)
    {
        var deprecated = new HashSet<string>();
        foreach (var (itemId, tooltip) in db.Query<(string, string)>(
            """SELECT ITEM_ID, TOOLTIP FROM ITEM_TOOLTIP WHERE TOOLTIP LIKE '%DEPRECATED%'"""))
        {
            foreach (var line in tooltip.Split('\n'))
            {
                var text = line.TrimStart();
                if (text.StartsWith("§4DEPRECATED", StringComparison.Ordinal)
                    || text.StartsWith("§4[DEPRECATED", StringComparison.Ordinal))
                {
                    deprecated.Add(itemId);
                    break;
                }
            }
        }
        return deprecated;
    }
}
