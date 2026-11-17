using System;
using System.Collections.Generic;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    public struct FreeRectangle
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

        public double Area => Width * Height;
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
            if (demandPieces == null || demandPieces.Count == 0 || repositoryPieces == null || repositoryPieces.Count == 0)
                return sheets;

            var placed = new bool[demandPieces.Count];
            int totalPlacedCount = 0;

            var freeRects = new List<FreeRectangle>(64);
            var failedSpecsOnSheet = new HashSet<long>();

            for (int r = 0; r < repositoryPieces.Count && totalPlacedCount < demandPieces.Count; r++)
            {
                var repo = repositoryPieces[r];
                if (repo.UsableLength <= 0 || repo.UsableWidth <= 0) continue;

                var layout = new SheetLayout(sheets.Count, repo.Index, repo.ExternalID, repo.Length, repo.Width, repo.Label, repo.Material);

                double startX = repo.TrimLeft;
                double startY = repo.TrimTop;
                double usableW = repo.UsableLength;
                double usableH = repo.UsableWidth;

                freeRects.Clear();
                freeRects.Add(new FreeRectangle(startX, startY, usableW, usableH));
                failedSpecsOnSheet.Clear();

                bool placedAnyOnThisSheet = false;
                double maxFreeW = usableW;
                double maxFreeH = usableH;

                int orderLen = chromosome.Order.Length;
                for (int step = 0; step < orderLen; step++)
                {
                    int pieceIdx = chromosome.Order[step];
                    if (pieceIdx < 0 || pieceIdx >= demandPieces.Count || placed[pieceIdx]) continue;
                    if (freeRects.Count == 0) break;

                    var piece = demandPieces[pieceIdx];

                    bool canFitNormalMax = piece.Length <= maxFreeW + Eps && piece.Width <= maxFreeH + Eps;
                    bool canFitRotMax = piece.CanRotate == 1 && piece.Width <= maxFreeW + Eps && piece.Length <= maxFreeH + Eps;
                    if (!canFitNormalMax && !canFitRotMax)
                        continue;

                    long specKey = ((long)piece.Length << 32) | ((long)(uint)piece.Width << 1) | (uint)(piece.CanRotate & 1);
                    if (failedSpecsOnSheet.Contains(specKey))
                        continue;

                    int bestRectIdx = -1;
                    bool bestRotated = false;
                    double bestPrimaryScore = double.MaxValue;
                    double bestSecondaryScore = double.MaxValue;

                    bool preferRotated = piece.CanRotate == 1
                        && chromosome.Rotated != null
                        && pieceIdx < chromosome.Rotated.Length
                        && chromosome.Rotated[pieceIdx];

                    byte heuristic = chromosome.PlacementHeuristic;

                    for (int i = 0; i < freeRects.Count; i++)
                    {
                        var fr = freeRects[i];

                        // Thử hướng gốc (Normal)
                        if (piece.Length <= fr.Width + Eps && piece.Width <= fr.Height + Eps)
                        {
                            EvaluateCandidate(
                                fr, piece.Length, piece.Width, false, preferRotated, heuristic,
                                i, ref bestRectIdx, ref bestRotated, ref bestPrimaryScore, ref bestSecondaryScore);
                        }

                        // Thử hướng xoay 90 độ (Nếu heuristic == 3 thì ưu tiên giữ hướng gốc dọc theo thớ tấm, chỉ xoay khi hướng gốc không vừa)
                        if (piece.CanRotate == 1 && heuristic != 3 && piece.Width <= fr.Width + Eps && piece.Length <= fr.Height + Eps)
                        {
                            EvaluateCandidate(
                                fr, piece.Width, piece.Length, true, preferRotated, heuristic,
                                i, ref bestRectIdx, ref bestRotated, ref bestPrimaryScore, ref bestSecondaryScore);
                        }
                    }

                    // Fallback cho heuristic == 3: nếu hướng gốc không nhét vừa ô nào mới thử xoay 90 độ
                    if (bestRectIdx < 0 && piece.CanRotate == 1 && heuristic == 3)
                    {
                        for (int i = 0; i < freeRects.Count; i++)
                        {
                            var fr = freeRects[i];
                            if (piece.Width <= fr.Width + Eps && piece.Length <= fr.Height + Eps)
                            {
                                EvaluateCandidate(
                                    fr, piece.Width, piece.Length, true, preferRotated, 0,
                                    i, ref bestRectIdx, ref bestRotated, ref bestPrimaryScore, ref bestSecondaryScore);
                            }
                        }
                    }

                    if (bestRectIdx >= 0)
                    {
                        var targetRect = freeRects[bestRectIdx];
                        int lastIdx = freeRects.Count - 1;
                        freeRects[bestRectIdx] = freeRects[lastIdx];
                        freeRects.RemoveAt(lastIdx);

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
                            piece.Label,
                            piece.Material
                        ));

                        placed[pieceIdx] = true;
                        totalPlacedCount++;
                        placedAnyOnThisSheet = true;

                        byte splitGene = (chromosome.SplitDirection != null && pieceIdx < chromosome.SplitDirection.Length)
                            ? chromosome.SplitDirection[pieceIdx]
                            : (byte)0;

                        PerformGuillotineSplit(
                            targetRect, pw, ph, bladeWidth,
                            splitGene,
                            cutAllStripsFirst,
                            maximalCutLength,
                            layout.Cuts,
                            freeRects
                        );

                        MergeFreeRectangles(freeRects);

                        maxFreeW = 0;
                        maxFreeH = 0;
                        for (int i = 0; i < freeRects.Count; i++)
                        {
                            if (freeRects[i].Width > maxFreeW) maxFreeW = freeRects[i].Width;
                            if (freeRects[i].Height > maxFreeH) maxFreeH = freeRects[i].Height;
                        }
                    }
                    else
                    {
                        failedSpecsOnSheet.Add(specKey);
                    }
                }

                if (placedAnyOnThisSheet)
                {
                    AddTrimCutsAndWastes(repo, bladeWidth, layout.Cuts, layout.WastePieces);

                    for (int i = 0; i < freeRects.Count; i++)
                    {
                        var fr = freeRects[i];
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

        private static void EvaluateCandidate(
            FreeRectangle fr,
            double pw,
            double ph,
            bool isRotated,
            bool preferRotated,
            byte heuristic,
            int rectIdx,
            ref int bestRectIdx,
            ref bool bestRotated,
            ref double bestPrimary,
            ref double bestSecondary)
        {
            double leftoverH = fr.Width - pw;
            double leftoverV = fr.Height - ph;
            double shortSide = Math.Min(leftoverH, leftoverV);
            double longSide = Math.Max(leftoverH, leftoverV);
            double areaFit = fr.Area - (pw * ph);

            double alignmentBonus = (leftoverH < Eps || leftoverV < Eps) ? -0.5 : 0.0;
            double rotationBias = (isRotated == preferRotated) ? -0.05 : 0.0;

            double primary, secondary;
            switch (heuristic)
            {
                case 1: // Best Area Fit (BAF)
                    primary = areaFit + alignmentBonus * 100.0;
                    secondary = shortSide + rotationBias;
                    break;
                case 2: // Best Long Side Fit (BLSF)
                    primary = longSide + alignmentBonus;
                    secondary = shortSide + rotationBias;
                    break;
                default: // 0 or 3 = Best Short Side Fit (BSSF)
                    primary = shortSide + alignmentBonus;
                    secondary = longSide + rotationBias;
                    break;
            }

            if (primary < bestPrimary - Eps || (Math.Abs(primary - bestPrimary) <= Eps && secondary < bestSecondary))
            {
                bestPrimary = primary;
                bestSecondary = secondary;
                bestRectIdx = rectIdx;
                bestRotated = isRotated;
            }
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

            if (sameW && sameH) return;

            if (sameW && ph < h)
            {
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
                cuts.Add(new Cut(x + pw, y, x + pw, y + h, blade));
                double remW = Math.Round(w - pw - blade, 1, MidpointRounding.AwayFromZero);
                if (remW > Eps)
                {
                    freeRects.Add(new FreeRectangle(x + pw + blade, y, remW, h));
                }
                return;
            }

            bool horizontalFirst;
            if (cutAllStripsFirst == 1)
            {
                horizontalFirst = true;
            }
            else if (cutAllStripsFirst == 2)
            {
                horizontalFirst = false;
            }
            else if (maxCutLength > 0 && w > maxCutLength && h <= maxCutLength)
            {
                horizontalFirst = false;
            }
            else if (maxCutLength > 0 && h > maxCutLength && w <= maxCutLength)
            {
                horizontalFirst = true;
            }
            else
            {
                double remW = Math.Max(0, w - pw - blade);
                double remH = Math.Max(0, h - ph - blade);

                if (splitGene == 0)
                {
                    horizontalFirst = (w - pw <= h - ph);
                }
                else if (splitGene == 1)
                {
                    horizontalFirst = (w - pw > h - ph);
                }
                else
                {
                    double maxAreaIfHoriz = Math.Max(remW * ph, w * remH);
                    double maxAreaIfVert = Math.Max(pw * remH, remW * h);
                    horizontalFirst = maxAreaIfHoriz >= maxAreaIfVert;
                }
            }

            if (horizontalFirst)
            {
                cuts.Add(new Cut(x, y + ph, x + w, y + ph, blade));
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
                cuts.Add(new Cut(x + pw, y, x + pw, y + h, blade));
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

        private static void MergeFreeRectangles(List<FreeRectangle> freeRects)
        {
            for (int i = 0; i < freeRects.Count; i++)
            {
                for (int j = i + 1; j < freeRects.Count; j++)
                {
                    var a = freeRects[i];
                    var b = freeRects[j];

                    if (Math.Abs(a.Y - b.Y) < Eps && Math.Abs(a.Height - b.Height) < Eps)
                    {
                        if (Math.Abs(a.X + a.Width - b.X) < Eps)
                        {
                            freeRects[i] = new FreeRectangle(a.X, a.Y, a.Width + b.Width, a.Height);
                            freeRects.RemoveAt(j);
                            i = -1;
                            break;
                        }
                        if (Math.Abs(b.X + b.Width - a.X) < Eps)
                        {
                            freeRects[i] = new FreeRectangle(b.X, b.Y, a.Width + b.Width, a.Height);
                            freeRects.RemoveAt(j);
                            i = -1;
                            break;
                        }
                    }

                    if (Math.Abs(a.X - b.X) < Eps && Math.Abs(a.Width - b.Width) < Eps)
                    {
                        if (Math.Abs(a.Y + a.Height - b.Y) < Eps)
                        {
                            freeRects[i] = new FreeRectangle(a.X, a.Y, a.Width, a.Height + b.Height);
                            freeRects.RemoveAt(j);
                            i = -1;
                            break;
                        }
                        if (Math.Abs(b.Y + b.Height - a.Y) < Eps)
                        {
                            freeRects[i] = new FreeRectangle(b.X, b.Y, a.Width, a.Height + b.Height);
                            freeRects.RemoveAt(j);
                            i = -1;
                            break;
                        }
                    }
                }
            }
        }

        public static void AddTrimCutsAndWastes(RepositoryPiece repo, double blade, List<Cut> cuts, List<WastePiece> wastes)
        {
            if (repo.TrimTop > 0)
            {
                wastes.Add(new WastePiece(0, 0, repo.Length, repo.TrimTop));
                cuts.Add(new Cut(0, repo.TrimTop, repo.Length, repo.TrimTop, blade, 0));
            }
            if (repo.TrimBottom > 0)
            {
                double y = repo.Width - repo.TrimBottom;
                wastes.Add(new WastePiece(0, y, repo.Length, repo.Width));
                cuts.Add(new Cut(0, y, repo.Length, y, blade, 0));
            }
            if (repo.TrimLeft > 0)
            {
                wastes.Add(new WastePiece(0, repo.TrimTop, repo.TrimLeft, repo.Width - repo.TrimBottom));
                cuts.Add(new Cut(repo.TrimLeft, repo.TrimTop, repo.TrimLeft, repo.Width - repo.TrimBottom, blade, 0));
            }
            if (repo.TrimRight > 0)
            {
                double x = repo.Length - repo.TrimRight;
                wastes.Add(new WastePiece(x, repo.TrimTop, repo.Length, repo.Width - repo.TrimBottom));
                cuts.Add(new Cut(x, repo.TrimTop, x, repo.Width - repo.TrimBottom, blade, 0));
            }
        }
    }
}
