namespace Craftiger.Api.Models;

/// <summary>Every buildable generator line, unpruned, with the display lookup for the blocks and fuels it names.</summary>
public sealed record GeneratorCatalogResponse(
    IReadOnlyList<GeneratorLineDto> Lines,
    IReadOnlyDictionary<string, ItemRefDto> Items);
