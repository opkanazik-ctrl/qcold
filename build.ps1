param(
    [Parameter(Mandatory=$true)]
    [string]$PeakDir
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$PeakDir = (Resolve-Path $PeakDir).Path

$required = @(
    (Join-Path $PeakDir "BepInEx\core\BepInEx.dll"),
    (Join-Path $PeakDir "PEAK_Data\Managed\UnityEngine.CoreModule.dll"),
    (Join-Path $PeakDir "PEAK_Data\Managed\UnityEngine.PhysicsModule.dll")
)

foreach ($file in $required) {
    if (!(Test-Path $file)) { throw "Missing PEAK reference: $file" }
}

dotnet build (Join-Path $root "DotaChaos.csproj") -c Release "-p:PEAK_DIR=$PeakDir"

$dll = Join-Path $root "bin\Release\netstandard2.1\DotaChaos.dll"
if (!(Test-Path $dll)) { throw "DotaChaos.dll was not produced." }

$pluginDir = Join-Path $PeakDir "BepInEx\plugins\DotaChaos"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item $dll (Join-Path $pluginDir "DotaChaos.dll") -Force

Write-Host "Built and installed DotaChaos.dll"
