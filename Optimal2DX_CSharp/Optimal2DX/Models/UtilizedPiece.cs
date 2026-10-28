using System;

namespace Optimal2DX.Models
{
    public class UtilizedPiece
    {
        public double TopX { get; set; }
        public double TopY { get; set; }
        public double BottomX { get; set; }
        public double BottomY { get; set; }
        public int Rotated { get; set; } // 1 = Rotated, 0 = Normal
        public int DemandIndex { get; set; }
        public int DemandExternalID { get; set; }
        public GrainDirection Grain { get; set; } = GrainDirection.None;
        public string Label { get; set; } = string.Empty;

        public double Width => Math.Round(Math.Abs(BottomX - TopX), 1, MidpointRounding.AwayFromZero);
        public double Height => Math.Round(Math.Abs(BottomY - TopY), 1, MidpointRounding.AwayFromZero);
        public double Area => Math.Round(Width * Height, 2, MidpointRounding.AwayFromZero);

        public UtilizedPiece(double topX, double topY, double bottomX, double bottomY, int rotated, int demandIndex, int demandExternalId, GrainDirection grain = GrainDirection.None, string label = "")
        {
            TopX = Math.Round(topX, 1, MidpointRounding.AwayFromZero);
            TopY = Math.Round(topY, 1, MidpointRounding.AwayFromZero);
            BottomX = Math.Round(bottomX, 1, MidpointRounding.AwayFromZero);
            BottomY = Math.Round(bottomY, 1, MidpointRounding.AwayFromZero);
            Rotated = rotated;
            DemandIndex = demandIndex;
            DemandExternalID = demandExternalId;
            Grain = grain;
            Label = label ?? string.Empty;
        }
    }
}
