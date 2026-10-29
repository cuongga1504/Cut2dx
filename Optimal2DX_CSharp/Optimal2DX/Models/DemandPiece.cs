namespace Optimal2DX.Models
{
    public class DemandPiece
    {
        public int Index { get; set; }
        public int Length { get; set; }
        public int Width { get; set; }
        public int CanRotate { get; set; } // 1 = Yes, 0 = No (respects grain direction)
        public int ExternalID { get; set; }
        public int Priority { get; set; }
        public GrainDirection Grain { get; set; } = GrainDirection.None;
        public string Label { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;

        public long Area => (long)Length * Width;

        public DemandPiece(int index, int length, int width, int canRotate, int externalId, int priority, GrainDirection grain = GrainDirection.None, string label = "", string material = "")
        {
            Index = index;
            Length = length;
            Width = width;
            CanRotate = canRotate;
            ExternalID = externalId;
            Priority = priority;
            Grain = grain;
            Label = label ?? string.Empty;
            Material = material ?? string.Empty;
        }

        public DemandPiece Clone()
        {
            return new DemandPiece(Index, Length, Width, CanRotate, ExternalID, Priority, Grain, Label, Material);
        }
    }
}
