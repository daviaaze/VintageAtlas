using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NetTopologySuite.Algorithm.Hull;
using NetTopologySuite.Geometries;
using Vintagestory.Common.Database;
using Vintagestory.Server;
using VintageAtlas.GeoJson;

namespace VintageAtlas.Export;

public struct ChunkPosition
{
    public ChunkPosition(int x, int z)
    {
        X = x;
        Z = z;
    }

    public int X;
    public int Z;
}

public class GroupedPosition
{
    public GroupedPosition(string version, List<ChunkPosition> positions)
    {
        Version = version;
        Positions = positions;
    }

    public string Version { get; set; }
    public List<ChunkPosition> Positions { get; set; }
}

public class GroupChunks
{
    public readonly List<Tuple<int, int>> Directions = new() { Tuple.Create(1, 0), Tuple.Create(-1, 0), Tuple.Create(0, 1), Tuple.Create(0, -1) };

    public readonly List<GroupedPosition> Grouped = new();
    public readonly HashSet<ulong> Visited = new();

    private readonly Dictionary<ChunkPos, string> _positions;
    private readonly Dictionary<ulong, string> _positionsLong;
    private ServerMain _server;
    private readonly int spawnChunkX;
    private readonly int spawnChunkZ;
    private Dictionary<string, string> _colors = new();

    public GroupChunks(Dictionary<ChunkPos, string> positions, ServerMain server)
    {
        _server = server;
        spawnChunkX = (int)(_server.World.DefaultSpawnPosition?.X ?? _server.WorldMap.MapSizeX / 2f);
        spawnChunkZ = (int)(_server.World.DefaultSpawnPosition?.Z ?? _server.WorldMap.MapSizeZ / 2f);

        _positions = positions;
        _positionsLong = new Dictionary<ulong, string>();
        foreach (var (pos, ver) in _positions)
        {
            var key = ChunkPos.ToChunkIndex(pos.X, 0, pos.Z);
            _positionsLong.Add(key, ver);
        }
    }

    public void IterativeDfs(int x, int z, string version, ulong key, List<ChunkPosition> group)
    {
        var stack = new Stack<(int x, int z, ulong key)>();
        stack.Push((x, z, key));

        while (stack.Count > 0)
        {
            var (currentX, currentZ, currentKey) = stack.Pop();

            if (Visited.Contains(currentKey)) continue;
            if (!_positionsLong.TryGetValue(currentKey, out var currentVersion) || currentVersion != version) continue;

            Visited.Add(currentKey);
            group.Add(new ChunkPosition(currentX, currentZ));

            foreach (var (dx, dz) in Directions)
            {
                var newX = currentX + dx;
                var newZ = currentZ + dz;
                var newKey = ChunkPos.ToChunkIndex(newX, 0, newZ);

                if (!Visited.Contains(newKey))
                {
                    stack.Push((newX, newZ, newKey));
                }
            }
        }
    }

    public List<GroupedPosition> GroupPositions()
    {
        // Iterate over all positions and perform DFS if the position is not visited
        foreach (var (pos, psvers) in _positions)
        {
            var key = ChunkPos.ToChunkIndex(pos.X, 0, pos.Z);
            if (!Visited.Contains(key))
            {
                var group = new List<ChunkPosition>();
                IterativeDfs(pos.X, pos.Z, psvers, key, group);
                if (group.Count > 0)
                {
                    Grouped.Add(new GroupedPosition(psvers, group));
                }
            }
        }

        return Grouped;
    }

    public void GenerateGradient(List<GroupedPosition> groupedPositions)
    {
        var startColor = new Vector3(255, 106, 0); // Orange (oldest)
        var endColor = new Vector3(0, 78, 255);     // Blue (newest)
        var gradientColors = new List<string>();

        var versions = groupedPositions.Select(g => g.Version).Distinct().Select(s => ProperVersion.SemVer.Parse(s)).OrderDescending().ToList();

        // Handle single version (avoid division by zero)
        if (versions.Count == 1)
        {
            // Use start color (orange) for single version
            var r = (int)startColor.X;
            var g = (int)startColor.Y;
            var b = (int)startColor.Z;
            gradientColors.Add($"#{r:X2}{g:X2}{b:X2}");
        }
        else
        {
            // Generate gradient for multiple versions
            var num = versions.Count - 1;
            for (var i = 0; i <= num; i++)
            {
                var ratio = i / (float)num;

                var r = (int)(startColor.X + ratio * (endColor.X - startColor.X));
                var g = (int)(startColor.Y + ratio * (endColor.Y - startColor.Y));
                var b = (int)(startColor.Z + ratio * (endColor.Z - startColor.Z));

                gradientColors.Add($"#{r:X2}{g:X2}{b:X2}");
            }
        }

        for (var index = 0; index < versions.Count; index++)
        {
            var version = versions[index];
            _colors[version.ToString()] = gradientColors[index];
        }
    }
}