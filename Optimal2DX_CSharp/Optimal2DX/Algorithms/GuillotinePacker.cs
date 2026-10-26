using System;
using System.Collections.Generic;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    public class FreeRectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public FreeRectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public class GuillotinePacker
    {
        public static List<SheetLayout> Pack(
            List<DemandPiece> demandPieces,
            List<RepositoryPiece> repositoryPieces,
            Chromosome chromosome,
            int bladeWidth,
            int cutAllStripsFirst,
            int maximalCutLength)
        {
            var sheets = new List<SheetLayout>();
            var placed = new bool[demandPieces.Count];
            int totalPlacedCount = 0;

            for (int r = 0; r < repositoryPieces.Count && totalPlacedCount < demandPieces.Count; r++)
            {
                var repo = repositoryPieces[r];
                if (repo.UsableLength <= 0 || repo.UsableWidth <= 0) continue;

                var layout = new SheetLayout(sheets.Count, repo.Index, repo.ExternalID, repo.Length, repo.Width, repo.Label);

                // Origin offset by Trim
                int startX = repo.TrimLeft;
                int startY = repo.TrimTop;
                int usableW = repo.UsableLength;
                int usableH = repo.UsableWidth;

                var freeRects = new List<FreeRectangle>
                {
                    new FreeRectangle(startX, startY, usableW, usableH)
                };

                // Place pieces according to chromosome sequence
                bool placedAnyOnThisSheet = false;
                for (int step = 0; step < chromosome.Order.Length; step++)
                {
                    int pieceIdx = chromosome.Order[step];
                    if (placed[pieceIdx]) continue;

                    var piece = demandPieces[pieceIdx];

                    // Find best free rectangle
                    int bestRectIdx = -1;
                    bool bestRotated = false;
                    int bestShortSide = int.MaxValue;
                    int bestLongSide = int.MaxValue;

                    for (int i = 0; i < freeRects.Count; i++)
                    {
                        var fr = freeRects[i];

                        // Test orientation from chromosome, or best fit
                        bool tryNormal = true;
                        bool tryRot = piece.CanRotate == 1;

                        if (tryNormal && piece.Length <= fr.Width && piece.Width <= fr.Height)
                        {
                            int leftoverH = fr.Width - piece.Length;
                            int leftoverV = fr.Height - piece.Width;
                            int shortSide = Math.Min(leftoverH, leftoverV);
                            int longSide = Math.Max(leftoverH, leftoverV);

                            if (shortSide < bestShortSide || (shortSide == bestShortSide && longSide < bestLongSide))
                            {
                                bestShortSide = shortSide;
                                bestLongSide = longSide;
                                bestRectIdx = i;
                                bestRotated = false;
                            }
                        }

                        if (tryRot && piece.Width <= fr.Width && piece.Length <= fr.Height)
                        {
                            int leftoverH = fr.Width - piece.Width;
                            int leftoverV = fr.Height - piece.Length;
                            int shortSide = Math.Min(leftoverH, leftoverV);
                            int longSide = Math.Max(leftoverH, leftoverV);

                            if (shortSide < bestShortSide || (shortSide == bestShortSide && longSide < bestLongSide))
                            {
                                bestShortSide = shortSide;
                                bestLongSide = longSide;
                                bestRectIdx = i;
                                bestRotated = true;
                            }
                        }
                    }

                    if (bestRectIdx >= 0)
                    {
                        // Place piece in freeRects[bestRectIdx]
                        var targetRect = freeRects[bestRectIdx];
                        freeRects.RemoveAt(bestRectIdx);

                        int pw = bestRotated ? piece.Width : piece.Length;
                        int ph = bestRotated ? piece.Length : piece.Width;

                        int topX = targetRect.X;
                        int topY = targetRect.Y;
                        int bottomX = topX + pw;
                        int bottomY = topY + ph;

                        layout.UtilizedPieces.Add(new UtilizedPiece(
                            topX, topY, bottomX, bottomY,
                            bestRotated ? 1 : 0,
                            piece.Index,
                            piece.ExternalID,
                            piece.Grain,
                            piece.Label
                        ));

                        placed[pieceIdx] = true;
                        totalPlacedCount++;
                        placedAnyOnThisSheet = true;

                        // Perform guillotine split and record cuts
                        PerformGuillotineSplit(
                            targetRect, pw, ph, bladeWidth,
                            chromosome.SplitDirection[pieceIdx],
                            cutAllStripsFirst,
                            maximalCutLength,
                            layout.Cuts,
                            freeRects
                        );
                    }
                }

                if (placedAnyOnThisSheet)
                {
                    // Remaining free rectangles become waste pieces
                    foreach (var fr in freeRects)
                    {
                        if (fr.Width > 0 && fr.Height > 0)
                        {
                            layout.WastePieces.Add(new WastePiece(fr.X, fr.Y, fr.X + fr.Width, fr.Y + fr.Height));
                        }
                    }

                    // Add trim margins as waste pieces if applicable
                    AddTrimWastes(repo, layout.WastePieces);

                    sheets.Add(layout);
                }
            }

            return sheets;
        }

        private static void PerformGuillotineSplit(
            FreeRectangle rect,
            int pw,
            int ph,
            int blade,
            byte splitGene,
            int cutAllStripsFirst,
            int maxCutLength,
            List<Cut> cuts,
            List<FreeRectangle> freeRects)
        {
            int w = rect.Width;
            int h = rect.Height;
            int x = rect.X;
            int y = rect.Y;

            if (pw == w && ph == h)
            {
                return;
            }

            if (pw == w && ph < h)
            {
                // Single horizontal cut
                cuts.Add(new Cut(x, y + ph, x + w, y + ph, blade));
                int remH = h - ph - blade;
                if (remH > 0)
                {
                    freeRects.Add(new FreeRectangle(x, y + ph + blade, w, remH));
                }
                return;
            }

            if (pw < w && ph == h)
            {
                // Single vertical cut
                cuts.Add(new Cut(x + pw, y, x + pw, y + h, blade));
                int remW = w - pw - blade;
                if (remW > 0)
                {
                    freeRects.Add(new FreeRectangle(x + pw + blade, y, remW, h));
                }
                return;
            }

            // Two cuts needed
            // Determine split direction: 0 = Horizontal first, 1 = Vertical first
            bool horizontalFirst;
            if (cutAllStripsFirst == 1)
            {
                horizontalFirst = true;
            }
            else if (cutAllStripsFirst == 2)
            {
                horizontalFirst = false;
            }
            else
            {
                // Shorter axis split or gene
                horizontalFirst = splitGene == 0 ? (w - pw <= h - ph) : (w - pw > h - ph);
            }

            if (horizontalFirst)
            {
                // Cut 1: Horizontal across strip
                cuts.Add(new Cut(x, y + ph, x + w, y + ph, blade));
                // Cut 2: Vertical in upper strip
                cuts.Add(new Cut(x + pw, y, x + pw, y + ph, blade));

                int rightW = w - pw - blade;
                if (rightW > 0)
                {
                    freeRects.Add(new FreeRectangle(x + pw + blade, y, rightW, ph));
                }

                int bottomH = h - ph - blade;
                if (bottomH > 0)
                {
                    freeRects.Add(new FreeRectangle(x, y + ph + blade, w, bottomH));
                }
            }
            else
            {
                // Cut 1: Vertical down strip
                cuts.Add(new Cut(x + pw, y, x + pw, y + h, blade));
                // Cut 2: Horizontal in left strip
                cuts.Add(new Cut(x, y + ph, x + pw, y + ph, blade));

                int bottomH = h - ph - blade;
                if (bottomH > 0)
                {
                    freeRects.Add(new FreeRectangle(x, y + ph + blade, pw, bottomH));
                }

                int rightW = w - pw - blade;
                if (rightW > 0)
                {
                    freeRects.Add(new FreeRectangle(x + pw + blade, y, rightW, h));
                }
            }
        }

        private static void AddTrimWastes(RepositoryPiece repo, List<WastePiece> wastes)
        {
            if (repo.TrimTop > 0)
            {
                wastes.Add(new WastePiece(0, 0, repo.Length, repo.TrimTop));
            }
            if (repo.TrimBottom > 0)
            {
                wastes.Add(new WastePiece(0, repo.Width - repo.TrimBottom, repo.Length, repo.Width));
            }
            if (repo.TrimLeft > 0)
            {
                wastes.Add(new WastePiece(0, repo.TrimTop, repo.TrimLeft, repo.Width - repo.TrimBottom));
            }
            if (repo.TrimRight > 0)
            {
                wastes.Add(new WastePiece(repo.Length - repo.TrimRight, repo.TrimTop, repo.Length, repo.Width - repo.TrimBottom));
            }
        }
    }
}
