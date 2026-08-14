using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

/// <summary>Packs all referenced icons from image.zip into one webp atlas plus an offsets json.</summary>
public interface IAtlasBuilder
{
    AtlasResult Build(
        string imageZipPath,
        IReadOnlyList<(string ItemId, string ImagePath)> icons,
        string atlasPath,
        string offsetsPath,
        int cell = 32,
        int? lossyQuality = null);
}
