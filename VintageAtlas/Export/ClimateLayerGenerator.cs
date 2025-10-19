using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using VintageAtlas.Storage;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageAtlas.Export;

public class ClimateLayerGenerator()
{
    internal void GenerateClimateLayerAsync(SavegameDataSource dataSource, MbTilesStorage mbTilesStorage, ICoreServerAPI api)
    {
        var tiles = dataSource.GetAllMapRegionPositions();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 20
        };

        Parallel.ForEach(tiles, parallelOptions, (tile, _) =>
        {
            var tilePos = tile.ToChunkIndex();
            var serverMapRegion = dataSource.GetServerMapRegion(tilePos);
            if (serverMapRegion == null) 
                return;
            
            using var tempBitmap = new SKBitmap(512, 512);
            using var rainBitmap = new SKBitmap(512, 512);
            using var tempCanvas = new SKCanvas(tempBitmap);
            using var rainCanvas = new SKCanvas(rainBitmap);
            tempCanvas.Clear();
            rainCanvas.Clear();

            for (var x = 0; x < 512; x++)
            {
                for (var z = 0; z < 512; z++)
                {
                    var interpolatedColor = serverMapRegion.ClimateMap.GetColorLerpedCorrectly(x / 512f, z / 512f);
                    var redValue = ColorUtil.ColorR(interpolatedColor);
                    var greenValue = ColorUtil.ColorG(interpolatedColor);
                        
                    var tempColor = new SKColor(255, 0, 0, redValue);
                    var rainColor = new SKColor(0, 0, 255, greenValue);
                        
                    tempCanvas.DrawPoint(x, z, new SKPaint
                    {
                        Color = tempColor
                    });
                        
                    rainCanvas.DrawPoint(x, z, new SKPaint
                    {
                        Color = rainColor
                    });
                }
            }
            tempCanvas.Save();
            rainCanvas.Save();
            mbTilesStorage.PutRainTile(tile.X, tile.Z, tempBitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray());
            mbTilesStorage.PutTempTile(tile.X, tile.Z, rainBitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray());
            tempCanvas.Dispose();
            tempBitmap.Dispose();
            rainCanvas.Dispose();
            rainBitmap.Dispose();
            api.Logger.Debug($"[VintageAtlas] Generated climate layer for {tile.X}-{tile.Z}");
        });
    }
}