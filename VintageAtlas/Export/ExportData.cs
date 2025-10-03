using System.Collections.Generic;
using ProtoBuf;

namespace VintageAtlas.Export;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ExportData
{
    public Dictionary<string, uint[]> Blocks { get; set; } = new();
}