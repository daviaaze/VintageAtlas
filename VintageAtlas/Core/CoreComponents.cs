using VintageAtlas.Export;
using VintageAtlas.Storage;
using VintageAtlas.Models.API;
using VintageAtlas.Web.API;

namespace VintageAtlas.Core;

/// <summary>
/// Container for core VintageAtlas components
/// </summary>
public record CoreComponents(
    MbTilesStorage Storage,
    BlockColorCache ColorCache,
    MapConfigController MapConfigController,
    MapExporter MapExporter
);