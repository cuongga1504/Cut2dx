using System;

namespace Optimal2DX
{
    public interface IOptimization2DX
    {
        // Properties
        int OptimizationLevel { get; set; }
        double BladeWidth { get; set; }
        int NumberOfDemandPieces { get; set; }
        int NumberOfRepositoryPieces { get; set; }
        int RandomSeed { get; set; }
        string ComputerID { get; }
        int IsRegistered { get; }
        int Expired { get; }
        string CallerName { get; }
        string Version { get; }
        int TotalNumberOfUtilizedDemandPieces { get; }
        int TotalNumberOfUtilizedRepositoryPieces { get; }
        int WhichStartMethodStarted { get; }
        int MinimalWidthBreakage { get; set; }
        int MinimalLengthBreakage { get; set; }
        int MaximalCutLength { get; set; }
        int FirstDirectionMultiStages { get; set; }
        int ShowWarnings { get; set; }
        int CutAllStripsFirst { get; set; }
        int OnlyPiecesOfTheSameHeightInAStrip { get; set; }
        int HeightTolerance { get; set; }
        int NumberOfStages { get; set; }
        int MinimalUsefulSize { get; set; }
        int MinStripSize { get; set; }
        int MaxStripSize { get; set; }
        int AllowPrecuts { get; set; }
        int PrecutDirection { get; set; }
        int GenerateBasicCNC { get; set; }
        int MaximalGarbageSize { get; set; }
        int SinglePieceStrips { get; set; }
        int WasteType { get; set; }
        int MinimizePatterns { get; set; }
        double TotalWaste { get; }

        // Methods
        void SetDemandPiece(int index, int length, int width, int canRotate, int externalId, int priority);
        void GetDemandPiece(int index, out int length, out int width, out int canRotate, out int externalId, out int priority);
        void SetDemandPieceMaterial(int index, string material);
        string GetDemandPieceMaterial(int index);

        void SetRepositoryPiece(int index, int length, int width, int trimTop, int trimLeft, int trimBottom, int trimRight, int externalId, int priority, int numberOfHoles);
        void GetRepositoryPiece(int index, out int length, out int width, out int trimTop, out int trimLeft, out int trimBottom, out int trimRight, out int externalId, out int priority, out int numberOfHoles);
        void SetRepositoryPieceMaterial(int index, string material);
        string GetRepositoryPieceMaterial(int index);

        string GetSheetMaterial(int sheetIndex);
        string GetUtilizedPieceMaterial(int sheetIndex, int pieceIndex);

        void StartGuillotine();
        void StartNested();
        void StartMultiStages();
        void Stop();

        void NumberOfCuttings(int sheetIndex, out int numberOfCuts, out int repositoryIndex, out int repositoryExternalId);
        void NumberOfUtilizedDemandPieces(int sheetIndex, out int numberOfUtilizedPieces, out int repositoryIndex, out int repositoryExternalId);
        void NumberOfWastePieces(int sheetIndex, out int numberOfWastePieces, out int repositoryIndex, out int repositoryExternalId);
        void SurfaceCovered(int sheetIndex, out double surfaceCovered, out int repositoryIndex, out int repositoryExternalId);

        // Integer versions for legacy backward compatibility
        void GetCut(int sheetIndex, int cutIndex, out int topX, out int topY, out int bottomX, out int bottomY, out int cutThickness, out int repositoryIndex, out int repositoryExternalId);
        void GetCutMultiStages(int sheetIndex, int cutIndex, out int topX, out int topY, out int bottomX, out int bottomY, out int cutThickness, out int level, out int repositoryIndex, out int repositoryExternalId);
        void GetUtilizedDemandPiece(int sheetIndex, int pieceIndex, out int topX, out int topY, out int bottomX, out int bottomY, out int rotated, out int repositoryIndex, out int demandIndex, out int repositoryExternalId, out int demandExternalId);
        void GetWastePiece(int sheetIndex, int wasteIndex, out int topX, out int topY, out int bottomX, out int bottomY, out int repositoryIndex, out int repositoryExternalId);

        // High-precision double versions
        void GetCut(int sheetIndex, int cutIndex, out double topX, out double topY, out double bottomX, out double bottomY, out double cutThickness, out int repositoryIndex, out int repositoryExternalId);
        void GetCutMultiStages(int sheetIndex, int cutIndex, out double topX, out double topY, out double bottomX, out double bottomY, out double cutThickness, out int level, out int repositoryIndex, out int repositoryExternalId);
        void GetUtilizedDemandPiece(int sheetIndex, int pieceIndex, out double topX, out double topY, out double bottomX, out double bottomY, out int rotated, out int repositoryIndex, out int demandIndex, out int repositoryExternalId, out int demandExternalId);
        void GetWastePiece(int sheetIndex, int wasteIndex, out double topX, out double topY, out double bottomX, out double bottomY, out int repositoryIndex, out int repositoryExternalId);

        void DemandPieceUtilized(int demandIndex, out int sheetIndex, out int repositoryIndex, out int demandExternalId, out int repositoryExternalId);
        void RepositoryPieceUtilized(int index, out int utilized, out int repositoryExternalId);
        void GetUtilizedRepositoryPiece(int sheetIndex, out int repositoryIndex, out int repositoryExternalId);

        void CutLength(int sheetIndex, out double length, out int repositoryIndex, out int repositoryExternalId);
        void WasteSurface(int sheetIndex, out double surface, out int repositoryIndex, out int repositoryExternalId);
        void GarbageWaste(int sheetIndex, out double surface, out int repositoryIndex, out int repositoryExternalId);
        void CutSurface(int sheetIndex, out double surface, out int repositoryIndex, out int repositoryExternalId);

        void SaveToFile(string fileName, int saveInputData, int saveParameters, int saveResults);
        void LoadFromFile(string fileName);

        // Events
        event Action? OnFinish;
        event Action? OnProgress;
    }
}
