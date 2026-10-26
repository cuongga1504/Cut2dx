using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Optimal2DX.Algorithms;
using Optimal2DX.Models;

namespace Optimal2DX
{
    public class Optimization2DX : IOptimization2DX
    {
        // Lists of parts and sheets
        private readonly List<DemandPiece> _demandPieces = new List<DemandPiece>();
        private readonly List<RepositoryPiece> _repositoryPieces = new List<RepositoryPiece>();
        private List<SheetLayout> _results = new List<SheetLayout>();

        private volatile bool _stopRequested = false;

        // Events
        public event Action? OnFinish;
        public event Action? OnProgress;

        // Properties
        public int OptimizationLevel { get; set; } = 50;
        public int BladeWidth { get; set; } = 0;

        public int NumberOfDemandPieces
        {
            get => _demandPieces.Count;
            set
            {
                _demandPieces.Clear();
                for (int i = 0; i < value; i++)
                {
                    _demandPieces.Add(new DemandPiece(i, 0, 0, 1, i, 0));
                }
            }
        }

        public int NumberOfRepositoryPieces
        {
            get => _repositoryPieces.Count;
            set
            {
                _repositoryPieces.Clear();
                for (int i = 0; i < value; i++)
                {
                    _repositoryPieces.Add(new RepositoryPiece(i, 0, 0, 0, 0, 0, 0, i, 0, 0));
                }
            }
        }

        public int RandomSeed { get; set; } = 1;
        public string ComputerID => "CS-CUT2DX-NET";
        public int IsRegistered => 1;
        public int Expired => 0;
        public string CallerName => "Optimal2DX .NET";
        public string Version => "4.0.0 (C# Edition)";

        public int TotalNumberOfUtilizedDemandPieces
        {
            get
            {
                int total = 0;
                foreach (var sheet in _results)
                {
                    total += sheet.UtilizedPieces.Count;
                }
                return total;
            }
        }

        public int TotalNumberOfUtilizedRepositoryPieces => _results.Count;

        /// <summary>
        /// Danh sách kết quả chi tiết các tấm đã xếp.
        /// </summary>
        public IReadOnlyList<SheetLayout> Results => _results;

        /// <summary>
        /// Lấy sơ đồ tấm theo chỉ số.
        /// </summary>
        public SheetLayout? GetSheetLayout(int sheetIndex)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
                return _results[sheetIndex];
            return null;
        }

        /// <summary>
        /// Gom nhóm các sơ đồ tấm giống hệt nhau thành danh sách các mẫu sơ đồ (Cutting Patterns).
        /// </summary>
        public List<GroupedSheetLayout> GetGroupedSheetLayouts()
        {
            return GroupedSheetLayout.GroupSheets(_results);
        }

        public int WhichStartMethodStarted { get; private set; } = 0; // 1 = Guillotine, 2 = Nested, 3 = MultiStages

        public int MinimalWidthBreakage { get; set; } = 0;
        public int MinimalLengthBreakage { get; set; } = 0;
        public int MaximalCutLength { get; set; } = 0;
        public int FirstDirectionMultiStages { get; set; } = 0;
        public int ShowWarnings { get; set; } = 0;
        public int CutAllStripsFirst { get; set; } = 0;
        public int OnlyPiecesOfTheSameHeightInAStrip { get; set; } = 0;
        public int HeightTolerance { get; set; } = 0;
        public int NumberOfStages { get; set; } = 2;
        public int MinimalUsefulSize { get; set; } = 0;
        public int MinStripSize { get; set; } = 0;
        public int MaxStripSize { get; set; } = 0;
        public int AllowPrecuts { get; set; } = 0;
        public int PrecutDirection { get; set; } = 0;
        public int GenerateBasicCNC { get; set; } = 0;
        public int MaximalGarbageSize { get; set; } = 0;
        public int SinglePieceStrips { get; set; } = 0;
        public int WasteType { get; set; } = 0;

        /// <summary>
        /// 1 = Tối ưu hóa giảm số lượng mẫu sơ đồ cắt (Pattern Minimization for Batch Production).
        /// 0 = Tối ưu hóa phân tán tự do theo diện tích (Standard Genetic Algorithm).
        /// Mặc định: 1.
        /// </summary>
        public int MinimizePatterns { get; set; } = 1;

        public double TotalWaste
        {
            get
            {
                double total = 0;
                foreach (var sheet in _results)
                {
                    total += sheet.WasteSurface;
                }
                return total;
            }
        }

        public void SetDemandPiece(int index, int length, int width, int canRotate, int externalId, int priority)
        {
            while (_demandPieces.Count <= index)
            {
                _demandPieces.Add(new DemandPiece(_demandPieces.Count, 0, 0, 1, _demandPieces.Count, 0));
            }
            _demandPieces[index] = new DemandPiece(index, length, width, canRotate, externalId, priority);
        }

        public void SetDemandPiece(int index, int length, int width, int canRotate, int externalId, int priority, GrainDirection grain)
        {
            while (_demandPieces.Count <= index)
            {
                _demandPieces.Add(new DemandPiece(_demandPieces.Count, 0, 0, 1, _demandPieces.Count, 0, GrainDirection.None));
            }
            _demandPieces[index] = new DemandPiece(index, length, width, canRotate, externalId, priority, grain);
        }

        public void SetDemandPiece(int index, int length, int width, int canRotate, int externalId, int priority, GrainDirection grain, string label)
        {
            while (_demandPieces.Count <= index)
            {
                _demandPieces.Add(new DemandPiece(_demandPieces.Count, 0, 0, 1, _demandPieces.Count, 0, GrainDirection.None, ""));
            }
            _demandPieces[index] = new DemandPiece(index, length, width, canRotate, externalId, priority, grain, label);
        }

        /// <summary>
        /// Thiết lập chi tiết theo thớ gỗ chuẩn theo tài liệu Cut2DX:
        /// https://www.optimalprograms.com/help/optimization2dx-4/files/grain_direction.htm
        /// </summary>
        public void SetDemandPieceWithGrain(int index, int length, int width, GrainDirection grain, int externalId = 0, int priority = 0, SheetGrainDirection sheetGrain = SheetGrainDirection.AlongLength)
        {
            SetDemandPieceWithGrain(index, length, width, grain, externalId, priority, sheetGrain, "");
        }

        public void SetDemandPieceWithGrain(int index, int length, int width, GrainDirection grain, int externalId = 0, int priority = 0, SheetGrainDirection sheetGrain = SheetGrainDirection.AlongLength, string label = "")
        {
            if (sheetGrain == SheetGrainDirection.AlongLength || sheetGrain == SheetGrainDirection.None)
            {
                switch (grain)
                {
                    case GrainDirection.None:
                        SetDemandPiece(index, length, width, 1, externalId, priority, grain, label);
                        break;
                    case GrainDirection.AlongLength:
                        SetDemandPiece(index, length, width, 0, externalId, priority, grain, label);
                        break;
                    case GrainDirection.AlongWidth:
                        // Theo tài liệu Cut2DX: Cạnh song song với thớ gỗ đặt làm Length
                        SetDemandPiece(index, width, length, 0, externalId, priority, grain, label);
                        break;
                    case GrainDirection.FixedNoRotate:
                        SetDemandPiece(index, length, width, 0, externalId, priority, grain, label);
                        break;
                }
            }
            else // SheetGrainDirection.AlongWidth
            {
                switch (grain)
                {
                    case GrainDirection.None:
                        SetDemandPiece(index, length, width, 1, externalId, priority, grain, label);
                        break;
                    case GrainDirection.AlongLength:
                        SetDemandPiece(index, width, length, 0, externalId, priority, grain, label);
                        break;
                    case GrainDirection.AlongWidth:
                        SetDemandPiece(index, length, width, 0, externalId, priority, grain, label);
                        break;
                    case GrainDirection.FixedNoRotate:
                        SetDemandPiece(index, length, width, 0, externalId, priority, grain, label);
                        break;
                }
            }
        }

        public string GetDemandPieceLabel(int index)
        {
            if (index >= 0 && index < _demandPieces.Count)
                return _demandPieces[index].Label;
            return string.Empty;
        }

        public void SetDemandPieceLabel(int index, string label)
        {
            if (index >= 0 && index < _demandPieces.Count)
                _demandPieces[index].Label = label ?? string.Empty;
        }

        public void GetDemandPiece(int index, out int length, out int width, out int canRotate, out int externalId, out int priority)
        {
            if (index >= 0 && index < _demandPieces.Count)
            {
                var p = _demandPieces[index];
                length = p.Length;
                width = p.Width;
                canRotate = p.CanRotate;
                externalId = p.ExternalID;
                priority = p.Priority;
            }
            else
            {
                length = 0; width = 0; canRotate = 0; externalId = 0; priority = 0;
            }
        }

        public void SetRepositoryPiece(int index, int length, int width, int trimTop, int trimLeft, int trimBottom, int trimRight, int externalId, int priority, int numberOfHoles)
        {
            while (_repositoryPieces.Count <= index)
            {
                _repositoryPieces.Add(new RepositoryPiece(_repositoryPieces.Count, 0, 0, 0, 0, 0, 0, _repositoryPieces.Count, 0, 0));
            }
            _repositoryPieces[index] = new RepositoryPiece(index, length, width, trimTop, trimLeft, trimBottom, trimRight, externalId, priority, numberOfHoles);
        }

        public void SetRepositoryPiece(int index, int length, int width, int trimTop, int trimLeft, int trimBottom, int trimRight, int externalId, int priority, int numberOfHoles, SheetGrainDirection grain)
        {
            while (_repositoryPieces.Count <= index)
            {
                _repositoryPieces.Add(new RepositoryPiece(_repositoryPieces.Count, 0, 0, 0, 0, 0, 0, _repositoryPieces.Count, 0, 0, SheetGrainDirection.AlongLength));
            }
            _repositoryPieces[index] = new RepositoryPiece(index, length, width, trimTop, trimLeft, trimBottom, trimRight, externalId, priority, numberOfHoles, grain);
        }

        public void SetRepositoryPiece(int index, int length, int width, int trimTop, int trimLeft, int trimBottom, int trimRight, int externalId, int priority, int numberOfHoles, SheetGrainDirection grain, string label)
        {
            while (_repositoryPieces.Count <= index)
            {
                _repositoryPieces.Add(new RepositoryPiece(_repositoryPieces.Count, 0, 0, 0, 0, 0, 0, _repositoryPieces.Count, 0, 0, SheetGrainDirection.AlongLength, ""));
            }
            _repositoryPieces[index] = new RepositoryPiece(index, length, width, trimTop, trimLeft, trimBottom, trimRight, externalId, priority, numberOfHoles, grain, label);
        }

        public string GetRepositoryPieceLabel(int index)
        {
            if (index >= 0 && index < _repositoryPieces.Count)
                return _repositoryPieces[index].Label;
            return string.Empty;
        }

        public void SetRepositoryPieceLabel(int index, string label)
        {
            if (index >= 0 && index < _repositoryPieces.Count)
                _repositoryPieces[index].Label = label ?? string.Empty;
        }

        public string GetSheetRepositoryLabel(int sheetIndex)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
                return _results[sheetIndex].RepositoryLabel;
            return string.Empty;
        }

        public string GetUtilizedPieceLabel(int sheetIndex, int pieceIndex)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count && pieceIndex >= 0 && pieceIndex < _results[sheetIndex].UtilizedPieces.Count)
                return _results[sheetIndex].UtilizedPieces[pieceIndex].Label;
            return string.Empty;
        }

        public SheetGrainDirection GetSheetGrain(int sheetIndex)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                int repIdx = _results[sheetIndex].RepositoryIndex;
                if (repIdx >= 0 && repIdx < _repositoryPieces.Count)
                    return _repositoryPieces[repIdx].Grain;
            }
            return SheetGrainDirection.AlongLength;
        }

        public GrainDirection GetDemandGrain(int demandIndex)
        {
            if (demandIndex >= 0 && demandIndex < _demandPieces.Count)
            {
                return _demandPieces[demandIndex].Grain;
            }
            return GrainDirection.None;
        }

        public GrainDirection GetUtilizedPieceGrain(int sheetIndex, int pieceIndex)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count && pieceIndex >= 0 && pieceIndex < _results[sheetIndex].UtilizedPieces.Count)
            {
                return _results[sheetIndex].UtilizedPieces[pieceIndex].Grain;
            }
            return GrainDirection.None;
        }

        public void GetRepositoryPiece(int index, out int length, out int width, out int trimTop, out int trimLeft, out int trimBottom, out int trimRight, out int externalId, out int priority, out int numberOfHoles)
        {
            if (index >= 0 && index < _repositoryPieces.Count)
            {
                var r = _repositoryPieces[index];
                length = r.Length;
                width = r.Width;
                trimTop = r.TrimTop;
                trimLeft = r.TrimLeft;
                trimBottom = r.TrimBottom;
                trimRight = r.TrimRight;
                externalId = r.ExternalID;
                priority = r.Priority;
                numberOfHoles = r.NumberOfHoles;
            }
            else
            {
                length = 0; width = 0; trimTop = 0; trimLeft = 0; trimBottom = 0; trimRight = 0; externalId = 0; priority = 0; numberOfHoles = 0;
            }
        }

        public void StartGuillotine()
        {
            WhichStartMethodStarted = 1;
            RunOptimization(1);
        }

        public void StartNested()
        {
            WhichStartMethodStarted = 2;
            RunOptimization(2);
        }

        public void StartMultiStages()
        {
            WhichStartMethodStarted = 3;
            RunOptimization(3);
        }

        public void Stop()
        {
            _stopRequested = true;
        }

        private void RunOptimization(int method)
        {
            _stopRequested = false;

            if (method == 1 && MinimizePatterns == 1)
            {
                _results = BatchPatternOptimizer.Optimize(
                    _demandPieces,
                    _repositoryPieces,
                    BladeWidth,
                    OptimizationLevel,
                    () => _stopRequested,
                    () => OnProgress?.Invoke()
                );
            }
            else
            {
                var optimizer = new GeneticOptimizer(
                    _demandPieces,
                    _repositoryPieces,
                    BladeWidth,
                    OptimizationLevel,
                    RandomSeed,
                    CutAllStripsFirst,
                    FirstDirectionMultiStages,
                    NumberOfStages,
                    OnlyPiecesOfTheSameHeightInAStrip,
                    HeightTolerance,
                    MaximalCutLength
                );

                _results = optimizer.Optimize(method, () => _stopRequested, () => OnProgress?.Invoke());
            }

            OnFinish?.Invoke();
        }

        public void NumberOfCuttings(int sheetIndex, out int numberOfCuts, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                numberOfCuts = s.Cuts.Count;
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
            }
            else
            {
                numberOfCuts = 0; repositoryIndex = 0; repositoryExternalId = 0;
            }
        }

        public void NumberOfUtilizedDemandPieces(int sheetIndex, out int numberOfUtilizedPieces, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                numberOfUtilizedPieces = s.UtilizedPieces.Count;
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
            }
            else
            {
                numberOfUtilizedPieces = 0; repositoryIndex = 0; repositoryExternalId = 0;
            }
        }

        public void NumberOfWastePieces(int sheetIndex, out int numberOfWastePieces, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                numberOfWastePieces = s.WastePieces.Count;
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
            }
            else
            {
                numberOfWastePieces = 0; repositoryIndex = 0; repositoryExternalId = 0;
            }
        }

        public void SurfaceCovered(int sheetIndex, out double surfaceCovered, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                surfaceCovered = s.SurfaceCovered;
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
            }
            else
            {
                surfaceCovered = 0; repositoryIndex = 0; repositoryExternalId = 0;
            }
        }

        public void GetCut(int sheetIndex, int cutIndex, out int topX, out int topY, out int bottomX, out int bottomY, out int cutThickness, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
                if (cutIndex >= 0 && cutIndex < s.Cuts.Count)
                {
                    var c = s.Cuts[cutIndex];
                    topX = c.TopX;
                    topY = c.TopY;
                    bottomX = c.BottomX;
                    bottomY = c.BottomY;
                    cutThickness = c.CutThickness;
                    return;
                }
            }
            topX = 0; topY = 0; bottomX = 0; bottomY = 0; cutThickness = 0; repositoryIndex = 0; repositoryExternalId = 0;
        }

        public void GetCutMultiStages(int sheetIndex, int cutIndex, out int topX, out int topY, out int bottomX, out int bottomY, out int cutThickness, out int level, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
                if (cutIndex >= 0 && cutIndex < s.Cuts.Count)
                {
                    var c = s.Cuts[cutIndex];
                    topX = c.TopX;
                    topY = c.TopY;
                    bottomX = c.BottomX;
                    bottomY = c.BottomY;
                    cutThickness = c.CutThickness;
                    level = c.Level;
                    return;
                }
            }
            topX = 0; topY = 0; bottomX = 0; bottomY = 0; cutThickness = 0; level = 0; repositoryIndex = 0; repositoryExternalId = 0;
        }

        public void GetUtilizedDemandPiece(int sheetIndex, int pieceIndex, out int topX, out int topY, out int bottomX, out int bottomY, out int rotated, out int repositoryIndex, out int demandIndex, out int repositoryExternalId, out int demandExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
                if (pieceIndex >= 0 && pieceIndex < s.UtilizedPieces.Count)
                {
                    var p = s.UtilizedPieces[pieceIndex];
                    topX = p.TopX;
                    topY = p.TopY;
                    bottomX = p.BottomX;
                    bottomY = p.BottomY;
                    rotated = p.Rotated;
                    demandIndex = p.DemandIndex;
                    demandExternalId = p.DemandExternalID;
                    return;
                }
            }
            topX = 0; topY = 0; bottomX = 0; bottomY = 0; rotated = 0; repositoryIndex = 0; demandIndex = 0; repositoryExternalId = 0; demandExternalId = 0;
        }

        public void GetWastePiece(int sheetIndex, int wasteIndex, out int topX, out int topY, out int bottomX, out int bottomY, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
                if (wasteIndex >= 0 && wasteIndex < s.WastePieces.Count)
                {
                    var w = s.WastePieces[wasteIndex];
                    topX = w.TopX;
                    topY = w.TopY;
                    bottomX = w.BottomX;
                    bottomY = w.BottomY;
                    return;
                }
            }
            topX = 0; topY = 0; bottomX = 0; bottomY = 0; repositoryIndex = 0; repositoryExternalId = 0;
        }

        public void DemandPieceUtilized(int demandIndex, out int sheetIndex, out int repositoryIndex, out int demandExternalId, out int repositoryExternalId)
        {
            for (int s = 0; s < _results.Count; s++)
            {
                var sheet = _results[s];
                foreach (var piece in sheet.UtilizedPieces)
                {
                    if (piece.DemandIndex == demandIndex)
                    {
                        sheetIndex = s;
                        repositoryIndex = sheet.RepositoryIndex;
                        demandExternalId = piece.DemandExternalID;
                        repositoryExternalId = sheet.RepositoryExternalID;
                        return;
                    }
                }
            }
            sheetIndex = -1; repositoryIndex = -1; demandExternalId = -1; repositoryExternalId = -1;
        }

        public void RepositoryPieceUtilized(int index, out int utilized, out int repositoryExternalId)
        {
            foreach (var s in _results)
            {
                if (s.RepositoryIndex == index)
                {
                    utilized = 1;
                    repositoryExternalId = s.RepositoryExternalID;
                    return;
                }
            }
            utilized = 0;
            repositoryExternalId = index < _repositoryPieces.Count ? _repositoryPieces[index].ExternalID : 0;
        }

        public void GetUtilizedRepositoryPiece(int sheetIndex, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                repositoryIndex = _results[sheetIndex].RepositoryIndex;
                repositoryExternalId = _results[sheetIndex].RepositoryExternalID;
            }
            else
            {
                repositoryIndex = -1;
                repositoryExternalId = -1;
            }
        }

        public void CutLength(int sheetIndex, out double length, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                length = s.TotalCutLength;
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
            }
            else
            {
                length = 0; repositoryIndex = 0; repositoryExternalId = 0;
            }
        }

        public void WasteSurface(int sheetIndex, out double surface, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                surface = s.WasteSurface;
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
            }
            else
            {
                surface = 0; repositoryIndex = 0; repositoryExternalId = 0;
            }
        }

        public void GarbageWaste(int sheetIndex, out double surface, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                surface = s.GarbageWaste(MinimalUsefulSize);
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
            }
            else
            {
                surface = 0; repositoryIndex = 0; repositoryExternalId = 0;
            }
        }

        public void CutSurface(int sheetIndex, out double surface, out int repositoryIndex, out int repositoryExternalId)
        {
            if (sheetIndex >= 0 && sheetIndex < _results.Count)
            {
                var s = _results[sheetIndex];
                double cutArea = 0;
                foreach (var c in s.Cuts)
                {
                    cutArea += c.Length * c.CutThickness;
                }
                surface = cutArea;
                repositoryIndex = s.RepositoryIndex;
                repositoryExternalId = s.RepositoryExternalID;
            }
            else
            {
                surface = 0; repositoryIndex = 0; repositoryExternalId = 0;
            }
        }

        public void SaveToFile(string fileName, int saveInputData, int saveParameters, int saveResults)
        {
            var sb = new StringBuilder();
            sb.AppendLine("//FormatVersion 2");
            sb.AppendLine($"//ComponentVersion {Version}");

            if (saveInputData == 1)
            {
                sb.AppendLine("//DEMAND PARTS");
                sb.AppendLine($"Optimization2DX2->set_NumberOfDemandPieces({_demandPieces.Count});");
                for (int i = 0; i < _demandPieces.Count; i++)
                {
                    var p = _demandPieces[i];
                    sb.AppendLine($"Optimization2DX2->SetDemandPiece({i}, {p.Length}, {p.Width}, {p.CanRotate}, {p.ExternalID}, {p.Priority});");
                }

                sb.AppendLine("//REPOSITORY PARTS");
                sb.AppendLine($"Optimization2DX2->set_NumberOfRepositoryPieces({_repositoryPieces.Count});");
                for (int i = 0; i < _repositoryPieces.Count; i++)
                {
                    var r = _repositoryPieces[i];
                    sb.AppendLine($"Optimization2DX2->SetRepositoryPiece({i}, {r.Length}, {r.Width}, {r.TrimTop}, {r.TrimLeft}, {r.TrimBottom}, {r.TrimRight}, {r.ExternalID}, {r.Priority}, {r.NumberOfHoles});");
                }
            }

            if (saveParameters == 1)
            {
                sb.AppendLine("//PARAMETERS");
                sb.AppendLine($"Optimization2DX2->set_OptimizationLevel({OptimizationLevel});");
                sb.AppendLine($"Optimization2DX2->set_BladeWidth({BladeWidth});");
                sb.AppendLine($"Optimization2DX2->set_RandomSeed({RandomSeed});");
                sb.AppendLine($"Optimization2DX2->set_MinimalUsefulSize({MinimalUsefulSize});");
            }

            if (saveResults == 1)
            {
                sb.AppendLine("RESULTS");
                sb.AppendLine($"NumberOfSheets {_results.Count}");
                for (int s = 0; s < _results.Count; s++)
                {
                    var sheet = _results[s];
                    sb.AppendLine($"SheetIndex {s} {sheet.RepositoryIndex} {sheet.RepositoryExternalID}");
                    sb.AppendLine($"NumberOfPieces {sheet.UtilizedPieces.Count}");
                    foreach (var p in sheet.UtilizedPieces)
                    {
                        sb.AppendLine($"{p.TopX} {p.TopY} {p.BottomX} {p.BottomY} {p.Rotated} {p.DemandIndex} {p.DemandExternalID}");
                    }
                    sb.AppendLine($"NumberOfWastes {sheet.WastePieces.Count}");
                    foreach (var w in sheet.WastePieces)
                    {
                        sb.AppendLine($"{w.TopX} {w.TopY} {w.BottomX} {w.BottomY}");
                    }
                    sb.AppendLine($"NumberOfCuts {sheet.Cuts.Count}");
                    foreach (var c in sheet.Cuts)
                    {
                        sb.AppendLine($"{c.TopX} {c.TopY} {c.BottomX} {c.BottomY} {c.CutThickness} {c.Level}");
                    }
                }
            }

            File.WriteAllText(fileName, sb.ToString());
        }

        public void LoadFromFile(string fileName)
        {
            if (!File.Exists(fileName)) return;
            // Parse lines if needed
        }
    }
}
