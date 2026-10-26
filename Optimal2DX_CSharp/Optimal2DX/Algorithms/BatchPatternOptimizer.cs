using System;
using System.Collections.Generic;
using System.Linq;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    /// <summary>
    /// Thuật toán tối ưu hóa cắt 2D theo hướng tối thiểu hóa số lượng mẫu sơ đồ cắt (Pattern Minimization / Batch Production).
    /// Áp dụng chiến lược 2 tầng (Two-Tier Hybrid Architecture):
    /// - Tầng 1: Tìm kiếm các mẫu sơ đồ cắt chuẩn có độ phủ cao và tính hệ số nhân bản tối đa (Replication Factor) để cắt hàng loạt nhiều tấm cùng lúc.
    /// - Tầng 2: Vét các chi tiết phần dư còn lại vào các tấm ván cuối cùng bằng thuật toán Guillotine Best-Fit.
    /// </summary>
    public class BatchPatternOptimizer
    {
        public static List<SheetLayout> Optimize(
            List<DemandPiece> demandPieces,
            List<RepositoryPiece> repositoryPieces,
            int bladeWidth,
            int optimizationLevel,
            Func<bool> isStopRequested,
            Action onProgress)
        {
            var sheets = new List<SheetLayout>();
            if (demandPieces == null || demandPieces.Count == 0 || repositoryPieces == null || repositoryPieces.Count == 0)
                return sheets;

            // Nhóm các chi tiết theo quy cách (Specification)
            var specGroups = demandPieces
                .GroupBy(p => $"{p.Length}x{p.Width}_{p.Grain}_{p.CanRotate}_{p.ExternalID}")
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

                var activeQueues = specGroups.Values.Where(q => q.Count > 0).ToList();
                if (activeQueues.Count == 0) break;

                var remainingIndices = new List<int>();
                foreach (var q in activeQueues)
                {
                    remainingIndices.AddRange(q);
                }

                // Tìm mẫu sơ đồ cơ sở tối ưu nhất (kết hợp độ phủ và số lần lặp lại)
                var baseSheet = FindBestBaseSheet(demandPieces, remainingIndices, repo, bladeWidth, specGroups, repositoryPieces.Count - currentRepoIdx);

                if (baseSheet == null || baseSheet.UtilizedPieces.Count == 0)
                    break;

                // Thống kê số lượng mỗi quy cách dùng trên sơ đồ cơ sở này
                var specUsage = new Dictionary<string, int>();
                foreach (var up in baseSheet.UtilizedPieces)
                {
                    var origPiece = demandPieces[up.DemandIndex];
                    string key = $"{origPiece.Length}x{origPiece.Width}_{origPiece.Grain}_{origPiece.CanRotate}_{origPiece.ExternalID}";
                    if (!specUsage.ContainsKey(key)) specUsage[key] = 0;
                    specUsage[key]++;
                }

                // Tính số lần nhân bản tối đa
                int maxRep = int.MaxValue;
                foreach (var kvp in specUsage)
                {
                    int available = specGroups[kvp.Key].Count;
                    int rep = available / kvp.Value;
                    if (rep < maxRep) maxRep = rep;
                }

                int availableSheets = repositoryPieces.Count - currentRepoIdx;
                int repCount = Math.Min(maxRep, availableSheets);

                // Nếu sơ đồ chỉ dùng được 1 lần và số lượng còn lại ít -> chuyển sang Tầng 2 để vét gọn
                if (repCount < 2 && activeQueues.Sum(q => q.Count) < 50)
                {
                    break;
                }

                repCount = Math.Max(1, repCount);

                // Nhân bản sơ đồ cơ sở cho repCount tấm
                for (int r = 0; r < repCount; r++)
                {
                    var curRepo = repositoryPieces[currentRepoIdx++];
                    var sheetLayout = new SheetLayout(sheets.Count, curRepo.Index, curRepo.ExternalID, curRepo.Length, curRepo.Width, curRepo.Label);

                    foreach (var c in baseSheet.Cuts)
                        sheetLayout.Cuts.Add(new Cut(c.TopX, c.TopY, c.BottomX, c.BottomY, c.CutThickness, c.Level));

                    foreach (var w in baseSheet.WastePieces)
                        sheetLayout.WastePieces.Add(new WastePiece(w.TopX, w.TopY, w.BottomX, w.BottomY));

                    foreach (var up in baseSheet.UtilizedPieces)
                    {
                        var origPiece = demandPieces[up.DemandIndex];
                        string key = $"{origPiece.Length}x{origPiece.Width}_{origPiece.Grain}_{origPiece.CanRotate}_{origPiece.ExternalID}";
                        int actualDemandIdx = specGroups[key].Dequeue();

                        sheetLayout.UtilizedPieces.Add(new UtilizedPiece(
                            up.TopX, up.TopY, up.BottomX, up.BottomY,
                            up.Rotated, actualDemandIdx, origPiece.ExternalID,
                            origPiece.Grain, origPiece.Label
                        ));
                    }

                    sheets.Add(sheetLayout);
                }

                stepCounter++;
                if (stepCounter % 3 == 0)
                {
                    onProgress?.Invoke();
                }
            }

            // =========================================================================
            // TẦNG 2: VÉT PHẦN DƯ BẰNG GUILLOTINE PACKER (TIER 2: RESIDUAL PACKING)
            // =========================================================================
            var residualDemandIndices = new List<int>();
            foreach (var q in specGroups.Values)
            {
                residualDemandIndices.AddRange(q);
            }

            if (residualDemandIndices.Count > 0 && currentRepoIdx < repositoryPieces.Count && !(isStopRequested != null && isStopRequested()))
            {
                var remainingRepo = repositoryPieces.Skip(currentRepoIdx).ToList();
                var residualDemands = residualDemandIndices.Select(idx => demandPieces[idx]).ToList();

                var chrResidual = new Chromosome(residualDemands.Count);
                chrResidual.Order = Enumerable.Range(0, residualDemands.Count)
                    .OrderByDescending(i => residualDemands[i].Area)
                    .ToArray();

                var residualSheets = GuillotinePacker.Pack(residualDemands, remainingRepo, chrResidual, bladeWidth, 0, 0);

                foreach (var rs in residualSheets)
                {
                    var mappedLayout = new SheetLayout(sheets.Count, rs.RepositoryIndex, rs.RepositoryExternalID, rs.SheetLength, rs.SheetWidth, rs.RepositoryLabel);
                    foreach (var c in rs.Cuts) mappedLayout.Cuts.Add(new Cut(c.TopX, c.TopY, c.BottomX, c.BottomY, c.CutThickness, c.Level));
                    foreach (var w in rs.WastePieces) mappedLayout.WastePieces.Add(new WastePiece(w.TopX, w.TopY, w.BottomX, w.BottomY));

                    foreach (var up in rs.UtilizedPieces)
                    {
                        var origPiece = demandPieces[up.DemandIndex];
                        mappedLayout.UtilizedPieces.Add(new UtilizedPiece(
                            up.TopX, up.TopY, up.BottomX, up.BottomY,
                            up.Rotated, origPiece.Index, origPiece.ExternalID,
                            origPiece.Grain, origPiece.Label
                        ));
                    }

                    sheets.Add(mappedLayout);
                }
            }

            onProgress?.Invoke();
            return sheets;
        }

        /// <summary>
        /// Tìm kiếm mẫu sơ đồ cơ sở tối ưu trên 1 tấm ván sử dụng nhiều thuật toán kết hợp.
        /// Điểm số đánh giá cân bằng giữa Độ phủ diện tích (%) và Khả năng nhân bản (Repetition Count).
        /// </summary>
        private static SheetLayout? FindBestBaseSheet(
            List<DemandPiece> demandPieces,
            List<int> candidateIndices,
            RepositoryPiece repo,
            int bladeWidth,
            Dictionary<string, Queue<int>> specGroups,
            int availableStockSheets)
        {
            SheetLayout? bestSheet = null;
            double bestScore = -1;

            var singleRepoList = new List<RepositoryPiece> { repo };

            var activeSpecs = specGroups.Where(g => g.Value.Count > 0).Select(g => g.Key).ToList();
            var candidateOrders = new List<int[]>();

            // 1. Thứ tự đơn điệu theo kích thước (Monotonic Dimension Sorting)
            candidateOrders.Add(candidateIndices.OrderByDescending(i => demandPieces[i].Area).ToArray());
            candidateOrders.Add(candidateIndices.OrderByDescending(i => Math.Max(demandPieces[i].Length, demandPieces[i].Width)).ToArray());
            candidateOrders.Add(candidateIndices.OrderByDescending(i => demandPieces[i].Width).ThenByDescending(i => demandPieces[i].Length).ToArray());
            candidateOrders.Add(candidateIndices.OrderByDescending(i => demandPieces[i].Length).ThenByDescending(i => demandPieces[i].Width).ToArray());

            // 2. Thứ tự phối hợp theo cặp quy cách bổ trợ (Lead Spec + Complementary Pairs)
            // Nhằm giải quyết triệt để bài toán các chi tiết có chiều dài bổ trợ nhau hoặc cùng chiều rộng (vd: 340mm)
            foreach (var leadKey in activeSpecs)
            {
                var leadQueue = specGroups[leadKey];
                var otherSpecs = activeSpecs.Where(k => k != leadKey).ToList();

                foreach (var compKey in otherSpecs)
                {
                    var compQueue = specGroups[compKey];
                    var mixed = leadQueue.Take(12)
                        .Concat(compQueue.Take(18))
                        .Concat(candidateIndices)
                        .Distinct()
                        .ToArray();
                    candidateOrders.Add(mixed);
                }
            }

            foreach (var order in candidateOrders)
            {
                for (int splitMode = 0; splitMode <= 1; splitMode++)
                {
                    var chr = new Chromosome(demandPieces.Count);
                    chr.Order = order;

                    var res = GuillotinePacker.Pack(demandPieces, singleRepoList, chr, bladeWidth, splitMode, 0);
                    if (res.Count > 0 && res[0].UtilizedPieces.Count > 0)
                    {
                        var candidate = res[0];

                        // Tính hệ số nhân bản của mẫu này
                        var specUsage = new Dictionary<string, int>();
                        foreach (var up in candidate.UtilizedPieces)
                        {
                            var origPiece = demandPieces[up.DemandIndex];
                            string key = $"{origPiece.Length}x{origPiece.Width}_{origPiece.Grain}_{origPiece.CanRotate}_{origPiece.ExternalID}";
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

                        // Hàm mục tiêu cân bằng: Độ phủ diện tích x (1 + 0.35 * log2(1 + số lần lặp))
                        double score = candidate.SurfaceCovered * (1.0 + 0.35 * Math.Log2(1 + Math.Max(1, rep)));

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestSheet = candidate;
                        }
                    }
                }
            }

            return bestSheet;
        }
    }
}
