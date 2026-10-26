using System;
using System.Collections.Generic;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    public class NestedPacker
    {
        public static List<SheetLayout> Pack(
            List<DemandPiece> demandPieces,
            List<RepositoryPiece> repositoryPieces,
            Chromosome chromosome,
            int bladeWidth)
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
                    int bestShortSide = int.MaxValue;
                    int bestLongSide = int.MaxValue;

                    for (int i = 0; i < freeRects.Count; i++)
                    {
                        var fr = freeRects[i];

                        bool tryNormal = true;
                        bool tryRot = piece.CanRotate == 1;

                        if (tryNormal && piece.Length <= fr.Width && piece.Width <= fr.Height)
                        {
                            int shortSide = Math.Min(fr.Width - piece.Length, fr.Height - piece.Width);
                            int longSide = Math.Max(fr.Width - piece.Length, fr.Height - piece.Width);
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
                            int shortSide = Math.Min(fr.Width - piece.Width, fr.Height - piece.Length);
                            int longSide = Math.Max(fr.Width - piece.Width, fr.Height - piece.Length);
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
                        var targetRect = freeRects[bestRectIdx];
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
                        if (fr.Width > 0 && fr.Height > 0)
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
                if (placed.X < fr.X + fr.Width && placed.X + placed.Width > fr.X &&
                    placed.Y < fr.Y + fr.Height && placed.Y + placed.Height > fr.Y)
                {
                    // Overlaps, split into up to 4 sub-rectangles
                    if (placed.X > fr.X && placed.X < fr.X + fr.Width)
                    {
                        freeRects.Add(new FreeRectangle(fr.X, fr.Y, placed.X - fr.X, fr.Height));
                    }
                    if (placed.X + placed.Width < fr.X + fr.Width)
                    {
                        freeRects.Add(new FreeRectangle(placed.X + placed.Width, fr.Y, fr.X + fr.Width - (placed.X + placed.Width), fr.Height));
                    }
                    if (placed.Y > fr.Y && placed.Y < fr.Y + fr.Height)
                    {
                        freeRects.Add(new FreeRectangle(fr.X, fr.Y, fr.Width, placed.Y - fr.Y));
                    }
                    if (placed.Y + placed.Height < fr.Y + fr.Height)
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
                    if (b.X >= a.X && b.Y >= a.Y && b.X + b.Width <= a.X + a.Width && b.Y + b.Height <= a.Y + a.Height)
                    {
                        freeRects.RemoveAt(j);
                        j--;
                    }
                    // If a is inside b, remove a
                    else if (a.X >= b.X && a.Y >= b.Y && a.X + a.Width <= b.X + b.Width && a.Y + a.Height <= b.Y + b.Height)
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
