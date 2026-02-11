param(
    [string]$OutputRoot = "artifacts",
    [string[]]$RuntimeIdentifiers = @("win-x64", "win-x86", "win-arm64")
)

$ErrorActionPreference = "Stop"

function Get-ArtifactArchTag {
    param([string]$Rid)

    switch ($Rid) {
        "win-x64" { return "win64" }
        "win-x86" { return "win32" }
        "win-arm64" { return "arm" }
        default { throw "Unsupported runtime identifier: $Rid" }
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$sln = Join-Path $repoRoot "GithubGet.slnx"
$appProject = Join-Path $repoRoot "src\GithubGet.App\GithubGet.App.csproj"
$workerProject = Join-Path $repoRoot "src\GithubGet.Worker\GithubGet.Worker.csproj"
$testProject = Join-Path $repoRoot "tests\GithubGet.Core.Tests\GithubGet.Core.Tests.csproj"
$configuration = "Release"

$outputRootPath = Join-Path $repoRoot $OutputRoot
$publishRoot = Join-Path $outputRootPath "publish"
$releaseRoot = Join-Path $outputRootPath "release"

if (Test-Path $publishRoot) {
    Remove-Item -Recurse -Force $publishRoot
}

if (Test-Path $releaseRoot) {
    Remove-Item -Recurse -Force $releaseRoot
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

dotnet restore $sln
dotnet build $sln -c $configuration --no-restore
dotnet test $testProject -c $configuration --no-restore

$versionTag = if ($env:GITHUB_REF_NAME) { $env:GITHUB_REF_NAME } else { "local" }

foreach ($rid in $RuntimeIdentifiers) {
    $artifactArch = Get-ArtifactArchTag -Rid $rid
    $appOut = Join-Path $publishRoot "GithubGet.App-$rid"
    $workerOut = Join-Path $publishRoot "GithubGet.Worker-$rid"
    $bundleDir = Join-Path $releaseRoot "GithubGet-$versionTag-$artifactArch-portable"
    $zipPath = "$bundleDir.zip"

    dotnet publish $appProject `
        -c $configuration `
        -r $rid `
        --self-contained true `
        -p:WindowsPackageType=None `
        -p:PublishSingleFile=false `
        -p:PublishReadyToRun=true `
        -p:PublishTrimmed=false `
        -o $appOut

    dotnet publish $workerProject `
        -c $configuration `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=true `
        -p:PublishTrimmed=false `
        -o $workerOut

    if (Test-Path $bundleDir) {
        Remove-Item -Recurse -Force $bundleDir
    }

    New-Item -ItemType Directory -Path $bundleDir -Force | Out-Null
    Copy-Item -Recurse -Force (Join-Path $appOut "*") $bundleDir

    $workerTarget = Join-Path $bundleDir "worker"
    New-Item -ItemType Directory -Path $workerTarget -Force | Out-Null
    Copy-Item -Recurse -Force (Join-Path $workerOut "*") $workerTarget

    $portableGuidePath = Join-Path $bundleDir "PORTABLE-INSTALL.txt"
    $portableGuide = @(
        "GithubGet Portable Package"
        ""
        "1. Run GithubGet.App.exe directly from this folder."
        "2. Optional worker tools are under .\worker\."
    )
    [System.IO.File]::WriteAllLines($portableGuidePath, $portableGuide, [System.Text.UTF8Encoding]::new($false))

    if (Test-Path $zipPath) {
        Remove-Item -Force $zipPath
    }

    Compress-Archive -Path (Join-Path $bundleDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
}

Write-Host "Release packages generated in: $releaseRoot"
