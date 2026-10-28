using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using Optimal2DX;
using Optimal2DX.Models;

namespace Optimal2DX.WinForms;

/// <summary>
/// Xuất toàn diện Báo cáo Tối ưu hóa Sơ đồ Cắt 2D sang định dạng HTML chuyên nghiệp.
/// Bao gồm:
/// - Thống kê tổng quan KPI (Executive Summary)
/// - Danh sách chi tiết cần cắt (Demand Pieces)
/// - Danh sách các tấm vật liệu được sử dụng & số lượng (Repository Sheets)
/// - Sơ đồ cắt trực quan Vector SVG (Hiển thị sơ đồ, nhãn, gạch chéo phế liệu, số lượng tấm)
/// - Danh sách chi tiết phế liệu (Waste Pieces)
/// - Hỗ trợ in ấn A4 và xuất PDF qua trình duyệt web.
/// </summary>
public static class HtmlReportExporter
{
    private static readonly string[] PieceColors = new[]
    {
        "#dae8fc", // Xanh dương nhạt
        "#d5e8d4", // Xanh lá nhạt
        "#f8cecc", // Đỏ hồng nhạt
        "#ffe6cc", // Cam pastel
        "#fff2cc", // Vàng nhạt
        "#e1d5e7", // Tím nhạt
        "#d0e1fd", // Xanh da trời
        "#e3c800", // Vàng nghệ
        "#cbf0f8", // Xanh cyan
        "#ead1dc"  // Hồng phấn
    };

    public static void Export(
        Optimization2DX opt,
        List<GroupedSheetLayout> groupedLayouts,
        DataGridView dgvDemand,
        DataGridView dgvRepo,
        double bladeWidth,
        int minUsefulSize,
        string filePath)
    {
        var sb = new StringBuilder();

        // 1. Dữ liệu tổng hợp
        int totalDemandPieces = opt.NumberOfDemandPieces;
        int totalPlacedPieces = opt.TotalNumberOfUtilizedDemandPieces;
        int totalRepoSheets = opt.TotalNumberOfUtilizedRepositoryPieces;
        int totalUniquePatterns = groupedLayouts.Count;

        // Tính tổng diện tích
        double totalUsefulArea = 0;
        foreach (var g in groupedLayouts)
        {
            double sheetCovered = g.RepresentativeSheet.UtilizedPieces.Sum(p => (double)p.Width * p.Height);
            totalUsefulArea += sheetCovered * g.Count;
        }
        double totalWasteArea = opt.TotalWaste;
        double totalGrossArea = totalUsefulArea + totalWasteArea;
        double avgUtilization = totalGrossArea > 0 ? (totalUsefulArea / totalGrossArea) * 100.0 : 0;

        // Bắt đầu viết HTML
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"vi\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine("  <title>Báo Cáo Sơ Đồ Cắt 2D - Cut2DX</title>");
        sb.AppendLine("  <style>");
        AppendStyles(sb);
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Top Navigation Bar
        sb.AppendLine("  <div class=\"top-bar no-print\">");
        sb.AppendLine("    <div class=\"top-bar-inner\">");
        sb.AppendLine("      <div class=\"brand\">🪵 <strong>Cut2DX</strong> • Báo Cáo Sơ Đồ Cắt 2D</div>");
        sb.AppendLine("      <div class=\"nav-links\">");
        sb.AppendLine("        <a href=\"#sec-kpi\">Tổng quan</a>");
        sb.AppendLine("        <a href=\"#sec-demand\">1. Chi tiết cần cắt</a>");
        sb.AppendLine("        <a href=\"#sec-repo\">2. Tấm vật liệu kho</a>");
        sb.AppendLine("        <a href=\"#sec-patterns\">3. Sơ đồ cắt (SVG)</a>");
        sb.AppendLine("        <a href=\"#sec-waste\">4. Danh sách phế liệu</a>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"actions\">");
        sb.AppendLine("        <button class=\"btn-print\" onclick=\"window.print()\">🖨 In báo cáo / Lưu PDF</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <div class=\"container\">");

        // Report Header
        sb.AppendLine("    <header class=\"report-header\">");
        sb.AppendLine("      <h1>BÁO CÁO TỐI ƯU HÓA SƠ ĐỒ CẮT 2D</h1>");
        sb.AppendLine($"      <p class=\"subtitle\">Hệ thống tính toán tối ưu cắt tấm công nghiệp • Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>");
        sb.AppendLine("      <div class=\"report-meta\">");
        sb.AppendLine($"        <span><strong>Độ dày mạch cưa (Kerf):</strong> {bladeWidth:0.#} mm</span>");
        sb.AppendLine($"        <span><strong>Khổ phế liệu tái sử dụng:</strong> &ge; {minUsefulSize} mm</span>");
        sb.AppendLine($"        <span><strong>Chế độ tối ưu:</strong> {(opt.MinimizePatterns == 1 ? "Gom giảm số lượng sơ đồ (Batch Mode)" : "Phân tán tự do")}</span>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");

        // KPI Summary Cards
        sb.AppendLine("    <section id=\"sec-kpi\" class=\"kpi-grid\">");
        sb.AppendLine($"      <div class=\"kpi-card highlight-blue\">");
        sb.AppendLine($"        <div class=\"kpi-val\">{totalPlacedPieces:N0} / {totalDemandPieces:N0}</div>");
        sb.AppendLine($"        <div class=\"kpi-label\">Chi tiết đã xếp ({(totalDemandPieces > 0 ? (double)totalPlacedPieces / totalDemandPieces * 100 : 0):F1}%)</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine($"      <div class=\"kpi-card highlight-green\">");
        sb.AppendLine($"        <div class=\"kpi-val\">{totalRepoSheets:N0} tấm</div>");
        sb.AppendLine($"        <div class=\"kpi-label\">Ván nguyên liệu sử dụng</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine($"      <div class=\"kpi-card highlight-purple\">");
        sb.AppendLine($"        <div class=\"kpi-val\">{totalUniquePatterns:N0} sơ đồ</div>");
        sb.AppendLine($"        <div class=\"kpi-label\">Mẫu sơ đồ cắt độc nhất</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine($"      <div class=\"kpi-card highlight-orange\">");
        sb.AppendLine($"        <div class=\"kpi-val\">{avgUtilization:F2}%</div>");
        sb.AppendLine($"        <div class=\"kpi-label\">Tỷ lệ lợi dụng vật liệu</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </section>");

        // 1. Danh sách chi tiết cần cắt
        AppendDemandSection(sb, dgvDemand, groupedLayouts);

        // 2. Danh sách các tấm vật liệu được sử dụng & số lượng
        AppendRepoSection(sb, dgvRepo, groupedLayouts, totalRepoSheets);

        // 3. Sơ đồ cắt trực quan
        AppendPatternsSection(sb, groupedLayouts, bladeWidth);

        // 4. Danh sách phế liệu
        AppendWasteSection(sb, groupedLayouts, minUsefulSize);

        // Report Footer
        sb.AppendLine("    <footer class=\"report-footer\">");
        sb.AppendLine("      <p>Báo cáo được khởi tạo tự động bởi <strong>Optimal2DX Engine</strong> • Bản quyền &copy; 2026</p>");
        sb.AppendLine("    </footer>");

        sb.AppendLine("  </div>"); // .container
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static void AppendDemandSection(StringBuilder sb, DataGridView dgv, List<GroupedSheetLayout> grouped)
    {
        sb.AppendLine("    <section id=\"sec-demand\" class=\"section-card\">");
        sb.AppendLine("      <div class=\"section-title\">");
        sb.AppendLine("        <h2>1. Danh sách chi tiết cần cắt (Demand Specifications)</h2>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"table-responsive\">");
        sb.AppendLine("        <table class=\"data-table\">");
        sb.AppendLine("          <thead>");
        sb.AppendLine("            <tr>");
        sb.AppendLine("              <th style=\"width:50px;\">#</th>");
        sb.AppendLine("              <th>Tên chi tiết / Nhãn</th>");
        sb.AppendLine("              <th>Chiều dài (mm)</th>");
        sb.AppendLine("              <th>Chiều rộng (mm)</th>");
        sb.AppendLine("              <th>SL yêu cầu</th>");
        sb.AppendLine("              <th>SL đã xếp</th>");
        sb.AppendLine("              <th>Trạng thái</th>");
        sb.AppendLine("              <th>DT 1 phôi (m²)</th>");
        sb.AppendLine("              <th>Tổng DT (m²)</th>");
        sb.AppendLine("              <th>Thớ gỗ</th>");
        sb.AppendLine("            </tr>");
        sb.AppendLine("          </thead>");
        sb.AppendLine("          <tbody>");

        int totalReq = 0;
        int totalDone = 0;
        double sumArea = 0;

        // Thống kê số lượng đã xếp từ kết quả
        var placedCountByDim = new Dictionary<string, int>();
        foreach (var grp in grouped)
        {
            foreach (var up in grp.RepresentativeSheet.UtilizedPieces)
            {
                // Key kích thước gốc của chi tiết
                double w = up.Width;
                double h = up.Height;
                if (up.Rotated == 1) (w, h) = (h, w);
                string key = $"{w:0.#}x{h:0.#}";
                if (!placedCountByDim.ContainsKey(key)) placedCountByDim[key] = 0;
                placedCountByDim[key] += grp.Count;
            }
        }

        int rowIdx = 0;
        for (int r = 0; r < dgv.Rows.Count; r++)
        {
            var row = dgv.Rows[r];
            if (row.IsNewRow) continue;
            rowIdx++;

            int len = Convert.ToInt32(row.Cells["Length"].Value ?? 0);
            int wid = Convert.ToInt32(row.Cells["Width"].Value ?? 0);
            string label = row.Cells["Label"].Value?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(label)) label = $"Chi tiết #{rowIdx} ({len}x{wid})";
            int qty = Convert.ToInt32(row.Cells["Quantity"].Value ?? 1);
            string grain = row.Cells["Grain"].Value?.ToString() ?? "-";

            string key1 = $"{len}x{wid}";
            string key2 = $"{wid}x{len}";
            int placed = 0;
            if (placedCountByDim.ContainsKey(key1)) placed = placedCountByDim[key1];
            else if (placedCountByDim.ContainsKey(key2)) placed = placedCountByDim[key2];

            double oneArea = (double)len * wid / 1_000_000.0;
            double rowTotalArea = oneArea * qty;

            totalReq += qty;
            totalDone += placed;
            sumArea += rowTotalArea;

            bool isFull = placed >= qty;
            string statusBadge = isFull
                ? "<span class=\"badge badge-success\">Đủ 100%</span>"
                : $"<span class=\"badge badge-warning\">Thiếu {qty - placed}</span>";

            sb.AppendLine("            <tr>");
            sb.AppendLine($"              <td class=\"text-center\">{rowIdx}</td>");
            sb.AppendLine($"              <td><strong>{WebUtility.HtmlEncode(label)}</strong></td>");
            sb.AppendLine($"              <td class=\"text-right\">{len:N0}</td>");
            sb.AppendLine($"              <td class=\"text-right\">{wid:N0}</td>");
            sb.AppendLine($"              <td class=\"text-right\">{qty:N0}</td>");
            sb.AppendLine($"              <td class=\"text-right\">{placed:N0}</td>");
            sb.AppendLine($"              <td class=\"text-center\">{statusBadge}</td>");
            sb.AppendLine($"              <td class=\"text-right\">{oneArea:F3}</td>");
            sb.AppendLine($"              <td class=\"text-right\">{rowTotalArea:F3}</td>");
            sb.AppendLine($"              <td>{WebUtility.HtmlEncode(grain)}</td>");
            sb.AppendLine("            </tr>");
        }

        sb.AppendLine("          </tbody>");
        sb.AppendLine("          <tfoot>");
        sb.AppendLine("            <tr class=\"total-row\">");
        sb.AppendLine("              <td colspan=\"4\" class=\"text-left\"><strong>TỔNG CỘNG</strong></td>");
        sb.AppendLine($"              <td class=\"text-right\"><strong>{totalReq:N0}</strong></td>");
        sb.AppendLine($"              <td class=\"text-right\"><strong>{totalDone:N0}</strong></td>");
        sb.AppendLine($"              <td class=\"text-center\"><strong>{(totalReq > 0 ? (double)totalDone / totalReq * 100 : 0):F1}%</strong></td>");
        sb.AppendLine("              <td></td>");
        sb.AppendLine($"              <td class=\"text-right\"><strong>{sumArea:F3} m²</strong></td>");
        sb.AppendLine("              <td></td>");
        sb.AppendLine("            </tr>");
        sb.AppendLine("          </tfoot>");
        sb.AppendLine("        </table>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </section>");
    }

    private static void AppendRepoSection(StringBuilder sb, DataGridView dgv, List<GroupedSheetLayout> grouped, int totalUsedSheets)
    {
        sb.AppendLine("    <section id=\"sec-repo\" class=\"section-card\">");
        sb.AppendLine("      <div class=\"section-title\">");
        sb.AppendLine("        <h2>2. Danh sách các tấm vật liệu được sử dụng (Repository Sheets)</h2>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"table-responsive\">");
        sb.AppendLine("        <table class=\"data-table\">");
        sb.AppendLine("          <thead>");
        sb.AppendLine("            <tr>");
        sb.AppendLine("              <th style=\"width:50px;\">#</th>");
        sb.AppendLine("              <th>Tên tấm kho / Vật liệu</th>");
        sb.AppendLine("              <th>Kích thước (Dài x Rộng mm)</th>");
        sb.AppendLine("              <th>Phần cắt bỏ (Trim T,L,B,R)</th>");
        sb.AppendLine("              <th>Thớ gỗ tấm</th>");
        sb.AppendLine("              <th>SL trong kho</th>");
        sb.AppendLine("              <th>SL thực dùng</th>");
        sb.AppendLine("              <th>SL dôi dư</th>");
        sb.AppendLine("              <th>Tỷ lệ dùng</th>");
        sb.AppendLine("            </tr>");
        sb.AppendLine("          </thead>");
        sb.AppendLine("          <tbody>");

        int totalDeclaredStock = 0;
        int totalActualUsed = 0;

        int rowIdx = 0;
        for (int r = 0; r < dgv.Rows.Count; r++)
        {
            var row = dgv.Rows[r];
            if (row.IsNewRow) continue;
            rowIdx++;

            int len = Convert.ToInt32(row.Cells["Length"].Value ?? 0);
            int wid = Convert.ToInt32(row.Cells["Width"].Value ?? 0);
            string label = row.Cells["Label"].Value?.ToString()?.Trim() ?? "";
            string material = row.Cells["Material"].Value?.ToString()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(material) && !string.IsNullOrEmpty(label))
                label = $"{label} ({material})";
            else if (string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(material))
                label = material;
            else if (string.IsNullOrEmpty(label))
                label = $"Ván kho #{rowIdx} ({len}x{wid})";
            int stockQty = Convert.ToInt32(row.Cells["Quantity"].Value ?? 1);
            string grain = row.Cells["SheetGrain"].Value?.ToString() ?? "Dọc (Theo Dài)";

            int top = Convert.ToInt32(row.Cells["TrimTop"].Value ?? 0);
            int left = Convert.ToInt32(row.Cells["TrimLeft"].Value ?? 0);
            int bottom = Convert.ToInt32(row.Cells["TrimBottom"].Value ?? 0);
            int right = Convert.ToInt32(row.Cells["TrimRight"].Value ?? 0);
            string trimStr = $"T:{top}, L:{left}, B:{bottom}, R:{right} mm";

            // Đếm số lượng tấm thực tế đã dùng theo quy cách tấm này
            int usedForThisSpec = 0;
            foreach (var grp in grouped)
            {
                if (grp.SheetLength == len && grp.SheetWidth == wid)
                {
                    usedForThisSpec += grp.Count;
                }
            }

            // Nếu chỉ có 1 dòng kho thì dùng toàn bộ totalUsedSheets
            if (dgv.Rows.Count <= 2 && usedForThisSpec == 0)
                usedForThisSpec = totalUsedSheets;

            int remaining = Math.Max(0, stockQty - usedForThisSpec);
            double pct = stockQty > 0 ? (double)usedForThisSpec / stockQty * 100 : 0;

            totalDeclaredStock += stockQty;
            totalActualUsed += usedForThisSpec;

            sb.AppendLine("            <tr>");
            sb.AppendLine($"              <td class=\"text-center\">{rowIdx}</td>");
            sb.AppendLine($"              <td><strong>{WebUtility.HtmlEncode(label)}</strong></td>");
            sb.AppendLine($"              <td class=\"text-center\"><strong>{len:N0} x {wid:N0}</strong></td>");
            sb.AppendLine($"              <td class=\"text-center\">{trimStr}</td>");
            sb.AppendLine($"              <td>{WebUtility.HtmlEncode(grain)}</td>");
            sb.AppendLine($"              <td class=\"text-right\">{stockQty:N0} tấm</td>");
            sb.AppendLine($"              <td class=\"text-right\"><strong class=\"text-primary\">{usedForThisSpec:N0} tấm</strong></td>");
            sb.AppendLine($"              <td class=\"text-right\"><strong class=\"text-success\">{remaining:N0} tấm</strong></td>");
            sb.AppendLine($"              <td class=\"text-center\"><span class=\"badge badge-info\">{pct:F1}%</span></td>");
            sb.AppendLine("            </tr>");
        }

        sb.AppendLine("          </tbody>");
        sb.AppendLine("          <tfoot>");
        sb.AppendLine("            <tr class=\"total-row\">");
        sb.AppendLine("              <td colspan=\"5\" class=\"text-left\"><strong>TỔNG CỘNG TẤM VÁN</strong></td>");
        sb.AppendLine($"              <td class=\"text-right\"><strong>{totalDeclaredStock:N0} tấm</strong></td>");
        sb.AppendLine($"              <td class=\"text-right\"><strong class=\"text-primary\">{totalActualUsed:N0} tấm</strong></td>");
        sb.AppendLine($"              <td class=\"text-right\"><strong class=\"text-success\">{totalDeclaredStock - totalActualUsed:N0} tấm</strong></td>");
        sb.AppendLine($"              <td class=\"text-center\"><strong>{(totalDeclaredStock > 0 ? (double)totalActualUsed / totalDeclaredStock * 100 : 0):F1}%</strong></td>");
        sb.AppendLine("            </tr>");
        sb.AppendLine("          </tfoot>");
        sb.AppendLine("        </table>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </section>");
    }

    private static void AppendPatternsSection(StringBuilder sb, List<GroupedSheetLayout> grouped, double bladeWidth)
    {
        sb.AppendLine("    <section id=\"sec-patterns\" class=\"section-card\">");
        sb.AppendLine("      <div class=\"section-title\">");
        sb.AppendLine($"        <h2>3. Sơ đồ cắt chi tiết ({grouped.Count} mẫu sơ đồ gom nhóm)</h2>");
        sb.AppendLine("        <p class=\"section-desc\">Mỗi sơ đồ hiển thị bản vẽ cắt dạng Vector SVG có thể phóng to thu nhỏ mà không vỡ nét, kèm nhãn chi tiết, vân gạch chéo cho phế liệu và số lượng tấm ván cắt lặp lại.</p>");
        sb.AppendLine("      </div>");

        for (int g = 0; g < grouped.Count; g++)
        {
            var grp = grouped[g];
            var sheet = grp.RepresentativeSheet;
            int repeatCount = grp.Count;
            int totalPartsOnPattern = sheet.UtilizedPieces.Count * repeatCount;

            string repoLabel = grp.RepositoryLabel?.Trim() ?? "";
            if (string.IsNullOrEmpty(repoLabel)) repoLabel = sheet.RepositoryLabel?.Trim() ?? "";
            if (string.IsNullOrEmpty(repoLabel)) repoLabel = $"Tấm kho #{sheet.RepositoryIndex + 1} ({sheet.SheetLength}x{sheet.SheetWidth})";

            sb.AppendLine($"      <div class=\"pattern-card\">");

            // Pattern Header
            sb.AppendLine("        <div class=\"pattern-header\">");
            sb.AppendLine("          <div class=\"pattern-title-area\">");
            sb.AppendLine($"            <span class=\"pattern-num\">SƠ ĐỒ #{g + 1}</span>");
            sb.AppendLine($"            <span class=\"badge-repo-label\">🪵 <strong>Tấm vật liệu:</strong> {WebUtility.HtmlEncode(repoLabel)}</span>");
            sb.AppendLine($"            <span class=\"stamp-batch\">✂ CẮT x{repeatCount} TẤM (Lặp {repeatCount} lần)</span>");
            sb.AppendLine("          </div>");
            sb.AppendLine("          <div class=\"pattern-badges\">");
            sb.AppendLine($"            <span class=\"badge-spec\">📐 Quy cách: {sheet.SheetLength} x {sheet.SheetWidth} mm</span>");
            sb.AppendLine($"            <span class=\"badge-util\">Độ phủ: {grp.SurfaceCovered:F2}%</span>");
            sb.AppendLine($"            <span class=\"badge-parts\">{sheet.UtilizedPieces.Count} phôi/tấm (Tổng: {totalPartsOnPattern:N0} phôi)</span>");
            sb.AppendLine($"            <span class=\"badge-cuts\">{sheet.Cuts.Count} nhát cắt • Mạch: {grp.TotalCutLength:N1} mm</span>");
            sb.AppendLine("          </div>");
            sb.AppendLine("        </div>");

            // SVG Diagram
            sb.AppendLine("        <div class=\"svg-container\">");
            AppendSvgDiagram(sb, sheet, g, bladeWidth, repoLabel, repeatCount);
            sb.AppendLine("        </div>");

            // Parts List inside this pattern
            sb.AppendLine("        <div class=\"pattern-parts-summary\">");
            sb.AppendLine($"          <h4>Chi tiết thành phần trên Sơ đồ này (Cắt từ tấm vật liệu: <strong class=\"text-primary\">{WebUtility.HtmlEncode(repoLabel)}</strong> - {sheet.SheetLength} x {sheet.SheetWidth} mm):</h4>");
            sb.AppendLine("          <table class=\"mini-table\">");
            sb.AppendLine("            <thead>");
            sb.AppendLine("              <tr>");
            sb.AppendLine("                <th>#</th>");
            sb.AppendLine("                <th>Tên chi tiết</th>");
            sb.AppendLine("                <th>Kích thước (D x R mm)</th>");
            sb.AppendLine("                <th>Tọa độ (TopX, TopY) &rarr; (BottomX, BottomY)</th>");
            sb.AppendLine("                <th>Xoay</th>");
            sb.AppendLine("                <th>SL/tấm</th>");
            sb.AppendLine($"                <th>Tổng SL ({repeatCount} tấm)</th>");
            sb.AppendLine("              </tr>");
            sb.AppendLine("            </thead>");
            sb.AppendLine("            <tbody>");

            for (int p = 0; p < sheet.UtilizedPieces.Count; p++)
            {
                var pc = sheet.UtilizedPieces[p];
                string rotStr = pc.Rotated == 1 ? "<span class=\"badge badge-warning\">↻ 90°</span>" : "0°";
                string colorDot = PieceColors[p % PieceColors.Length];

                string pieceLabel = pc.Label?.Trim() ?? "";
                if (string.IsNullOrEmpty(pieceLabel)) pieceLabel = $"Chi tiết #{p + 1} ({pc.Width}x{pc.Height})";

                sb.AppendLine("              <tr>");
                sb.AppendLine($"                <td class=\"text-center\"><span class=\"color-box\" style=\"background:{colorDot};\"></span>{p + 1}</td>");
                sb.AppendLine($"                <td><strong>{WebUtility.HtmlEncode(pieceLabel)}</strong></td>");
                sb.AppendLine($"                <td class=\"text-center\">{pc.Width} x {pc.Height}</td>");
                sb.AppendLine($"                <td class=\"text-center font-mono\">({pc.TopX}, {pc.TopY}) &rarr; ({pc.BottomX}, {pc.BottomY})</td>");
                sb.AppendLine($"                <td class=\"text-center\">{rotStr}</td>");
                sb.AppendLine($"                <td class=\"text-center\">1</td>");
                sb.AppendLine($"                <td class=\"text-center\"><strong class=\"text-primary\">{repeatCount}</strong></td>");
                sb.AppendLine("              </tr>");
            }

            sb.AppendLine("            </tbody>");
            sb.AppendLine("          </table>");
            sb.AppendLine("        </div>");

            sb.AppendLine("      </div>"); // .pattern-card
        }

        sb.AppendLine("    </section>");
    }

    private static void AppendSvgDiagram(StringBuilder sb, SheetLayout sheet, int patternIdx, double bladeWidth, string repoLabel, int repeatCount)
    {
        int sw = sheet.SheetLength;
        int sh = sheet.SheetWidth;

        int marginX = 65;
        int marginY = 82;
        int totalSvgW = sw + marginX + 30;
        int totalSvgH = sh + marginY + 30;

        string cleanRepoLabel = WebUtility.HtmlEncode(repoLabel);

        sb.AppendLine($"<svg viewBox=\"0 0 {totalSvgW} {totalSvgH}\" preserveAspectRatio=\"xMidYMid meet\" class=\"cutting-svg\">");
        sb.AppendLine("  <defs>");
        // Hoa văn Gạch chéo (DiagonalCross) cho phần phế liệu
        sb.AppendLine($"    <pattern id=\"wasteHatch_{patternIdx}\" width=\"24\" height=\"24\" patternUnits=\"userSpaceOnUse\">");
        sb.AppendLine("      <path d=\"M 0 0 L 24 24 M 24 0 L 0 24\" stroke=\"#d49758\" stroke-width=\"1.8\" />");
        sb.AppendLine("    </pattern>");
        sb.AppendLine("  </defs>");

        // Banner thông tin Tấm vật liệu & Số lượng cắt ở trên cùng SVG
        sb.AppendLine($"  <rect x=\"{marginX}\" y=\"10\" width=\"{sw}\" height=\"34\" rx=\"5\" fill=\"#fef9ee\" stroke=\"#d49758\" stroke-width=\"1.5\" />");
        sb.AppendLine($"  <text x=\"{marginX + 15}\" y=\"33\" font-family=\"Segoe UI, sans-serif\" font-size=\"18\" font-weight=\"bold\" fill=\"#78350f\">🪵 Sơ đồ #{patternIdx + 1} • Tấm vật liệu: [{cleanRepoLabel}] • Quy cách: {sw} x {sh} mm</text>");
        sb.AppendLine($"  <text x=\"{marginX + sw - 15}\" y=\"33\" font-family=\"Segoe UI, sans-serif\" font-size=\"17\" font-weight=\"bold\" fill=\"#15803d\" text-anchor=\"end\">✂ CẮT x{repeatCount} TẤM</text>");

        // Thước đo cạnh trên (Chiều dài)
        sb.AppendLine($"  <line x1=\"{marginX}\" y1=\"{marginY - 14}\" x2=\"{marginX + sw}\" y2=\"{marginY - 14}\" stroke=\"#4a5568\" stroke-width=\"1.5\" />");
        sb.AppendLine($"  <line x1=\"{marginX}\" y1=\"{marginY - 20}\" x2=\"{marginX}\" y2=\"{marginY - 8}\" stroke=\"#4a5568\" stroke-width=\"1.5\" />");
        sb.AppendLine($"  <line x1=\"{marginX + sw}\" y1=\"{marginY - 20}\" x2=\"{marginX + sw}\" y2=\"{marginY - 8}\" stroke=\"#4a5568\" stroke-width=\"1.5\" />");
        sb.AppendLine($"  <text x=\"{marginX + sw / 2}\" y=\"{marginY - 18}\" font-family=\"Segoe UI, sans-serif\" font-size=\"20\" font-weight=\"bold\" fill=\"#2d3748\" text-anchor=\"middle\">{sw} mm (Chiều dài)</text>");

        // Thước đo cạnh trái (Chiều rộng)
        sb.AppendLine($"  <line x1=\"{marginX - 14}\" y1=\"{marginY}\" x2=\"{marginX - 14}\" y2=\"{marginY + sh}\" stroke=\"#4a5568\" stroke-width=\"1.5\" />");
        sb.AppendLine($"  <line x1=\"{marginX - 20}\" y1=\"{marginY}\" x2=\"{marginX - 8}\" y2=\"{marginY}\" stroke=\"#4a5568\" stroke-width=\"1.5\" />");
        sb.AppendLine($"  <line x1=\"{marginX - 20}\" y1=\"{marginY + sh}\" x2=\"{marginX - 8}\" y2=\"{marginY + sh}\" stroke=\"#4a5568\" stroke-width=\"1.5\" />");
        sb.AppendLine($"  <text x=\"{marginX - 22}\" y=\"{marginY + sh / 2}\" font-family=\"Segoe UI, sans-serif\" font-size=\"20\" font-weight=\"bold\" fill=\"#2d3748\" text-anchor=\"middle\" transform=\"rotate(-90 {marginX - 22} {marginY + sh / 2})\">{sh} mm (Chiều rộng)</text>");

        // Nền tấm ván kho
        sb.AppendLine($"  <rect x=\"{marginX}\" y=\"{marginY}\" width=\"{sw}\" height=\"{sh}\" fill=\"#faf6ee\" stroke=\"#2d3748\" stroke-width=\"2.5\" rx=\"3\" />");

        // 1. Vẽ các mảnh phế liệu (Waste Pieces) có vân gạch chéo
        foreach (var wp in sheet.WastePieces)
        {
            if (wp.Width <= 0 || wp.Height <= 0) continue;
            double rx = marginX + wp.TopX;
            double ry = marginY + wp.TopY;
            double rw = wp.Width;
            double rh = wp.Height;

            string sRx = rx.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string sRy = ry.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string sRw = rw.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string sRh = rh.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

            // Nền cam nhạt + gạch chéo đan lưới
            sb.AppendLine($"  <rect x=\"{sRx}\" y=\"{sRy}\" width=\"{sRw}\" height=\"{sRh}\" fill=\"#fef4ea\" stroke=\"#c88242\" stroke-width=\"1.5\" stroke-dasharray=\"6,4\" />");
            sb.AppendLine($"  <rect x=\"{sRx}\" y=\"{sRy}\" width=\"{sRw}\" height=\"{sRh}\" fill=\"url(#wasteHatch_{patternIdx})\" />");

            // Khung nhãn phế liệu nếu kích thước vừa đủ
            if (rw >= 110 && rh >= 55)
            {
                double badgeW = Math.Min(rw - 10, 110);
                double badgeH = Math.Min(rh - 10, 48);
                double bx = rx + (rw - badgeW) / 2;
                double by = ry + (rh - badgeH) / 2;
                sb.AppendLine($"  <rect x=\"{bx.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" y=\"{by.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" width=\"{badgeW.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" height=\"{badgeH.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" rx=\"5\" fill=\"rgba(255,255,255,0.92)\" stroke=\"#d49758\" stroke-width=\"1\" />");
                sb.AppendLine($"  <text x=\"{(bx + badgeW / 2).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" y=\"{(by + 20).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI, sans-serif\" font-size=\"18\" font-style=\"italic\" font-weight=\"bold\" fill=\"#9c4e0b\" text-anchor=\"middle\">w</text>");
                sb.AppendLine($"  <text x=\"{(bx + badgeW / 2).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" y=\"{(by + 38).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI, sans-serif\" font-size=\"16\" font-weight=\"bold\" fill=\"#9c4e0b\" text-anchor=\"middle\">{wp.Width:0.#}x{wp.Height:0.#}</text>");
            }
        }

        // 2. Vẽ các chi tiết thành phẩm (Utilized Pieces)
        for (int p = 0; p < sheet.UtilizedPieces.Count; p++)
        {
            var up = sheet.UtilizedPieces[p];
            double rx = marginX + up.TopX;
            double ry = marginY + up.TopY;
            double rw = up.Width;
            double rh = up.Height;
            string color = PieceColors[p % PieceColors.Length];

            string sRx = rx.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string sRy = ry.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string sRw = rw.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string sRh = rh.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

            sb.AppendLine($"  <g class=\"part-group\">");
            sb.AppendLine($"    <rect x=\"{sRx}\" y=\"{sRy}\" width=\"{sRw}\" height=\"{sRh}\" fill=\"{color}\" stroke=\"#1f2937\" stroke-width=\"2\" rx=\"2\" />");

            // Nhãn và kích thước chi tiết
            if (rw >= 70 && rh >= 40)
            {
                double cx = rx + rw / 2;
                double cy = ry + rh / 2;
                string cleanLabel = up.Label?.Trim() ?? "";
                if (string.IsNullOrEmpty(cleanLabel)) cleanLabel = $"#{p + 1} ({up.Width:0.#}x{up.Height:0.#})";
                cleanLabel = WebUtility.HtmlEncode(cleanLabel);
                if (cleanLabel.Length > 20) cleanLabel = cleanLabel.Substring(0, 18) + "...";

                string sCx = cx.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                string sCy = cy.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

                sb.AppendLine($"    <text x=\"{sCx}\" y=\"{(cy - 6).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI, sans-serif\" font-size=\"20\" font-weight=\"bold\" fill=\"#111827\" text-anchor=\"middle\">{cleanLabel}</text>");
                sb.AppendLine($"    <text x=\"{sCx}\" y=\"{(cy + 16).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI, sans-serif\" font-size=\"17\" font-weight=\"600\" fill=\"#374151\" text-anchor=\"middle\">{up.Width:0.#} x {up.Height:0.#} mm</text>");

                if (up.Rotated == 1 && rw >= 90)
                {
                    sb.AppendLine($"    <text x=\"{sCx}\" y=\"{(cy + 34).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}\" font-family=\"Segoe UI, sans-serif\" font-size=\"14\" font-weight=\"bold\" fill=\"#b45309\" text-anchor=\"middle\">↻ Xoay 90°</text>");
                }
            }
            sb.AppendLine("  </g>");
        }

        // 3. Vẽ nhát cắt Guillotine (Cut lines)
        foreach (var c in sheet.Cuts)
        {
            double cx1 = marginX + c.TopX;
            double cy1 = marginY + c.TopY;
            double cx2 = marginX + c.BottomX;
            double cy2 = marginY + c.BottomY;
            double thick = Math.Max(1.5, c.CutThickness);

            string sCx1 = cx1.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string sCy1 = cy1.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string sCx2 = cx2.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string sCy2 = cy2.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            string sThick = thick.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

            sb.AppendLine($"  <line x1=\"{sCx1}\" y1=\"{sCy1}\" x2=\"{sCx2}\" y2=\"{sCy2}\" stroke=\"#e11d48\" stroke-width=\"{sThick}\" stroke-opacity=\"0.75\" />");
        }

        sb.AppendLine("</svg>");
    }

    private static void AppendWasteSection(StringBuilder sb, List<GroupedSheetLayout> grouped, int minUsefulSize)
    {
        sb.AppendLine("    <section id=\"sec-waste\" class=\"section-card\">");
        sb.AppendLine("      <div class=\"section-title\">");
        sb.AppendLine("        <h2>4. Danh sách các mảnh phế liệu (Waste Pieces List)</h2>");
        sb.AppendLine("        <p class=\"section-desc\">Toàn bộ phần thừa ngoài quy cách cắt thành phẩm trên các tấm, phân loại theo khả năng tái sử dụng kho hoặc vụn cưa.</p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"table-responsive\">");
        sb.AppendLine("        <table class=\"data-table\">");
        sb.AppendLine("          <thead>");
        sb.AppendLine("            <tr>");
        sb.AppendLine("              <th style=\"width:50px;\">#</th>");
        sb.AppendLine("              <th>Thuộc Sơ đồ</th>");
        sb.AppendLine("              <th>Tọa độ (TopX, TopY) &rarr; (BottomX, BottomY)</th>");
        sb.AppendLine("              <th>Kích thước Dài x Rộng (mm)</th>");
        sb.AppendLine("              <th>Diện tích 1 tấm (m²)</th>");
        sb.AppendLine("              <th>Tổng DT cả lô (m²)</th>");
        sb.AppendLine("              <th>Phân loại</th>");
        sb.AppendLine("            </tr>");
        sb.AppendLine("          </thead>");
        sb.AppendLine("          <tbody>");

        int globalWasteIdx = 0;
        double sumWasteArea = 0;
        double sumReusableArea = 0;

        for (int g = 0; g < grouped.Count; g++)
        {
            var grp = grouped[g];
            var sheet = grp.RepresentativeSheet;
            int repCount = grp.Count;

            string wasteRepoLabel = grp.RepositoryLabel?.Trim() ?? "";
            if (string.IsNullOrEmpty(wasteRepoLabel)) wasteRepoLabel = sheet.RepositoryLabel?.Trim() ?? "";
            if (string.IsNullOrEmpty(wasteRepoLabel)) wasteRepoLabel = $"Tấm kho #{sheet.RepositoryIndex + 1}";

            for (int w = 0; w < sheet.WastePieces.Count; w++)
            {
                var wp = sheet.WastePieces[w];
                globalWasteIdx++;

                double area1 = wp.Area / 1_000_000.0;
                double areaBatch = area1 * repCount;
                sumWasteArea += areaBatch;

                bool isReusable = (wp.Width >= minUsefulSize && wp.Height >= minUsefulSize);
                if (isReusable) sumReusableArea += areaBatch;

                string badgeClass = isReusable ? "badge-success" : "badge-secondary";
                string classText = isReusable ? "♻ Tái sử dụng" : "Vụn cưa";

                sb.AppendLine("            <tr>");
                sb.AppendLine($"              <td class=\"text-center\">{globalWasteIdx}</td>");
                sb.AppendLine($"              <td><strong>Sơ đồ #{g + 1}</strong> <span class=\"badge-repo-small\">[{WebUtility.HtmlEncode(wasteRepoLabel)}]</span> (x{repCount} tấm)</td>");
                sb.AppendLine($"              <td class=\"text-center font-mono\">({wp.TopX}, {wp.TopY}) &rarr; ({wp.BottomX}, {wp.BottomY})</td>");
                sb.AppendLine($"              <td class=\"text-center\"><strong>{wp.Width} x {wp.Height}</strong></td>");
                sb.AppendLine($"              <td class=\"text-right\">{area1:F4}</td>");
                sb.AppendLine($"              <td class=\"text-right\"><strong>{areaBatch:F4}</strong></td>");
                sb.AppendLine($"              <td class=\"text-center\"><span class=\"badge {badgeClass}\">{classText}</span></td>");
                sb.AppendLine("            </tr>");
            }
        }

        sb.AppendLine("          </tbody>");
        sb.AppendLine("          <tfoot>");
        sb.AppendLine("            <tr class=\"total-row\">");
        sb.AppendLine("              <td colspan=\"5\" class=\"text-left\"><strong>TỔNG DIỆN TÍCH PHẾ LIỆU TOÀN BỘ ĐƠN HÀNG</strong></td>");
        sb.AppendLine($"              <td class=\"text-right\"><strong class=\"text-danger\">{sumWasteArea:F3} m²</strong></td>");
        sb.AppendLine($"              <td class=\"text-center\"><strong>Tái sử dụng: {sumReusableArea:F3} m²</strong></td>");
        sb.AppendLine("            </tr>");
        sb.AppendLine("          </tfoot>");
        sb.AppendLine("        </table>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </section>");
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.AppendLine(@"
    :root {
      --primary: #0284c7;
      --primary-dark: #0369a1;
      --success: #16a34a;
      --warning: #d97706;
      --danger: #dc2626;
      --bg: #f8fafc;
      --card-bg: #ffffff;
      --text: #1e293b;
      --text-muted: #64748b;
      --border: #e2e8f0;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      background-color: var(--bg);
      color: var(--text);
      line-height: 1.5;
      padding-bottom: 60px;
    }
    .top-bar {
      position: sticky;
      top: 0;
      z-index: 1000;
      background: #0f172a;
      color: #fff;
      box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    }
    .top-bar-inner {
      max-width: 1240px;
      margin: 0 auto;
      padding: 12px 20px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 12px;
    }
    .brand { font-size: 1.1rem; }
    .nav-links a {
      color: #94a3b8;
      text-decoration: none;
      margin-right: 18px;
      font-size: 0.9rem;
      font-weight: 500;
      transition: color 0.2s;
    }
    .nav-links a:hover { color: #38bdf8; }
    .btn-print {
      background: #2563eb;
      color: #fff;
      border: none;
      padding: 8px 18px;
      border-radius: 6px;
      font-weight: 600;
      font-size: 0.9rem;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn-print:hover { background: #1d4ed8; }
    .container {
      max-width: 1240px;
      margin: 25px auto;
      padding: 0 20px;
    }
    .report-header {
      background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);
      color: #fff;
      padding: 30px;
      border-radius: 12px;
      margin-bottom: 25px;
      box-shadow: 0 4px 16px rgba(15,23,42,0.1);
    }
    .report-header h1 {
      font-size: 1.85rem;
      margin-bottom: 6px;
      letter-spacing: -0.5px;
    }
    .subtitle { color: #94a3b8; font-size: 0.95rem; margin-bottom: 16px; }
    .report-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 20px;
      font-size: 0.88rem;
      color: #cbd5e1;
      border-top: 1px solid rgba(255,255,255,0.1);
      padding-top: 14px;
    }
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
      gap: 16px;
      margin-bottom: 25px;
    }
    .kpi-card {
      background: var(--card-bg);
      border-radius: 10px;
      padding: 20px;
      border: 1px solid var(--border);
      box-shadow: 0 2px 6px rgba(0,0,0,0.04);
      position: relative;
      overflow: hidden;
    }
    .kpi-card::before {
      content: '';
      position: absolute;
      top: 0; left: 0; bottom: 0;
      width: 5px;
    }
    .highlight-blue::before { background: var(--primary); }
    .highlight-green::before { background: var(--success); }
    .highlight-purple::before { background: #8b5cf6; }
    .highlight-orange::before { background: var(--warning); }
    .kpi-val { font-size: 1.7rem; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .kpi-label { font-size: 0.85rem; color: var(--text-muted); font-weight: 500; }

    .section-card {
      background: var(--card-bg);
      border-radius: 10px;
      padding: 24px;
      border: 1px solid var(--border);
      box-shadow: 0 2px 8px rgba(0,0,0,0.04);
      margin-bottom: 25px;
    }
    .section-title { margin-bottom: 18px; border-bottom: 2px solid #f1f5f9; padding-bottom: 12px; }
    .section-title h2 { font-size: 1.25rem; color: #0f172a; }
    .section-desc { font-size: 0.88rem; color: var(--text-muted); margin-top: 4px; }

    .table-responsive { overflow-x: auto; }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.9rem;
    }
    .data-table th, .data-table td {
      padding: 10px 14px;
      border-bottom: 1px solid #f1f5f9;
    }
    .data-table th {
      background: #f8fafc;
      color: #475569;
      font-weight: 600;
      text-align: left;
    }
    .data-table tr:hover { background: #f8fafc; }
    .total-row { background: #f1f5f9; font-weight: 700; border-top: 2px solid var(--border); }

    .mini-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.84rem;
      margin-top: 12px;
    }
    .mini-table th, .mini-table td {
      padding: 7px 10px;
      border-bottom: 1px solid #f1f5f9;
    }
    .mini-table th { background: #f8fafc; color: #64748b; font-weight: 600; }

    .pattern-card {
      background: #ffffff;
      border: 1px solid var(--border);
      border-radius: 10px;
      padding: 20px;
      margin-bottom: 25px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.04);
      page-break-inside: avoid;
    }
    .pattern-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 12px;
      margin-bottom: 16px;
      border-bottom: 1px solid #f1f5f9;
      padding-bottom: 12px;
    }
    .pattern-title-area { display: flex; align-items: center; gap: 12px; }
    .pattern-num { font-size: 1.2rem; font-weight: 800; color: #0f172a; }
    .badge-repo-label {
      background: #fef3c7;
      color: #78350f;
      border: 1px solid #fde68a;
      padding: 4px 10px;
      border-radius: 6px;
      font-weight: 700;
      font-size: 0.88rem;
    }
    .badge-repo-small {
      display: inline-block;
      background: #fef3c7;
      color: #92400e;
      border: 1px solid #fde68a;
      border-radius: 4px;
      padding: 1px 6px;
      font-size: 0.78rem;
      font-weight: 600;
      margin-left: 4px;
    }
    .stamp-batch {
      background: #15803d;
      color: #fff;
      padding: 4px 12px;
      border-radius: 20px;
      font-size: 0.88rem;
      font-weight: 700;
      box-shadow: 0 2px 4px rgba(21,128,61,0.25);
    }
    .pattern-badges { display: flex; flex-wrap: wrap; gap: 8px; font-size: 0.82rem; }
    .badge-spec, .badge-util, .badge-parts, .badge-cuts {
      padding: 4px 10px;
      border-radius: 6px;
      font-weight: 600;
    }
    .badge-spec { background: #f1f5f9; color: #334155; }
    .badge-util { background: #e0f2fe; color: #0369a1; }
    .badge-parts { background: #dcfce7; color: #15803d; }
    .badge-cuts { background: #fef3c7; color: #b45309; }

    .svg-container {
      background: #f8fafc;
      padding: 16px;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      margin-bottom: 16px;
      display: flex;
      justify-content: center;
    }
    .cutting-svg {
      width: 100%;
      max-width: 1080px;
      height: auto;
      filter: drop-shadow(0 2px 4px rgba(0,0,0,0.05));
    }
    .pattern-parts-summary h4 {
      font-size: 0.95rem;
      color: #334155;
      margin-bottom: 6px;
    }

    .badge {
      display: inline-block;
      padding: 3px 8px;
      border-radius: 4px;
      font-size: 0.78rem;
      font-weight: 700;
    }
    .badge-success { background: #dcfce7; color: #15803d; }
    .badge-warning { background: #fef3c7; color: #b45309; }
    .badge-info { background: #e0f2fe; color: #0369a1; }
    .badge-secondary { background: #f1f5f9; color: #64748b; }
    .color-box {
      display: inline-block;
      width: 12px;
      height: 12px;
      border-radius: 2px;
      margin-right: 6px;
      vertical-align: middle;
      border: 1px solid rgba(0,0,0,0.2);
    }
    .text-center { text-align: center; }
    .text-right { text-align: right; }
    .text-left { text-align: left; }
    .text-primary { color: var(--primary); }
    .text-success { color: var(--success); }
    .text-danger { color: var(--danger); }
    .font-mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }

    .report-footer {
      text-align: center;
      font-size: 0.85rem;
      color: var(--text-muted);
      margin-top: 40px;
      border-top: 1px solid var(--border);
      padding-top: 20px;
    }

    @media print {
      body { background: #fff; padding-bottom: 0; }
      .no-print { display: none !important; }
      .container { max-width: 100%; margin: 0; padding: 0; }
      .report-header { box-shadow: none; border-radius: 0; padding: 20px; }
      .section-card { box-shadow: none; border-radius: 0; border: 1px solid #ccc; margin-bottom: 20px; page-break-inside: avoid; }
      .pattern-card { box-shadow: none; border-radius: 0; border: 1px solid #999; page-break-after: always; }
      .svg-container { background: #fff; padding: 0; border: none; }
    }
");
    }
}
