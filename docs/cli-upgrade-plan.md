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

- [ ] Run `winget upgrade --all --accept-source-agreements --accept-package-agreements`
- [ ] Run `rustup update stable`
- [ ] Run `npm install -g npm@latest`
- [ ] Run `python -m pip install -U pip uv poetry`
- [ ] Verify all tool versions

## Verification Results

_To be filled in after execution._
