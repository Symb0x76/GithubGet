# GithubGet

`GithubGet` 是一个基于 WinUI 3 的 Windows 桌面应用，用于订阅 GitHub 仓库 Release、筛选可安装资产，并按需执行批量安装。

## 功能概览

- 订阅 GitHub 仓库并跟踪最新 Release
- 按规则选择安装资产（扩展名、架构、关键词等）
- 支持预安装 PowerShell 脚本（项目级）
- 软件更新与已安装软件分离视图
- 手动勾选后批量安装（不自动安装）
- Worker 支持一次性检查与通知

## 项目结构

- `src/GithubGet.App`：WinUI 3 桌面应用
- `src/GithubGet.Core`：核心模型、GitHub 客户端、更新/安装编排、存储
- `src/GithubGet.Worker`：命令行 Worker（计划任务场景）
- `tests/GithubGet.Core.Tests`：Core 单元测试
- `Build.ps1`：本地构建脚本
- `GithubGet.slnx`：首选解决方案入口

## 环境要求

- Windows 10/11
- .NET SDK 8.x
- Windows App SDK 运行时（开发机构建通常会自动满足）

## 本地开发

### 1) 一键构建（推荐）

```powershell
.\Build.ps1 -Configuration Debug
```

### 2) 手动命令

```powershell
dotnet build GithubGet.slnx -c Debug
dotnet test tests/GithubGet.Core.Tests/GithubGet.Core.Tests.csproj -c Debug
```

### 3) 运行 App

```powershell
dotnet run --project src/GithubGet.App/GithubGet.App.csproj -c Debug
```

### 4) 运行 Worker（单次检查）

```powershell
dotnet run --project src/GithubGet.Worker/GithubGet.Worker.csproj -- --run-once
```

可选参数：

- `--subscription <id>`：仅检查某个订阅
- `--no-toast`：跳过通知
- 环境变量 `GITHUBGET_TOKEN`：GitHub PAT（提高 API 速率上限）

## 数据目录

应用默认数据目录在：

`%LOCALAPPDATA%\GithubGet`

常见内容：

- `githubget.db`：SQLite 数据库
- `cache\downloads`：下载缓存
- `scripts\projects\<owner>\<repo>\install.ps1`：项目级安装脚本
- `logs`：日志

## 自动编译与发布（GitHub Actions）

仓库内置两个工作流：

- `ci.yml`：在 `push` / `pull_request` 时执行构建与测试
- `release.yml`：在推送 tag（如 `v1.2.0`）时自动打包并发布 Release

### 发布流程

1. 推送语义化版本 tag：

```powershell
git tag v1.0.0
git push origin v1.0.0
```

2. Actions 会自动：
   - 构建与测试
   - 发布 `win-x64` / `win-x86` / `win-arm64`
   - 生成按 commit 类型分类的版本变更日志（新功能、修复、重构等）
   - 生成 Portable ZIP、MSI、EXE 安装包并上传到 GitHub Release

产物名称示例：

- `GithubGet-v1.0.0-win64-portable.zip`
- `GithubGet-v1.0.0-win32-portable.zip`
- `GithubGet-v1.0.0-arm-portable.zip`
- `GithubGet-v1.0.0-win64.msi`
- `GithubGet-v1.0.0-win32.msi`
- `GithubGet-v1.0.0-arm.msi`
- `GithubGet-v1.0.0-win64.exe`
- `GithubGet-v1.0.0-win32.exe`
- `GithubGet-v1.0.0-arm.exe`

### 安装方式

Release 会附带 `INSTALLERS.md`，并支持：

- Portable ZIP（解压后根目录直接运行 `GithubGet.App.exe`）
- MSI（适合企业部署）
- EXE 安装包（向导式安装）

常见命令（示例：x64）：

```powershell
msiexec /i .\GithubGet-v1.0.0-win64.msi
```

或直接运行 EXE 安装器：

```powershell
.\GithubGet-v1.0.0-win64.exe
```
