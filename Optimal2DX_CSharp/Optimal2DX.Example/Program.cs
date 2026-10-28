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
        }
    }
}
