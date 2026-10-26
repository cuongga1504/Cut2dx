using System;

namespace Optimal2DX.Algorithms
{
    public class Chromosome
    {
        public int[] Order { get; set; }           // Permutation of demand piece indices
        public bool[] Rotated { get; set; }         // Rotation flag for each demand piece
        public byte[] SplitDirection { get; set; }   // 0 = Horizontal first, 1 = Vertical first
        public double Fitness { get; set; }

        public Chromosome(int pieceCount)
        {
            Order = new int[pieceCount];
            Rotated = new bool[pieceCount];
            SplitDirection = new byte[pieceCount];
            for (int i = 0; i < pieceCount; i++)
            {
                Order[i] = i;
            }
        }

        public Chromosome Clone()
        {
            var c = new Chromosome(Order.Length);
            Array.Copy(Order, c.Order, Order.Length);
            Array.Copy(Rotated, c.Rotated, Rotated.Length);
            Array.Copy(SplitDirection, c.SplitDirection, SplitDirection.Length);
            c.Fitness = Fitness;
            return c;
        }
    }
}
