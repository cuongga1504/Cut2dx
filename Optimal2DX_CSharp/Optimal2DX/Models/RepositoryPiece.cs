using System;

namespace Optimal2DX.Models
{
    public class RepositoryPiece
    {
        public int Index { get; set; }
        public int Length { get; set; }
        public int Width { get; set; }
        public int TrimTop { get; set; }
        public int TrimLeft { get; set; }
        public int TrimBottom { get; set; }
        public int TrimRight { get; set; }
        public int ExternalID { get; set; }
        public int Priority { get; set; }
        public int NumberOfHoles { get; set; }
        public SheetGrainDirection Grain { get; set; } = SheetGrainDirection.AlongLength;
        public string Label { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;

        public int UsableLength => Math.Max(0, Length - TrimLeft - TrimRight);
        public int UsableWidth => Math.Max(0, Width - TrimTop - TrimBottom);
        public long TotalArea => (long)Length * Width;
        public long UsableArea => (long)UsableLength * UsableWidth;

        public RepositoryPiece(int index, int length, int width, int trimTop, int trimLeft, int trimBottom, int trimRight, int externalId, int priority, int numberOfHoles, SheetGrainDirection grain = SheetGrainDirection.AlongLength, string label = "", string material = "")
        {
            Index = index;
            Length = length;
            Width = width;
            TrimTop = trimTop;
            TrimLeft = trimLeft;
            TrimBottom = trimBottom;
            TrimRight = trimRight;
            ExternalID = externalId;
            Priority = priority;
            NumberOfHoles = numberOfHoles;
            Grain = grain;
            Label = label ?? string.Empty;
            Material = material ?? string.Empty;
        }

        public RepositoryPiece Clone()
        {
            return new RepositoryPiece(Index, Length, Width, TrimTop, TrimLeft, TrimBottom, TrimRight, ExternalID, Priority, NumberOfHoles, Grain, Label, Material);
        }
    }
}
