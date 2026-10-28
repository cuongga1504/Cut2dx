using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Optimal2DX;
using Optimal2DX.Models;
using Optimal2DX.Web.Models;

namespace Optimal2DX.Web.Services;

public class CuttingOptimizationService
{
    private static readonly string[] PieceColors = new[]
    {
        "#dae8fc", // Xanh dương pastel
        "#d5e8d4", // Xanh lá pastel
        "#ffe6cc", // Cam pastel
        "#fff2cc", // Vàng pastel
        "#f8cecc", // Đỏ hồng pastel
        "#e1d5e7", // Tím pastel
        "#d0e1fd", // Xanh da trời pastel
        "#e3c800", // Vàng nghệ pastel
        "#cbf0f8", // Cyan pastel
        "#ead1dc"  // Hồng phấn pastel
    };

    private static readonly string[] PieceStrokeColors = new[]
    {
        "#6c8ebf",
        "#82b366",
        "#d79b00",
        "#d6b656",
        "#b85450",
        "#9673a6",
        "#4a90e2",
        "#b59f00",
        "#3fb1c5",
        "#b85e80"
    };

    public OptimizationResultDto Optimize(OptimizationRequestDto req)
    {
        var sw = Stopwatch.StartNew();
        var result = new OptimizationResultDto();

        if (req.DemandPieces == null || req.DemandPieces.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Vui lòng nhập ít nhất một quy cách chi tiết cần cắt!";
            return result;
        }

        if (req.RepoPieces == null || req.RepoPieces.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Vui lòng nhập ít nhất một tấm vật liệu trong kho (Stock Sheet)!";
            return result;
        }

        var opt = new Optimization2DX();

        // 1. Repo Pieces (Stock)
        int totalRepoRequested = req.RepoPieces.Sum(r => Math.Max(0, r.Quantity));
        if (totalRepoRequested <= 0)
        {
            result.Success = false;
            result.ErrorMessage = "Số lượng tấm vật liệu trong kho phải lớn hơn 0!";
            return result;
        }

        opt.NumberOfRepositoryPieces = totalRepoRequested;
        var repoMetaList = new List<RepoInputDto>();
        int currentRepoIdx = 0;

        for (int i = 0; i < req.RepoPieces.Count; i++)
        {
            var r = req.RepoPieces[i];
            int qty = Math.Max(0, r.Quantity);
            if (r.Length <= 0 || r.Width <= 0 || qty <= 0) continue;

            string label = string.IsNullOrWhiteSpace(r.Label) ? $"Tấm kho #{i + 1}" : r.Label.Trim();
            if (!string.IsNullOrWhiteSpace(r.Material))
                label = $"{label} ({r.Material.Trim()})";

            string grainStr = r.SheetGrain ?? "";
            SheetGrainDirection sheetGrain = grainStr switch
            {
                var s when s.Contains("Ngang") || s.Contains("|||") => SheetGrainDirection.AlongWidth,
                var s when s.Contains("Không thớ") => SheetGrainDirection.None,
                _ => SheetGrainDirection.AlongLength
            };

            for (int k = 0; k < qty; k++)
            {
                repoMetaList.Add(new RepoInputDto
                {
                    Length = r.Length,
                    Width = r.Width,
                    Quantity = 1,
                    Material = r.Material ?? "",
                    SheetGrain = r.SheetGrain ?? "",
                    Label = label,
                    Price = r.Price,
                    TrimTop = r.TrimTop,
                    TrimLeft = r.TrimLeft,
                    TrimBottom = r.TrimBottom,
                    TrimRight = r.TrimRight,
                    Priority = r.Priority,
                    ExternalID = r.ExternalID > 0 ? r.ExternalID : i + 1
                });

                opt.SetRepositoryPiece(
                    currentRepoIdx,
                    r.Length,
                    r.Width,
                    r.TrimTop,
                    r.TrimLeft,
                    r.TrimBottom,
                    r.TrimRight,
                    r.ExternalID > 0 ? r.ExternalID : i + 1,
                    (int)r.Price,
                    r.Priority,
                    sheetGrain,
                    label
                );
                currentRepoIdx++;
            }
        }

        SheetGrainDirection primarySheetGrain = repoMetaList.Count > 0 ? (repoMetaList[0].SheetGrain?.Contains("Ngang") == true ? SheetGrainDirection.AlongWidth : SheetGrainDirection.AlongLength) : SheetGrainDirection.AlongLength;

        // 2. Demand Pieces
        int totalDemandRequested = req.DemandPieces.Sum(d => Math.Max(0, d.Quantity));
        if (totalDemandRequested <= 0)
        {
            result.Success = false;
            result.ErrorMessage = "Số lượng chi tiết cần cắt phải lớn hơn 0!";
            return result;
        }

        opt.NumberOfDemandPieces = totalDemandRequested;
        var demandMetaList = new List<DemandInputDto>();
        int currentDemandIdx = 0;

        for (int i = 0; i < req.DemandPieces.Count; i++)
        {
            var d = req.DemandPieces[i];
            int qty = Math.Max(0, d.Quantity);
            if (d.Length <= 0 || d.Width <= 0 || qty <= 0) continue;

            string label = string.IsNullOrWhiteSpace(d.Label) ? $"Chi tiết #{i + 1}" : d.Label.Trim();
            string grainStr = d.Grain ?? "";
            GrainDirection grain = grainStr switch
            {
                var s when s.Contains("Ngang") || s.Contains("|||") => GrainDirection.AlongWidth,
                var s when s.Contains("Dọc") || s.Contains("≡") => GrainDirection.AlongLength,
                var s when s.Contains("Cố định") || s.Contains("Khóa") => GrainDirection.FixedNoRotate,
                _ => GrainDirection.None
            };

            for (int k = 0; k < qty; k++)
            {
                demandMetaList.Add(new DemandInputDto
                {
                    Length = d.Length,
                    Width = d.Width,
                    Quantity = 1,
                    Material = d.Material ?? "",
                    Grain = d.Grain ?? "",
                    Label = label,
                    CanRotate = d.CanRotate,
                    ExternalID = d.ExternalID > 0 ? d.ExternalID : i + 1,
                    Priority = d.Priority
                });

                opt.SetDemandPieceWithGrain(
                    currentDemandIdx,
                    d.Length,
                    d.Width,
                    grain,
                    d.ExternalID > 0 ? d.ExternalID : i + 1,
                    d.Priority,
                    primarySheetGrain,
                    label
                );
                currentDemandIdx++;
            }
        }

        // 3. Parameters
        opt.BladeWidth = Math.Round(Math.Max(0.0, req.BladeWidth), 1, MidpointRounding.AwayFromZero);
        opt.OptimizationLevel = Math.Clamp(req.OptimizationLevel, 1, 100);
        opt.RandomSeed = req.RandomSeed > 0 ? req.RandomSeed : 1;
        opt.MinimalUsefulSize = Math.Max(0, req.MinimalUsefulSize);
        opt.MaximalCutLength = Math.Max(0, req.MaximalCutLength);
        opt.MinimizePatterns = req.MinimizePatterns ? 1 : 0;

        // 4. Run Optimization Algorithm
        switch (req.Method)
        {
            case 1:
                opt.StartNested();
                break;
            case 2:
                opt.StartMultiStages();
                break;
            default:
                opt.StartGuillotine();
                break;
        }

        sw.Stop();
        result.ExecutionTimeMs = sw.ElapsedMilliseconds;

        // 5. Grouped Layouts & Results
        var groupedLayouts = opt.GetGroupedSheetLayouts();
        int totalUtilizedSheets = opt.TotalNumberOfUtilizedRepositoryPieces;
        int totalPlacedPieces = opt.TotalNumberOfUtilizedDemandPieces;

        result.TotalDemandPiecesRequested = totalDemandRequested;
        result.TotalDemandPiecesPlaced = totalPlacedPieces;
        result.TotalRepoSheetsStock = totalRepoRequested;
        result.TotalRepoSheetsUsed = totalUtilizedSheets;
        result.TotalUniquePatterns = groupedLayouts.Count;
        result.UnplacedPiecesCount = Math.Max(0, totalDemandRequested - totalPlacedPieces);

        // Aggregate areas
        double totalUsefulArea = 0;
        double totalCutLength = 0;
        foreach (var g in groupedLayouts)
        {
            double patternUseful = g.RepresentativeSheet.UtilizedPieces.Sum(p => (double)p.Width * p.Height);
            totalUsefulArea += patternUseful * g.Count;
            totalCutLength += g.TotalCutLength * g.Count;
        }

        double totalWasteArea = opt.TotalWaste;
        double totalGrossArea = totalUsefulArea + totalWasteArea;

        result.TotalUsefulAreaM2 = totalUsefulArea / 1_000_000.0;
        result.TotalWasteAreaM2 = totalWasteArea / 1_000_000.0;
        result.TotalGrossAreaM2 = totalGrossArea / 1_000_000.0;
        result.OverallUtilizationPercent = totalGrossArea > 0 ? (totalUsefulArea / totalGrossArea) * 100.0 : 0;
        result.TotalCutLengthMm = totalCutLength;

        // 6. Build pattern DTOs & SVG
        string wasteHatch = string.IsNullOrEmpty(req.WasteHatchStyle) ? "DiagonalCross" : req.WasteHatchStyle;

        for (int pIdx = 0; pIdx < groupedLayouts.Count; pIdx++)
        {
            var grp = groupedLayouts[pIdx];
            var sheet = grp.RepresentativeSheet;
            int repeatCount = grp.Count;

            string repoLabel = grp.RepositoryLabel;
            if (string.IsNullOrWhiteSpace(repoLabel))
                repoLabel = $"Tấm kho #{sheet.RepositoryIndex + 1} ({sheet.SheetLength}x{sheet.SheetWidth})";

            var patternDto = new PatternResultDto
            {
                PatternIndex = pIdx,
                RepositoryLabel = repoLabel,
                SheetLength = sheet.SheetLength,
                SheetWidth = sheet.SheetWidth,
                SheetCount = repeatCount,
                PiecesPerSheet = sheet.UtilizedPieces.Count,
                TotalPiecesInPattern = sheet.UtilizedPieces.Count * repeatCount,
                SurfaceCoveredPercent = grp.SurfaceCovered,
                TotalCutLength = grp.TotalCutLength,
                WasteSurface = grp.WasteSurface,
                SvgContent = GenerateSvg(sheet, pIdx, opt.BladeWidth, repoLabel, repeatCount, wasteHatch, demandMetaList)
            };

            // Utilized Pieces
            for (int i = 0; i < sheet.UtilizedPieces.Count; i++)
            {
                var up = sheet.UtilizedPieces[i];
                string pieceLabel = up.Label;
                string mat = "";
                string grainLabel = "⟳ Tự do (Không thớ)";

                if (up.DemandIndex >= 0 && up.DemandIndex < demandMetaList.Count)
                {
                    var meta = demandMetaList[up.DemandIndex];
                    if (string.IsNullOrEmpty(pieceLabel)) pieceLabel = meta.Label;
                    mat = meta.Material;
                    grainLabel = meta.Grain;
                }
                if (string.IsNullOrEmpty(pieceLabel)) pieceLabel = $"Chi tiết #{up.DemandExternalID}";

                patternDto.UtilizedPieces.Add(new UtilizedPieceDto
                {
                    Index = i + 1,
                    Label = pieceLabel,
                    TopX = up.TopX,
                    TopY = up.TopY,
                    BottomX = up.BottomX,
                    BottomY = up.BottomY,
                    Width = up.Width,
                    Height = up.Height,
                    Rotated = up.Rotated,
                    DemandIndex = up.DemandIndex,
                    DemandExternalID = up.DemandExternalID,
                    GrainLabel = grainLabel,
                    Material = mat
                });
            }

            // Cuts
            for (int i = 0; i < sheet.Cuts.Count; i++)
            {
                var cut = sheet.Cuts[i];
                string dir = (Math.Abs(cut.TopX - cut.BottomX) < 0.05) ? "Dọc" : (Math.Abs(cut.TopY - cut.BottomY) < 0.05) ? "Ngang" : "Khác";
                patternDto.Cuts.Add(new CutItemDto
                {
                    Index = i + 1,
                    TopX = cut.TopX,
                    TopY = cut.TopY,
                    BottomX = cut.BottomX,
                    BottomY = cut.BottomY,
                    CutThickness = cut.CutThickness,
                    Level = cut.Level,
                    Direction = dir
                });
            }

            // Waste Pieces
            SheetGrainDirection sGrain = opt.GetSheetGrain(sheet.SheetIndex);
            for (int i = 0; i < sheet.WastePieces.Count; i++)
            {
                var wp = sheet.WastePieces[i];
                bool isUseful = (wp.Width >= opt.MinimalUsefulSize && wp.Height >= opt.MinimalUsefulSize);
                string grainAvail = isUseful
                    ? (sGrain == SheetGrainDirection.AlongLength ? "⟷ Dọc (Dài theo X)" : "↕ Ngang (Rộng theo Y)")
                    : "-";

                patternDto.WastePieces.Add(new WastePieceItemDto
                {
                    Index = i + 1,
                    TopX = wp.TopX,
                    TopY = wp.TopY,
                    BottomX = wp.BottomX,
                    BottomY = wp.BottomY,
                    Width = wp.Width,
                    Height = wp.Height,
                    Area = wp.Area,
                    IsUseful = isUseful,
                    Classification = isUseful ? "Tái sử dụng" : "Vụn",
                    GrainAvailable = grainAvail
                });
            }

            result.Patterns.Add(patternDto);
        }

        return result;
    }

    private string GenerateSvg(
        SheetLayout sheet,
        int patternIdx,
        double bladeWidth,
        string repoLabel,
        int repeatCount,
        string wasteHatchStyle,
        List<DemandInputDto> demandMeta)
    {
        int sw = sheet.SheetLength;
        int sh = sheet.SheetWidth;

        int marginX = 75;
        int marginY = 90;
        int totalSvgW = sw + marginX + 35;
        int totalSvgH = sh + marginY + 35;

        string cleanRepoLabel = WebUtility.HtmlEncode(repoLabel);
        var sb = new StringBuilder();

        sb.AppendLine($"<svg viewBox=\"0 0 {totalSvgW} {totalSvgH}\" preserveAspectRatio=\"xMidYMid meet\" class=\"cutting-svg-canvas\" data-pattern=\"{patternIdx}\">");
        sb.AppendLine("  <defs>");

        // Waste Hatch Pattern
        switch (wasteHatchStyle)
        {
            case "BackwardDiagonal":
                sb.AppendLine($"    <pattern id=\"wasteHatch_{patternIdx}\" width=\"20\" height=\"20\" patternUnits=\"userSpaceOnUse\">");
                sb.AppendLine("      <path d=\"M 0 20 L 20 0\" stroke=\"#d97706\" stroke-width=\"1.8\" />");
                sb.AppendLine("    </pattern>");
                break;
            case "ForwardDiagonal":
                sb.AppendLine($"    <pattern id=\"wasteHatch_{patternIdx}\" width=\"20\" height=\"20\" patternUnits=\"userSpaceOnUse\">");
                sb.AppendLine("      <path d=\"M 0 0 L 20 20\" stroke=\"#d97706\" stroke-width=\"1.8\" />");
                sb.AppendLine("    </pattern>");
                break;
            case "Solid":
                sb.AppendLine($"    <pattern id=\"wasteHatch_{patternIdx}\" width=\"10\" height=\"10\" patternUnits=\"userSpaceOnUse\">");
                sb.AppendLine("      <rect width=\"10\" height=\"10\" fill=\"#fed7aa\" />");
                sb.AppendLine("    </pattern>");
                break;
            case "CornerX":
                sb.AppendLine($"    <pattern id=\"wasteHatch_{patternIdx}\" width=\"32\" height=\"32\" patternUnits=\"userSpaceOnUse\">");
                sb.AppendLine("      <path d=\"M 0 0 L 32 32 M 32 0 L 0 32\" stroke=\"#ea580c\" stroke-width=\"1.5\" />");
                sb.AppendLine("    </pattern>");
                break;
            default: // DiagonalCross
                sb.AppendLine($"    <pattern id=\"wasteHatch_{patternIdx}\" width=\"24\" height=\"24\" patternUnits=\"userSpaceOnUse\">");
                sb.AppendLine("      <path d=\"M 0 0 L 24 24 M 24 0 L 0 24\" stroke=\"#d97706\" stroke-width=\"1.8\" />");
                sb.AppendLine("    </pattern>");
                break;
        }

        // Drop shadow filter for pieces
        sb.AppendLine("    <filter id=\"pieceShadow\" x=\"-5%\" y=\"-5%\" width=\"110%\" height=\"110%\">");
        sb.AppendLine("      <feDropShadow dx=\"1\" dy=\"1\" stdDeviation=\"1.5\" flood-opacity=\"0.15\" />");
        sb.AppendLine("    </filter>");
        sb.AppendLine("  </defs>");

        // Header Banner inside SVG
        sb.AppendLine($"  <rect x=\"{marginX}\" y=\"10\" width=\"{sw}\" height=\"40\" rx=\"6\" fill=\"#fefce8\" stroke=\"#ca8a04\" stroke-width=\"1.5\" />");
        sb.AppendLine($"  <text x=\"{marginX + 16}\" y=\"36\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"18\" font-weight=\"bold\" fill=\"#854d0e\">🪵 Sơ đồ #{patternIdx + 1} • Tấm vật liệu: [{cleanRepoLabel}] • {sw} x {sh} mm</text>");
        sb.AppendLine($"  <text x=\"{marginX + sw - 16}\" y=\"36\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"18\" font-weight=\"bold\" fill=\"#16a34a\" text-anchor=\"end\">✂ CẮT x{repeatCount} TẤM (Lặp {repeatCount} lần)</text>");

        // Top Dimension line (Length)
        sb.AppendLine($"  <line x1=\"{marginX}\" y1=\"{marginY - 16}\" x2=\"{marginX + sw}\" y2=\"{marginY - 16}\" stroke=\"#475569\" stroke-width=\"1.8\" />");
        sb.AppendLine($"  <line x1=\"{marginX}\" y1=\"{marginY - 24}\" x2=\"{marginX}\" y2=\"{marginY - 8}\" stroke=\"#475569\" stroke-width=\"1.8\" />");
        sb.AppendLine($"  <line x1=\"{marginX + sw}\" y1=\"{marginY - 24}\" x2=\"{marginX + sw}\" y2=\"{marginY - 8}\" stroke=\"#475569\" stroke-width=\"1.8\" />");
        sb.AppendLine($"  <text x=\"{marginX + sw / 2}\" y=\"{marginY - 22}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"20\" font-weight=\"bold\" fill=\"#1e293b\" text-anchor=\"middle\">{sw} mm (Chiều dài)</text>");

        // Left Dimension line (Width)
        sb.AppendLine($"  <line x1=\"{marginX - 16}\" y1=\"{marginY}\" x2=\"{marginX - 16}\" y2=\"{marginY + sh}\" stroke=\"#475569\" stroke-width=\"1.8\" />");
        sb.AppendLine($"  <line x1=\"{marginX - 24}\" y1=\"{marginY}\" x2=\"{marginX - 8}\" y2=\"{marginY}\" stroke=\"#475569\" stroke-width=\"1.8\" />");
        sb.AppendLine($"  <line x1=\"{marginX - 24}\" y1=\"{marginY + sh}\" x2=\"{marginX - 8}\" y2=\"{marginY + sh}\" stroke=\"#475569\" stroke-width=\"1.8\" />");
        sb.AppendLine($"  <text x=\"{marginX - 24}\" y=\"{marginY + sh / 2}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"20\" font-weight=\"bold\" fill=\"#1e293b\" text-anchor=\"middle\" transform=\"rotate(-90 {marginX - 24} {marginY + sh / 2})\">{sh} mm (Chiều rộng)</text>");

        // Base Sheet background
        sb.AppendLine($"  <rect x=\"{marginX}\" y=\"{marginY}\" width=\"{sw}\" height=\"{sh}\" fill=\"#f8fafc\" stroke=\"#334155\" stroke-width=\"3\" rx=\"4\" />");

        // 1. Draw Waste Pieces
        foreach (var wp in sheet.WastePieces)
        {
            if (wp.Width <= 0 || wp.Height <= 0) continue;
            double rx = marginX + wp.TopX;
            double ry = marginY + wp.TopY;
            double rw = wp.Width;
            double rh = wp.Height;

            string sRx = rx.ToString("0.#", CultureInfo.InvariantCulture);
            string sRy = ry.ToString("0.#", CultureInfo.InvariantCulture);
            string sRw = rw.ToString("0.#", CultureInfo.InvariantCulture);
            string sRh = rh.ToString("0.#", CultureInfo.InvariantCulture);

            sb.AppendLine($"  <rect x=\"{sRx}\" y=\"{sRy}\" width=\"{sRw}\" height=\"{sRh}\" fill=\"#fffbeb\" stroke=\"#f59e0b\" stroke-width=\"1.5\" stroke-dasharray=\"6,4\" />");
            sb.AppendLine($"  <rect x=\"{sRx}\" y=\"{sRy}\" width=\"{sRw}\" height=\"{sRh}\" fill=\"url(#wasteHatch_{patternIdx})\" />");

            if (rw >= 80 && rh >= 40)
            {
                double cx = rx + rw / 2;
                double cy = ry + rh / 2;
                sb.AppendLine($"  <rect x=\"{(cx - 50).ToString("0.#", CultureInfo.InvariantCulture)}\" y=\"{(cy - 14).ToString("0.#", CultureInfo.InvariantCulture)}\" width=\"100\" height=\"28\" rx=\"4\" fill=\"rgba(255,255,255,0.85)\" stroke=\"#f59e0b\" stroke-width=\"1\" />");
                sb.AppendLine($"  <text x=\"{cx.ToString("0.#", CultureInfo.InvariantCulture)}\" y=\"{(cy + 4).ToString("0.#", CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"13\" font-weight=\"bold\" fill=\"#b45309\" text-anchor=\"middle\">{rw:0.#} x {rh:0.#}</text>");
            }
        }

        // 2. Draw Utilized Pieces
        for (int i = 0; i < sheet.UtilizedPieces.Count; i++)
        {
            var p = sheet.UtilizedPieces[i];
            double px = marginX + p.TopX;
            double py = marginY + p.TopY;
            double pw = p.Width;
            double ph = p.Height;

            string sPx = px.ToString("0.#", CultureInfo.InvariantCulture);
            string sPy = py.ToString("0.#", CultureInfo.InvariantCulture);
            string sPw = pw.ToString("0.#", CultureInfo.InvariantCulture);
            string sPh = ph.ToString("0.#", CultureInfo.InvariantCulture);

            string colorFill = PieceColors[i % PieceColors.Length];
            string colorStroke = PieceStrokeColors[i % PieceStrokeColors.Length];

            string pieceLabel = p.Label;
            if (string.IsNullOrEmpty(pieceLabel) && p.DemandIndex >= 0 && p.DemandIndex < demandMeta.Count)
                pieceLabel = demandMeta[p.DemandIndex].Label;
            if (string.IsNullOrEmpty(pieceLabel))
                pieceLabel = $"#{i + 1}";

            string safeLabel = WebUtility.HtmlEncode(pieceLabel);
            string rotMarker = p.Rotated == 1 ? " ↻ 90°" : "";

            sb.AppendLine($"  <g class=\"svg-piece\" data-piece-id=\"{i + 1}\" data-label=\"{safeLabel}\" data-dim=\"{pw:0.#}x{ph:0.#}\" data-pos=\"({p.TopX:0.#},{p.TopY:0.#})-({p.BottomX:0.#},{p.BottomY:0.#})\">");
            sb.AppendLine($"    <rect x=\"{sPx}\" y=\"{sPy}\" width=\"{sPw}\" height=\"{sPh}\" fill=\"{colorFill}\" stroke=\"{colorStroke}\" stroke-width=\"2\" rx=\"3\" filter=\"url(#pieceShadow)\" />");

            // Text labels if piece is big enough
            if (pw >= 45 && ph >= 30)
            {
                double cx = px + pw / 2;
                double cy = py + ph / 2;
                string sCx = cx.ToString("0.#", CultureInfo.InvariantCulture);

                if (ph >= 60 && pw >= 75)
                {
                    // Piece # Badge
                    sb.AppendLine($"    <rect x=\"{(px + 4).ToString("0.#", CultureInfo.InvariantCulture)}\" y=\"{(py + 4).ToString("0.#", CultureInfo.InvariantCulture)}\" width=\"24\" height=\"20\" rx=\"3\" fill=\"{colorStroke}\" />");
                    sb.AppendLine($"    <text x=\"{(px + 16).ToString("0.#", CultureInfo.InvariantCulture)}\" y=\"{(py + 18).ToString("0.#", CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"12\" font-weight=\"bold\" fill=\"#ffffff\" text-anchor=\"middle\">{i + 1}</text>");

                    // Piece Name
                    sb.AppendLine($"    <text x=\"{sCx}\" y=\"{(cy - 6).ToString("0.#", CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"15\" font-weight=\"bold\" fill=\"#1e293b\" text-anchor=\"middle\">{safeLabel}</text>");
                    // Piece Dimension
                    sb.AppendLine($"    <text x=\"{sCx}\" y=\"{(cy + 15).ToString("0.#", CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"13\" font-weight=\"bold\" fill=\"#475569\" text-anchor=\"middle\">{pw:0.#} x {ph:0.#}{rotMarker}</text>");
                }
                else
                {
                    sb.AppendLine($"    <text x=\"{sCx}\" y=\"{(cy + 5).ToString("0.#", CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"13\" font-weight=\"bold\" fill=\"#1e293b\" text-anchor=\"middle\">{pw:0.#}x{ph:0.#}</text>");
                }
            }
            sb.AppendLine("  </g>");
        }

        // 3. Draw Cuts (Guillotine cut lines)
        foreach (var c in sheet.Cuts)
        {
            double cx1 = marginX + c.TopX;
            double cy1 = marginY + c.TopY;
            double cx2 = marginX + c.BottomX;
            double cy2 = marginY + c.BottomY;

            string sCx1 = cx1.ToString("0.#", CultureInfo.InvariantCulture);
            string sCy1 = cy1.ToString("0.#", CultureInfo.InvariantCulture);
            string sCx2 = cx2.ToString("0.#", CultureInfo.InvariantCulture);
            string sCy2 = cy2.ToString("0.#", CultureInfo.InvariantCulture);

            sb.AppendLine($"  <line x1=\"{sCx1}\" y1=\"{sCy1}\" x2=\"{sCx2}\" y2=\"{sCy2}\" stroke=\"#ef4444\" stroke-width=\"1.8\" stroke-dasharray=\"5,3\" opacity=\"0.85\" />");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }
}
