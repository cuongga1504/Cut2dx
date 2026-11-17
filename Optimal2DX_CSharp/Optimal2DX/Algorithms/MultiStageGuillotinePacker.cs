using System;
using System.Collections.Generic;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    public class MultiStageGuillotinePacker
    {
        private const double Eps = 0.001;

        private class StripState
        {
            public double StartCoord;
            public double Thickness;
            public double CurrentOffset;
        }

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
            if (demandPieces == null || demandPieces.Count == 0 || repositoryPieces == null || repositoryPieces.Count == 0)
                return sheets;

            var placed = new bool[demandPieces.Count];
            int totalPlacedCount = 0;
            bool horizontalFirst = (firstDirection == 0);

            for (int r = 0; r < repositoryPieces.Count && totalPlacedCount < demandPieces.Count; r++)
            {
                var repo = repositoryPieces[r];
                if (repo.UsableLength <= 0 || repo.UsableWidth <= 0) continue;

                var layout = new SheetLayout(sheets.Count, repo.Index, repo.ExternalID, repo.Length, repo.Width, repo.Label, repo.Material);

                double startX = repo.TrimLeft;
                double startY = repo.TrimTop;
                double usableW = repo.UsableLength;
                double usableH = repo.UsableWidth;

                var strips = new List<StripState>();
                double nextStripCoord = horizontalFirst ? startY : startX;
                double maxSheetCrossCoord = horizontalFirst ? (startY + usableH) : (startX + usableW);
                double maxStripLengthCoord = horizontalFirst ? (startX + usableW) : (startY + usableH);
                double stripOriginOffset = horizontalFirst ? startX : startY;

                bool placedAny = false;

                for (int step = 0; step < chromosome.Order.Length; step++)
                {
                    int pieceIdx = chromosome.Order[step];
                    if (pieceIdx < 0 || pieceIdx >= demandPieces.Count || placed[pieceIdx]) continue;

                    var piece = demandPieces[pieceIdx];
                    bool preferRot = piece.CanRotate == 1
                        && chromosome.Rotated != null
                        && pieceIdx < chromosome.Rotated.Length
                        && chromosome.Rotated[pieceIdx];

                    int bestStripIdx = -1;
                    bool bestRot = false;
                    double bestThicknessDiff = double.MaxValue;

                    for (int s = 0; s < strips.Count; s++)
                    {
                        var st = strips[s];

                        for (int attempt = 0; attempt < 2; attempt++)
                        {
                            bool rot = (attempt == 0) ? preferRot : !preferRot;
                            if (rot && piece.CanRotate != 1) continue;

                            double pw = rot ? piece.Width : piece.Length;
                            double ph = rot ? piece.Length : piece.Width;

                            double itemAlong = horizontalFirst ? pw : ph;
                            double itemCross = horizontalFirst ? ph : pw;

                            if (st.CurrentOffset + itemAlong <= maxStripLengthCoord + Eps && itemCross <= st.Thickness + Eps)
                            {
                                double diff = st.Thickness - itemCross;
                                if (onlySameHeightInStrip == 1 && diff > heightTolerance + Eps)
                                    continue;

                                if (diff < bestThicknessDiff)
                                {
                                    bestThicknessDiff = diff;
                                    bestStripIdx = s;
                                    bestRot = rot;
                                }
                            }
                        }
                    }

                    if (bestStripIdx >= 0)
                    {
                        var st = strips[bestStripIdx];
                        PlacePieceInStrip(layout, piece, bestRot, st, horizontalFirst, startX, startY, usableW, usableH, bladeWidth);

                        placed[pieceIdx] = true;
                        totalPlacedCount++;
                        placedAny = true;
                    }
                    else
                    {
                        for (int attempt = 0; attempt < 2; attempt++)
                        {
                            bool rot = (attempt == 0) ? preferRot : !preferRot;
                            if (rot && piece.CanRotate != 1) continue;

                            double pw = rot ? piece.Width : piece.Length;
                            double ph = rot ? piece.Length : piece.Width;

                            double itemAlong = horizontalFirst ? pw : ph;
                            double itemCross = horizontalFirst ? ph : pw;

                            if (itemAlong > maxStripLengthCoord - stripOriginOffset + Eps) continue;

                            double stripEndCoord = Math.Round(nextStripCoord + itemCross, 1, MidpointRounding.AwayFromZero);
                            if (stripEndCoord > maxSheetCrossCoord + Eps) continue;

                            var newStrip = new StripState
                            {
                                StartCoord = nextStripCoord,
                                Thickness = itemCross,
                                CurrentOffset = stripOriginOffset
                            };
                            strips.Add(newStrip);

                            PlacePieceInStrip(layout, piece, rot, newStrip, horizontalFirst, startX, startY, usableW, usableH, bladeWidth);

                            if (stripEndCoord < maxSheetCrossCoord - Eps)
                            {
                                if (horizontalFirst)
                                    layout.Cuts.Add(new Cut(startX, stripEndCoord, startX + usableW, stripEndCoord, bladeWidth, level: 1));
                                else
                                    layout.Cuts.Add(new Cut(stripEndCoord, startY, stripEndCoord, startY + usableH, bladeWidth, level: 1));
                            }

                            nextStripCoord = Math.Round(stripEndCoord + bladeWidth, 1, MidpointRounding.AwayFromZero);

                            placed[pieceIdx] = true;
                            totalPlacedCount++;
                            placedAny = true;
                            break;
                        }
                    }
                }

                if (placedAny)
                {
                    GuillotinePacker.AddTrimCutsAndWastes(repo, bladeWidth, layout.Cuts, layout.WastePieces);

                    foreach (var st in strips)
                    {
                        if (st.CurrentOffset < maxStripLengthCoord - Eps)
                        {
                            if (horizontalFirst)
                                layout.WastePieces.Add(new WastePiece(st.CurrentOffset, st.StartCoord, startX + usableW, st.StartCoord + st.Thickness));
                            else
                                layout.WastePieces.Add(new WastePiece(st.StartCoord, st.CurrentOffset, st.StartCoord + st.Thickness, startY + usableH));
                        }
                    }

                    if (strips.Count > 0)
                    {
                        var lastStrip = strips[strips.Count - 1];
                        double usedCrossEnd = Math.Round(lastStrip.StartCoord + lastStrip.Thickness, 1, MidpointRounding.AwayFromZero);
                        if (usedCrossEnd < maxSheetCrossCoord - Eps)
                        {
                            if (horizontalFirst)
                                layout.WastePieces.Add(new WastePiece(startX, usedCrossEnd, startX + usableW, startY + usableH));
                            else
                                layout.WastePieces.Add(new WastePiece(usedCrossEnd, startY, startX + usableW, startY + usableH));
                        }
                    }

                    sheets.Add(layout);
                }
            }

            return sheets;
        }

        private static void PlacePieceInStrip(
            SheetLayout layout,
            DemandPiece piece,
            bool rotated,
            StripState st,
            bool horizontalFirst,
            double startX,
            double startY,
            double usableW,
            double usableH,
            double bladeWidth)
        {
            double pw = rotated ? piece.Width : piece.Length;
            double ph = rotated ? piece.Length : piece.Width;

            if (horizontalFirst)
            {
                double x1 = st.CurrentOffset;
                double y1 = st.StartCoord;
                double x2 = Math.Round(x1 + pw, 1, MidpointRounding.AwayFromZero);
                double y2 = Math.Round(y1 + ph, 1, MidpointRounding.AwayFromZero);

                layout.UtilizedPieces.Add(new UtilizedPiece(
                    x1, y1, x2, y2, rotated ? 1 : 0, piece.Index, piece.ExternalID, piece.Grain, piece.Label, piece.Material));

                if (x2 < startX + usableW - Eps)
                {
                    layout.Cuts.Add(new Cut(x2, y1, x2, y1 + st.Thickness, bladeWidth, level: 2));
                }

                if (ph < st.Thickness - Eps)
                {
                    layout.Cuts.Add(new Cut(x1, y2, x2, y2, bladeWidth, level: 3));
                    double trimWasteTop = Math.Round(y2 + bladeWidth, 1, MidpointRounding.AwayFromZero);
                    if (trimWasteTop < y1 + st.Thickness - Eps)
                    {
                        layout.WastePieces.Add(new WastePiece(x1, trimWasteTop, x2, y1 + st.Thickness));
                    }
                }

                st.CurrentOffset = Math.Round(x2 + bladeWidth, 1, MidpointRounding.AwayFromZero);
            }
            else
            {
                double x1 = st.StartCoord;
                double y1 = st.CurrentOffset;
                double x2 = Math.Round(x1 + pw, 1, MidpointRounding.AwayFromZero);
                double y2 = Math.Round(y1 + ph, 1, MidpointRounding.AwayFromZero);

                layout.UtilizedPieces.Add(new UtilizedPiece(
                    x1, y1, x2, y2, rotated ? 1 : 0, piece.Index, piece.ExternalID, piece.Grain, piece.Label, piece.Material));

                if (y2 < startY + usableH - Eps)
                {
                    layout.Cuts.Add(new Cut(x1, y2, x1 + st.Thickness, y2, bladeWidth, level: 2));
                }

                if (pw < st.Thickness - Eps)
                {
                    layout.Cuts.Add(new Cut(x2, y1, x2, y2, bladeWidth, level: 3));
                    double trimWasteLeft = Math.Round(x2 + bladeWidth, 1, MidpointRounding.AwayFromZero);
                    if (trimWasteLeft < x1 + st.Thickness - Eps)
                    {
                        layout.WastePieces.Add(new WastePiece(trimWasteLeft, y1, x1 + st.Thickness, y2));
                    }
                }

                st.CurrentOffset = Math.Round(y2 + bladeWidth, 1, MidpointRounding.AwayFromZero);
            }
        }
    }
}
