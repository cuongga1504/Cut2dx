namespace Optimal2DX.Models
{
    public class Cut
    {
        public int TopX { get; set; }
        public int TopY { get; set; }
        public int BottomX { get; set; }
        public int BottomY { get; set; }
        public int CutThickness { get; set; }
        public int Level { get; set; } // Stage / hierarchy level

        public double Length
        {
            get
            {
                int dx = BottomX - TopX;
                int dy = BottomY - TopY;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public Cut(int topX, int topY, int bottomX, int bottomY, int cutThickness, int level = 0)
        {
            TopX = topX;
            TopY = topY;
            BottomX = bottomX;
            BottomY = bottomY;
            CutThickness = cutThickness;
            Level = level;
        }
    }
}
