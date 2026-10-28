using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MiniExcelLibs;
using Optimal2DX.Web.Models;

namespace Optimal2DX.Web.Services;

public class ExcelService
{
    public byte[] GenerateTemplateExcel()
    {
        var demandRows = new List<Dictionary<string, object>>
        {
            new() { { "STT", 1 }, { "Chiều dài (mm)", 1568 }, { "Chiều rộng (mm)", 953 },  { "Số lượng (SL)", 45 },  { "Tên / Nhãn chi tiết", "Cánh tủ trên" },    { "Thớ gỗ", "≡ Dọc (Theo Dài)" }, { "Vật liệu", "MDF" }, { "Được xoay", "Không" } },
            new() { { "STT", 2 }, { "Chiều dài (mm)", 754 },  { "Chiều rộng (mm)", 1008 }, { "Số lượng (SL)", 45 },  { "Tên / Nhãn chi tiết", "Hông tủ trái" },    { "Thớ gỗ", "≡ Dọc (Theo Dài)" }, { "Vật liệu", "MDF" }, { "Được xoay", "Không" } },
            new() { { "STT", 3 }, { "Chiều dài (mm)", 1284 }, { "Chiều rộng (mm)", 959 },  { "Số lượng (SL)", 36 },  { "Tên / Nhãn chi tiết", "Hông tủ phải" },    { "Thớ gỗ", "≡ Dọc (Theo Dài)" }, { "Vật liệu", "MDF" }, { "Được xoay", "Không" } },
            new() { { "STT", 4 }, { "Chiều dài (mm)", 618 },  { "Chiều rộng (mm)", 642 },  { "Số lượng (SL)", 96 },  { "Tên / Nhãn chi tiết", "Đáy tủ" },          { "Thớ gỗ", "≡ Dọc (Theo Dài)" }, { "Vật liệu", "MDF" }, { "Được xoay", "Không" } },
            new() { { "STT", 5 }, { "Chiều dài (mm)", 482 },  { "Chiều rộng (mm)", 340 },  { "Số lượng (SL)", 288 }, { "Tên / Nhãn chi tiết", "Ngăn kéo lớn" },    { "Thớ gỗ", "≡ Dọc (Theo Dài)" }, { "Vật liệu", "MDF" }, { "Được xoay", "Không" } },
            new() { { "STT", 6 }, { "Chiều dài (mm)", 779 },  { "Chiều rộng (mm)", 340 },  { "Số lượng (SL)", 180 }, { "Tên / Nhãn chi tiết", "Đợt ngăn di động" }, { "Thớ gỗ", "≡ Dọc (Theo Dài)" }, { "Vật liệu", "MDF" }, { "Được xoay", "Không" } },
            new() { { "STT", 7 }, { "Chiều dài (mm)", 663 },  { "Chiều rộng (mm)", 340 },  { "Số lượng (SL)", 270 }, { "Tên / Nhãn chi tiết", "Thành ngăn kéo" },  { "Thớ gỗ", "≡ Dọc (Theo Dài)" }, { "Vật liệu", "MDF" }, { "Được xoay", "Không" } },
            new() { { "STT", 8 }, { "Chiều dài (mm)", 650 },  { "Chiều rộng (mm)", 340 },  { "Số lượng (SL)", 90 },  { "Tên / Nhãn chi tiết", "Hậu tủ nhỏ" },      { "Thớ gỗ", "≡ Dọc (Theo Dài)" }, { "Vật liệu", "MDF" }, { "Được xoay", "Không" } }
        };

        var stockRows = new List<Dictionary<string, object>>
        {
            new() {
                { "STT", 1 },
                { "Chiều dài (mm)", 2440 },
                { "Chiều rộng (mm)", 1220 },
                { "Số lượng tồn kho", 163 },
                { "Tên tấm ván kho", "Ván MDF 2440x1220" },
                { "Thớ gỗ tấm", "≡ Dọc (Theo Dài)" },
                { "Vật liệu", "MDF" },
                { "Trim Trên (mm)", 2 },
                { "Trim Trái (mm)", 2 },
                { "Trim Dưới (mm)", 0 },
                { "Trim Phải (mm)", 0 },
                { "Giá tấm", 0 }
            }
        };

        var sheets = new Dictionary<string, object>
        {
            ["ChiTiet_CanCat"] = demandRows,
            ["TamVatLieu_Kho"] = stockRows
        };

        using var ms = new MemoryStream();
        ms.SaveAs(sheets);
        return ms.ToArray();
    }

    public ExcelImportResultDto ImportExcel(Stream stream)
    {
        var result = new ExcelImportResultDto();

        try
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);

            ms.Position = 0;
            var sheetNames = ms.GetSheetNames().ToList();

            string? demandSheet = sheetNames.FirstOrDefault(s =>
                s.Contains("Demand", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("ChiTiet", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Chi Tiet", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Phôi", StringComparison.OrdinalIgnoreCase));

            string? stockSheet = sheetNames.FirstOrDefault(s =>
                s.Contains("Stock", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Kho", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Tam", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("VatLieu", StringComparison.OrdinalIgnoreCase));

            if (demandSheet != null)
            {
                ms.Position = 0;
                var rows = ms.Query(useHeaderRow: true, sheetName: demandSheet).Cast<IDictionary<string, object>>().ToList();
                result.DemandPieces = ParseDemandRows(rows);
            }

            if (stockSheet != null)
            {
                ms.Position = 0;
                var rows = ms.Query(useHeaderRow: true, sheetName: stockSheet).Cast<IDictionary<string, object>>().ToList();
                result.RepoPieces = ParseRepoRows(rows);
            }

            // Fallback if sheets were not named explicitly: check first sheet
            if (result.DemandPieces.Count == 0 && result.RepoPieces.Count == 0 && sheetNames.Count > 0)
            {
                ms.Position = 0;
                var firstRows = ms.Query(useHeaderRow: true, sheetName: sheetNames[0]).Cast<IDictionary<string, object>>().ToList();
                if (firstRows.Count > 0)
                {
                    bool looksLikeStock = firstRows[0].Keys.Any(k =>
                        k.Contains("Trim", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("Tồn kho", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("Stock", StringComparison.OrdinalIgnoreCase));

                    if (looksLikeStock)
                    {
                        result.RepoPieces = ParseRepoRows(firstRows);
                    }
                    else
                    {
                        result.DemandPieces = ParseDemandRows(firstRows);
                    }
                }

                // If second sheet exists, parse it as the other
                if (sheetNames.Count > 1)
                {
                    ms.Position = 0;
                    var secondRows = ms.Query(useHeaderRow: true, sheetName: sheetNames[1]).Cast<IDictionary<string, object>>().ToList();
                    if (result.DemandPieces.Count > 0 && result.RepoPieces.Count == 0)
                    {
                        result.RepoPieces = ParseRepoRows(secondRows);
                    }
                    else if (result.RepoPieces.Count > 0 && result.DemandPieces.Count == 0)
                    {
                        result.DemandPieces = ParseDemandRows(secondRows);
                    }
                }
            }

            result.Success = true;
            result.Message = $"Đã nhập thành công {result.DemandPieces.Count} chi tiết cần cắt và {result.RepoPieces.Count} tấm vật liệu kho từ file Excel.";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = "Lỗi khi đọc file Excel: " + ex.Message;
        }

        return result;
    }

    public List<DemandInputDto> ParseDemandRows(IEnumerable<IDictionary<string, object>> rows)
    {
        var list = new List<DemandInputDto>();
        int idx = 1;

        foreach (var row in rows)
        {
            int len = 0, wid = 0, qty = 1, pri = 0, extId = idx;
            string label = "", mat = "", grain = "≡ Dọc (Theo Dài)";
            bool canRot = false;

            foreach (var kv in row)
            {
                string key = CleanKey(kv.Key);
                string val = kv.Value?.ToString()?.Trim() ?? "";

                if (MatchKey(key, "dai", "chieudai", "length", "l", "dai(mm)"))
                {
                    len = ParseInt(val);
                }
                else if (MatchKey(key, "rong", "chieurong", "width", "w", "rong(mm)"))
                {
                    wid = ParseInt(val);
                }
                else if (MatchKey(key, "soluong", "sl", "quantity", "qty", "count", "soluong(sl)"))
                {
                    qty = ParseInt(val, 1);
                }
                else if (MatchKey(key, "nhan", "ten", "name", "label", "tenchitiet", "description", "mota"))
                {
                    label = val;
                }
                else if (MatchKey(key, "vatlieu", "material", "chatlieu"))
                {
                    mat = val;
                }
                else if (MatchKey(key, "thogo", "grain", "thogochitiet", "graindirection"))
                {
                    grain = NormalizeGrain(val);
                    if (grain.Contains("Tự do")) canRot = true;
                }
                else if (MatchKey(key, "xoay", "canrotate", "rotate", "duocxoay"))
                {
                    canRot = val.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                             val.Equals("1") ||
                             val.Equals("co", StringComparison.OrdinalIgnoreCase) ||
                             val.Equals("có", StringComparison.OrdinalIgnoreCase) ||
                             val.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    if (canRot) grain = "🔄 Tự do (Không thớ)";
                }
                else if (MatchKey(key, "extid", "id", "externalid"))
                {
                    extId = ParseInt(val, idx);
                }
                else if (MatchKey(key, "priority", "uutien", "mucdouutien"))
                {
                    pri = ParseInt(val, 0);
                }
            }

            if (len > 0 && wid > 0)
            {
                if (string.IsNullOrEmpty(label)) label = $"Chi tiết #{idx} ({len}x{wid})";
                list.Add(new DemandInputDto
                {
                    Length = len,
                    Width = wid,
                    Quantity = qty > 0 ? qty : 1,
                    Label = label,
                    Material = mat,
                    Grain = grain,
                    CanRotate = canRot,
                    ExternalID = extId,
                    Priority = pri
                });
                idx++;
            }
        }

        return list;
    }

    public List<RepoInputDto> ParseRepoRows(IEnumerable<IDictionary<string, object>> rows)
    {
        var list = new List<RepoInputDto>();
        int idx = 1;

        foreach (var row in rows)
        {
            int len = 0, wid = 0, qty = 1, extId = idx, pri = 0;
            int tTop = 0, tLeft = 0, tBot = 0, tRight = 0;
            decimal price = 0;
            string label = "", mat = "", grain = "≡ Dọc (Theo Dài)";

            foreach (var kv in row)
            {
                string key = CleanKey(kv.Key);
                string val = kv.Value?.ToString()?.Trim() ?? "";

                if (MatchKey(key, "dai", "chieudai", "length", "l", "dai(mm)"))
                {
                    len = ParseInt(val);
                }
                else if (MatchKey(key, "rong", "chieurong", "width", "w", "rong(mm)"))
                {
                    wid = ParseInt(val);
                }
                else if (MatchKey(key, "soluong", "sl", "quantity", "qty", "count", "soluongtonkho", "soluong(sl)"))
                {
                    qty = ParseInt(val, 1);
                }
                else if (MatchKey(key, "nhan", "ten", "name", "label", "tentam", "tentamvankho", "description"))
                {
                    label = val;
                }
                else if (MatchKey(key, "vatlieu", "material", "chatlieu"))
                {
                    mat = val;
                }
                else if (MatchKey(key, "thogo", "grain", "thogotam", "graindirection"))
                {
                    grain = NormalizeSheetGrain(val);
                }
                else if (MatchKey(key, "trimtop", "trimtren", "tren", "top"))
                {
                    tTop = ParseInt(val, 0);
                }
                else if (MatchKey(key, "trimleft", "trimtrai", "trai", "left"))
                {
                    tLeft = ParseInt(val, 0);
                }
                else if (MatchKey(key, "trimbottom", "trimduoi", "duoi", "bottom"))
                {
                    tBot = ParseInt(val, 0);
                }
                else if (MatchKey(key, "trimright", "trimphai", "phai", "right"))
                {
                    tRight = ParseInt(val, 0);
                }
                else if (MatchKey(key, "price", "gia", "giatam", "cost"))
                {
                    if (decimal.TryParse(val, out var p)) price = p;
                }
                else if (MatchKey(key, "extid", "id", "externalid"))
                {
                    extId = ParseInt(val, idx);
                }
                else if (MatchKey(key, "priority", "uutien", "mucdouutien"))
                {
                    pri = ParseInt(val, 0);
                }
            }

            if (len > 0 && wid > 0)
            {
                if (string.IsNullOrEmpty(label)) label = $"Tấm kho #{idx} ({len}x{wid})";
                list.Add(new RepoInputDto
                {
                    Length = len,
                    Width = wid,
                    Quantity = qty > 0 ? qty : 1,
                    Label = label,
                    Material = mat,
                    SheetGrain = grain,
                    TrimTop = tTop,
                    TrimLeft = tLeft,
                    TrimBottom = tBot,
                    TrimRight = tRight,
                    Price = price,
                    ExternalID = extId,
                    Priority = pri
                });
                idx++;
            }
        }

        return list;
    }

    public byte[] ExportResultsToExcel(OptimizationResultDto result)
    {
        // 1. Summary sheet
        var summaryRows = new List<Dictionary<string, object>>
        {
            new() { { "Chỉ số KPI", "Tổng số chi tiết cần cắt (yêu cầu)" }, { "Giá trị", result.TotalDemandPiecesRequested } },
            new() { { "Chỉ số KPI", "Số chi tiết đã xếp lên sơ đồ" }, { "Giá trị", result.TotalDemandPiecesPlaced } },
            new() { { "Chỉ số KPI", "Số chi tiết chưa xếp được" }, { "Giá trị", result.UnplacedPiecesCount } },
            new() { { "Chỉ số KPI", "Tổng số tấm vật liệu kho khai báo" }, { "Giá trị", result.TotalRepoSheetsStock } },
            new() { { "Chỉ số KPI", "Số tấm vật liệu thực tế sử dụng" }, { "Giá trị", result.TotalRepoSheetsUsed } },
            new() { { "Chỉ số KPI", "Số mẫu sơ đồ cắt gom nhóm (Patterns)" }, { "Giá trị", result.TotalUniquePatterns } },
            new() { { "Chỉ số KPI", "Tỷ lệ sử dụng bề mặt vật liệu (%)" }, { "Giá trị", Math.Round(result.OverallUtilizationPercent, 2) } },
            new() { { "Chỉ số KPI", "Tổng diện tích phôi hữu ích (m²)" }, { "Giá trị", Math.Round(result.TotalUsefulAreaM2, 3) } },
            new() { { "Chỉ số KPI", "Tổng diện tích phế liệu (m²)" }, { "Giá trị", Math.Round(result.TotalWasteAreaM2, 3) } },
            new() { { "Chỉ số KPI", "Tổng diện tích tấm ván đã dùng (m²)" }, { "Giá trị", Math.Round(result.TotalGrossAreaM2, 3) } },
            new() { { "Chỉ số KPI", "Tổng chiều dài mạch cưa (mm)" }, { "Giá trị", Math.Round(result.TotalCutLengthMm, 1) } },
            new() { { "Chỉ số KPI", "Thời gian tính toán tối ưu (ms)" }, { "Giá trị", result.ExecutionTimeMs } }
        };

        // 2. Patterns sheet
        var patternRows = new List<Dictionary<string, object>>();
        foreach (var p in result.Patterns)
        {
            patternRows.Add(new Dictionary<string, object>
            {
                { "Sơ đồ #", p.PatternIndex + 1 },
                { "Tên tấm ván kho", p.RepositoryLabel },
                { "Kích thước tấm (DxR mm)", $"{p.SheetLength} x {p.SheetWidth}" },
                { "Số lượng tấm cắt (SL)", p.SheetCount },
                { "Số chi tiết / tấm", p.PiecesPerSheet },
                { "Tổng chi tiết tạo ra", p.TotalPiecesInPattern },
                { "Tỷ lệ độ phủ (%)", Math.Round(p.SurfaceCoveredPercent, 2) },
                { "Chiều dài mạch cắt (mm)", Math.Round(p.TotalCutLength, 1) },
                { "Diện tích phế liệu / tấm", Math.Round(p.WasteSurface, 0) }
            });
        }

        // 3. Utilized Pieces sheet
        var pieceRows = new List<Dictionary<string, object>>();
        foreach (var pat in result.Patterns)
        {
            foreach (var up in pat.UtilizedPieces)
            {
                pieceRows.Add(new Dictionary<string, object>
                {
                    { "Mẫu sơ đồ", $"Sơ đồ #{pat.PatternIndex + 1}" },
                    { "Số lượng tấm cắt", pat.SheetCount },
                    { "Chi tiết #", up.Index },
                    { "Tên / Nhãn chi tiết", up.Label },
                    { "Kích thước (DxR mm)", $"{up.Width:0.#} x {up.Height:0.#}" },
                    { "Tọa độ X1", up.TopX },
                    { "Tọa độ Y1", up.TopY },
                    { "Tọa độ X2", up.BottomX },
                    { "Tọa độ Y2", up.BottomY },
                    { "Xoay 90°", up.Rotated == 1 ? "Có" : "Không" },
                    { "Thớ gỗ", up.GrainLabel },
                    { "Vật liệu", up.Material }
                });
            }
        }

        // 4. Waste pieces sheet
        var wasteRows = new List<Dictionary<string, object>>();
        foreach (var pat in result.Patterns)
        {
            foreach (var w in pat.WastePieces)
            {
                wasteRows.Add(new Dictionary<string, object>
                {
                    { "Mẫu sơ đồ", $"Sơ đồ #{pat.PatternIndex + 1}" },
                    { "Số lượng tấm", pat.SheetCount },
                    { "Mảnh phế liệu #", w.Index },
                    { "Kích thước (DxR mm)", $"{w.Width:0.#} x {w.Height:0.#}" },
                    { "Diện tích 1 tấm (mm²)", Math.Round(w.Area, 2) },
                    { "Tổng diện tích (mm²)", Math.Round(w.Area * pat.SheetCount, 2) },
                    { "Phân loại", w.Classification },
                    { "Thớ gỗ tái sử dụng", w.GrainAvailable },
                    { "Tọa độ X1", w.TopX },
                    { "Tọa độ Y1", w.TopY },
                    { "Tọa độ X2", w.BottomX },
                    { "Tọa độ Y2", w.BottomY }
                });
            }
        }

        var workbook = new Dictionary<string, object>
        {
            ["1_TongQuanKPI"] = summaryRows,
            ["2_MauSoDo"] = patternRows,
            ["3_ChiTietCat"] = pieceRows,
            ["4_PheLieu"] = wasteRows
        };

        using var ms = new MemoryStream();
        ms.SaveAs(workbook);
        return ms.ToArray();
    }

    private static string CleanKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        string noDiacritics = RemoveDiacritics(key);
        string clean = Regex.Replace(noDiacritics, @"\s+", "").ToLowerInvariant();
        clean = clean.Replace("_", "").Replace("-", "").Replace(".", "").Replace("(", "").Replace(")", "").Replace("/", "").Replace("\\", "");
        return clean;
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).Replace("đ", "d").Replace("Đ", "D");
    }

    private static bool MatchKey(string key, params string[] targets)
    {
        foreach (var t in targets)
        {
            if (t.Length <= 1)
            {
                if (key.Equals(t, StringComparison.OrdinalIgnoreCase)) return true;
            }
            else
            {
                if (key.Equals(t, StringComparison.OrdinalIgnoreCase) ||
                    key.Contains(t, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static int ParseInt(string val, int defaultVal = 0)
    {
        if (string.IsNullOrWhiteSpace(val)) return defaultVal;
        // Strip commas or periods if thousands separator
        string clean = val.Trim().Replace(" ", "").Replace(",", "");
        if (int.TryParse(clean, out int res)) return res;
        if (double.TryParse(clean, out double dRes)) return (int)Math.Round(dRes);
        return defaultVal;
    }

    private static string NormalizeGrain(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return "≡ Dọc (Theo Dài)";
        string lower = val.ToLowerInvariant();
        if (lower.Contains("ngang") || lower.Contains("width") || lower.Contains("|||"))
            return "||| Ngang (Theo Rộng)";
        if (lower.Contains("tự do") || lower.Contains("tu do") || lower.Contains("none") || lower.Contains("không thớ") || lower.Contains("khong tho") || lower.Contains("🔄"))
            return "🔄 Tự do (Không thớ)";
        if (lower.Contains("cố định") || lower.Contains("co dinh") || lower.Contains("fixed") || lower.Contains("khóa") || lower.Contains("🔒"))
            return "🔒 Cố định (Không xoay)";
        return "≡ Dọc (Theo Dài)";
    }

    private static string NormalizeSheetGrain(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return "≡ Dọc (Theo Dài)";
        string lower = val.ToLowerInvariant();
        if (lower.Contains("ngang") || lower.Contains("width") || lower.Contains("|||"))
            return "||| Ngang (Theo Rộng)";
        if (lower.Contains("không thớ") || lower.Contains("khong tho") || lower.Contains("none") || lower.Contains("tự do") || lower.Contains("🔄"))
            return "🔄 Không thớ";
        return "≡ Dọc (Theo Dài)";
    }
}
