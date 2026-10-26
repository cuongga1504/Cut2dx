$opt = New-Object -ComObject 'optimal2dx.Optimization2DX'
Write-Output ("Created COM object: " + ($opt -ne $null))
Write-Output ("Version: " + $opt.Version)
Write-Output ("IsRegistered: " + $opt.IsRegistered)

# Let's run the exact VB 2005 Example from https://www.optimalprograms.com/help/optimization2dx-4/files/examples.html#VB_2005_Example

# Step 1: Set pieces to be cut (Demand pieces)
$opt.NumberOfDemandPieces = 14
$opt.SetDemandPiece(0, 60, 40, 1, 0, 0)
$opt.SetDemandPiece(1, 60, 40, 1, 1, 0)
$opt.SetDemandPiece(2, 48, 40, 1, 2, 0)
$opt.SetDemandPiece(3, 60, 53, 1, 3, 0)
$opt.SetDemandPiece(4, 45, 45, 1, 4, 0)
$opt.SetDemandPiece(5, 50, 80, 1, 5, 0)
$opt.SetDemandPiece(6, 45, 75, 1, 6, 0)
$opt.SetDemandPiece(7, 100, 100, 1, 7, 0)
$opt.SetDemandPiece(8, 100, 100, 1, 8, 0)
$opt.SetDemandPiece(9, 90, 90, 1, 9, 0)
$opt.SetDemandPiece(10, 90, 90, 1, 10, 0)
$opt.SetDemandPiece(11, 90, 90, 1, 11, 0)
$opt.SetDemandPiece(12, 110, 110, 1, 12, 0)
$opt.SetDemandPiece(13, 110, 110, 1, 13, 0)

# Step 2: Set repository pieces (stock)
$opt.NumberOfRepositoryPieces = 3
$opt.SetRepositoryPiece(0, 300, 200, 0, 0, 0, 0, 0, 0, 0)
$opt.SetRepositoryPiece(1, 200, 200, 0, 0, 0, 0, 1, 0, 0)
$opt.SetRepositoryPiece(2, 300, 250, 0, 0, 0, 0, 2, 0, 0)

# Step 3: Set parameters
$opt.BladeWidth = 0
$opt.OptimizationLevel = 50
$opt.RandomSeed = 1

# Step 4: Start Guillotine
Write-Output "Starting Guillotine optimization..."
$opt.StartGuillotine()
Write-Output "Finished Guillotine optimization!"

# Step 5: Check results
$totalUtilized = $opt.TotalNumberOfUtilizedRepositoryPieces
Write-Output ("Total Utilized Repository Pieces: " + $totalUtilized)
$totalPieces = $opt.TotalNumberOfUtilizedDemandPieces
Write-Output ("Total Utilized Demand Pieces: " + $totalPieces)

for ($sheet = 0; $sheet -lt $totalUtilized; $sheet++) {
    [double]$surfCovered = 0
    [int]$repIndex = 0
    [int]$repID = 0
    $opt.SurfaceCovered($sheet, [ref]$surfCovered, [ref]$repIndex, [ref]$repID)
    Write-Output ("Sheet $sheet (RepIndex: $repIndex, RepID: $repID): Surface Covered = $surfCovered")

    [int]$noCuts = 0
    $opt.NumberOfCuttings($sheet, [ref]$noCuts, [ref]$repIndex, [ref]$repID)
    Write-Output ("  NumberOfCuttings = $noCuts")
    for ($i = 0; $i -lt $noCuts; $i++) {
        [int]$tx = 0; [int]$ty = 0; [int]$bx = 0; [int]$by = 0; [int]$thick = 0
        $opt.GetCut($sheet, $i, [ref]$tx, [ref]$ty, [ref]$bx, [ref]$by, [ref]$thick, [ref]$repIndex, [ref]$repID)
        Write-Output "    Cut $i - ($tx, $ty) -> ($bx, $by), thickness=$thick"
    }

    [int]$noPieces = 0
    $opt.NumberOfUtilizedDemandPieces($sheet, [ref]$noPieces, [ref]$repIndex, [ref]$repID)
    Write-Output ("  NumberOfUtilizedDemandPieces = $noPieces")
    for ($i = 0; $i -lt $noPieces; $i++) {
        [int]$tx = 0; [int]$ty = 0; [int]$bx = 0; [int]$by = 0; [int]$rot = 0
        [int]$demIdx = 0; [int]$demID = 0
        $opt.GetUtilizedDemandPiece($sheet, $i, [ref]$tx, [ref]$ty, [ref]$bx, [ref]$by, [ref]$rot, [ref]$repIndex, [ref]$demIdx, [ref]$repID, [ref]$demID)
        $w = [Math]::Abs($bx - $tx)
        $h = [Math]::Abs($by - $ty)
        Write-Output "    Piece $i - ($tx, $ty) -> ($bx, $by), rot=$rot, demIdx=$demIdx, demID=$demID, size=${w}x${h}"
    }

    [int]$noWastes = 0
    $opt.NumberOfWastePieces($sheet, [ref]$noWastes, [ref]$repIndex, [ref]$repID)
    Write-Output ("  NumberOfWastePieces = $noWastes")
    for ($i = 0; $i -lt $noWastes; $i++) {
        [int]$tx = 0; [int]$ty = 0; [int]$bx = 0; [int]$by = 0
        $opt.GetWastePiece($sheet, $i, [ref]$tx, [ref]$ty, [ref]$bx, [ref]$by, [ref]$repIndex, [ref]$repID)
        $w = [Math]::Abs($bx - $tx)
        $h = [Math]::Abs($by - $ty)
        Write-Output "    Waste $i - ($tx, $ty) -> ($bx, $by), size=${w}x${h}"
    }
}
