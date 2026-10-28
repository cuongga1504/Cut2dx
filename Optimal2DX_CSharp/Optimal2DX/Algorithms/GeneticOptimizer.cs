using System;
using System.Collections.Generic;
using Optimal2DX.Models;

namespace Optimal2DX.Algorithms
{
    public class GeneticOptimizer
    {
        private readonly List<DemandPiece> _demandPieces;
        private readonly List<RepositoryPiece> _repositoryPieces;
        private readonly double _bladeWidth;
        private readonly int _optimizationLevel;
        private readonly int _randomSeed;
        private readonly int _cutAllStripsFirst;
        private readonly int _firstDirectionMultiStages;
        private readonly int _numberOfStages;
        private readonly int _onlySameHeightInStrip;
        private readonly int _heightTolerance;
        private readonly int _maximalCutLength;

        public GeneticOptimizer(
            List<DemandPiece> demandPieces,
            List<RepositoryPiece> repositoryPieces,
            double bladeWidth,
            int optimizationLevel,
            int randomSeed,
            int cutAllStripsFirst,
            int firstDirectionMultiStages,
            int numberOfStages,
            int onlySameHeightInStrip,
            int heightTolerance,
            int maximalCutLength)
        {
            _demandPieces = demandPieces;
            _repositoryPieces = repositoryPieces;
            _bladeWidth = Math.Round(Math.Max(0.0, bladeWidth), 1, MidpointRounding.AwayFromZero);
            _optimizationLevel = optimizationLevel;
            _randomSeed = randomSeed;
            _cutAllStripsFirst = cutAllStripsFirst;
            _firstDirectionMultiStages = firstDirectionMultiStages;
            _numberOfStages = numberOfStages;
            _onlySameHeightInStrip = onlySameHeightInStrip;
            _heightTolerance = heightTolerance;
            _maximalCutLength = maximalCutLength;
        }

        public List<SheetLayout> Optimize(int method, Func<bool> isStopRequested, Action onProgress)
        {
            if (_demandPieces.Count == 0 || _repositoryPieces.Count == 0)
            {
                return new List<SheetLayout>();
            }

            var rand = new Random(_randomSeed);
            int n = _demandPieces.Count;

            int popSize = Math.Max(15, Math.Min(60, _optimizationLevel * 2));
            int generations = Math.Max(10, _optimizationLevel * 3);

            var population = new List<Chromosome>(popSize);

            // Seed 1: Sort by Area descending
            var cArea = new Chromosome(n);
            var indices = new List<int>(n);
            for (int i = 0; i < n; i++) indices.Add(i);
            indices.Sort((a, b) => _demandPieces[b].Area.CompareTo(_demandPieces[a].Area));
            cArea.Order = indices.ToArray();
            population.Add(cArea);

            // Seed 2: Sort by Max dimension descending
            var cMaxDim = new Chromosome(n);
            indices.Sort((a, b) =>
                Math.Max(_demandPieces[b].Length, _demandPieces[b].Width)
                .CompareTo(Math.Max(_demandPieces[a].Length, _demandPieces[a].Width)));
            cMaxDim.Order = indices.ToArray();
            population.Add(cMaxDim);

            // Seed 3: Sort by Priority (lower number = higher priority) then Area
            var cPri = new Chromosome(n);
            indices.Sort((a, b) =>
            {
                int p = _demandPieces[a].Priority.CompareTo(_demandPieces[b].Priority);
                return p != 0 ? p : _demandPieces[b].Area.CompareTo(_demandPieces[a].Area);
            });
            cPri.Order = indices.ToArray();
            population.Add(cPri);

            // Fill remaining with random chromosomes
            while (population.Count < popSize)
            {
                var c = new Chromosome(n);
                Shuffle(c.Order, rand);
                for (int i = 0; i < n; i++)
                {
                    c.Rotated[i] = _demandPieces[i].CanRotate == 1 && rand.Next(2) == 1;
                    c.SplitDirection[i] = (byte)rand.Next(2);
                }
                population.Add(c);
            }

            // Evaluate initial population
            Chromosome bestChr = population[0].Clone();
            List<SheetLayout> bestLayouts = new List<SheetLayout>();
            double bestFitness = double.MinValue;

            foreach (var c in population)
            {
                var layouts = Evaluate(c, method);
                double fit = CalculateFitness(layouts);
                c.Fitness = fit;
                if (fit > bestFitness)
                {
                    bestFitness = fit;
                    bestChr = c.Clone();
                    bestLayouts = layouts;
                }
            }

            // Evolutionary loop
            for (int gen = 0; gen < generations; gen++)
            {
                if (isStopRequested != null && isStopRequested())
                {
                    break;
                }

                var nextGen = new List<Chromosome>(popSize);

                // Elitism: keep best
                nextGen.Add(bestChr.Clone());

                while (nextGen.Count < popSize)
                {
                    var p1 = TournamentSelect(population, rand);
                    var p2 = TournamentSelect(population, rand);

                    var child = OrderCrossover(p1, p2, rand);
                    Mutate(child, rand);

                    var layouts = Evaluate(child, method);
                    double fit = CalculateFitness(layouts);
                    child.Fitness = fit;

                    if (fit > bestFitness)
                    {
                        bestFitness = fit;
                        bestChr = child.Clone();
                        bestLayouts = layouts;
                    }

                    nextGen.Add(child);
                }

                population = nextGen;

                if (gen % 5 == 0)
                {
                    onProgress?.Invoke();
                }
            }

            return bestLayouts ?? Evaluate(bestChr ?? population[0], method);
        }

        private List<SheetLayout> Evaluate(Chromosome c, int method)
        {
            switch (method)
            {
                case 1: // Guillotine
                    return GuillotinePacker.Pack(_demandPieces, _repositoryPieces, c, _bladeWidth, _cutAllStripsFirst, _maximalCutLength);
                case 2: // Nested
                    return NestedPacker.Pack(_demandPieces, _repositoryPieces, c, _bladeWidth);
                case 3: // Multi-Stages
                    return MultiStageGuillotinePacker.Pack(_demandPieces, _repositoryPieces, c, _bladeWidth, _firstDirectionMultiStages, _numberOfStages, _onlySameHeightInStrip, _heightTolerance);
                default:
                    return GuillotinePacker.Pack(_demandPieces, _repositoryPieces, c, _bladeWidth, _cutAllStripsFirst, _maximalCutLength);
            }
        }

        private double CalculateFitness(List<SheetLayout> layouts)
        {
            if (layouts == null || layouts.Count == 0) return 0;

            double totalUtilizedArea = 0;
            int totalPiecesPlaced = 0;
            double totalWasteArea = 0;

            foreach (var sheet in layouts)
            {
                foreach (var piece in sheet.UtilizedPieces)
                {
                    totalUtilizedArea += piece.Area;
                    totalPiecesPlaced++;
                }
                foreach (var waste in sheet.WastePieces)
                {
                    totalWasteArea += waste.Area;
                }
            }

            // Main goal: maximize total utilized pieces and area, minimize number of sheets used
            double score = (totalPiecesPlaced * 10_000_000.0)
                         + (totalUtilizedArea * 10.0)
                         - (layouts.Count * 1_000_000.0)
                         - (totalWasteArea * 0.1);
            return score;
        }

        private Chromosome TournamentSelect(List<Chromosome> pop, Random rand)
        {
            int tSize = 3;
            Chromosome best = pop[rand.Next(pop.Count)];
            for (int i = 1; i < tSize; i++)
            {
                var candidate = pop[rand.Next(pop.Count)];
                if (candidate.Fitness > best.Fitness)
                {
                    best = candidate;
                }
            }
            return best;
        }

        private Chromosome OrderCrossover(Chromosome p1, Chromosome p2, Random rand)
        {
            int n = p1.Order.Length;
            var child = new Chromosome(n);

            int start = rand.Next(n);
            int end = rand.Next(n);
            if (start > end)
            {
                int tmp = start;
                start = end;
                end = tmp;
            }

            var inChild = new bool[n];
            for (int i = start; i <= end; i++)
            {
                int val = p1.Order[i];
                child.Order[i] = val;
                child.Rotated[i] = p1.Rotated[i];
                child.SplitDirection[i] = p1.SplitDirection[i];
                inChild[val] = true;
            }

            int p2Idx = 0;
            for (int i = 0; i < n; i++)
            {
                if (i >= start && i <= end) continue;

                while (p2Idx < n && inChild[p2.Order[p2Idx]])
                {
                    p2Idx++;
                }
                if (p2Idx < n)
                {
                    int val = p2.Order[p2Idx];
                    child.Order[i] = val;
                    child.Rotated[i] = p2.Rotated[p2Idx];
                    child.SplitDirection[i] = p2.SplitDirection[p2Idx];
                    inChild[val] = true;
                    p2Idx++;
                }
            }

            return child;
        }

        private void Mutate(Chromosome c, Random rand)
        {
            int n = c.Order.Length;
            if (n <= 1) return;

            // Swap mutation in order
            if (rand.NextDouble() < 0.25)
            {
                int i1 = rand.Next(n);
                int i2 = rand.Next(n);
                int tmp = c.Order[i1];
                c.Order[i1] = c.Order[i2];
                c.Order[i2] = tmp;
            }

            // Rotation flip
            if (rand.NextDouble() < 0.2)
            {
                int i = rand.Next(n);
                int pieceIdx = c.Order[i];
                if (_demandPieces[pieceIdx].CanRotate == 1)
                {
                    c.Rotated[i] = !c.Rotated[i];
                }
            }

            // Split direction flip
            if (rand.NextDouble() < 0.15)
            {
                int i = rand.Next(n);
                c.SplitDirection[i] = (byte)(1 - c.SplitDirection[i]);
            }
        }

        private static void Shuffle(int[] arr, Random rand)
        {
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                int tmp = arr[i];
                arr[i] = arr[j];
                arr[j] = tmp;
            }
        }
    }
}
