namespace Optimal2DX.Models
{
    /// <summary>
    /// Hướng thớ gỗ cho chi tiết cần cắt (Demand Piece).
    /// Tham khảo: https://www.optimalprograms.com/help/optimization2dx-4/files/grain_direction.htm
    /// </summary>
    public enum GrainDirection
    {
        /// <summary>
        /// Không có thớ gỗ / Cho phép xoay tự do 90 độ (CanRotate = 1).
        /// </summary>
        None = 0,

        /// <summary>
        /// Thớ gỗ song song với chiều Dài (Length). Cấm xoay (CanRotate = 0).
        /// </summary>
        AlongLength = 1,

        /// <summary>
        /// Thớ gỗ song song với chiều Rộng (Width). Cấm xoay (CanRotate = 0).
        /// </summary>
        AlongWidth = 2,

        /// <summary>
        /// Cố định hướng, không xoay (CanRotate = 0).
        /// </summary>
        FixedNoRotate = 3
    }

    /// <summary>
    /// Hướng thớ gỗ cho tấm nguyên tồn kho (Repository Sheet / Stock).
    /// </summary>
    public enum SheetGrainDirection
    {
        /// <summary>
        /// Chuẩn mặc định Cut2DX: Thớ gỗ chạy dọc theo chiều Dài của tấm (trục X).
        /// </summary>
        AlongLength = 0,

        /// <summary>
        /// Thớ gỗ chạy theo chiều Rộng của tấm (trục Y).
        /// </summary>
        AlongWidth = 1,

        /// <summary>
        /// Tấm không có hướng thớ (MDF trơn, kim loại, kính,...).
        /// </summary>
        None = 2
    }
}
