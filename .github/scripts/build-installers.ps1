param(
    [string]$OutputRoot = "artifacts",
    [string[]]$RuntimeIdentifiers = @("win-x64", "win-x86", "win-arm64"),
    [switch]$SkipMsi
)

$ErrorActionPreference = "Stop"

function Find-ToolPath {
    param(
        [string]$CommandName,
        [string[]]$Candidates
    )

    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    foreach ($candidate in $Candidates) {
        if ($candidate.Contains("*")) {
            $resolved = Get-ChildItem -Path $candidate -File -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($resolved) {
                return $resolved.FullName
            }
            continue
        }

        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Get-RidMetadata {
    param([string]$Rid)

    switch ($Rid) {
        "win-x64" {
            return @{
                WixPlatform       = "x64"
                ProgramFilesDir   = "ProgramFiles64Folder"
                InstallDirForNsis = '$PROGRAMFILES64\GithubGet'
                ArchLabel         = "x64"
                ArtifactTag       = "win64"
            }
        }
        "win-x86" {
            return @{
                WixPlatform       = "x86"
                ProgramFilesDir   = "ProgramFilesFolder"
                InstallDirForNsis = '$PROGRAMFILES\GithubGet'
                ArchLabel         = "x86"
                ArtifactTag       = "win32"
            }
        }
        "win-arm64" {
            return @{
                WixPlatform       = "x64"
                ProgramFilesDir   = "ProgramFiles64Folder"
                InstallDirForNsis = '$PROGRAMFILES64\GithubGet'
                ArchLabel         = "arm64"
                ArtifactTag       = "arm"
            }
        }
        default {
            throw "Unsupported runtime identifier: $Rid"
        }
    }
}

function Get-MsiVersion {
    param([string]$VersionTag)

    if ($VersionTag -match '^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:\.(?<revision>\d+))?$') {
        $major = [int]$Matches.major
        $minor = [int]$Matches.minor
        $patch = [int]$Matches.patch
        $revision = if ($Matches.revision) { [int]$Matches.revision } else { 0 }
        return "$major.$minor.$patch.$revision"
    }

    return "1.0.0.0"
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$outputRootPath = Join-Path $repoRoot $OutputRoot
$publishRoot = Join-Path $outputRootPath "publish"
$releaseRoot = Join-Path $outputRootPath "release"
$installersRoot = Join-Path $outputRootPath "installers"

if (-not (Test-Path $publishRoot)) {
    throw "Publish output not found: $publishRoot. Run publish-release.ps1 first."
}

if (Test-Path $installersRoot) {
    Remove-Item -Recurse -Force $installersRoot
}
New-Item -ItemType Directory -Path $installersRoot -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

$makensisExe = Find-ToolPath -CommandName "makensis.exe" -Candidates @(
    "C:\Program Files (x86)\NSIS\makensis.exe",
    "C:\Program Files (x86)\NSIS\Bin\makensis.exe",
    "C:\Program Files\NSIS\makensis.exe",
    "C:\Program Files\NSIS\Bin\makensis.exe"
)
if (-not $makensisExe) {
    throw "NSIS (makensis.exe) not found."
}

$heatExe = $null
$candleExe = $null
$lightExe = $null
if (-not $SkipMsi) {
    $heatExe = Find-ToolPath -CommandName "heat.exe" -Candidates @(
        "C:\Program Files (x86)\WiX Toolset v*\bin\heat.exe",
        "C:\Program Files\WiX Toolset v*\bin\heat.exe"
    )
    $candleExe = Find-ToolPath -CommandName "candle.exe" -Candidates @(
        "C:\Program Files (x86)\WiX Toolset v*\bin\candle.exe",
        "C:\Program Files\WiX Toolset v*\bin\candle.exe"
    )
    $lightExe = Find-ToolPath -CommandName "light.exe" -Candidates @(
        "C:\Program Files (x86)\WiX Toolset v*\bin\light.exe",
        "C:\Program Files\WiX Toolset v*\bin\light.exe"
    )

    if (-not $heatExe -or -not $candleExe -or -not $lightExe) {
        throw "WiX tools not found. Install WiX Toolset v3 or run with -SkipMsi."
    }
}

$versionTag = if ($env:GITHUB_REF_NAME) { $env:GITHUB_REF_NAME } else { "local" }
$msiVersion = Get-MsiVersion -VersionTag $versionTag

$upgradeCodes = @{
    "win-x64"   = "{F46185F9-C7D7-4E3E-9B03-840B5D4EBA11}"
    "win-x86"   = "{FB00E29D-9505-45A2-8EA4-01653314B7FC}"
    "win-arm64" = "{4B92F84E-2FE7-4375-A334-BEEFD4E09EE4}"
}

foreach ($rid in $RuntimeIdentifiers) {
    $metadata = Get-RidMetadata -Rid $rid
    $appSource = Join-Path $publishRoot "GithubGet.App-$rid"
    if (-not (Test-Path $appSource)) {
        throw "App publish folder missing for ${rid}: $appSource"
    }

    $ridRoot = Join-Path $installersRoot $rid
    New-Item -ItemType Directory -Path $ridRoot -Force | Out-Null

    if (-not $SkipMsi) {
        $wixRoot = Join-Path $ridRoot "wix"
        New-Item -ItemType Directory -Path $wixRoot -Force | Out-Null

        $wixProduct = Join-Path $wixRoot "Product.wxs"
        $wixHarvest = Join-Path $wixRoot "AppFiles.wxs"

        $productXml = @"
<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*" Name="GithubGet ($($metadata.ArchLabel))" Language="1033" Version="$msiVersion" Manufacturer="GithubGet" UpgradeCode="$($upgradeCodes[$rid])">
    <Package InstallerVersion="500" Compressed="yes" InstallScope="perMachine" Platform="$($metadata.WixPlatform)" />
    <MajorUpgrade DowngradeErrorMessage="A newer version of GithubGet is already installed." />
    <MediaTemplate EmbedCab="yes" />
    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="$($metadata.ProgramFilesDir)">
        <Directory Id="INSTALLFOLDER" Name="GithubGet" />
      </Directory>
    </Directory>
    <Feature Id="MainFeature" Title="GithubGet" Level="1">
      <ComponentGroupRef Id="AppFiles" />
    </Feature>
  </Product>
</Wix>
"@
        [System.IO.File]::WriteAllText($wixProduct, $productXml, [System.Text.UTF8Encoding]::new($false))

        & $heatExe dir $appSource -cg AppFiles -dr INSTALLFOLDER -gg -g1 -srd -scom -sreg -var "var.SourceDir" -out $wixHarvest
        if ($LASTEXITCODE -ne 0) {
            throw "heat.exe failed for $rid"
        }

        if ($rid -ne "win-x86") {
            $harvestContent = [System.IO.File]::ReadAllText($wixHarvest)
            $harvestContent = $harvestContent.Replace('<Component Id="', '<Component Win64="yes" Id="')
            [System.IO.File]::WriteAllText($wixHarvest, $harvestContent, [System.Text.UTF8Encoding]::new($false))
        }

        & $candleExe -nologo -dSourceDir="$appSource" -out (Join-Path $wixRoot "\") $wixProduct $wixHarvest
        if ($LASTEXITCODE -ne 0) {
            throw "candle.exe failed for $rid"
        }

        $msiPath = Join-Path $releaseRoot "GithubGet-$versionTag-$($metadata.ArtifactTag).msi"
        & $lightExe -nologo -out $msiPath (Join-Path $wixRoot "Product.wixobj") (Join-Path $wixRoot "AppFiles.wixobj")
        if ($LASTEXITCODE -ne 0) {
            throw "light.exe failed for $rid"
        }
    }

    $setupExePath = Join-Path $releaseRoot "GithubGet-$versionTag-$($metadata.ArtifactTag).exe"
    $uninstallRegKey = "Software\Microsoft\Windows\CurrentVersion\Uninstall\GithubGet_$($metadata.ArtifactTag)"
    $nsisScriptPath = Join-Path $ridRoot "GithubGet-setup.nsi"

    $nsisTemplate = @'
Unicode true
!include "MUI2.nsh"

Name "GithubGet"
OutFile "__OUTFILE__"
InstallDir "__INSTALLDIR__"
RequestExecutionLevel admin

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

Section "Install"
  SetOutPath "$INSTDIR"
  File /r "__APP_SOURCE__\*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "__UNINSTALL_REG_KEY__" "DisplayName" "GithubGet"
  WriteRegStr HKLM "__UNINSTALL_REG_KEY__" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "__UNINSTALL_REG_KEY__" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "__UNINSTALL_REG_KEY__" "DisplayVersion" "__VERSION__"
  CreateDirectory "$SMPROGRAMS\GithubGet"
  CreateShortcut "$SMPROGRAMS\GithubGet\GithubGet.lnk" "$INSTDIR\GithubGet.App.exe"
  CreateShortcut "$DESKTOP\GithubGet.lnk" "$INSTDIR\GithubGet.App.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\GithubGet.lnk"
  Delete "$SMPROGRAMS\GithubGet\GithubGet.lnk"
  RMDir "$SMPROGRAMS\GithubGet"
  DeleteRegKey HKLM "__UNINSTALL_REG_KEY__"
  RMDir /r "$INSTDIR"
SectionEnd
'@

    $nsisScript = $nsisTemplate
    $nsisScript = $nsisScript.Replace("__OUTFILE__", $setupExePath)
    $nsisScript = $nsisScript.Replace("__INSTALLDIR__", $metadata.InstallDirForNsis)
    $nsisScript = $nsisScript.Replace("__APP_SOURCE__", $appSource)
    $nsisScript = $nsisScript.Replace("__UNINSTALL_REG_KEY__", $uninstallRegKey)
    $nsisScript = $nsisScript.Replace("__VERSION__", $msiVersion)

    [System.IO.File]::WriteAllText($nsisScriptPath, $nsisScript, [System.Text.UTF8Encoding]::new($false))

    & $makensisExe "/V2" $nsisScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw "makensis failed for $rid"
    }

    if (-not (Test-Path $setupExePath)) {
        throw "Expected NSIS installer not found: $setupExePath"
    }
}

$installerGuide = Join-Path $releaseRoot "INSTALLERS.md"
$guideLines = @(
    "# Installer Packages",
    "",
    "This release includes:",
    "- Portable ZIP packages (run `GithubGet.App.exe` directly).",
    "- EXE installers built by NSIS.",
    "- MSI installers for enterprise/manual deployment.",
    "",
    "## Installation",
    "1. EXE: run `GithubGet-<version>-win64.exe`.",
    "2. MSI: run `msiexec /i GithubGet-<version>-win64.msi`.",
    "3. Portable: unzip and run `GithubGet.App.exe` from the root folder."
)
[System.IO.File]::WriteAllLines($installerGuide, $guideLines, [System.Text.UTF8Encoding]::new($false))

Write-Host "Installer artifacts generated in: $releaseRoot"
