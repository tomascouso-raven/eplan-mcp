<#
.SYNOPSIS
    Development only: registers or unregisters the EplanMcp add-in with EPLAN's own mechanism.

.DESCRIPTION
    Uses the documented command-line form of EplApiModuleAction (api-notes B2):
        EPLAN.exe /Variant:"<variant>" EplApiModuleAction /register:"<dll>"
        EPLAN.exe /Variant:"<variant>" EplApiModuleAction /unregister:"EplanMcp.Bridge.Eplan"
    This starts EPLAN, which loads the add-in right away (bLoadOnStart = true, api-notes A10) and again at every
    start. Registration is per variant: run it once for each variant in use (-Variant 'Electric P8').
    After rebuilding the add-in, restart EPLAN, because a loaded assembly cannot be unloaded (api-notes A8). Product installs will use 'eplanmcp install-addin' (M5) instead of this script.
    Each EPLAN version has its own add-in build (docs/design/2026-11-19-per-version-bridge-builds.md): -EplanVersion
    picks the build in bin\<configuration>\net481\eplan<year> and, without -EplanBin, the installation of that version.

.EXAMPLE
    eng\scripts\eplan-addin.ps1 -Action register

.EXAMPLE
    eng\scripts\eplan-addin.ps1 -Action register -EplanVersion 2025 -Variant 'Electric P8'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('register', 'unregister')]
    [string] $Action,

    [string] $Variant = 'Pro Panel',

    # The EPLAN version year, as for dotnet build -p:EplanVersion=<year>.
    [ValidatePattern('^\d{4}$')]
    [string] $EplanVersion = '2026',

    # Default: the Bin folder of the single <Program Files>\EPLAN\Platform\<EplanVersion>.* installation.
    [string] $EplanBin,

    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
if (-not $EplanBin) {
    $platforms = @(Get-ChildItem (Join-Path $env:ProgramFiles 'EPLAN\Platform') -Directory -Filter "$EplanVersion.*" -ErrorAction SilentlyContinue)
    if ($platforms.Count -ne 1) { throw "Found $($platforms.Count) EPLAN $EplanVersion installations under $env:ProgramFiles\EPLAN\Platform; pass -EplanBin." }
    $EplanBin = Join-Path $platforms[0].FullName 'Bin'
}
$eplan = Join-Path $EplanBin 'EPLAN.exe'
if (-not (Test-Path $eplan)) { throw "EPLAN.exe not found in $EplanBin" }

if ($Action -eq 'register') {
    $dll = Join-Path $PSScriptRoot "..\..\src\EplanMcp.Bridge.Eplan\bin\$Configuration\net481\eplan$EplanVersion\EplanMcp.Bridge.Eplan.dll"
    if (-not (Test-Path $dll)) { throw "Build the add-in first: dotnet build src\EplanMcp.Bridge.Eplan -c $Configuration -p:EplanVersion=$EplanVersion" }
    $dll = (Resolve-Path $dll).Path
    & $eplan "/Variant:$Variant" 'EplApiModuleAction' "/register:$dll"
}
else {
    & $eplan "/Variant:$Variant" 'EplApiModuleAction' '/unregister:EplanMcp.Bridge.Eplan'
}
