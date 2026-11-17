using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
            if (_demandPieces == null || _demandPieces.Count == 0 || _repositoryPieces == null || _repositoryPieces.Count == 0)
            {
                return new List<SheetLayout>();
            }

            var rand = new Random(_randomSeed);
            int n = _demandPieces.Count;

            int popSize = Math.Max(16, Math.Min(64, _optimizationLevel * 2));
            int generations = Math.Max(12, _optimizationLevel * 2);

            var population = new List<Chromosome>(popSize);
            var baseIndices = new List<int>(n);
            for (int i = 0; i < n; i++) baseIndices.Add(i);

            // Seed 1: Sort by Area descending (BSSF + SLAS)
            var cArea = new Chromosome(n) { PlacementHeuristic = 0 };
            baseIndices.Sort((a, b) => _demandPieces[b].Area.CompareTo(_demandPieces[a].Area));
            cArea.Order = baseIndices.ToArray();
            population.Add(cArea);

            // Seed 2: Sort by Area descending (BAF + MINAS)
            var cAreaBaf = cArea.Clone();
            cAreaBaf.PlacementHeuristic = 1;
            for (int i = 0; i < n; i++) cAreaBaf.SplitDirection[i] = 2;
            population.Add(cAreaBaf);

            // Seed 3: Sort by Max dimension descending
            var cMaxDim = new Chromosome(n) { PlacementHeuristic = 0 };
            baseIndices.Sort((a, b) =>
                Math.Max(_demandPieces[b].Length, _demandPieces[b].Width)
                .CompareTo(Math.Max(_demandPieces[a].Length, _demandPieces[a].Width)));
            cMaxDim.Order = baseIndices.ToArray();
            population.Add(cMaxDim);

            // Seed 4: Sort by Width descending then Length descending
            var cWidth = new Chromosome(n) { PlacementHeuristic = 0 };
            baseIndices.Sort((a, b) =>
            {
                int cmp = _demandPieces[b].Width.CompareTo(_demandPieces[a].Width);
                return cmp != 0 ? cmp : _demandPieces[b].Length.CompareTo(_demandPieces[a].Length);
            });
            cWidth.Order = baseIndices.ToArray();
            population.Add(cWidth);

            // Seed 5: Sort by Length descending then Width descending
            var cLength = new Chromosome(n) { PlacementHeuristic = 0 };
            baseIndices.Sort((a, b) =>
            {
                int cmp = _demandPieces[b].Length.CompareTo(_demandPieces[a].Length);
                return cmp != 0 ? cmp : _demandPieces[b].Width.CompareTo(_demandPieces[a].Width);
            });
            cLength.Order = baseIndices.ToArray();
            population.Add(cLength);

            // Seed 6: Sort by Priority then Area
            var cPri = new Chromosome(n) { PlacementHeuristic = 0 };
            baseIndices.Sort((a, b) =>
            {
                int p = _demandPieces[a].Priority.CompareTo(_demandPieces[b].Priority);
                return p != 0 ? p : _demandPieces[b].Area.CompareTo(_demandPieces[a].Area);
            });
            cPri.Order = baseIndices.ToArray();
            population.Add(cPri);

            while (population.Count < popSize)
            {
                var c = new Chromosome(n)
                {
                    PlacementHeuristic = (byte)rand.Next(3)
                };
                Shuffle(c.Order, rand);
                for (int pieceIdx = 0; pieceIdx < n; pieceIdx++)
                {
                    c.Rotated[pieceIdx] = _demandPieces[pieceIdx].CanRotate == 1 && rand.Next(2) == 1;
                    c.SplitDirection[pieceIdx] = (byte)rand.Next(3);
                }
                population.Add(c);
            }

            var initLayouts = new List<SheetLayout>[popSize];
            Parallel.For(0, popSize, i =>
            {
                var layouts = Evaluate(population[i], method);
                population[i].Fitness = CalculateFitness(layouts);
                initLayouts[i] = layouts;
            });

            Chromosome bestChr = population[0].Clone();
            List<SheetLayout> bestLayouts = initLayouts[0];
            double bestFitness = population[0].Fitness;

            for (int i = 1; i < popSize; i++)
            {
                if (population[i].Fitness > bestFitness)
                {
                    bestFitness = population[i].Fitness;
                    bestChr = population[i].Clone();
                    bestLayouts = initLayouts[i];
                }
            }

            int stagnationCount = 0;
            const int maxStagnationGenerations = 25;

            for (int gen = 0; gen < generations; gen++)
            {
                if (isStopRequested != null && isStopRequested())
                    break;

                var nextGen = new Chromosome[popSize];
                var genLayouts = new List<SheetLayout>[popSize];

                nextGen[0] = bestChr.Clone();
                genLayouts[0] = bestLayouts;

                for (int i = 1; i < popSize; i++)
                {
                    var p1 = TournamentSelect(population, rand);
                    var p2 = TournamentSelect(population, rand);
                    var child = OrderCrossover(p1, p2, rand);
                    Mutate(child, rand);
                    nextGen[i] = child;
                }

                Parallel.For(1, popSize, i =>
                {
                    var layouts = Evaluate(nextGen[i], method);
                    nextGen[i].Fitness = CalculateFitness(layouts);
                    genLayouts[i] = layouts;
                });

                bool improvedInThisGen = false;
                for (int i = 1; i < popSize; i++)
                {
                    if (nextGen[i].Fitness > bestFitness)
                    {
                        bestFitness = nextGen[i].Fitness;
                        bestChr = nextGen[i].Clone();
                        bestLayouts = genLayouts[i];
                        improvedInThisGen = true;
                    }
                }

                population = new List<Chromosome>(nextGen);

                if (improvedInThisGen)
                {
                    stagnationCount = 0;
                }
                else
                {
                    stagnationCount++;
                    if (stagnationCount >= maxStagnationGenerations)
                    {
                        break;
                    }
                }

                if (gen % 4 == 0)
                {
                    onProgress?.Invoke();
                }
            }

            onProgress?.Invoke();
            return bestLayouts ?? Evaluate(bestChr ?? population[0], method);
        }

        private List<SheetLayout> Evaluate(Chromosome c, int method)
        {
            switch (method)
            {
                case 1:
                    return GuillotinePacker.Pack(_demandPieces, _repositoryPieces, c, _bladeWidth, _cutAllStripsFirst, _maximalCutLength);
                case 2:
                    return NestedPacker.Pack(_demandPieces, _repositoryPieces, c, _bladeWidth);
                case 3:
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
            double largestSingleWasteOnLastSheet = 0;

            for (int s = 0; s < layouts.Count; s++)
            {
                var sheet = layouts[s];
                foreach (var piece in sheet.UtilizedPieces)
                {
                    totalUtilizedArea += piece.Area;
                    totalPiecesPlaced++;
                }
                foreach (var waste in sheet.WastePieces)
                {
                    totalWasteArea += waste.Area;
                    if (s == layouts.Count - 1 && waste.Area > largestSingleWasteOnLastSheet)
                    {
                        largestSingleWasteOnLastSheet = waste.Area;
                    }
                }
            }

            double score = (totalPiecesPlaced * 10_000_000.0)
                         + (totalUtilizedArea * 10.0)
                         - (layouts.Count * 1_000_000.0)
                         - (totalWasteArea * 0.1)
                         + (largestSingleWasteOnLastSheet * 0.25);
            return score;
        }

        private Chromosome TournamentSelect(List<Chromosome> pop, Random rand)
        {
            const int tSize = 3;
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
            var child = new Chromosome(n)
            {
                PlacementHeuristic = rand.Next(2) == 0 ? p1.PlacementHeuristic : p2.PlacementHeuristic
            };

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
                int pieceIdx = p1.Order[i];
                child.Order[i] = pieceIdx;
                child.Rotated[pieceIdx] = p1.Rotated[pieceIdx];
                child.SplitDirection[pieceIdx] = p1.SplitDirection[pieceIdx];
                inChild[pieceIdx] = true;
            }

            int p2Pos = 0;
            for (int i = 0; i < n; i++)
            {
                if (i >= start && i <= end) continue;

                while (p2Pos < n && inChild[p2.Order[p2Pos]])
                {
                    p2Pos++;
                }
                if (p2Pos < n)
                {
                    int pieceIdx = p2.Order[p2Pos];
                    child.Order[i] = pieceIdx;
                    child.Rotated[pieceIdx] = p2.Rotated[pieceIdx];
                    child.SplitDirection[pieceIdx] = p2.SplitDirection[pieceIdx];
                    inChild[pieceIdx] = true;
                    p2Pos++;
                }
            }

            return child;
        }

        private void Mutate(Chromosome c, Random rand)
        {
            int n = c.Order.Length;
            if (n <= 1) return;

            if (rand.NextDouble() < 0.25)
            {
                int i1 = rand.Next(n);
                int i2 = rand.Next(n);
                int tmp = c.Order[i1];
                c.Order[i1] = c.Order[i2];
                c.Order[i2] = tmp;
            }

            if (n >= 4 && rand.NextDouble() < 0.15)
            {
                int i1 = rand.Next(n);
                int len = Math.Min(rand.Next(2, 8), n - i1);
                Array.Reverse(c.Order, i1, len);
            }

            if (rand.NextDouble() < 0.20)
            {
                int pieceIdx = rand.Next(n);
                if (_demandPieces[pieceIdx].CanRotate == 1)
                {
                    c.Rotated[pieceIdx] = !c.Rotated[pieceIdx];
                }
            }

            if (rand.NextDouble() < 0.15)
            {
                int pieceIdx = rand.Next(n);
                c.SplitDirection[pieceIdx] = (byte)rand.Next(3);
            }

            if (rand.NextDouble() < 0.08)
            {
                c.PlacementHeuristic = (byte)rand.Next(3);
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
