using System;

namespace VintageAtlas.Tracking;

public class TileCoordinate
{
    public int Zoom { get; set; }
    public int X { get; set; }
    public int Z { get; set; }

    public override int GetHashCode()
    {
        return HashCode.Combine(Zoom, X, Z);
    }

    public override bool Equals(object? obj)
    {
        return obj is TileCoordinate other &&
               Zoom == other.Zoom &&
               X == other.X &&
               Z == other.Z;
    }
}