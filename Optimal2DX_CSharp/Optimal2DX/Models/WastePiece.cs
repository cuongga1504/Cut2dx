namespace Optimal2DX.Models
{
    public class WastePiece
    {
        public int TopX { get; set; }
        public int TopY { get; set; }
        public int BottomX { get; set; }
        public int BottomY { get; set; }

        public int Width => Math.Abs(BottomX - TopX);
        public int Height => Math.Abs(BottomY - TopY);
        public long Area => (long)Width * Height;

        public WastePiece(int topX, int topY, int bottomX, int bottomY)
        {
            TopX = topX;
            TopY = topY;
            BottomX = bottomX;
            BottomY = bottomY;
        }
    }
}
