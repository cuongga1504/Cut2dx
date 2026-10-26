using System;

namespace Optimal2DX.Models
{
    public class UtilizedPiece
    {
        public int TopX { get; set; }
        public int TopY { get; set; }
        public int BottomX { get; set; }
        public int BottomY { get; set; }
        public int Rotated { get; set; } // 1 = Rotated, 0 = Normal
        public int DemandIndex { get; set; }
        public int DemandExternalID { get; set; }
        public GrainDirection Grain { get; set; } = GrainDirection.None;
        public string Label { get; set; } = string.Empty;

        public int Width => Math.Abs(BottomX - TopX);
        public int Height => Math.Abs(BottomY - TopY);
        public long Area => (long)Width * Height;

        public UtilizedPiece(int topX, int topY, int bottomX, int bottomY, int rotated, int demandIndex, int demandExternalId, GrainDirection grain = GrainDirection.None, string label = "")
        {
            TopX = topX;
            TopY = topY;
            BottomX = bottomX;
            BottomY = bottomY;
            Rotated = rotated;
            DemandIndex = demandIndex;
            DemandExternalID = demandExternalId;
            Grain = grain;
            Label = label ?? string.Empty;
        }
    }
}
