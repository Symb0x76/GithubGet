# ==============================================================================
# GithubGet 项目构建与测试脚本
# ==============================================================================
# 此脚本用于：
# 1. 清理项目（删除bin/obj目录）
# 2. 恢复NuGet依赖
# 3. 构建整个解决方案
# 4. 运行单元测试
# ==============================================================================

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("x86", "x64")]    #, "ARM64")]
    [string]$Platform = "x64",

    [switch]$Clean,
    [switch]$SkipTests,
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
$solutionPath = $PSScriptRoot
if (-not $solutionPath) {
    $solutionPath = Split-Path -Parent $MyInvocation.MyCommandPath
}
$solutionFile = Join-Path $solutionPath "GithubGet.slnx"
$solutionExtension = [System.IO.Path]::GetExtension($solutionFile)
$buildPlatform = if ($solutionExtension -ieq ".slnx") { "Any CPU" } else { $Platform }

Write-Host "===============================================" -ForegroundColor Green
Write-Host "GithubGet 项目构建脚本" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host "配置: $Configuration"
Write-Host "平台: $Platform"
Write-Host "构建平台: $buildPlatform"
Write-Host "路径: $solutionPath"
Write-Host ""

# 检查dotnet命令
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK 版本: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host "✗ 未找到 dotnet 命令，请确保已安装 .NET SDK" -ForegroundColor Red
    exit 1
}

# 第1步：清理
if ($Clean) {
    Write-Host ""
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host "步骤 1: 清理项目..." -ForegroundColor Cyan
    Write-Host "===============================================" -ForegroundColor Cyan

    try {
        & dotnet clean $solutionFile -c $Configuration /p:Platform="$buildPlatform" --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet clean 失败，退出码: $LASTEXITCODE" }
        Write-Host "✓ 清理完成" -ForegroundColor Green
    }
    catch {
        Write-Host "✗ 清理失败: $_" -ForegroundColor Red
        exit 1
    }
}

# 第2步：恢复NuGet包
if (-not $NoRestore) {
    Write-Host ""
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host "步骤 2: 恢复 NuGet 依赖..." -ForegroundColor Cyan
    Write-Host "===============================================" -ForegroundColor Cyan

    try {
        & dotnet restore $solutionFile --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore 失败，退出码: $LASTEXITCODE" }
        Write-Host "✓ NuGet 包恢复完成" -ForegroundColor Green
    }
    catch {
        Write-Host "✗ NuGet 恢复失败: $_" -ForegroundColor Red
        exit 1
    }
}

# 第3步：构建解决方案
Write-Host ""
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "步骤 3: 构建解决方案..." -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

try {
    & dotnet build $solutionFile -c $Configuration /p:Platform="$buildPlatform" --nologo --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet build 失败，退出码: $LASTEXITCODE" }
    Write-Host "✓ 构建成功" -ForegroundColor Green
}
catch {
    Write-Host "✗ 构建失败: $_" -ForegroundColor Red
    exit 1
}

# 第4步：运行单元测试
if (-not $SkipTests) {
    Write-Host ""
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host "步骤 4: 运行单元测试..." -ForegroundColor Cyan
    Write-Host "===============================================" -ForegroundColor Cyan

    $testProject = Join-Path $solutionPath "tests\GithubGet.Core.Tests\GithubGet.Core.Tests.csproj"

    if (Test-Path $testProject) {
        try {
            & dotnet test $testProject -c $Configuration --nologo --no-restore --logger "console;verbosity=normal"
            if ($LASTEXITCODE -ne 0) { throw "dotnet test 失败，退出码: $LASTEXITCODE" }
            Write-Host "✓ 测试完成" -ForegroundColor Green
        }
        catch {
            Write-Host "✗ 测试执行失败: $_" -ForegroundColor Red
            exit 1
        }
    }
    else {
        Write-Host "⊘ 未找到测试项目，跳过测试" -ForegroundColor Yellow
    }
}

# 完成
Write-Host ""
Write-Host "===============================================" -ForegroundColor Green
Write-Host "✓ 所有步骤完成!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host ""
Write-Host "构建输出位置:"
$appOutputPath = if ($buildPlatform -ieq "Any CPU") {
    "$solutionPath\src\GithubGet.App\bin\$Configuration"
}
else {
    "$solutionPath\src\GithubGet.App\bin\$Platform\$Configuration"
}
Write-Host "  - App: $appOutputPath"
Write-Host "  - Core: $solutionPath\src\GithubGet.Core\bin\$Configuration"
Write-Host "  - Worker: $solutionPath\src\GithubGet.Worker\bin\$Configuration"
Write-Host ""
