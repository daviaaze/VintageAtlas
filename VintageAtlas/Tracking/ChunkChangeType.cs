namespace VintageAtlas.Tracking;

/// <summary>
/// Types of chunk changes to track
/// </summary>
public enum ChunkChangeType
{
    BlockModified,
    NewlyGenerated,
    StructurePlaced,
    StructureRemoved
}