using Vintagestory.Common.Database;

namespace VintageAtlas.Export;

public static class ChunkPosExtension
{
    public static void Set(this ref ChunkPos chunkPos, int x, int y, int z)
    {
        chunkPos.X = x;
        chunkPos.Y = y;
        chunkPos.Z = z;
    } 
}