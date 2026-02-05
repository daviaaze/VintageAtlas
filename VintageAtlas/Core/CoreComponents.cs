using VintageAtlas.Export.Colors;
using VintageAtlas.Storage;
using VintageAtlas.Web.API;

namespace VintageAtlas.Core;

/// <summary>
/// Container for core VintageAtlas components.
/// Uses interfaces for better testability and dependency inversion.
/// </summary>
public record CoreComponents(
    ITileStorage Storage,
    IMetadataStorage MetadataStorage,
    IBlockColorCache ColorCache,
    IMapConfigController MapConfigController,
    IMapExporter MapExporter
);