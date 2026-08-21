namespace Craftiger.Api.Models;

public sealed record ListResponse(IReadOnlyList<ItemSummaryDto> Items, int Total, int Page, int PageSize);
