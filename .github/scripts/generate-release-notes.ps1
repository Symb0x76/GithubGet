param(
    [string]$OutputPath = "artifacts/release/release-notes.md",
    [string]$CurrentTag
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
Set-Location $repoRoot

if (-not $CurrentTag -and $env:GITHUB_REF -like "refs/tags/*") {
    $CurrentTag = $env:GITHUB_REF_NAME
}

if ($CurrentTag) {
    $tagExists = git tag --list $CurrentTag
    if (-not $tagExists) {
        $CurrentTag = $null
    }
}

$previousTag = $null
if ($CurrentTag) {
    $candidate = git describe --tags --abbrev=0 "$CurrentTag^" 2>$null
    if ($candidate) {
        $previousTag = $candidate.Trim()
    }
}

$logRangeText = "HEAD"
$logArgs = @("HEAD")
if ($previousTag -and $CurrentTag) {
    $logRangeText = "$previousTag..$CurrentTag"
    $logArgs = @("$previousTag..$CurrentTag")
} elseif ($CurrentTag) {
    $logRangeText = "$CurrentTag (first release in history)"
    $logArgs = @($CurrentTag)
}

$logLines = git log @logArgs --pretty=format:"%H`t%s`t%an" --no-merges

$groupOrder = @(
    "Features",
    "Fixes",
    "Performance",
    "Refactors",
    "Docs",
    "Tests",
    "BuildAndCi",
    "Others"
)

$groups = @{
    Features = New-Object System.Collections.Generic.List[object]
    Fixes = New-Object System.Collections.Generic.List[object]
    Performance = New-Object System.Collections.Generic.List[object]
    Refactors = New-Object System.Collections.Generic.List[object]
    Docs = New-Object System.Collections.Generic.List[object]
    Tests = New-Object System.Collections.Generic.List[object]
    BuildAndCi = New-Object System.Collections.Generic.List[object]
    Others = New-Object System.Collections.Generic.List[object]
}

foreach ($line in $logLines) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    $parts = $line -split "`t", 3
    if ($parts.Count -lt 3) {
        continue
    }

    $commitHash = $parts[0]
    $subject = $parts[1].Trim()
    $author = $parts[2].Trim()
    $shortHash = $commitHash.Substring(0, [Math]::Min(7, $commitHash.Length))

    $commitType = $null
    $title = $subject
    $typeMatch = [regex]::Match($subject, "^(?<type>feat|fix|perf|refactor|docs|test|build|ci|chore)(\([^)]+\))?:\s*(?<title>.+)$", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($typeMatch.Success) {
        $commitType = $typeMatch.Groups["type"].Value.ToLowerInvariant()
        $title = $typeMatch.Groups["title"].Value.Trim()
    }

    $entry = [PSCustomObject]@{
        Subject = $title
        Author = $author
        ShortHash = $shortHash
    }

    switch ($commitType) {
        "feat" { $groups.Features.Add($entry); break }
        "fix" { $groups.Fixes.Add($entry); break }
        "perf" { $groups.Performance.Add($entry); break }
        "refactor" { $groups.Refactors.Add($entry); break }
        "docs" { $groups.Docs.Add($entry); break }
        "test" { $groups.Tests.Add($entry); break }
        "build" { $groups.BuildAndCi.Add($entry); break }
        "ci" { $groups.BuildAndCi.Add($entry); break }
        "chore" { $groups.BuildAndCi.Add($entry); break }
        default { $groups.Others.Add($entry); break }
    }
}

$displayTag = if ($CurrentTag) { $CurrentTag } else { "Unreleased" }
$nowUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'")

$content = New-Object System.Collections.Generic.List[string]
$content.Add("# $displayTag")
$content.Add("")
$content.Add("## 发布摘要")
$content.Add("- 生成时间: $nowUtc")
$content.Add('- 提交范围: `' + $logRangeText + '`')

if ($env:GITHUB_REPOSITORY -and $previousTag -and $CurrentTag) {
    $compareUrl = "https://github.com/$($env:GITHUB_REPOSITORY)/compare/$previousTag...$CurrentTag"
    $content.Add("- 对比链接: $compareUrl")
}

$sectionTitles = @{
    Features = "新功能"
    Fixes = "问题修复"
    Performance = "性能优化"
    Refactors = "代码重构"
    Docs = "文档更新"
    Tests = "测试相关"
    BuildAndCi = "构建与维护"
    Others = "其他变更"
}

foreach ($groupKey in $groupOrder) {
    $content.Add("")
    $content.Add("## $($sectionTitles[$groupKey])")
    $items = $groups[$groupKey]
    if ($items.Count -eq 0) {
        $content.Add("- 无")
        continue
    }

    foreach ($item in $items) {
        $content.Add('- ' + $item.Subject + ' (`' + $item.ShortHash + '`) @' + $item.Author)
    }
}

$outputFilePath = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
$outputDirectory = Split-Path -Parent $outputFilePath
if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

[System.IO.File]::WriteAllLines($outputFilePath, $content, [System.Text.UTF8Encoding]::new($false))
Write-Host "Release notes generated: $outputFilePath"
