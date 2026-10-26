using System.Collections.Generic;

namespace Optimal2DX.Models
{
    public class SheetLayout
    {
        public int SheetIndex { get; set; }
        public int RepositoryIndex { get; set; }
        public int RepositoryExternalID { get; set; }
        public int SheetLength { get; set; }
        public int SheetWidth { get; set; }
        public string RepositoryLabel { get; set; } = string.Empty;

        public List<UtilizedPiece> UtilizedPieces { get; set; } = new List<UtilizedPiece>();
        public List<Cut> Cuts { get; set; } = new List<Cut>();
        public List<WastePiece> WastePieces { get; set; } = new List<WastePiece>();

        public double SurfaceCovered
        {
            get
            {
                long totalSheetArea = (long)SheetLength * SheetWidth;
                if (totalSheetArea <= 0) return 0;
                long totalUtilizedArea = 0;
                foreach (var piece in UtilizedPieces)
                {
                    totalUtilizedArea += piece.Area;
                }
                return ((double)totalUtilizedArea / totalSheetArea) * 100.0;
            }
        }

        public double TotalCutLength
        {
            get
            {
                double total = 0;
                foreach (var cut in Cuts)
                {
                    total += cut.Length;
                }
                return total;
            }
        }

        public double WasteSurface
        {
            get
            {
                double total = 0;
                foreach (var waste in WastePieces)
                {
                    total += waste.Area;
                }
                return total;
            }
        }

        public double GarbageWaste(int minimalUsefulSize)
        {
            double total = 0;
            foreach (var waste in WastePieces)
            {
                if (waste.Width < minimalUsefulSize || waste.Height < minimalUsefulSize)
                {
                    total += waste.Area;
                }
            }
            return total;
        }

        public SheetLayout(int sheetIndex, int repositoryIndex, int repositoryExternalId, int sheetLength, int sheetWidth, string repositoryLabel = "")
        {
            SheetIndex = sheetIndex;
            RepositoryIndex = repositoryIndex;
            RepositoryExternalID = repositoryExternalId;
            SheetLength = sheetLength;
            SheetWidth = sheetWidth;
            RepositoryLabel = repositoryLabel ?? string.Empty;
        }
    }
}
