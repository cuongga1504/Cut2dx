using System;
using System.Collections.Generic;
using System.Linq;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    /// <summary>
    /// Thuật toán tối ưu hóa cắt 2D theo hướng tối thiểu hóa số lượng mẫu sơ đồ cắt (Pattern Minimization / Batch Production).
    /// Áp dụng chiến lược 2 tầng kết hợp Đối chứng Đa chính sách Toàn cục (Global Multi-Policy Selection):
    /// - Chạy đối chứng giữa chế độ xoay tự do (Free Rotation), chế độ khóa hướng theo băng (Grain-Aligned),
    ///   và chế độ chuẩn hóa chiều dài/rộng (Normalized Orientation) để tránh bẫy xoay tham lam làm vỡ tỷ lệ bộ (Kit Ratio).
    /// </summary>
    public class BatchPatternOptimizer
    {
        private static string GetSpecKey(DemandPiece p)
            => $"{p.Length}x{p.Width}_{(int)p.Grain}_{p.CanRotate}_{p.Material}";

        public static List<SheetLayout> Optimize(
            List<DemandPiece> demandPieces,
            List<RepositoryPiece> repositoryPieces,
            double bladeWidth,
            int optimizationLevel,
            Func<bool>? isStopRequested,
            Action? onProgress)
        {
            if (demandPieces == null || demandPieces.Count == 0 || repositoryPieces == null || repositoryPieces.Count == 0)
                return new List<SheetLayout>();

            // 1. Chạy với cấu hình CanRotate hiện tại của người dùng
            var bestResult = RunBatchCore(demandPieces, repositoryPieces, bladeWidth, isStopRequested, onProgress);

            // 2. Nếu có bất kỳ chi tiết nào đang bật CanRotate == 1 (Không có thớ / Cho phép xoay),
            // chạy thêm nhánh đối chứng giữ nguyên hướng gốc (CanRotate = 0) và nhánh chuẩn hóa (Length >= Width)
            // để tránh bẫy xoay tham lam (BSSF/BAF) trên 1 tấm đơn lẻ làm vỡ tỷ lệ bộ của toàn đơn hàng.
            if (demandPieces.Any(p => p.CanRotate == 1) && !(isStopRequested != null && isStopRequested()))
            {
                // Nhánh B: Khóa hướng gốc (CanRotate = 0)
                var lockedDemands = demandPieces
                    .Select(p => new DemandPiece(p.Index, p.Length, p.Width, 0, p.ExternalID, p.Priority, p.Grain, p.Label, p.Material))
                    .ToList();

                var resultLocked = RunBatchCore(lockedDemands, repositoryPieces, bladeWidth, isStopRequested, null);
                if (IsBetterGlobalSolution(resultLocked, bestResult))
                {
                    bestResult = resultLocked;
                }

                // Nhánh C: Chuẩn hóa cạnh dài dọc theo chiều dài tấm ván (Length >= Width) và khóa xoay
                var normalizedDemands = demandPieces
                    .Select(p => p.CanRotate == 1
                        ? new DemandPiece(
                            p.Index,
                            Math.Max(p.Length, p.Width),
                            Math.Min(p.Length, p.Width),
                            0, p.ExternalID, p.Priority, p.Grain, p.Label, p.Material)
                        : p.Clone())
                    .ToList();

                var resultNormalized = RunBatchCore(normalizedDemands, repositoryPieces, bladeWidth, isStopRequested, null);

                var origByIdx = demandPieces.ToDictionary(p => p.Index);
                foreach (var sheet in resultNormalized)
                {
                    foreach (var up in sheet.UtilizedPieces)
                    {
                        var orig = origByIdx[up.DemandIndex];
                        bool isRot = Math.Abs(up.Width - orig.Width) < 0.1 && Math.Abs(up.Height - orig.Length) < 0.1 && orig.Length != orig.Width;
                        up.Rotated = isRot ? 1 : 0;
                    }
                }

                if (IsBetterGlobalSolution(resultNormalized, bestResult))
                {
                    bestResult = resultNormalized;
                }
            }

            return bestResult;
        }

        private static bool IsBetterGlobalSolution(List<SheetLayout> candidate, List<SheetLayout> currentBest)
        {
            if (candidate == null || candidate.Count == 0) return false;
            if (currentBest == null || currentBest.Count == 0) return true;

            int candPlaced = candidate.Sum(s => s.UtilizedPieces.Count);
            int bestPlaced = currentBest.Sum(s => s.UtilizedPieces.Count);
            if (candPlaced != bestPlaced)
                return candPlaced > bestPlaced; // Ưu tiên xếp được nhiều phôi hơn

            if (candidate.Count != currentBest.Count)
                return candidate.Count < currentBest.Count; // Ưu tiên tốn ít tấm ván hơn (vd: 49 tấm < 51 tấm)

            int candPatterns = GroupedSheetLayout.GroupSheets(candidate).Count;
            int bestPatterns = GroupedSheetLayout.GroupSheets(currentBest).Count;
            if (candPatterns != bestPatterns)
                return candPatterns < bestPatterns; // Cùng số tấm thì ưu tiên ít mẫu sơ đồ hơn

            double candCuts = candidate.Sum(s => s.Cuts.Count);
            double bestCuts = currentBest.Sum(s => s.Cuts.Count);
            return candCuts < bestCuts;
        }

        private static List<SheetLayout> RunBatchCore(
            List<DemandPiece> demandPieces,
            List<RepositoryPiece> repositoryPieces,
            double bladeWidth,
            Func<bool>? isStopRequested,
            Action? onProgress)
        {
            bladeWidth = Math.Round(Math.Max(0.0, bladeWidth), 1, MidpointRounding.AwayFromZero);
            var sheets = new List<SheetLayout>();

            var pieceByIndex = new Dictionary<int, DemandPiece>(demandPieces.Count);
            foreach (var dp in demandPieces)
            {
                pieceByIndex[dp.Index] = dp;
            }

            var specGroups = demandPieces
                .GroupBy(GetSpecKey)
                .ToDictionary(g => g.Key, g => new Queue<int>(g.Select(p => p.Index)));

            int currentRepoIdx = 0;
            int stepCounter = 0;

            // =========================================================================
            // TẦNG 1: TỐI ƯU CẮT HÀNG LOẠT VỚI HỆ SỐ NHÂN BẢN CAO (TIER 1: BATCH PATTERNS)
            // =========================================================================
            while (specGroups.Values.Any(q => q.Count > 0) && currentRepoIdx < repositoryPieces.Count)
            {
                if (isStopRequested != null && isStopRequested())
                    break;

                var repo = repositoryPieces[currentRepoIdx];
                if (repo.UsableLength <= 0 || repo.UsableWidth <= 0)
                {
                    currentRepoIdx++;
                    continue;
                }

                int sameSpecRepoCount = 1;
                for (int k = currentRepoIdx + 1; k < repositoryPieces.Count; k++)
                {
                    var nextR = repositoryPieces[k];
                    if (nextR.Length == repo.Length && nextR.Width == repo.Width &&
                        nextR.TrimTop == repo.TrimTop && nextR.TrimLeft == repo.TrimLeft &&
                        nextR.TrimBottom == repo.TrimBottom && nextR.TrimRight == repo.TrimRight &&
                        nextR.Grain == repo.Grain &&
                        string.Equals(nextR.Material, repo.Material, StringComparison.OrdinalIgnoreCase))
                    {
                        sameSpecRepoCount++;
                    }
                    else break;
                }

                var baseSheet = FindBestBaseSheet(
                    demandPieces,
                    pieceByIndex,
                    repo,
                    bladeWidth,
                    specGroups,
                    sameSpecRepoCount,
                    isStopRequested);

                if (baseSheet == null || baseSheet.UtilizedPieces.Count == 0)
                    break;

                var specUsage = new Dictionary<string, int>();
                foreach (var up in baseSheet.UtilizedPieces)
                {
                    var origPiece = pieceByIndex[up.DemandIndex];
                    string key = GetSpecKey(origPiece);
                    if (!specUsage.ContainsKey(key)) specUsage[key] = 0;
                    specUsage[key]++;
                }

                int maxRep = int.MaxValue;
                foreach (var kvp in specUsage)
                {
                    int available = specGroups[kvp.Key].Count;
                    int rep = available / kvp.Value;
                    if (rep < maxRep) maxRep = rep;
                }

                int repCount = Math.Min(maxRep, sameSpecRepoCount);
                int totalRemainingPieces = specGroups.Values.Sum(q => q.Count);

                if (repCount < 2 && baseSheet.SurfaceCovered < 86.0 && totalRemainingPieces <= 60)
                {
                    break;
                }

                repCount = Math.Max(1, repCount);

                for (int r = 0; r < repCount; r++)
                {
                    var curRepo = repositoryPieces[currentRepoIdx++];
                    var sheetLayout = new SheetLayout(sheets.Count, curRepo.Index, curRepo.ExternalID, curRepo.Length, curRepo.Width, curRepo.Label, curRepo.Material);

                    foreach (var c in baseSheet.Cuts)
                        sheetLayout.Cuts.Add(new Cut(c.TopX, c.TopY, c.BottomX, c.BottomY, c.CutThickness, c.Level));

                    foreach (var w in baseSheet.WastePieces)
                        sheetLayout.WastePieces.Add(new WastePiece(w.TopX, w.TopY, w.BottomX, w.BottomY));

                    foreach (var up in baseSheet.UtilizedPieces)
                    {
                        var origPiece = pieceByIndex[up.DemandIndex];
                        string key = GetSpecKey(origPiece);
                        int actualDemandIdx = specGroups[key].Dequeue();
                        var dequeuedPiece = pieceByIndex[actualDemandIdx];

                        sheetLayout.UtilizedPieces.Add(new UtilizedPiece(
                            up.TopX, up.TopY, up.BottomX, up.BottomY,
                            up.Rotated, dequeuedPiece.Index, dequeuedPiece.ExternalID,
                            dequeuedPiece.Grain, dequeuedPiece.Label, dequeuedPiece.Material
                        ));
                    }

                    sheets.Add(sheetLayout);
                }

                stepCounter++;
                if (stepCounter % 2 == 0)
                {
                    onProgress?.Invoke();
                }
            }

            // =========================================================================
            // TẦNG 2: VÉT PHẦN DƯ BẰNG ĐA CHIẾN LƯỢC GUILLOTINE (TIER 2: MULTI-PASS RESIDUAL)
            // =========================================================================
            var residualDemands = new List<DemandPiece>();
            foreach (var q in specGroups.Values)
            {
                while (q.Count > 0)
                {
                    residualDemands.Add(pieceByIndex[q.Dequeue()]);
                }
            }

            if (residualDemands.Count > 0 && currentRepoIdx < repositoryPieces.Count && !(isStopRequested != null && isStopRequested()))
            {
                var remainingRepo = repositoryPieces.Skip(currentRepoIdx).ToList();

                var localDemands = new List<DemandPiece>(residualDemands.Count);
                for (int i = 0; i < residualDemands.Count; i++)
                {
                    var d = residualDemands[i];
                    localDemands.Add(new DemandPiece(i, d.Length, d.Width, d.CanRotate, d.ExternalID, d.Priority, d.Grain, d.Label, d.Material));
                }

                List<SheetLayout>? bestResidualSheets = null;
                double bestResidualScore = double.MinValue;

                var sortStrategies = new List<int[]>
                {
                    Enumerable.Range(0, localDemands.Count).OrderByDescending(i => localDemands[i].Area).ToArray(),
                    Enumerable.Range(0, localDemands.Count).OrderByDescending(i => Math.Max(localDemands[i].Length, localDemands[i].Width)).ThenByDescending(i => localDemands[i].Area).ToArray(),
                    Enumerable.Range(0, localDemands.Count).OrderByDescending(i => localDemands[i].Width).ThenByDescending(i => localDemands[i].Length).ToArray(),
                    Enumerable.Range(0, localDemands.Count).OrderByDescending(i => localDemands[i].Length).ThenByDescending(i => localDemands[i].Width).ToArray(),
                    Enumerable.Range(0, localDemands.Count).OrderByDescending(i => localDemands[i].Length + localDemands[i].Width).ToArray()
                };

                foreach (var order in sortStrategies)
                {
                    for (int stripMode = 0; stripMode <= 2; stripMode++)
                    {
                        for (byte heuristic = 0; heuristic <= 3; heuristic++)
                        {
                            for (int splitMode = 0; splitMode <= 2; splitMode++)
                            {
                                var chr = new Chromosome(localDemands.Count)
                                {
                                    Order = order,
                                    PlacementHeuristic = heuristic
                                };
                                for (int i = 0; i < localDemands.Count; i++)
                                    chr.SplitDirection[i] = (byte)splitMode;

                                var candidateSheets = GuillotinePacker.Pack(localDemands, remainingRepo, chr, bladeWidth, stripMode, 0);
                                double score = ScoreResidualSolution(candidateSheets);
                                if (score > bestResidualScore)
                                {
                                    bestResidualScore = score;
                                    bestResidualSheets = candidateSheets;
                                }
                            }
                        }
                    }
                }

                if (bestResidualSheets != null)
                {
                    foreach (var rs in bestResidualSheets)
                    {
                        var mappedLayout = new SheetLayout(sheets.Count, rs.RepositoryIndex, rs.RepositoryExternalID, rs.SheetLength, rs.SheetWidth, rs.RepositoryLabel, rs.Material);
                        foreach (var c in rs.Cuts) mappedLayout.Cuts.Add(new Cut(c.TopX, c.TopY, c.BottomX, c.BottomY, c.CutThickness, c.Level));
                        foreach (var w in rs.WastePieces) mappedLayout.WastePieces.Add(new WastePiece(w.TopX, w.TopY, w.BottomX, w.BottomY));

                        foreach (var up in rs.UtilizedPieces)
                        {
                            var origPiece = residualDemands[up.DemandIndex];
                            mappedLayout.UtilizedPieces.Add(new UtilizedPiece(
                                up.TopX, up.TopY, up.BottomX, up.BottomY,
                                up.Rotated, origPiece.Index, origPiece.ExternalID,
                                origPiece.Grain, origPiece.Label, origPiece.Material
                            ));
                        }

                        sheets.Add(mappedLayout);
                    }
                }
            }

            onProgress?.Invoke();
            return sheets;
        }

        private static double ScoreResidualSolution(List<SheetLayout> sheets)
        {
            if (sheets == null || sheets.Count == 0) return double.MinValue;
            int placedCount = 0;
            double totalArea = 0;
            double maxSingleWasteOnLastSheet = 0;

            for (int s = 0; s < sheets.Count; s++)
            {
                var sh = sheets[s];
                placedCount += sh.UtilizedPieces.Count;
                foreach (var p in sh.UtilizedPieces) totalArea += p.Area;

                if (s == sheets.Count - 1)
                {
                    foreach (var w in sh.WastePieces)
                    {
                        if (w.Area > maxSingleWasteOnLastSheet)
                            maxSingleWasteOnLastSheet = w.Area;
                    }
                }
            }

            return (placedCount * 100_000_000.0)
                 + (totalArea * 10.0)
                 - (sheets.Count * 10_000_000.0)
                 + (maxSingleWasteOnLastSheet * 0.5);
        }

        private static SheetLayout? FindBestBaseSheet(
            List<DemandPiece> allDemandPieces,
            Dictionary<int, DemandPiece> pieceByIndex,
            RepositoryPiece repo,
            double bladeWidth,
            Dictionary<string, Queue<int>> specGroups,
            int availableStockSheets,
            Func<bool>? isStopRequested)
        {
            long sheetUsableArea = repo.UsableArea;
            if (sheetUsableArea <= 0) return null;

            var activeSpecs = new List<string>();
            var boundedPoolBySpec = new Dictionary<string, List<int>>();
            var allBoundedIndices = new List<int>();

            foreach (var kvp in specGroups)
            {
                if (kvp.Value.Count == 0) continue;
                int sampleIdx = kvp.Value.Peek();
                var samplePiece = pieceByIndex[sampleIdx];
                if (samplePiece.Area <= 0) continue;

                bool fitsNormal = samplePiece.Length <= repo.UsableLength && samplePiece.Width <= repo.UsableWidth;
                bool fitsRot = samplePiece.CanRotate == 1 && samplePiece.Width <= repo.UsableLength && samplePiece.Length <= repo.UsableWidth;
                if (!fitsNormal && !fitsRot) continue;

                int maxPossibleOnOneSheet = Math.Max(1, (int)(sheetUsableArea / samplePiece.Area) + 2);
                var list = kvp.Value.Take(maxPossibleOnOneSheet).ToList();

                activeSpecs.Add(kvp.Key);
                boundedPoolBySpec[kvp.Key] = list;
                allBoundedIndices.AddRange(list);
            }

            if (allBoundedIndices.Count == 0) return null;

            var localPieces = new List<DemandPiece>(allBoundedIndices.Count);
            var localIdxByOrigIdx = new Dictionary<int, int>(allBoundedIndices.Count);
            for (int i = 0; i < allBoundedIndices.Count; i++)
            {
                var orig = pieceByIndex[allBoundedIndices[i]];
                localIdxByOrigIdx[orig.Index] = i;
                localPieces.Add(new DemandPiece(i, orig.Length, orig.Width, orig.CanRotate, orig.ExternalID, orig.Priority, orig.Grain, orig.Label, orig.Material));
            }

            var localAllIndices = Enumerable.Range(0, localPieces.Count).ToList();
            var candidateOrders = new List<int[]>();

            candidateOrders.Add(localAllIndices.OrderByDescending(i => localPieces[i].Area).ToArray());
            candidateOrders.Add(localAllIndices.OrderByDescending(i => Math.Max(localPieces[i].Length, localPieces[i].Width)).ThenByDescending(i => localPieces[i].Area).ToArray());
            candidateOrders.Add(localAllIndices.OrderByDescending(i => localPieces[i].Width).ThenByDescending(i => localPieces[i].Length).ToArray());
            candidateOrders.Add(localAllIndices.OrderByDescending(i => localPieces[i].Length).ThenByDescending(i => localPieces[i].Width).ToArray());
            candidateOrders.Add(localAllIndices.OrderByDescending(i => localPieces[i].Length + localPieces[i].Width).ToArray());

            candidateOrders.Add(localAllIndices
                .OrderByDescending(i => specGroups[GetSpecKey(localPieces[i])].Count)
                .ThenByDescending(i => localPieces[i].Area)
                .ToArray());

            if (activeSpecs.Count > 1)
            {
                int minSpecCount = activeSpecs.Min(k => specGroups[k].Count);
                var specsByAreaDesc = activeSpecs
                    .OrderByDescending(k => pieceByIndex[specGroups[k].Peek()].Area)
                    .ToList();
                var specsByWidthDesc = activeSpecs
                    .OrderByDescending(k => pieceByIndex[specGroups[k].Peek()].Width)
                    .ThenByDescending(k => pieceByIndex[specGroups[k].Peek()].Length)
                    .ToList();

                foreach (var specOrdering in new[] { specsByAreaDesc, specsByWidthDesc })
                {
                    for (int mult = 1; mult <= 4; mult++)
                    {
                        var interleaved = new List<int>(localPieces.Count);
                        var posBySpec = specOrdering.ToDictionary(k => k, _ => 0);

                        while (interleaved.Count < localPieces.Count)
                        {
                            bool addedAny = false;
                            foreach (var k in specOrdering)
                            {
                                var pool = boundedPoolBySpec[k];
                                int pos = posBySpec[k];
                                if (pos >= pool.Count) continue;

                                int ratio = Math.Max(1, (int)Math.Round((double)specGroups[k].Count / Math.Max(1, minSpecCount)));
                                int takeCount = Math.Min(ratio * mult, pool.Count - pos);
                                for (int t = 0; t < takeCount; t++)
                                {
                                    interleaved.Add(localIdxByOrigIdx[pool[pos + t]]);
                                }
                                posBySpec[k] = pos + takeCount;
                                addedAny = true;
                            }
                            if (!addedAny) break;
                        }
                        candidateOrders.Add(interleaved.ToArray());
                    }
                }
            }

            foreach (var leadKey in activeSpecs)
            {
                var leadLocal = boundedPoolBySpec[leadKey].Select(origIdx => localIdxByOrigIdx[origIdx]).ToList();
                var pureLeadFirst = leadLocal.Concat(localAllIndices).Distinct().ToArray();
                candidateOrders.Add(pureLeadFirst);

                foreach (var compKey in activeSpecs)
                {
                    if (compKey == leadKey) continue;
                    var compLocal = boundedPoolBySpec[compKey].Select(origIdx => localIdxByOrigIdx[origIdx]).ToList();
                    var mixed = leadLocal
                        .Concat(compLocal)
                        .Concat(localAllIndices)
                        .Distinct()
                        .ToArray();
                    candidateOrders.Add(mixed);
                }
            }

            var singleRepoList = new List<RepositoryPiece> { repo };
            SheetLayout? bestSheet = null;
            double bestScore = -1;

            foreach (var order in candidateOrders)
            {
                if (isStopRequested != null && isStopRequested()) break;

                for (int stripMode = 0; stripMode <= 2; stripMode++)
                {
                    for (byte heuristic = 0; heuristic <= 3; heuristic++)
                    {
                        for (int splitMode = 0; splitMode <= 1; splitMode++)
                        {
                            var chr = new Chromosome(localPieces.Count)
                            {
                                Order = order,
                                PlacementHeuristic = heuristic
                            };
                            for (int i = 0; i < localPieces.Count; i++)
                                chr.SplitDirection[i] = (byte)splitMode;

                            var res = GuillotinePacker.Pack(localPieces, singleRepoList, chr, bladeWidth, stripMode, 0);
                            if (res.Count > 0 && res[0].UtilizedPieces.Count > 0)
                            {
                                var candidate = res[0];

                                var specUsage = new Dictionary<string, int>();
                                foreach (var up in candidate.UtilizedPieces)
                                {
                                    var lp = localPieces[up.DemandIndex];
                                    string key = GetSpecKey(lp);
                                    if (!specUsage.ContainsKey(key)) specUsage[key] = 0;
                                    specUsage[key]++;
                                }

                                int rep = int.MaxValue;
                                foreach (var kvp in specUsage)
                                {
                                    int avail = specGroups[kvp.Key].Count;
                                    int r = avail / kvp.Value;
                                    if (r < rep) rep = r;
                                }
                                if (rep == int.MaxValue) rep = 1;
                                rep = Math.Min(rep, availableStockSheets);

                                double cutEfficiencyBonus = -0.002 * candidate.Cuts.Count;
                                double score = candidate.SurfaceCovered * (1.0 + 0.35 * Math.Log(1 + Math.Max(1, rep), 2))
                                             + cutEfficiencyBonus;

                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestSheet = candidate;
                                }
                            }
                        }
                    }
                }
            }

            if (bestSheet != null)
            {
                foreach (var up in bestSheet.UtilizedPieces)
                {
                    int origIdx = allBoundedIndices[up.DemandIndex];
                    var origPiece = pieceByIndex[origIdx];
                    up.DemandIndex = origPiece.Index;
                    up.DemandExternalID = origPiece.ExternalID;
                }
            }

            return bestSheet;
        }
    }
}
