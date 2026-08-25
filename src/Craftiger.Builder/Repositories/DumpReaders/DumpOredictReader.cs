using Craftiger.Builder.Interfaces.DumpReaders;
using Craftiger.Builder.Models.Dump;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.Repositories.DumpReaders;

public sealed class DumpOredictReader : IDumpOredictReader
{
    public DumpOredictSet Read(SqliteConnection db)
    {
        var oredict = db.Query<(string OredictName, string GroupId)>(
                """SELECT NAME, ITEM_GROUP_ID FROM ORE_DICTIONARY""")
            .Select(r => new DumpOredictEntry(r.OredictName, r.GroupId))
            .ToList();

        DumpQueries.RequireTable(db, "GREG_TECH_ORE_DICT_UNIFICATION", "0.6.3");
        var unifiedOredictTargets = db.Query<(string Name, string TargetId)>(
            """SELECT NAME, TARGET_ID FROM GREG_TECH_ORE_DICT_UNIFICATION""")
            .ToDictionary(r => r.Name, r => r.TargetId);
        var unificationBlacklist = db.Query<string>(
            """SELECT ITEM_ID FROM GREG_TECH_UNIFICATION_BLACKLIST""").ToHashSet();

        DumpQueries.RequireTable(db, "GREG_TECH_ORE_PREFIX", "0.6.3");
        var orePrefixes = db.Query<(string Name, bool Unifiable, bool SelfReferencing, bool MaterialBased,
            bool Container, bool Recyclable, long MaterialAmount)>("""
            SELECT NAME, UNIFIABLE, SELF_REFERENCING, MATERIAL_BASED, CONTAINER, RECYCLABLE, MATERIAL_AMOUNT
            FROM GREG_TECH_ORE_PREFIX
            """)
            .ToDictionary(r => r.Name, r => new DumpOrePrefix(
                r.Name, r.Unifiable, r.SelfReferencing, r.MaterialBased, r.Container, r.Recyclable,
                r.MaterialAmount));

        DumpQueries.RequireTable(db, "ITEM_CONTAINER", "0.6.4");
        var itemContainers = db.Query<(string ItemId, string ContainerId)>(
            """SELECT ITEM_ID, CONTAINER_ITEM_ID FROM ITEM_CONTAINER""")
            .ToDictionary(r => r.ItemId, r => r.ContainerId);

        DumpQueries.RequireTable(db, "GREG_TECH_ITEM_DATA", "0.6.4");
        var itemDataByproducts = new Dictionary<string, List<DumpMaterialAmount>>();
        foreach (var (dataId, material, amount) in db.Query<(string, string, long)>("""
            SELECT GREG_TECH_ITEM_DATA_ID, BY_PRODUCTS_MATERIAL_NAME, BY_PRODUCTS_AMOUNT
            FROM GREG_TECH_ITEM_DATA_BY_PRODUCTS WHERE BY_PRODUCTS_MATERIAL_NAME IS NOT NULL
            """))
        {
            DumpQueries.Add(itemDataByproducts, dataId, new DumpMaterialAmount(material, amount));
        }
        var itemData = db.Query<(string Id, string ItemId, string? Prefix, string Material, long Amount)>("""
            SELECT ID, ITEM_ID, PREFIX_NAME, MATERIAL_NAME, MATERIAL_AMOUNT
            FROM GREG_TECH_ITEM_DATA WHERE MATERIAL_NAME IS NOT NULL
            """)
            .Select(r => new DumpItemData(
                r.ItemId, r.Prefix, r.Material, r.Amount,
                itemDataByproducts.GetValueOrDefault(r.Id) ?? []))
            .ToList();

        return new DumpOredictSet(
            oredict, unifiedOredictTargets, unificationBlacklist, new OrePrefixIndex(orePrefixes),
            itemContainers, new ItemDataIndex(itemData));
    }
}
