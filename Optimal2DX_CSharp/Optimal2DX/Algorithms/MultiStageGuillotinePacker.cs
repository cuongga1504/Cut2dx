using System;
using System.Collections.Generic;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    public class MultiStageGuillotinePacker
    {
        public static List<SheetLayout> Pack(
            List<DemandPiece> demandPieces,
            List<RepositoryPiece> repositoryPieces,
            Chromosome chromosome,
            int bladeWidth,
            int firstDirection, // 0 = Horizontal first, 1 = Vertical first
            int numberOfStages,
            int onlySameHeightInStrip,
            int heightTolerance)
        {
            var sheets = new List<SheetLayout>();
            var placed = new bool[demandPieces.Count];
            int totalPlacedCount = 0;

            for (int r = 0; r < repositoryPieces.Count && totalPlacedCount < demandPieces.Count; r++)
            {
                var repo = repositoryPieces[r];
                if (repo.UsableLength <= 0 || repo.UsableWidth <= 0) continue;

                var layout = new SheetLayout(sheets.Count, repo.Index, repo.ExternalID, repo.Length, repo.Width, repo.Label);

                int startX = repo.TrimLeft;
                int startY = repo.TrimTop;
                int usableW = repo.UsableLength;
                int usableH = repo.UsableWidth;

                // Strip-based packing (Shelf algorithm)
                int currentX = startX;
                int currentY = startY;
                int currentStripThickness = 0; // strip height (if horizontal) or strip width (if vertical)
                bool horizontalFirst = (firstDirection == 0);

                bool placedAny = false;

                for (int step = 0; step < chromosome.Order.Length; step++)
                {
                    int pieceIdx = chromosome.Order[step];
                    if (placed[pieceIdx]) continue;

                    var piece = demandPieces[pieceIdx];

                    // Check orientations
                    int pw = piece.Length;
                    int ph = piece.Width;
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
                        if (currentX + pw > startX + usableW)
                        {
                            // Close current strip, move to next strip
                            if (currentStripThickness > 0)
                            {
                                int stripEndY = currentY + currentStripThickness;
                                layout.Cuts.Add(new Cut(startX, stripEndY, startX + usableW, stripEndY, bladeWidth, level: 1));
                                currentY = stripEndY + bladeWidth;
                                currentX = startX;
                                currentStripThickness = 0;
                            }
                        }

                        // Check if fits vertically
                        if (currentY + ph <= startY + usableH && currentX + pw <= startX + usableW)
                        {
                            layout.UtilizedPieces.Add(new UtilizedPiece(
                                currentX, currentY, currentX + pw, currentY + ph,
                                rotated ? 1 : 0, piece.Index, piece.ExternalID, piece.Grain, piece.Label
                            ));

                            // Stage 2 cut: vertical cut separating piece in strip
                            layout.Cuts.Add(new Cut(currentX + pw, currentY, currentX + pw, currentY + ph, bladeWidth, level: 2));

                            currentX += pw + bladeWidth;
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
                        if (currentY + ph > startY + usableH)
                        {
                            if (currentStripThickness > 0)
                            {
                                int stripEndX = currentX + currentStripThickness;
                                layout.Cuts.Add(new Cut(stripEndX, startY, stripEndX, startY + usableH, bladeWidth, level: 1));
                                currentX = stripEndX + bladeWidth;
                                currentY = startY;
                                currentStripThickness = 0;
                            }
                        }

                        if (currentX + pw <= startX + usableW && currentY + ph <= startY + usableH)
                        {
                            layout.UtilizedPieces.Add(new UtilizedPiece(
                                currentX, currentY, currentX + pw, currentY + ph,
                                rotated ? 1 : 0, piece.Index, piece.ExternalID, piece.Grain, piece.Label
                            ));

                            // Stage 2 cut: horizontal cut separating piece in vertical strip
                            layout.Cuts.Add(new Cut(currentX, currentY + ph, currentX + pw, currentY + ph, bladeWidth, level: 2));

                            currentY += ph + bladeWidth;
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
                        int remY = currentY + currentStripThickness;
                        if (remY < startY + usableH)
                        {
                            layout.WastePieces.Add(new WastePiece(startX, remY, startX + usableW, startY + usableH));
                        }
                        if (currentX < startX + usableW && currentStripThickness > 0)
                        {
                            layout.WastePieces.Add(new WastePiece(currentX, currentY, startX + usableW, currentY + currentStripThickness));
                        }
                    }
                    else
                    {
                        int remX = currentX + currentStripThickness;
                        if (remX < startX + usableW)
                        {
                            layout.WastePieces.Add(new WastePiece(remX, startY, startX + usableW, startY + usableH));
                        }
                        if (currentY < startY + usableH && currentStripThickness > 0)
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
