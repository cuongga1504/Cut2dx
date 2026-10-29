using System;
using System.Diagnostics;
using System.Linq;
using Optimal2DX;
using Optimal2DX.Models;

namespace Optimal2DX.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("================================================================================");
            Console.WriteLine("     KIỂM THỬ THUẬT TOÁN TỐI ƯU HÓA SƠ ĐỒ CẮT (PATTERN MINIMIZATION)");
            Console.WriteLine("     Bộ dữ liệu mẫu: 1.050 chi tiết / 163 tấm ván 2440x1220 mm");
            Console.WriteLine("================================================================================\n");

            var opt = new Optimization2DX();

            // 1. Khai báo 163 tấm kho 2440x1220 mm (Trim Top 2, Left 2, Bottom 0, Right 0, Thớ dọc)
            opt.NumberOfRepositoryPieces = 163;
            for (int i = 0; i < 163; i++)
            {
                opt.SetRepositoryPiece(i, 2440, 1220, 2, 2, 0, 0, 1, 0, 0, SheetGrainDirection.AlongLength, "MDF 2440x1220");
            }

            // 2. Khai báo 8 quy cách chi tiết (tổng cộng 1.050 chi tiết)
            int[] lens = { 1568, 754, 1284, 618, 482, 779, 663, 650 };
            int[] wids = { 953, 1008, 959, 642, 340, 340, 340, 340 };
            int[] qtys = { 45, 45, 36, 96, 288, 180, 270, 90 };
            string[] names = {
                "Chi tiết #1 (1568x953)",
                "Chi tiết #2 (754x1008)",
                "Chi tiết #3 (1284x959)",
                "Chi tiết #4 (618x642)",
                "Chi tiết #5 (482x340)",
                "Chi tiết #6 (779x340)",
                "Chi tiết #7 (663x340)",
                "Chi tiết #8 (650x340)"
            };

            int totalDemands = qtys.Sum();
            opt.NumberOfDemandPieces = totalDemands;

            int dIdx = 0;
            for (int i = 0; i < lens.Length; i++)
            {
                for (int k = 0; k < qtys[i]; k++)
                {
                    opt.SetDemandPieceWithGrain(
                        dIdx,
                        lens[i],
                        wids[i],
                        GrainDirection.AlongLength,
                        i + 1,
                        0,
                        SheetGrainDirection.AlongLength,
                        names[i]
                    );
                    dIdx++;
                }
            }

            // 3. Cấu hình thông số tối ưu
            opt.BladeWidth = 3.2;
            opt.OptimizationLevel = 5;
            opt.MinimizePatterns = 1; // Bật chế độ Batch Pattern Optimizer

            Console.WriteLine($"-> Đang chạy tối ưu với MinimizePatterns = 1 (Batch Optimizer)...");
            var sw = Stopwatch.StartNew();
            opt.StartGuillotine();
            sw.Stop();

            Console.WriteLine($"\n[KẾT QUẢ TỐI ƯU HÓA]:");
            Console.WriteLine($"• Thời gian tính: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"• Chi tiết đã xếp: {opt.TotalNumberOfUtilizedDemandPieces} / {opt.NumberOfDemandPieces}");
            Console.WriteLine($"• Số tấm kho đã dùng: {opt.TotalNumberOfUtilizedRepositoryPieces} / {opt.NumberOfRepositoryPieces}");

            var grouped = opt.GetGroupedSheetLayouts();
            Console.WriteLine($"• Số lượng sơ đồ cắt độc lập (Unique Patterns): {grouped.Count}\n");

            for (int g = 0; g < grouped.Count; g++)
            {
                var grp = grouped[g];
                Console.WriteLine($"  Sơ đồ #{g + 1}: [{grp.RepositoryLabel}] | CẮT x{grp.Count} TẤM | Độ phủ: {grp.SurfaceCovered:F1}% | Chi tiết: {grp.RepresentativeSheet.UtilizedPieces.Count}/tấm (Tổng {grp.RepresentativeSheet.UtilizedPieces.Count * grp.Count})");
            }

            // -------------------------------------------------------------------------
            // TEST MULTI-MATERIAL SEGREGATION
            // -------------------------------------------------------------------------
            Console.WriteLine("\n================================================================================");
            Console.WriteLine("     KIỂM THỬ TÁCH BIỆT THEO VẬT LIỆU (MULTI-MATERIAL MATCHING TEST)");
            Console.WriteLine("================================================================================");

            var matOpt = new Optimization2DX();
            matOpt.BladeWidth = 3.2;
            matOpt.OptimizationLevel = 10;
            matOpt.MinimizePatterns = 1;

            // 2 tấm ván kho MDF, 2 tấm Melamine
            matOpt.NumberOfRepositoryPieces = 4;
            matOpt.SetRepositoryPiece(0, 2440, 1220, 2, 2, 0, 0, 1, 0, 0, SheetGrainDirection.AlongLength, "Tấm MDF #1", "MDF 17mm");
            matOpt.SetRepositoryPiece(1, 2440, 1220, 2, 2, 0, 0, 2, 0, 0, SheetGrainDirection.AlongLength, "Tấm MDF #2", "MDF 17mm");
            matOpt.SetRepositoryPiece(2, 2440, 1220, 2, 2, 0, 0, 3, 0, 0, SheetGrainDirection.AlongLength, "Tấm Melamine #1", "Melamine 18mm");
            matOpt.SetRepositoryPiece(3, 2440, 1220, 2, 2, 0, 0, 4, 0, 0, SheetGrainDirection.AlongLength, "Tấm Melamine #2", "Melamine 18mm");

            // Chi tiết: 6 chi tiết MDF 17mm, 6 chi tiết Melamine 18mm, 4 chi tiết Alu 3mm (không có tấm kho)
            matOpt.NumberOfDemandPieces = 16;
            for (int i = 0; i < 6; i++)
            {
                matOpt.SetDemandPieceWithGrain(i, 600, 400, GrainDirection.AlongLength, i + 1, 0, SheetGrainDirection.AlongLength, $"Chi tiết MDF #{i + 1}", "MDF 17mm");
            }
            for (int i = 6; i < 12; i++)
            {
                matOpt.SetDemandPieceWithGrain(i, 700, 500, GrainDirection.AlongLength, i + 1, 0, SheetGrainDirection.AlongLength, $"Chi tiết Melamine #{i - 5}", "Melamine 18mm");
            }
            for (int i = 12; i < 16; i++)
            {
                matOpt.SetDemandPieceWithGrain(i, 800, 600, GrainDirection.AlongLength, i + 1, 0, SheetGrainDirection.AlongLength, $"Chi tiết Alu #{i - 11}", "Alu 3mm");
            }

            matOpt.StartGuillotine();

            Console.WriteLine($"• Tổng số chi tiết yêu cầu: {matOpt.NumberOfDemandPieces}");
            Console.WriteLine($"• Số chi tiết đã xếp: {matOpt.TotalNumberOfUtilizedDemandPieces}");
            Console.WriteLine($"• Số chi tiết chưa xếp (do thiếu kho): {matOpt.NumberOfDemandPieces - matOpt.TotalNumberOfUtilizedDemandPieces}");
            Console.WriteLine($"• Số tấm kho đã sử dụng: {matOpt.TotalNumberOfUtilizedRepositoryPieces}");

            bool materialCrossContamination = false;
            foreach (var sheet in matOpt.Results)
            {
                Console.WriteLine($"  [Tấm kết quả #{sheet.SheetIndex + 1}] Vật liệu tấm: '{sheet.Material}' | Nhãn: '{sheet.RepositoryLabel}' | Chi tiết: {sheet.UtilizedPieces.Count}");
                foreach (var up in sheet.UtilizedPieces)
                {
                    Console.WriteLine($"     -> Chi tiết: '{up.Label}' | Vật liệu: '{up.Material}'");
                    if (!string.Equals(sheet.Material, up.Material, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"     [LỖI TRỘN VẬT LIỆU]: Chi tiết '{up.Material}' được xếp lên tấm '{sheet.Material}'!");
                        materialCrossContamination = true;
                    }
                }
            }

            if (!materialCrossContamination && matOpt.TotalNumberOfUtilizedDemandPieces == 12)
            {
                Console.WriteLine("\n>>> KẾT QUẢ KIỂM THỬ: THÀNH CÔNG RỰC RỠ! VẬT LIỆU ĐƯỢC TÁCH BIỆT HOÀN TOÀN, KHÔNG LẪN LỘN!");
            }
            else
            {
                Console.WriteLine("\n>>> KẾT QUẢ KIỂM THỬ: THẤT BẠI! CÓ VẤN ĐỀ VỀ VẬT LIỆU!");
            }
        }
    }
}
