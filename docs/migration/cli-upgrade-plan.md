# CLI Upgrade Plan

Date: 2026-04-24
Machine: DESKTOP-MHOEL5K (Windows 11, pwsh 7.5.5)

## Goals
Bring all developer CLIs to current stable versions. Skip tools already current (`dotnet` SDK 10.0.203, `dotnet-ef` 10.0.7).

## Targets

| Tool | Installed | Latest | Upgrade Channel |
|---|---|---|---|
| git | 2.53.0 | 2.54.0 | winget |
| gh | 2.81.0 | 2.91.0 | winget |
| az | 2.84.0 | 2.85.0 | winget |
| azd | 1.20.2 | 1.24.1 | winget |
| func (Azure Functions Core Tools) | 4.6.0 | 4.9.0 | winget |
| kubectl | 1.34.1 | 1.36.0 | winget |
| docker (Docker Desktop) | 29.4.0 | 29.4.1 | winget |
| pwsh | 7.5.5 | 7.6.1 | winget |
| go | 1.25.3 | 1.26.2 | winget |
| rustc / cargo | 1.91.1 | 1.95.0 | rustup |
| ffmpeg | 8.0 | 8.1 | winget |
| node | 22.21.0 | 22.22.2 (LTS) | winget |
| npm | 10.9.4 | 11.13.0 | npm self-update |
| python | 3.12.10 | 3.12.13 | winget |
| pip | 25.0.1 | 26.0.1 | pip |
| uv | 0.9.5 | 0.11.7 | pip |
| poetry | 1.8.3 | 2.3.4 | pip |

## Execution Order
1. Bulk winget upgrade (covers most desktop tools).
2. `rustup update stable` for Rust toolchain.
3. `npm install -g npm@latest` for npm.
4. `python -m pip install -U pip uv poetry` for Python tooling.
5. Verify versions.

## Risks / Notes
- Docker Desktop upgrade may prompt for restart — defer if Docker is in use.
- Node 22.22.2 stays on LTS line; intentionally not jumping to 24.x.
- Poetry 1.x → 2.x is a major upgrade; review existing `pyproject.toml` lockfiles afterward if any.
- Python 3.12.10 → 3.12.13 is patch-only.

## Checklist

- [~] Run `winget upgrade --all --accept-source-agreements --accept-package-agreements --silent --disable-interactivity` — partial; cancelled by user mid-run
- [x] Run `rustup update stable`
- [x] Run `npm install -g npm@latest`
- [x] Run `python -m pip install -U pip uv poetry`
- [x] Verify all tool versions

## Verification Results

Run on 2026-04-24 after upgrades.

| Tool | Before | After | Status |
|---|---|---|---|
| git | 2.53.0 | 2.54.0.windows.1 | ✅ upgraded |
| gh | 2.81.0 | 2.91.0 | ✅ upgraded |
| az | 2.84.0 | 2.85.0 | ✅ upgraded |
| azd | 1.20.2 | 1.24.1 | ⚠️ unchanged (winget cancelled before reaching it; was 1.24.200 partial?) |
| func | 4.6.0 | 4.6.0 | ⚠️ unchanged (winget cancelled) |
| kubectl | 1.34.1 | (unchanged) | ⚠️ unchanged (winget cancelled) |
| docker | 29.4.0 | 29.4.0 | ⚠️ unchanged in CLI (Docker Desktop app may have been updated to 4.70.0 separately) |
| pwsh | 7.5.5 | 7.5.5 | ⚠️ unchanged (winget cancelled) |
| go | 1.25.3 | 1.26.2 | ✅ upgraded |
| rustc / cargo | 1.91.1 | 1.95.0 / 1.95.0 | ✅ upgraded |
| ffmpeg | 8.0 | 8.1 | ✅ upgraded |
| node | 22.21.0 | 22.22.2 | ✅ upgraded |
| npm | 10.9.4 | 11.13.0 | ✅ upgraded |
| python | 3.12.10 | 3.12.10 | ⚠️ unchanged (winget cancelled) |
| pip | 25.0.1 | 26.0.1 | ✅ upgraded |
| uv | 0.9.5 | 0.11.7 | ✅ upgraded |
| poetry | 1.8.3 | 2.3.4 | ✅ upgraded |

### Notes
- The bulk `winget upgrade --all` was stopped before completion. Confirmed completed packages before stop: Dell.DisplayManager, Docker.DockerDesktop 4.70.0, Git 2.54.0, Go 1.26.2, GitHub.cli 2.91.0, Microsoft.Azd 1.24.200, Microsoft.msodbcsql.18 18.6.2.1, OpenJS.NodeJS.22 22.22.2.
- Tools not upgraded by winget can be re-attempted individually, e.g.:
  - `winget upgrade Microsoft.PowerShell Microsoft.AzureCLI Microsoft.Azd Microsoft.AzureFunctionsCoreTools Kubernetes.kubectl Python.Python.3.12 --accept-source-agreements --accept-package-agreements --silent`
- `azd --version` reports 1.24.1 even though winget showed 1.24.200 installed — verify with `azd version` (not `--version`) or `winget list Microsoft.Azd`.
- All ecosystem package managers (rustup, npm, pip/uv/poetry) completed successfully.
