using System.IO.Compression;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Gtnh.Planner.Builder;

public sealed record AtlasResult(int Width, int Height, int Cell);

/// <summary>Packs all referenced icons from image.zip into one webp atlas plus an offsets json.</summary>
public static class AtlasBuilder
{
    public static AtlasResult Build(
        string imageZipPath,
        IReadOnlyList<(string ItemId, string ImagePath)> icons,
        string atlasPath,
        string offsetsPath,
        int cell = 32,
        int? lossyQuality = null)
    {
        var raw = new byte[icons.Count][];
        using (var zip = ZipFile.OpenRead(imageZipPath))
        {
            var entries = zip.Entries.Where(e => e.Length > 0).ToDictionary(e => e.FullName);
            for (var i = 0; i < icons.Count; i++)
            {
                if (!entries.TryGetValue(icons[i].ImagePath, out var entry)) continue;
                using var stream = entry.Open();
                using var buffer = new MemoryStream((int)entry.Length);
                stream.CopyTo(buffer);
                raw[i] = buffer.ToArray();
            }
        }

        var tiles = new Image<Rgba32>?[icons.Count];
        Parallel.For(0, icons.Count, i =>
        {
            if (raw[i] is null) return;
            try
            {
                var image = Image.Load<Rgba32>(raw[i]);
                if (image.Width != cell || image.Height != cell)
                    image.Mutate(x => x.Resize(cell, cell, KnownResamplers.Bicubic));
                tiles[i] = image;
            }
            catch (ImageFormatException)
            {
            }
        });

        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(icons.Count)));
        var rows = Math.Max(1, (icons.Count + columns - 1) / columns);
        using var atlas = new Image<Rgba32>(columns * cell, rows * cell);

        var offsets = new Dictionary<string, int[]>(icons.Count);
        for (var i = 0; i < icons.Count; i++)
        {
            var u = i % columns * cell;
            var v = i / columns * cell;
            offsets[icons[i].ItemId] = [u, v];
            if (tiles[i] is not { } tile) continue;
            atlas.Mutate(x => x.DrawImage(tile, new Point(u, v), 1f));
            tile.Dispose();
        }

        atlas.SaveAsWebp(atlasPath, lossyQuality is { } quality
            ? new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = quality }
            : new WebpEncoder { FileFormat = WebpFileFormatType.Lossless });
        File.WriteAllText(offsetsPath, JsonSerializer.Serialize(offsets));

        return new AtlasResult(atlas.Width, atlas.Height, cell);
    }
}