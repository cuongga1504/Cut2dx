using System;

namespace Optimal2DX.Algorithms
{
    public class Chromosome
    {
        /// <summary>
        /// Thứ tự đưa các chi tiết vào xếp (chứa các chỉ số 0 .. N-1).
        /// </summary>
        public int[] Order { get; set; }

        /// <summary>
        /// Cờ ưu tiên xoay 90 độ của từng chi tiết (truy cập theo pieceIdx).
        /// </summary>
        public bool[] Rotated { get; set; }

        /// <summary>
        /// Quy tắc chia cắt Guillotine cho từng chi tiết (truy cập theo pieceIdx):
        /// 0 = Shorter Leftover Axis (SLAS), 1 = Longer Leftover Axis (LLAS), 2 = Maximize Larger Area (MINAS).
        /// </summary>
        public byte[] SplitDirection { get; set; }

        /// <summary>
        /// Quy tắc chọn vùng trống: 0 = Best Short Side Fit (BSSF), 1 = Best Area Fit (BAF), 2 = Best Long Side Fit (BLSF).
        /// </summary>
        public byte PlacementHeuristic { get; set; }

        public double Fitness { get; set; }

        public Chromosome(int pieceCount)
        {
            Order = new int[pieceCount];
            Rotated = new bool[pieceCount];
            SplitDirection = new byte[pieceCount];
            PlacementHeuristic = 0;
            for (int i = 0; i < pieceCount; i++)
            {
                Order[i] = i;
            }
        }

        public Chromosome Clone()
        {
            int len = Order.Length;
            var c = new Chromosome(len)
            {
                PlacementHeuristic = PlacementHeuristic,
                Fitness = Fitness
            };
            Buffer.BlockCopy(Order, 0, c.Order, 0, len * sizeof(int));
            Array.Copy(Rotated, c.Rotated, len);
            Array.Copy(SplitDirection, c.SplitDirection, len);
            return c;
        }
    }
}
