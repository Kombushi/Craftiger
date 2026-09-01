using Craftiger.Api.Models;

namespace Craftiger.Api.Interfaces;

/// <summary>Read endpoints over the loaded artifact and a cached solve.</summary>
public interface IPlannerQueryService
{
    MetaResponse Meta();

    ListResponse List(SolveEntry entry, int page, int pageSize, bool hideUnreachable);

    IReadOnlyList<ItemSummaryDto> Search(SolveEntry? entry, string query);

    ItemDetailResponse? ItemDetail(SolveEntry entry, string itemId, bool allScopes = false);

    IReadOnlyList<string> Machines(IEnumerable<string> targetIds, bool deep);

    BomResponse Bom(SolveEntry entry, BomRequest request);
}
