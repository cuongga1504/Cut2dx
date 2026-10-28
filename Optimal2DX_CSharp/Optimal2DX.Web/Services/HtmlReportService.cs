using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Optimal2DX.Web.Models;

namespace Optimal2DX.Web.Services;

public class HtmlReportService
{
    public string GenerateHtmlReport(OptimizationRequestDto req, OptimizationResultDto result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"vi\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine("  <title>Báo Cáo Sơ Đồ Cắt 2D - Cut2DX Web</title>");
        sb.AppendLine("  <style>");
        AppendStyles(sb);
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Top Navigation Bar
        sb.AppendLine("  <div class=\"top-bar no-print\">");
        sb.AppendLine("    <div class=\"top-bar-inner\">");
        sb.AppendLine("      <div class=\"brand\">🪵 <strong>Cut2DX Web</strong> • Báo Cáo Sơ Đồ Cắt 2D Chuyên Nghiệp</div>");
        sb.AppendLine("      <div class=\"nav-links\">");
        sb.AppendLine("        <a href=\"#sec-kpi\">Tổng quan KPI</a>");
        sb.AppendLine("        <a href=\"#sec-demand\">1. Chi tiết cần cắt</a>");
        sb.AppendLine("        <a href=\"#sec-repo\">2. Tấm vật liệu kho</a>");
        sb.AppendLine("        <a href=\"#sec-patterns\">3. Sơ đồ cắt chi tiết</a>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"actions\">");
        sb.AppendLine("        <button class=\"btn-print\" onclick=\"window.print()\">🖨 In báo cáo / Lưu PDF</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <div class=\"container\">");

        // Header
        sb.AppendLine("    <header class=\"report-header\">");
        sb.AppendLine("      <div class=\"header-main\">");
        sb.AppendLine("        <h1>BÁO CÁO TỐI ƯU HÓA SƠ ĐỒ CẮT 2D</h1>");
        sb.AppendLine($"        <p class=\"subtitle\">Hệ thống tự động gom các sơ đồ cắt giống hệt nhau (Pattern Grouping) • Xuất bản lúc: {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"header-badges\">");
        sb.AppendLine($"        <span class=\"badge-stat\">⏱ Thời gian tính: <strong>{result.ExecutionTimeMs} ms</strong></span>");
        sb.AppendLine($"        <span class=\"badge-stat\">⚡ Mạch cưa: <strong>{req.BladeWidth:0.#} mm</strong></span>");
        sb.AppendLine($"        <span class=\"badge-stat\">🎯 Cấp độ tối ưu: <strong>{req.OptimizationLevel}/100</strong></span>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </header>");

        // 1. KPI Cards
        sb.AppendLine("    <section id=\"sec-kpi\" class=\"kpi-grid\">");
        sb.AppendLine($"      <div class=\"kpi-card highlight\"><div class=\"kpi-title\">TỶ LỆ SỬ DỤNG VẬT LIỆU</div><div class=\"kpi-val\">{result.OverallUtilizationPercent:F1}%</div><div class=\"kpi-sub\">Độ phủ bề mặt trung bình</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">SỐ MẪU SƠ ĐỒ (PATTERNS)</div><div class=\"kpi-val text-primary\">{result.TotalUniquePatterns}</div><div class=\"kpi-sub\">Đã gom nhóm trùng lặp</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">TẤM VẬT LIỆU SỬ DỤNG</div><div class=\"kpi-val text-success\">{result.TotalRepoSheetsUsed} / {result.TotalRepoSheetsStock}</div><div class=\"kpi-sub\">Tấm thực dùng / Tổng tồn kho</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">CHI TIẾT ĐÃ XẾP ĐỦ</div><div class=\"kpi-val text-success\">{result.TotalDemandPiecesPlaced} / {result.TotalDemandPiecesRequested}</div><div class=\"kpi-sub\">Tỷ lệ hoàn thành: {(result.TotalDemandPiecesRequested > 0 ? (double)result.TotalDemandPiecesPlaced / result.TotalDemandPiecesRequested * 100 : 0):F1}%</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">DIỆN TÍCH HỮU ÍCH</div><div class=\"kpi-val text-purple\">{result.TotalUsefulAreaM2:F2} m²</div><div class=\"kpi-sub\">Phế liệu: {result.TotalWasteAreaM2:F2} m²</div></div>");
        sb.AppendLine($"      <div class=\"kpi-card\"><div class=\"kpi-title\">TỔNG MẠCH CƯA</div><div class=\"kpi-val text-amber\">{result.TotalCutLengthMm:N0} mm</div><div class=\"kpi-sub\">Tổng chiều dài các nhát cắt</div></div>");
        sb.AppendLine("    </section>");

        // 2. Demand Pieces Table
        sb.AppendLine("    <section id=\"sec-demand\" class=\"section-card\">");
        sb.AppendLine("      <div class=\"section-title\"><h2>1. Danh sách chi tiết cần cắt (Demand Pieces)</h2></div>");
        sb.AppendLine("      <div class=\"table-responsive\"><table class=\"data-table\"><thead><tr><th>#</th><th>Tên / Nhãn chi tiết</th><th>Chiều dài (mm)</th><th>Chiều rộng (mm)</th><th>Số lượng (SL)</th><th>Vật liệu</th><th>Thớ gỗ</th><th>Diện tích (m²)</th></tr></thead><tbody>");
        for (int i = 0; i < req.DemandPieces.Count; i++)
        {
            var d = req.DemandPieces[i];
            double area = (double)d.Length * d.Width * d.Quantity / 1_000_000.0;
            sb.AppendLine($"<tr><td class=\"text-center\">{i + 1}</td><td><strong>{WebUtility.HtmlEncode(d.Label)}</strong></td><td class=\"text-right\">{d.Length:N0}</td><td class=\"text-right\">{d.Width:N0}</td><td class=\"text-right\"><strong>{d.Quantity:N0}</strong></td><td>{WebUtility.HtmlEncode(d.Material)}</td><td>{WebUtility.HtmlEncode(d.Grain)}</td><td class=\"text-right\">{area:F3}</td></tr>");
        }
        sb.AppendLine("</tbody></table></div></section>");

        // 3. Stock Repo Table
        sb.AppendLine("    <section id=\"sec-repo\" class=\"section-card\">");
        sb.AppendLine("      <div class=\"section-title\"><h2>2. Danh sách các tấm vật liệu được sử dụng (Repository Sheets)</h2></div>");
        sb.AppendLine("      <div class=\"table-responsive\"><table class=\"data-table\"><thead><tr><th>#</th><th>Tên tấm kho</th><th>Kích thước (DxR mm)</th><th>Tồn kho (SL)</th><th>Cắt bỏ mép viền (Trim)</th><th>Thớ gỗ</th><th>Vật liệu</th></tr></thead><tbody>");
        for (int i = 0; i < req.RepoPieces.Count; i++)
        {
            var r = req.RepoPieces[i];
            sb.AppendLine($"<tr><td class=\"text-center\">{i + 1}</td><td><strong>{WebUtility.HtmlEncode(r.Label)}</strong></td><td class=\"text-right\">{r.Length:N0} x {r.Width:N0}</td><td class=\"text-right\"><strong>{r.Quantity:N0}</strong></td><td class=\"text-center\">T:{r.TrimTop}, L:{r.TrimLeft}, B:{r.TrimBottom}, R:{r.TrimRight}</td><td>{WebUtility.HtmlEncode(r.SheetGrain)}</td><td>{WebUtility.HtmlEncode(r.Material)}</td></tr>");
        }
        sb.AppendLine("</tbody></table></div></section>");

        // 4. Cutting Patterns & SVG
        sb.AppendLine("    <section id=\"sec-patterns\" class=\"section-card\">");
        sb.AppendLine($"      <div class=\"section-title\"><h2>3. Sơ đồ cắt chi tiết ({result.Patterns.Count} mẫu sơ đồ gom nhóm)</h2><p class=\"section-desc\">Mỗi sơ đồ hiển thị bản vẽ cắt Vector SVG kèm nhãn chi tiết, vân gạch chéo phế liệu và số lượng tấm ván cắt lặp lại.</p></div>");

        foreach (var p in result.Patterns)
        {
            sb.AppendLine("      <div class=\"pattern-card\">");
            sb.AppendLine("        <div class=\"pattern-header\">");
            sb.AppendLine("          <div class=\"pattern-title-area\">");
            sb.AppendLine($"            <span class=\"pattern-num\">SƠ ĐỒ #{p.PatternIndex + 1}</span>");
            sb.AppendLine($"            <span class=\"badge-repo-label\">🪵 <strong>Tấm vật liệu:</strong> {WebUtility.HtmlEncode(p.RepositoryLabel)}</span>");
            sb.AppendLine($"            <span class=\"stamp-batch\">✂ CẮT x{p.SheetCount} TẤM (Lặp {p.SheetCount} lần)</span>");
            sb.AppendLine("          </div>");
            sb.AppendLine("          <div class=\"pattern-badges\">");
            sb.AppendLine($"            <span class=\"badge-spec\">📐 {p.SheetLength} x {p.SheetWidth} mm</span>");
            sb.AppendLine($"            <span class=\"badge-util\">Độ phủ: {p.SurfaceCoveredPercent:F2}%</span>");
            sb.AppendLine($"            <span class=\"badge-parts\">{p.PiecesPerSheet} phôi/tấm (Tổng: {p.TotalPiecesInPattern} phôi)</span>");
            sb.AppendLine($"            <span class=\"badge-cuts\">{p.Cuts.Count} nhát cắt</span>");
            sb.AppendLine("          </div>");
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"svg-container\">");
            sb.AppendLine(p.SvgContent);
            sb.AppendLine("        </div>");

            sb.AppendLine("      </div>");
        }
        sb.AppendLine("    </section>");

        // Footer
        sb.AppendLine("    <footer class=\"report-footer\">");
        sb.AppendLine("      <p>Báo cáo xuất bản tự động bởi <strong>Cut2DX Web</strong> • Công cụ Tối ưu hóa Sơ đồ Cắt 2D Chuyên Nghiệp</p>");
        sb.AppendLine("    </footer>");

        sb.AppendLine("  </div>"); // .container
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
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
      --text: #0f172a;
      --text-muted: #64748b;
      --border: #e2e8f0;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: var(--bg); color: var(--text); line-height: 1.5; }
    .top-bar { position: sticky; top: 0; z-index: 100; background: #ffffff; border-bottom: 1px solid var(--border); box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .top-bar-inner { max-width: 1300px; margin: 0 auto; padding: 12px 20px; display: flex; align-items: center; justify-content: space-between; }
    .brand { font-size: 1.15rem; color: #1e293b; }
    .nav-links a { margin-right: 18px; color: var(--text-muted); text-decoration: none; font-size: 0.9rem; font-weight: 500; }
    .nav-links a:hover { color: var(--primary); }
    .btn-print { background: var(--primary); color: #fff; border: none; padding: 8px 16px; border-radius: 6px; font-weight: 600; cursor: pointer; }
    .container { max-width: 1300px; margin: 25px auto; padding: 0 20px; }
    .report-header { background: #fff; padding: 24px; border-radius: 12px; border: 1px solid var(--border); margin-bottom: 24px; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 16px; }
    .report-header h1 { font-size: 1.6rem; color: #0f172a; margin-bottom: 6px; }
    .subtitle { font-size: 0.9rem; color: var(--text-muted); }
    .header-badges { display: flex; gap: 10px; }
    .badge-stat { background: #f1f5f9; padding: 6px 12px; border-radius: 6px; font-size: 0.85rem; color: #334155; }
    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 14px; margin-bottom: 24px; }
    .kpi-card { background: #fff; border-radius: 10px; padding: 18px; border: 1px solid var(--border); border-left: 4px solid var(--primary); }
    .kpi-card.highlight { background: linear-gradient(135deg, #0284c7, #0369a1); color: #fff; border-left: none; }
    .kpi-card.highlight .kpi-title, .kpi-card.highlight .kpi-sub { color: rgba(255,255,255,0.85); }
    .kpi-card.highlight .kpi-val { color: #fff; }
    .kpi-title { font-size: 0.75rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.05em; color: var(--text-muted); margin-bottom: 6px; }
    .kpi-val { font-size: 1.8rem; font-weight: 800; line-height: 1.2; margin-bottom: 4px; }
    .kpi-sub { font-size: 0.8rem; color: var(--text-muted); }
    .text-primary { color: var(--primary); }
    .text-success { color: var(--success); }
    .text-purple { color: #8b5cf6; }
    .text-amber { color: #d97706; }
    .section-card { background: #fff; border-radius: 12px; border: 1px solid var(--border); padding: 22px; margin-bottom: 24px; }
    .section-title h2 { font-size: 1.25rem; color: #0f172a; margin-bottom: 4px; }
    .section-desc { font-size: 0.9rem; color: var(--text-muted); margin-bottom: 16px; }
    .table-responsive { overflow-x: auto; margin-top: 12px; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
    .data-table th, .data-table td { padding: 10px 14px; text-align: left; border-bottom: 1px solid var(--border); }
    .data-table th { background: #f8fafc; font-weight: 600; color: #475569; }
    .text-center { text-align: center; }
    .text-right { text-align: right; }
    .pattern-card { border: 1px solid var(--border); border-radius: 10px; margin-bottom: 24px; overflow: hidden; background: #fff; }
    .pattern-header { background: #f8fafc; padding: 16px 20px; border-bottom: 1px solid var(--border); display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 12px; }
    .pattern-title-area { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .pattern-num { background: #1e293b; color: #fff; font-weight: 700; padding: 5px 12px; border-radius: 6px; font-size: 0.85rem; }
    .badge-repo-label { font-size: 0.95rem; color: #334155; }
    .stamp-batch { background: #dcfce7; color: #15803d; font-weight: 800; padding: 5px 14px; border-radius: 20px; font-size: 0.85rem; border: 1px solid #86efac; }
    .pattern-badges { display: flex; gap: 8px; flex-wrap: wrap; }
    .badge-spec, .badge-util, .badge-parts, .badge-cuts { font-size: 0.8rem; padding: 4px 10px; border-radius: 6px; background: #fff; border: 1px solid #cbd5e1; font-weight: 600; color: #334155; }
    .badge-util { background: #eff6ff; color: #1d4ed8; border-color: #bfdbfe; }
    .svg-container { padding: 20px; display: flex; justify-content: center; background: #fafafa; }
    .cutting-svg-canvas { width: 100%; max-width: 1050px; height: auto; display: block; border-radius: 8px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }
    .report-footer { text-align: center; color: var(--text-muted); font-size: 0.85rem; padding: 30px 0; border-top: 1px solid var(--border); margin-top: 40px; }
    @media print {
      .no-print { display: none !important; }
      body { background: #fff !important; }
      .container { max-width: 100% !important; margin: 0 !important; padding: 0 !important; }
      .pattern-card { page-break-inside: avoid; margin-bottom: 30px; border: 1px solid #000; }
      .section-card { border: none !important; padding: 0 !important; }
    }
        ");
    }
}
