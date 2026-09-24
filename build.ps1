<#
    Builds and packages The Mark of Oden for Thunderstore.

    Usage:
        .\build.ps1              # build + verify + validate + zip
        .\build.ps1 -Install     # also install a development copy into the Gale client profile
        .\build.ps1 -RemoveDev   # take the development copy out again

    Testing a change: switch the released mod off in Gale, run -Install, play. When the new
    version is published, run -RemoveDev and switch the released mod back on in Gale.
    The development copy lives in its own folder, PicSoul-TheMarkOfOden-DEV, and never touches
    the folder Gale manages - see devcopy.ps1 for why that matters.

    Thunderstore package versions are immutable: once a version is uploaded it can
    never be edited or replaced. Bump version_number in manifest.json AND <Version>
    in the .csproj AND PluginVersion in Plugin.cs before repackaging.
#>

[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$RemoveDev,
    [switch]$SkipPatchCheck,
    [string]$Profile = "1.0 Release Client Mods"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Fail($msg) { Write-Host "FAIL: $msg" -ForegroundColor Red; exit 1 }
function Ok($msg)   { Write-Host "  ok   $msg" -ForegroundColor Green }

. "$root\devcopy.ps1"
$devFolder = "PicSoul-TheMarkOfOden-DEV"
$devDll = "MarkOfOden.dll"

if ($RemoveDev) {
    Remove-DevCopy -ProfileName $Profile -FolderName $devFolder -DllName $devDll
    Write-Host "`ndone.`n" -ForegroundColor Cyan
    exit 0
}

# ---------------------------------------------------------------- version sync
$manifest = Get-Content "$root\manifest.json" -Raw | ConvertFrom-Json
$csproj = [xml](Get-Content "$root\src\MarkOfOden\MarkOfOden.csproj")
$csprojVersion = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Write-Host "`nTheMarkOfOden $($manifest.version_number)" -ForegroundColor Cyan

if ($csprojVersion -ne $manifest.version_number) {
    Fail "version mismatch: manifest.json says $($manifest.version_number), csproj says $csprojVersion"
}

# The BepInPlugin attribute needs a compile-time constant, so the version is
# declared a third time in Plugin.cs. Catch it drifting.
$pluginSource = Get-Content "$root\src\MarkOfOden\Plugin.cs" -Raw
if ($pluginSource -notmatch 'PluginVersion\s*=\s*"([^"]+)"') { Fail "could not find PluginVersion in Plugin.cs" }
if ($Matches[1] -ne $manifest.version_number) {
    Fail "version mismatch: manifest.json says $($manifest.version_number), Plugin.cs says $($Matches[1])"
}
Ok "version $($manifest.version_number) matches in manifest.json, csproj and Plugin.cs"

# The changelog should mention the version being packaged.
$changelog = Get-Content "$root\CHANGELOG.md" -Raw
if ($changelog -notmatch [regex]::Escape("## $($manifest.version_number)")) {
    Fail "CHANGELOG.md has no '## $($manifest.version_number)' section"
}
Ok "CHANGELOG.md documents this version"

# ---------------------------------------------------------------------- build
Write-Host "`nbuilding..." -ForegroundColor Cyan
$buildLog = & dotnet build "$root\MarkOfOden.sln" -c Release -v minimal --nologo 2>&1
if ($LASTEXITCODE -ne 0) { $buildLog; Fail "build failed" }
Ok "compiled"

$dll = "$root\src\MarkOfOden\bin\Release\MarkOfOden.dll"
if (-not (Test-Path $dll)) { Fail "expected output missing: $dll" }

# ------------------------------------------------------- verify patch targets
# Every Harmony target named by string is checked against the installed game, since
# the compiler cannot check those and a missing one throws at startup, taking the
# whole mod down with it. See tools/PatchCheck.
if (-not $SkipPatchCheck) {
    Write-Host "`nverifying patch targets against the installed game..." -ForegroundColor Cyan

    $props = @{}
    if (Test-Path "$root\Local.props") {
        $local = [xml](Get-Content "$root\Local.props")
        foreach ($pg in $local.Project.PropertyGroup) {
            foreach ($node in $pg.ChildNodes) { if ($node.NodeType -eq "Element") { $props[$node.Name] = $node.InnerText } }
        }
    }
    $valheim = $props["ValheimInstall"]
    if (-not $valheim) { $valheim = $env:VALHEIM_INSTALL }
    if (-not $valheim) { $valheim = "C:\Program Files (x86)\Steam\steamapps\common\Valheim" }

    $bepinex = $props["BepInExCore"]
    if ($bepinex) { $bepinex = $bepinex.Replace('$(AppData)', $env:APPDATA) }
    if (-not $bepinex) { $bepinex = $env:BEPINEX_CORE }
    if (-not $bepinex) { $bepinex = "$valheim\BepInEx\core" }

    $checkOutput = & dotnet run --project "$root\tools\PatchCheck\patchcheck.csproj" -c Release -- `
        $dll "$valheim\valheim_Data\Managed" $bepinex 2>&1
    if ($LASTEXITCODE -ne 0) { $checkOutput; Fail "one or more patch targets do not exist in this build of Valheim" }
    Ok ($checkOutput | Select-String "patch targets checked" | Select-Object -First 1).ToString().Trim()
}

# --------------------------------------------------------------------- stage
$stage = "$root\package"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item $dll $stage
foreach ($f in @("manifest.json", "icon.png", "README.md", "CHANGELOG.md", "LICENSE")) {
    if (Test-Path "$root\$f") { Copy-Item "$root\$f" $stage }
}

# ------------------------------------------------------------------ validate
Write-Host "`nvalidating against Thunderstore rules..." -ForegroundColor Cyan

foreach ($required in @("manifest.json", "icon.png", "README.md")) {
    if (-not (Test-Path "$stage\$required")) { Fail "$required missing from package root" }
}
Ok "manifest.json, icon.png, README.md present at package root"

if ($manifest.name -notmatch '^[a-zA-Z0-9_]+$') { Fail "name '$($manifest.name)' has illegal characters (allowed: a-z A-Z 0-9 _)" }
if ($manifest.name.Length -gt 128)              { Fail "name exceeds 128 characters" }
Ok "name '$($manifest.name)' is valid"

if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') { Fail "version_number must be Major.Minor.Patch" }
Ok "version_number is valid semver"

if ($manifest.description.Length -gt 250) { Fail "description is $($manifest.description.Length) chars (max 250)" }
Ok "description is $($manifest.description.Length)/250 chars"

if ($null -eq $manifest.website_url) { Fail "website_url must be present (use an empty string if unused)" }
Ok "website_url present"

foreach ($dep in $manifest.dependencies) {
    if ($dep -notmatch '^[a-zA-Z0-9_]+-[a-zA-Z0-9_]+-\d+\.\d+\.\d+$') {
        Fail "dependency '$dep' is not in {team}-{package}-{major.minor.patch} form"
    }
}
Ok "$($manifest.dependencies.Count) dependency string(s) well-formed"

Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile("$stage\icon.png")
$w = $img.Width; $h = $img.Height
$img.Dispose()
if ($w -ne 256 -or $h -ne 256) { Fail "icon.png is ${w}x${h}, must be exactly 256x256" }
Ok "icon.png is 256x256"

try { [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes("$stage\README.md")) | Out-Null }
catch { Fail "README.md is not valid UTF-8" }
Ok "README.md is UTF-8"

# ---------------------------------------------------------------------- zip
$zip = "$root\$($manifest.name)-$($manifest.version_number).zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Ok "packaged $(Split-Path $zip -Leaf) ($([math]::Round((Get-Item $zip).Length / 1KB, 1)) KB)"

# ------------------------------------------------------------------- install
if ($Install) {
    Install-DevCopy -ProfileName $Profile -FolderName $devFolder -DllName $devDll `
        -Stage $stage -Version $manifest.version_number
}

Write-Host "`ndone.`n" -ForegroundColor Cyan
