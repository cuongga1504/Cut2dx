using System;

namespace Optimal2DX.Models
{
    public class WastePiece
    {
        public double TopX { get; set; }
        public double TopY { get; set; }
        public double BottomX { get; set; }
        public double BottomY { get; set; }

        public double Width => Math.Round(Math.Abs(BottomX - TopX), 1, MidpointRounding.AwayFromZero);
        public double Height => Math.Round(Math.Abs(BottomY - TopY), 1, MidpointRounding.AwayFromZero);
        public double Area => Math.Round(Width * Height, 2, MidpointRounding.AwayFromZero);

        public WastePiece(double topX, double topY, double bottomX, double bottomY)
        {
            TopX = Math.Round(topX, 1, MidpointRounding.AwayFromZero);
            TopY = Math.Round(topY, 1, MidpointRounding.AwayFromZero);
            BottomX = Math.Round(bottomX, 1, MidpointRounding.AwayFromZero);
            BottomY = Math.Round(bottomY, 1, MidpointRounding.AwayFromZero);
        }
    }
}
