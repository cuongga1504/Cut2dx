using System;

namespace Optimal2DX.Models
{
    public class Cut
    {
        public double TopX { get; set; }
        public double TopY { get; set; }
        public double BottomX { get; set; }
        public double BottomY { get; set; }
        public double CutThickness { get; set; }
        public int Level { get; set; } // Stage / hierarchy level

        public double Length
        {
            get
            {
                double dx = BottomX - TopX;
                double dy = BottomY - TopY;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public Cut(double topX, double topY, double bottomX, double bottomY, double cutThickness, int level = 0)
        {
            TopX = Math.Round(topX, 1, MidpointRounding.AwayFromZero);
            TopY = Math.Round(topY, 1, MidpointRounding.AwayFromZero);
            BottomX = Math.Round(bottomX, 1, MidpointRounding.AwayFromZero);
            BottomY = Math.Round(bottomY, 1, MidpointRounding.AwayFromZero);
            CutThickness = Math.Round(cutThickness, 1, MidpointRounding.AwayFromZero);
            Level = level;
        }
    }
}
