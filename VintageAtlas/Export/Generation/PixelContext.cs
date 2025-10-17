namespace VintageAtlas.Export;

public sealed partial class UnifiedTileGenerator
{
    /// <summary>
    /// Context for rendering a single pixel in a chunk.
    /// </summary>
    private readonly struct PixelContext(int x, int z, int height, int offsetX, int offsetZ)
    {
        public readonly int X = x;
        public readonly int Z = z;
        public readonly int Height = height;
        public readonly int ImgX = offsetX + x;
        public readonly int ImgZ = offsetZ + z;
    }
}