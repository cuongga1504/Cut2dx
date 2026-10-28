using System;
using System.Collections.Generic;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    public class FreeRectangle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public FreeRectangle(double x, double y, double width, double height)
        {
            X = Math.Round(x, 1, MidpointRounding.AwayFromZero);
            Y = Math.Round(y, 1, MidpointRounding.AwayFromZero);
            Width = Math.Round(width, 1, MidpointRounding.AwayFromZero);
            Height = Math.Round(height, 1, MidpointRounding.AwayFromZero);
        }
    }

    public class GuillotinePacker
    {
        private const double Eps = 0.001;

        public static List<SheetLayout> Pack(
            List<DemandPiece> demandPieces,
            List<RepositoryPiece> repositoryPieces,
            Chromosome chromosome,
            double bladeWidth,
            int cutAllStripsFirst,
            int maximalCutLength)
        {
            bladeWidth = Math.Round(Math.Max(0.0, bladeWidth), 1, MidpointRounding.AwayFromZero);

            var sheets = new List<SheetLayout>();
            var placed = new bool[demandPieces.Count];
            int totalPlacedCount = 0;

            for (int r = 0; r < repositoryPieces.Count && totalPlacedCount < demandPieces.Count; r++)
            {
                var repo = repositoryPieces[r];
                if (repo.UsableLength <= 0 || repo.UsableWidth <= 0) continue;

                var layout = new SheetLayout(sheets.Count, repo.Index, repo.ExternalID, repo.Length, repo.Width, repo.Label);

                // Origin offset by Trim
                double startX = repo.TrimLeft;
                double startY = repo.TrimTop;
                double usableW = repo.UsableLength;
                double usableH = repo.UsableWidth;

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
                    double bestShortSide = double.MaxValue;
                    double bestLongSide = double.MaxValue;

                    for (int i = 0; i < freeRects.Count; i++)
                    {
                        var fr = freeRects[i];

                        // Test orientation from chromosome, or best fit
                        bool tryNormal = true;
                        bool tryRot = piece.CanRotate == 1;

                        if (tryNormal && piece.Length <= fr.Width + Eps && piece.Width <= fr.Height + Eps)
                        {
                            double leftoverH = fr.Width - piece.Length;
                            double leftoverV = fr.Height - piece.Width;
                            double shortSide = Math.Min(leftoverH, leftoverV);
                            double longSide = Math.Max(leftoverH, leftoverV);

                            if (shortSide < bestShortSide || (Math.Abs(shortSide - bestShortSide) < Eps && longSide < bestLongSide))
                            {
                                bestShortSide = shortSide;
                                bestLongSide = longSide;
                                bestRectIdx = i;
                                bestRotated = false;
                            }
                        }

                        if (tryRot && piece.Width <= fr.Width + Eps && piece.Length <= fr.Height + Eps)
                        {
                            double leftoverH = fr.Width - piece.Width;
                            double leftoverV = fr.Height - piece.Length;
                            double shortSide = Math.Min(leftoverH, leftoverV);
                            double longSide = Math.Max(leftoverH, leftoverV);

                            if (shortSide < bestShortSide || (Math.Abs(shortSide - bestShortSide) < Eps && longSide < bestLongSide))
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

                        double pw = bestRotated ? piece.Width : piece.Length;
                        double ph = bestRotated ? piece.Length : piece.Width;

                        double topX = targetRect.X;
                        double topY = targetRect.Y;
                        double bottomX = Math.Round(topX + pw, 1, MidpointRounding.AwayFromZero);
                        double bottomY = Math.Round(topY + ph, 1, MidpointRounding.AwayFromZero);

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
                        if (fr.Width > Eps && fr.Height > Eps)
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
            double pw,
            double ph,
            double blade,
            byte splitGene,
            int cutAllStripsFirst,
            int maxCutLength,
            List<Cut> cuts,
            List<FreeRectangle> freeRects)
        {
            double w = rect.Width;
            double h = rect.Height;
            double x = rect.X;
            double y = rect.Y;

            bool sameW = Math.Abs(pw - w) < Eps;
            bool sameH = Math.Abs(ph - h) < Eps;

            if (sameW && sameH)
            {
                return;
            }

            if (sameW && ph < h)
            {
                // Single horizontal cut
                cuts.Add(new Cut(x, y + ph, x + w, y + ph, blade));
                double remH = Math.Round(h - ph - blade, 1, MidpointRounding.AwayFromZero);
                if (remH > Eps)
                {
                    freeRects.Add(new FreeRectangle(x, y + ph + blade, w, remH));
                }
                return;
            }

            if (pw < w && sameH)
            {
                // Single vertical cut
                cuts.Add(new Cut(x + pw, y, x + pw, y + h, blade));
                double remW = Math.Round(w - pw - blade, 1, MidpointRounding.AwayFromZero);
                if (remW > Eps)
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

                double rightW = Math.Round(w - pw - blade, 1, MidpointRounding.AwayFromZero);
                if (rightW > Eps)
                {
                    freeRects.Add(new FreeRectangle(x + pw + blade, y, rightW, ph));
                }

                double bottomH = Math.Round(h - ph - blade, 1, MidpointRounding.AwayFromZero);
                if (bottomH > Eps)
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

                double bottomH = Math.Round(h - ph - blade, 1, MidpointRounding.AwayFromZero);
                if (bottomH > Eps)
                {
                    freeRects.Add(new FreeRectangle(x, y + ph + blade, pw, bottomH));
                }

                double rightW = Math.Round(w - pw - blade, 1, MidpointRounding.AwayFromZero);
                if (rightW > Eps)
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
