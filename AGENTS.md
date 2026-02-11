# AGENTS.md

This file defines working rules for contributors and coding agents in this repository.
Scope: the entire repository rooted at this file.

## 1) Project Purpose

`GithubGet` is a WinUI 3 desktop app + core library + worker that:

- Subscribes to GitHub repositories.
- Detects latest releases and selects installable assets.
- Supports script-assisted install pipelines.
- Installs selected subscriptions on demand (manual batch install).

Current UX direction:

- Main page has separate sections for subscription management, software updates, and installed software.
- Repo search is shown in the subscription management section.
- Software updates and installed software are separate pages with independent layouts.
- Install/update is manual via explicit top actions, not automatic on add.

## 2) Repository Layout

- `src/GithubGet.App/`:
  WinUI 3 UI, view models, app services.
- `src/GithubGet.Core/`:
  Domain models, GitHub client, update and install pipeline, storage.
- `src/GithubGet.Worker/`:
  CLI worker for scheduled checks.
- `tests/GithubGet.Core.Tests/`:
  Unit tests for core behavior.
- `Build.ps1`:
  Local build/publish helper script.
- `GithubGet.slnx`:
  Preferred solution entrypoint.

## 3) Preferred Build/Test Commands

Use solution/project commands below as defaults:

- Build app:
  `dotnet build src/GithubGet.App/GithubGet.App.csproj -c Debug`
- Build core:
  `dotnet build src/GithubGet.Core/GithubGet.Core.csproj -c Debug`
- Run core tests:
  `dotnet test tests/GithubGet.Core.Tests/GithubGet.Core.Tests.csproj -c Debug`

When using solution-level commands, prefer:

- `dotnet build GithubGet.slnx`

## 4) UI Structure and Rules

### 4.1 Main Page Sections

Main page has three active sections:

- `SubscriptionsSection` (subscription management + repo search)
- `UpdatesSection` (software update list and actions)
- `InstalledSection` (installed/subscribed table view)
- `SettingsSection`

### 4.2 Installed Software Table

The installed software page list in `MainPage.xaml` should display:

- Selection checkbox
- Repo
- Owner
- Latest version
- Last updated date
- Update state

Data source:

- `SubscriptionsViewModel.VisibleSubscriptions` (`SubscriptionListItem`)

### 4.3 Repo Search Pane

Repo search results should remain in the right pane of the subscription management page.
Search should continue to filter out repositories without release data.

## 5) Core Update/Install Behavior

### 5.1 Add Subscription

Adding a subscription should:

1. Persist subscription.
2. Fetch latest release metadata (if any).
3. Store release metadata/update event for visibility.
4. **Not** automatically run install pipeline.

### 5.2 Manual Install

Install should happen only through explicit user action (`InstallSelectedSubscriptions`).
Batch install must:

- Resolve latest release.
- Select asset via `AssetSelector`.
- Run `InstallCoordinator`.
- Persist update events and subscription metadata.
- Report success/failure/skip counts.

### 5.3 Rate Limit UX

GitHub API rate limit handling should:

- Detect limit events in core (`GitHubClient`).
- Surface clear user messaging in app.
- Include PAT hint when token is missing.

## 6) Persistence and Models

Keep these model responsibilities stable:

- `Subscription`: source-of-truth per repo configuration.
- `UpdateEvent`: historical update/install event log.
- `SubscriptionListItem`: UI projection (checkbox + display fields).

Do not put transient UI-only state into core models.

## 7) Coding Conventions

- Keep changes local and minimal.
- Prefer explicit names over abbreviations.
- Avoid one-letter variables.
- Avoid inline comments unless the logic is genuinely non-obvious.
- Keep WinUI bindings simple and strongly typed (`x:Bind` where possible).

## 8) Safety and Non-Goals

- Do not use destructive git commands in normal workflows.
- Do not silently change install semantics.
- Do not reintroduce hidden auto-install side effects.
- Do not broaden scope to unrelated refactors unless requested.

## 9) Validation Checklist (Before Handoff)

At minimum, run:

1. `dotnet build src/GithubGet.App/GithubGet.App.csproj -c Debug`
2. `dotnet test tests/GithubGet.Core.Tests/GithubGet.Core.Tests.csproj -c Debug`

If UI XAML changed, also verify:

- No stale bindings remain.
- No removed section names remain referenced in code-behind.
- Navigation tags and visibility switching still align.

## 10) Common Extension Points

Use these when adding features:

- Repo/network behavior:
  `src/GithubGet.Core/Services/GitHubClient.cs`
- Update check orchestration:
  `src/GithubGet.Core/Services/UpdateChecker.cs`
- Install orchestration:
  `src/GithubGet.Core/Services/InstallCoordinator.cs`
- Installed-software page logic:
  `src/GithubGet.App/ViewModels/SubscriptionsViewModel.cs`
- Main page layout:
  `src/GithubGet.App/Views/MainPage.xaml`

## 11) Change Management Note

If requirements ask to split "Installed Software" and "Updates" again:

- Treat it as a product-level UX change.
- Add explicit acceptance criteria for:
  navigation labels, data ownership, and install trigger behavior.
