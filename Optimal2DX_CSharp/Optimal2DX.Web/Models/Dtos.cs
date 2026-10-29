using System;
using System.Collections.Generic;

namespace Optimal2DX.Web.Models;

public class DemandInputDto
{
    public int Length { get; set; }
    public int Width { get; set; }
    public int Quantity { get; set; } = 1;
    public string Material { get; set; } = string.Empty;
    public string Grain { get; set; } = "≡ Dọc (Theo Dài)"; // "≡ Dọc (Theo Dài)", "||| Ngang (Theo Rộng)", "🔄 Tự do (Không thớ)", "🔒 Cố định (Không xoay)"
    public string Label { get; set; } = string.Empty;
    public bool CanRotate { get; set; } = false;
    public int ExternalID { get; set; } = 0;
    public int Priority { get; set; } = 0;
}

public class RepoInputDto
{
    public int Length { get; set; } = 2440;
    public int Width { get; set; } = 1220;
    public int Quantity { get; set; } = 1;
    public string Material { get; set; } = string.Empty;
    public string SheetGrain { get; set; } = "≡ Dọc (Theo Dài)"; // "≡ Dọc (Theo Dài)", "||| Ngang (Theo Rộng)", "🔄 Không thớ"
    public string Label { get; set; } = string.Empty;
    public decimal Price { get; set; } = 0;
    public int TrimTop { get; set; } = 0;
    public int TrimLeft { get; set; } = 0;
    public int TrimBottom { get; set; } = 0;
    public int TrimRight { get; set; } = 0;
    public int Priority { get; set; } = 0;
    public int ExternalID { get; set; } = 0;
}

public class OptimizationRequestDto
{
    public List<DemandInputDto> DemandPieces { get; set; } = new();
    public List<RepoInputDto> RepoPieces { get; set; } = new();
    
    // Parameters
    public int Method { get; set; } = 0; // 0: Guillotine, 1: Nested, 2: MultiStages
    public double BladeWidth { get; set; } = 3.2;
    public int OptimizationLevel { get; set; } = 50;
    public int RandomSeed { get; set; } = 1;
    public int MinimalUsefulSize { get; set; } = 100;
    public int MaximalCutLength { get; set; } = 0;
    public bool MinimizePatterns { get; set; } = true;
    public string WasteHatchStyle { get; set; } = "DiagonalCross"; // DiagonalCross, BackwardDiagonal, ForwardDiagonal, CornerX, Solid
}

public class UtilizedPieceDto
{
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
    public double TopX { get; set; }
    public double TopY { get; set; }
    public double BottomX { get; set; }
    public double BottomY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int Rotated { get; set; }
    public int DemandIndex { get; set; }
    public int DemandExternalID { get; set; }
    public string GrainLabel { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
}

public class CutItemDto
{
    public int Index { get; set; }
    public double TopX { get; set; }
    public double TopY { get; set; }
    public double BottomX { get; set; }
    public double BottomY { get; set; }
    public double CutThickness { get; set; }
    public int Level { get; set; }
    public string Direction { get; set; } = string.Empty;
}

public class WastePieceItemDto
{
    public int Index { get; set; }
    public double TopX { get; set; }
    public double TopY { get; set; }
    public double BottomX { get; set; }
    public double BottomY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Area { get; set; }
    public bool IsUseful { get; set; }
    public string Classification { get; set; } = string.Empty;
    public string GrainAvailable { get; set; } = string.Empty;
}

public class PatternResultDto
{
    public int PatternIndex { get; set; }
    public string RepositoryLabel { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public int SheetLength { get; set; }
    public int SheetWidth { get; set; }
    public int SheetCount { get; set; }
    public int PiecesPerSheet { get; set; }
    public int TotalPiecesInPattern { get; set; }
    public double SurfaceCoveredPercent { get; set; }
    public double TotalCutLength { get; set; }
    public double WasteSurface { get; set; }
    public string SvgContent { get; set; } = string.Empty;
    public List<UtilizedPieceDto> UtilizedPieces { get; set; } = new();
    public List<CutItemDto> Cuts { get; set; } = new();
    public List<WastePieceItemDto> WastePieces { get; set; } = new();
}

public class OptimizationResultDto
{
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? WarningMessage { get; set; }
    public long ExecutionTimeMs { get; set; }

    // Summary KPIs
    public int TotalDemandPiecesRequested { get; set; }
    public int TotalDemandPiecesPlaced { get; set; }
    public int TotalRepoSheetsStock { get; set; }
    public int TotalRepoSheetsUsed { get; set; }
    public int TotalUniquePatterns { get; set; }
    public double OverallUtilizationPercent { get; set; }
    public double TotalUsefulAreaM2 { get; set; }
    public double TotalWasteAreaM2 { get; set; }
    public double TotalGrossAreaM2 { get; set; }
    public double TotalCutLengthMm { get; set; }
    public int UnplacedPiecesCount { get; set; }

    public List<PatternResultDto> Patterns { get; set; } = new();
}

public class ExcelImportResultDto
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public List<DemandInputDto> DemandPieces { get; set; } = new();
    public List<RepoInputDto> RepoPieces { get; set; } = new();
}
