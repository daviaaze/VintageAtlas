using VintageAtlas.Export;
using VintageAtlas.Export.Colors;
using VintageAtlas.Storage;
using VintageAtlas.Web.API;

namespace VintageAtlas.Core;

/// <summary>
/// Container for core VintageAtlas components
/// </summary>
public record CoreComponents(
    MbTilesStorage Storage,
    MetadataStorage MetadataStorage,
    BlockColorCache ColorCache,
    MapConfigController MapConfigController,
    MapExporter MapExporter
);