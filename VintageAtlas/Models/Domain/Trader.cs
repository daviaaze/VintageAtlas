using Vintagestory.API.MathTools;

namespace VintageAtlas.Models.Domain;
    public class Trader
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public BlockPos Pos { get; set; } = new BlockPos(0, 0, 0);
    }