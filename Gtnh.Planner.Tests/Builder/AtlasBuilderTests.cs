using System.IO.Compression;
using System.Text.Json;
using Gtnh.Planner.Builder.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Gtnh.Planner.Tests.Builder;

public sealed class AtlasBuilderTests
{
    [Fact]
    public void PacksIconsIntoGridWithOffsets()
    {
        var dir = Directory.CreateTempSubdirectory("craftiger-atlas").FullName;
        try
        {
            var zipPath = Path.Combine(dir, "image.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                using var icon = new Image<Rgba32>(64, 64, new Rgba32(255, 0, 0, 255));
                using var stream = zip.CreateEntry("item/red.png").Open();
                icon.SaveAsPng(stream);
            }

            var atlasPath = Path.Combine(dir, "atlas.webp");
            var offsetsPath = Path.Combine(dir, "offsets.json");
            var icons = new List<(string, string)>
            {
                ("a", "item/red.png"),
                ("b", "item/missing.png"),
                ("c", "item/red.png")
            };
            var result = new AtlasBuilder().Build(zipPath, icons, atlasPath, offsetsPath);

            Assert.Equal(64, result.Width);
            Assert.Equal(64, result.Height);
            Assert.Equal(32, result.Cell);
            Assert.True(File.Exists(atlasPath));

            var offsets = JsonSerializer.Deserialize<Dictionary<string, int[]>>(File.ReadAllText(offsetsPath))!;
            Assert.Equal([0, 0], offsets["a"]);
            Assert.Equal([32, 0], offsets["b"]);
            Assert.Equal([0, 32], offsets["c"]);

            using var atlas = Image.Load<Rgba32>(atlasPath);
            Assert.Equal(255, atlas[0, 0].R);
            Assert.Equal(0, atlas[32, 0].A);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
