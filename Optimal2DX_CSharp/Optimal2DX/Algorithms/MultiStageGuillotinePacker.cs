using System;
using System.Collections.Generic;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    public class MultiStageGuillotinePacker
    {
        private const double Eps = 0.001;

        public static List<SheetLayout> Pack(
            List<DemandPiece> demandPieces,
            List<RepositoryPiece> repositoryPieces,
            Chromosome chromosome,
            double bladeWidth,
            int firstDirection, // 0 = Horizontal first, 1 = Vertical first
            int numberOfStages,
            int onlySameHeightInStrip,
            int heightTolerance)
        {
            bladeWidth = Math.Round(Math.Max(0.0, bladeWidth), 1, MidpointRounding.AwayFromZero);

            var sheets = new List<SheetLayout>();
            var placed = new bool[demandPieces.Count];
            int totalPlacedCount = 0;

            for (int r = 0; r < repositoryPieces.Count && totalPlacedCount < demandPieces.Count; r++)
            {
                var repo = repositoryPieces[r];
                if (repo.UsableLength <= 0 || repo.UsableWidth <= 0) continue;

                var layout = new SheetLayout(sheets.Count, repo.Index, repo.ExternalID, repo.Length, repo.Width, repo.Label, repo.Material);

                double startX = repo.TrimLeft;
                double startY = repo.TrimTop;
                double usableW = repo.UsableLength;
                double usableH = repo.UsableWidth;

                // Strip-based packing (Shelf algorithm)
                double currentX = startX;
                double currentY = startY;
                double currentStripThickness = 0; // strip height (if horizontal) or strip width (if vertical)
                bool horizontalFirst = (firstDirection == 0);

                bool placedAny = false;

                for (int step = 0; step < chromosome.Order.Length; step++)
                {
                    int pieceIdx = chromosome.Order[step];
                    if (placed[pieceIdx]) continue;

                    var piece = demandPieces[pieceIdx];

                    // Check orientations
                    double pw = piece.Length;
                    double ph = piece.Width;
                    bool rotated = false;

                    if (piece.CanRotate == 1 && chromosome.Rotated[pieceIdx])
                    {
                        pw = piece.Width;
                        ph = piece.Length;
                        rotated = true;
                    }

                    if (horizontalFirst)
                    {
                        // Horizontal strips: strips stacked along Y axis, items placed along X axis
                        if (currentX + pw > startX + usableW + Eps)
                        {
                            // Close current strip, move to next strip
                            if (currentStripThickness > Eps)
                            {
                                double stripEndY = Math.Round(currentY + currentStripThickness, 1, MidpointRounding.AwayFromZero);
                                layout.Cuts.Add(new Cut(startX, stripEndY, startX + usableW, stripEndY, bladeWidth, level: 1));
                                currentY = Math.Round(stripEndY + bladeWidth, 1, MidpointRounding.AwayFromZero);
                                currentX = startX;
                                currentStripThickness = 0;
                            }
                        }

                        // Check if fits vertically
                        if (currentY + ph <= startY + usableH + Eps && currentX + pw <= startX + usableW + Eps)
                        {
                            layout.UtilizedPieces.Add(new UtilizedPiece(
                                currentX, currentY, Math.Round(currentX + pw, 1, MidpointRounding.AwayFromZero), Math.Round(currentY + ph, 1, MidpointRounding.AwayFromZero),
                                rotated ? 1 : 0, piece.Index, piece.ExternalID, piece.Grain, piece.Label, piece.Material
                            ));

                            // Stage 2 cut: vertical cut separating piece in strip
                            layout.Cuts.Add(new Cut(currentX + pw, currentY, currentX + pw, currentY + ph, bladeWidth, level: 2));

                            currentX = Math.Round(currentX + pw + bladeWidth, 1, MidpointRounding.AwayFromZero);
                            if (ph > currentStripThickness)
                            {
                                currentStripThickness = ph;
                            }

                            placed[pieceIdx] = true;
                            totalPlacedCount++;
                            placedAny = true;
                        }
                    }
                    else
                    {
                        // Vertical strips: strips stacked along X axis, items placed along Y axis
                        if (currentY + ph > startY + usableH + Eps)
                        {
                            if (currentStripThickness > Eps)
                            {
                                double stripEndX = Math.Round(currentX + currentStripThickness, 1, MidpointRounding.AwayFromZero);
                                layout.Cuts.Add(new Cut(stripEndX, startY, stripEndX, startY + usableH, bladeWidth, level: 1));
                                currentX = Math.Round(stripEndX + bladeWidth, 1, MidpointRounding.AwayFromZero);
                                currentY = startY;
                                currentStripThickness = 0;
                            }
                        }

                        if (currentX + pw <= startX + usableW + Eps && currentY + ph <= startY + usableH + Eps)
                        {
                            layout.UtilizedPieces.Add(new UtilizedPiece(
                                currentX, currentY, Math.Round(currentX + pw, 1, MidpointRounding.AwayFromZero), Math.Round(currentY + ph, 1, MidpointRounding.AwayFromZero),
                                rotated ? 1 : 0, piece.Index, piece.ExternalID, piece.Grain, piece.Label, piece.Material
                            ));

                            // Stage 2 cut: horizontal cut separating piece in vertical strip
                            layout.Cuts.Add(new Cut(currentX, currentY + ph, currentX + pw, currentY + ph, bladeWidth, level: 2));

                            currentY = Math.Round(currentY + ph + bladeWidth, 1, MidpointRounding.AwayFromZero);
                            if (pw > currentStripThickness)
                            {
                                currentStripThickness = pw;
                            }

                            placed[pieceIdx] = true;
                            totalPlacedCount++;
                            placedAny = true;
                        }
                    }
                }

                if (placedAny)
                {
                    // Compute waste pieces (remaining unallocated area)
                    if (horizontalFirst)
                    {
                        double remY = Math.Round(currentY + currentStripThickness, 1, MidpointRounding.AwayFromZero);
                        if (remY < startY + usableH - Eps)
                        {
                            layout.WastePieces.Add(new WastePiece(startX, remY, startX + usableW, startY + usableH));
                        }
                        if (currentX < startX + usableW - Eps && currentStripThickness > Eps)
                        {
                            layout.WastePieces.Add(new WastePiece(currentX, currentY, startX + usableW, currentY + currentStripThickness));
                        }
                    }
                    else
                    {
                        double remX = Math.Round(currentX + currentStripThickness, 1, MidpointRounding.AwayFromZero);
                        if (remX < startX + usableW - Eps)
                        {
                            layout.WastePieces.Add(new WastePiece(remX, startY, startX + usableW, startY + usableH));
                        }
                        if (currentY < startY + usableH - Eps && currentStripThickness > Eps)
                        {
                            layout.WastePieces.Add(new WastePiece(currentX, currentY, currentX + currentStripThickness, startY + usableH));
                        }
                    }

                    sheets.Add(layout);
                }
            }

            return sheets;
        }
    }
}
