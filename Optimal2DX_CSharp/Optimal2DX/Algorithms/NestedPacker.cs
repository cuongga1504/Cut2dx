using System;
using System.Collections.Generic;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    public class NestedPacker
    {
        private const double Eps = 0.001;

        public static List<SheetLayout> Pack(
            List<DemandPiece> demandPieces,
            List<RepositoryPiece> repositoryPieces,
            Chromosome chromosome,
            double bladeWidth)
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

                double startX = repo.TrimLeft;
                double startY = repo.TrimTop;
                double usableW = repo.UsableLength;
                double usableH = repo.UsableWidth;

                // MaxRects free rectangles
                var freeRects = new List<FreeRectangle>
                {
                    new FreeRectangle(startX, startY, usableW, usableH)
                };

                bool placedAny = false;

                for (int step = 0; step < chromosome.Order.Length; step++)
                {
                    int pieceIdx = chromosome.Order[step];
                    if (placed[pieceIdx]) continue;

                    var piece = demandPieces[pieceIdx];

                    int bestRectIdx = -1;
                    bool bestRotated = false;
                    double bestShortSide = double.MaxValue;
                    double bestLongSide = double.MaxValue;

                    for (int i = 0; i < freeRects.Count; i++)
                    {
                        var fr = freeRects[i];

                        bool tryNormal = true;
                        bool tryRot = piece.CanRotate == 1;

                        if (tryNormal && piece.Length <= fr.Width + Eps && piece.Width <= fr.Height + Eps)
                        {
                            double shortSide = Math.Min(fr.Width - piece.Length, fr.Height - piece.Width);
                            double longSide = Math.Max(fr.Width - piece.Length, fr.Height - piece.Width);
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
                            double shortSide = Math.Min(fr.Width - piece.Width, fr.Height - piece.Length);
                            double longSide = Math.Max(fr.Width - piece.Width, fr.Height - piece.Length);
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
                        var targetRect = freeRects[bestRectIdx];
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
                        placedAny = true;

                        // Split all overlapping free rectangles (Maximal Rectangles split)
                        var placedRect = new FreeRectangle(topX, topY, pw + bladeWidth, ph + bladeWidth);
                        SplitFreeRectangles(freeRects, placedRect);
                        PruneFreeRectangles(freeRects);
                    }
                }

                if (placedAny)
                {
                    // For non-guillotine cuttings, cuts list is empty (NumberOfCuttings = 0)
                    // Compute waste pieces from non-overlapping free spaces
                    foreach (var fr in freeRects)
                    {
                        if (fr.Width > Eps && fr.Height > Eps)
                        {
                            layout.WastePieces.Add(new WastePiece(fr.X, fr.Y, fr.X + fr.Width, fr.Y + fr.Height));
                        }
                    }

                    sheets.Add(layout);
                }
            }

            return sheets;
        }

        private static void SplitFreeRectangles(List<FreeRectangle> freeRects, FreeRectangle placed)
        {
            int count = freeRects.Count;
            for (int i = 0; i < count; i++)
            {
                var fr = freeRects[i];
                if (placed.X < fr.X + fr.Width - Eps && placed.X + placed.Width > fr.X + Eps &&
                    placed.Y < fr.Y + fr.Height - Eps && placed.Y + placed.Height > fr.Y + Eps)
                {
                    // Overlaps, split into up to 4 sub-rectangles
                    if (placed.X > fr.X + Eps && placed.X < fr.X + fr.Width - Eps)
                    {
                        freeRects.Add(new FreeRectangle(fr.X, fr.Y, placed.X - fr.X, fr.Height));
                    }
                    if (placed.X + placed.Width < fr.X + fr.Width - Eps)
                    {
                        freeRects.Add(new FreeRectangle(placed.X + placed.Width, fr.Y, fr.X + fr.Width - (placed.X + placed.Width), fr.Height));
                    }
                    if (placed.Y > fr.Y + Eps && placed.Y < fr.Y + fr.Height - Eps)
                    {
                        freeRects.Add(new FreeRectangle(fr.X, fr.Y, fr.Width, placed.Y - fr.Y));
                    }
                    if (placed.Y + placed.Height < fr.Y + fr.Height - Eps)
                    {
                        freeRects.Add(new FreeRectangle(fr.X, placed.Y + placed.Height, fr.Width, fr.Y + fr.Height - (placed.Y + placed.Height)));
                    }

                    freeRects.RemoveAt(i);
                    i--;
                    count--;
                }
            }
        }

        private static void PruneFreeRectangles(List<FreeRectangle> freeRects)
        {
            for (int i = 0; i < freeRects.Count; i++)
            {
                for (int j = i + 1; j < freeRects.Count; j++)
                {
                    var a = freeRects[i];
                    var b = freeRects[j];

                    // If b is inside a, remove b
                    if (b.X >= a.X - Eps && b.Y >= a.Y - Eps && b.X + b.Width <= a.X + a.Width + Eps && b.Y + b.Height <= a.Y + a.Height + Eps)
                    {
                        freeRects.RemoveAt(j);
                        j--;
                    }
                    // If a is inside b, remove a
                    else if (a.X >= b.X - Eps && a.Y >= b.Y - Eps && a.X + a.Width <= b.X + b.Width + Eps && a.Y + a.Height <= b.Y + b.Height + Eps)
                    {
                        freeRects.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }
        }
    }
}
