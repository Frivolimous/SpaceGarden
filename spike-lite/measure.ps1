# Measures idle CPU, memory and frame rate of the lite spike and the Godot prototype, side by side.
# Each run: launch, wait out the warm-up (tiered JIT, settling), then read the process's CPU time over a window.
# Frame rate comes from separate --fpslog runs, since Godot's logging measures render time and costs a little.
# Run from anywhere: powershell -ExecutionPolicy Bypass -File spike-lite\measure.ps1 [-Repeats 2] [-WarmUp 30] [-Window 15]
param([int]$Repeats = 2, [int]$WarmUp = 30, [int]$Window = 15, [int]$Parts = 800)
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot
$lite = Join-Path $PSScriptRoot "bin\Release\net8.0\SpaceGarden.Lite.exe"
$godot = if ($env:GODOT) { $env:GODOT } else { "C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" }
$profileJson = Join-Path $env:APPDATA "Space Garden Prototype\spiketest.json"
$out = Join-Path $PSScriptRoot "results"
New-Item -ItemType Directory -Force $out | Out-Null

if (Get-Process *odot* -ErrorAction SilentlyContinue) { throw "A Godot instance is already open (maybe the real garden): close it first." }

Write-Host "Building..."
dotnet build "$PSScriptRoot" -c Release -nologo -v q | Out-Null
Push-Location (Join-Path $root "godot"); dotnet build -nologo -v q | Out-Null; Pop-Location

Add-Type -AssemblyName System.Windows.Forms
$cursor = [System.Windows.Forms.Cursor]::Position
[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point 5, 5 # away from the plant (bottom right)

function Run($app, $garden, $fpsLog, $tag) {
    $extra = @(); if ($garden -ne "empty") { $extra += "--parts=$Parts" }
    if ($fpsLog) { $extra += "--fpslog" }
    $log = Join-Path $out "$tag.log"
    if ($app -eq "lite") {
        $p = Start-Process $lite -ArgumentList (@("--seed=1", "--grow=0") + $extra) -PassThru -RedirectStandardOutput $log
    } else {
        $godotArgs = @("--path", "game", "--log-file", "`"$log`"", "--", "--fresh", "--seed=1", "--grow=0", "--profile=spiketest") + $extra
        $p = Start-Process $godot -ArgumentList $godotArgs -WorkingDirectory (Join-Path $root "godot") -PassThru
    }
    try {
        Start-Sleep $WarmUp
        $p.Refresh(); $cpu0 = $p.TotalProcessorTime.TotalSeconds; $t0 = [Diagnostics.Stopwatch]::StartNew()
        Start-Sleep $Window
        $p.Refresh(); $cpu = ($p.TotalProcessorTime.TotalSeconds - $cpu0) / $t0.Elapsed.TotalSeconds * 100
        $row = [pscustomobject]@{
            App = $app; Garden = $garden; FpsLog = $fpsLog
            CpuPct = [math]::Round($cpu, 2)
            WorkingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 1)
            PrivateMB = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
        }
    } finally {
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        $p.WaitForExit(5000) | Out-Null
    }
    Start-Sleep 2
    $row
}

$rows = @()
try {
    for ($r = 1; $r -le $Repeats; $r++) {
        foreach ($garden in "empty", "$Parts") {
            foreach ($app in "lite", "godot") {
                $row = Run $app $garden $false "$app-$garden-$r"; $row | Format-Table -HideTableHeaders | Out-String | Write-Host; $rows += $row
            }
        }
    }
    foreach ($garden in "empty", "$Parts") {
        foreach ($app in "lite", "godot") {
            $row = Run $app $garden $true "$app-$garden-fps"; $row | Format-Table -HideTableHeaders | Out-String | Write-Host; $rows += $row
        }
    }
} finally {
    [System.Windows.Forms.Cursor]::Position = $cursor
    Remove-Item $profileJson -ErrorAction SilentlyContinue
    if (Get-Process *odot*, SpaceGarden.Lite -ErrorAction SilentlyContinue) { Write-Warning "Something is still running!" }
}
$rows | Export-Csv (Join-Path $out "results.csv") -NoTypeInformation
$rows | Format-Table -AutoSize
