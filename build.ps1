<#
.SYNOPSIS
    Skrypt do automatycznej kompilacji projektu SquawkServer do pojedynczego pliku .exe.

.DESCRIPTION
    Skrypt wykrywa pliki źródłowe, kompiluje projekt przy użyciu dotnet publish z optymalizacją,
    a następnie czyści wszystkie pliki tymczasowe, pozostawiając jedynie plik SquawkServer.exe
    w głównym katalogu projektu.

.PARAMETER Compiler
    Typ kompilatora do użycia. Obecnie wspierane: "DotNet" (domyślny).
    Planowane wsparcie: "MSVC", "MinGW" (wymagałoby plików .cpp/.h).

.EXAMPLE
    .\build.ps1
#>

param (
    [Parameter(Mandatory=$false)]
    [ValidateSet("DotNet", "MSVC", "MinGW")]
    [string]$Compiler = "DotNet",

    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Get-Item $PSScriptRoot
$ServerDir = Join-Path $ProjectRoot.FullName "server"
$CsprojPath = Join-Path $ServerDir "SquawkServer.csproj"
$OutputExeName = "SquawkServer.exe"
$FinalExePath = Join-Path $ProjectRoot.FullName $OutputExeName

# Logowanie procesu
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $Color = switch ($Level) {
        "ERROR" { "Red" }
        "WARN"  { "Yellow" }
        "SUCCESS" { "Green" }
        default { "Cyan" }
    }
    Write-Host "[$Timestamp] [$Level] $Message" -ForegroundColor $Color
}

Write-Log "Rozpoczynanie procesu budowania projektu Squawk..."

try {
    if ($Compiler -eq "DotNet") {
        Write-Log "Uzywanie kompilatora .NET (dotnet CLI)..."
        
        if (-not (Test-Path $CsprojPath)) {
            throw "Nie znaleziono pliku projektu: $CsprojPath"
        }

        Write-Log "Przywracanie zaleznosci i kompilacja (PublishSingleFile=true)..."
        
        # Flagi optymalizacji dla pojedynczego pliku exe
        $PublishArgs = @(
            "publish", $CsprojPath,
            "-c", $Configuration,
            "-r", "win-x64",
            "--self-contained", "true",
            "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:DebugType=none",
            "-p:DebugSymbols=false",
            "-o", (Join-Path $ServerDir "publish_temp")
        )

        & dotnet @PublishArgs

        if ($LASTEXITCODE -ne 0) {
            throw "Blad kompilacji dotnet publish (kod wyjscia: $LASTEXITCODE)"
        }

        $TempExePath = Join-Path $ServerDir "publish_temp\$OutputExeName"
        if (Test-Path $TempExePath) {
            Write-Log "Przenoszenie finalnego pliku .exe do glownego katalogu..."
            Move-Item -Path $TempExePath -Destination $FinalExePath -Force
            Write-Log "Plik .exe wygenerowany pomyslnie: $FinalExePath" -Level "SUCCESS"
        } else {
            throw "Nie znaleziono wygenerowanego pliku .exe w folderze tymczasowym."
        }
    }
    elseif ($Compiler -eq "MSVC") {
        Write-Log "Kompilator MSVC wykryty, ale projekt jest w .NET. Przelaczanie na DotNet..." -Level "WARN"
        throw "MSVC nie jest obecnie obslugiwany dla tego typu projektu (.NET)."
    }
    elseif ($Compiler -eq "MinGW") {
        Write-Log "Kompilator MinGW wykryty, ale projekt jest w .NET. Przelaczanie na DotNet..." -Level "WARN"
        throw "MinGW nie jest obecnie obslugiwany dla tego typu projektu (.NET)."
    }

    # Czyszczenie plików tymczasowych
    Write-Log "Czyszczenie plików tymczasowych (.obj, .pdb, folder bin/obj)..."
    
    $PathsToCleanup = @(
        (Join-Path $ServerDir "publish_temp"),
        (Join-Path $ServerDir "bin"),
        (Join-Path $ServerDir "obj")
    )

    foreach ($Path in $PathsToCleanup) {
        if (Test-Path $Path) {
            Remove-Item -Path $Path -Recurse -Force
            Write-Log "Usunieto: $Path"
        }
    }

    Write-Log "Proces zakonczony sukcesem!" -Level "SUCCESS"

}
catch {
    $ErrMsg = $_.Exception.Message
    Write-Log "WYSTAPIL BLAD PODCZAS BUDOWANIA: $ErrMsg" -Level "ERROR"
    exit 1
}
