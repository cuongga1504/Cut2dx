using System;
using System.Collections.Generic;
using System.Linq;

namespace Optimal2DX.Models
{
    /// <summary>
    /// Đại diện cho một mẫu sơ đồ cắt (Cutting Pattern) được gom nhóm từ các tấm cắt giống hệt nhau.
    /// </summary>
    public class GroupedSheetLayout
    {
        /// <summary>
        /// Số thứ tự mẫu sơ đồ (0, 1, 2,...).
        /// </summary>
        public int PatternIndex { get; set; }

        /// <summary>
        /// Sơ đồ tấm đại diện (chứa danh sách đường cắt, chi tiết, phế liệu để hiển thị).
        /// </summary>
        public SheetLayout RepresentativeSheet { get; set; }

        /// <summary>
        /// Danh sách chỉ số các tấm thực tế (SheetIndex) có sơ đồ giống hệt mẫu này.
        /// </summary>
        public List<int> SheetIndices { get; set; } = new List<int>();

        /// <summary>
        /// Số lượng tấm cần cắt theo sơ đồ này.
        /// </summary>
        public int Count => SheetIndices.Count;

        public int SheetLength => RepresentativeSheet.SheetLength;
        public int SheetWidth => RepresentativeSheet.SheetWidth;
        public string RepositoryLabel => RepresentativeSheet.RepositoryLabel;
        public int RepositoryIndex => RepresentativeSheet.RepositoryIndex;
        public int RepositoryExternalID => RepresentativeSheet.RepositoryExternalID;
        public double SurfaceCovered => RepresentativeSheet.SurfaceCovered;
        public double TotalCutLength => RepresentativeSheet.TotalCutLength;
        public double WasteSurface => RepresentativeSheet.WasteSurface;

        public GroupedSheetLayout(int patternIndex, SheetLayout representativeSheet)
        {
            PatternIndex = patternIndex;
            RepresentativeSheet = representativeSheet;
            SheetIndices.Add(representativeSheet.SheetIndex);
        }

        private static bool Diff(double a, double b) => Math.Abs(a - b) > 0.05;

        /// <summary>
        /// So sánh hai sơ đồ tấm xem có giống hệt nhau về quy cách cắt hay không.
        /// Hai sơ đồ được coi là giống nhau khi có:
        /// 1. Cùng kích thước tấm (Dài x Rộng)
        /// 2. Cùng số lượng và tọa độ các đường cắt Guillotine
        /// 3. Cùng số lượng, tọa độ, kích thước và góc xoay của các chi tiết
        /// 4. Cùng vị trí và kích thước các mảnh phế liệu
        /// </summary>
        public static bool ArePatternsIdentical(SheetLayout s1, SheetLayout s2)
        {
            if (s1 == null || s2 == null) return s1 == s2;
            if (s1.SheetLength != s2.SheetLength || s1.SheetWidth != s2.SheetWidth) return false;
            if (!string.Equals(s1.RepositoryLabel, s2.RepositoryLabel, StringComparison.OrdinalIgnoreCase)) return false;
            if (s1.UtilizedPieces.Count != s2.UtilizedPieces.Count) return false;
            if (s1.Cuts.Count != s2.Cuts.Count) return false;
            if (s1.WastePieces.Count != s2.WastePieces.Count) return false;

            // 1. So sánh chi tiết đã xếp (sắp xếp theo tọa độ TopX, TopY để so sánh bất kể thứ tự)
            var p1 = s1.UtilizedPieces.OrderBy(p => p.TopX).ThenBy(p => p.TopY).ThenBy(p => p.BottomX).ThenBy(p => p.BottomY).ToList();
            var p2 = s2.UtilizedPieces.OrderBy(p => p.TopX).ThenBy(p => p.TopY).ThenBy(p => p.BottomX).ThenBy(p => p.BottomY).ToList();

            for (int i = 0; i < p1.Count; i++)
            {
                if (Diff(p1[i].TopX, p2[i].TopX) || Diff(p1[i].TopY, p2[i].TopY) ||
                    Diff(p1[i].BottomX, p2[i].BottomX) || Diff(p1[i].BottomY, p2[i].BottomY) ||
                    p1[i].Rotated != p2[i].Rotated)
                    return false;

                if (Diff(p1[i].Width, p2[i].Width) || Diff(p1[i].Height, p2[i].Height))
                    return false;

                if (p1[i].Grain != p2[i].Grain)
                    return false;
            }

            // 2. So sánh nhát cắt Guillotine
            var c1 = s1.Cuts.OrderBy(c => c.TopX).ThenBy(c => c.TopY).ThenBy(c => c.BottomX).ThenBy(c => c.BottomY).ToList();
            var c2 = s2.Cuts.OrderBy(c => c.TopX).ThenBy(c => c.TopY).ThenBy(c => c.BottomX).ThenBy(c => c.BottomY).ToList();

            for (int i = 0; i < c1.Count; i++)
            {
                if (Diff(c1[i].TopX, c2[i].TopX) || Diff(c1[i].TopY, c2[i].TopY) ||
                    Diff(c1[i].BottomX, c2[i].BottomX) || Diff(c1[i].BottomY, c2[i].BottomY) ||
                    Diff(c1[i].CutThickness, c2[i].CutThickness))
                    return false;
            }

            // 3. So sánh phế liệu
            var w1 = s1.WastePieces.OrderBy(w => w.TopX).ThenBy(w => w.TopY).ThenBy(w => w.BottomX).ThenBy(w => w.BottomY).ToList();
            var w2 = s2.WastePieces.OrderBy(w => w.TopX).ThenBy(w => w.TopY).ThenBy(w => w.BottomX).ThenBy(w => w.BottomY).ToList();

            for (int i = 0; i < w1.Count; i++)
            {
                if (Diff(w1[i].TopX, w2[i].TopX) || Diff(w1[i].TopY, w2[i].TopY) ||
                    Diff(w1[i].BottomX, w2[i].BottomX) || Diff(w1[i].BottomY, w2[i].BottomY))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Gom nhóm danh sách các sơ đồ tấm thành danh sách các mẫu sơ đồ duy nhất kèm số lượng lặp lại.
        /// </summary>
        public static List<GroupedSheetLayout> GroupSheets(IList<SheetLayout> sheets)
        {
            var result = new List<GroupedSheetLayout>();
            if (sheets == null || sheets.Count == 0) return result;

            foreach (var sheet in sheets)
            {
                var existing = result.FirstOrDefault(g => ArePatternsIdentical(g.RepresentativeSheet, sheet));
                if (existing != null)
                {
                    existing.SheetIndices.Add(sheet.SheetIndex);
                }
                else
                {
                    result.Add(new GroupedSheetLayout(result.Count, sheet));
                }
            }

            return result;
        }
    }
}
