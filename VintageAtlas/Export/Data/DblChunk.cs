using Vintagestory.Common.Database;

namespace VintageAtlas.Export;

public record DblChunk<T>(ChunkPos Position, T Data)
{
    public ChunkPos Position { get; set; } = Position;

    public T Data { get; set; } = Data;
}