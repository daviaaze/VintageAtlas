using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ProtoBuf;

namespace VintageAtlas.Export;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ExportData
{
    /// <summary>
    /// Block color mappings. Uses int[] internally to handle signed color values from JSON,
    /// but provides conversion to uint[] for compatibility.
    /// Colors in ARGB format can be negative when serialized to JSON.
    /// </summary>
    [JsonProperty("Blocks")]
    public Dictionary<string, int[]> BlocksRaw { get; set; } = new();
    
    /// <summary>
    /// Get blocks as uint[] by converting signed integers to unsigned.
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, uint[]> Blocks => 
        BlocksRaw.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(i => unchecked((uint)i)).ToArray()
        );
}