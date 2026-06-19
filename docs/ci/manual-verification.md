# Manual and Auxiliary Verification

Scripts outside required CI that validate user-visible or release-critical behavior.
Authoritative exclusions list:
[`specs/003-improve-testing-cicd/contracts/protected-domain-coverage.md`](../../specs/003-improve-testing-cicd/contracts/protected-domain-coverage.md).

## Required automation (for reference)

| Workflow | CI job | Local command |
|----------|--------|---------------|
| Headless integration | `Integration tests (*)` | `dotnet run --project src/Autonocraft -c Release -- --test` |
| Agent HTTP API | `Agent E2E (Linux)` | `USE_XVFB=1 scripts/ci_e2e.sh` (Linux CI) / `scripts/ci_e2e.sh` (local display) |
| Live API suite | via `ci_e2e.sh` | `.cursor/skills/autonocraft-game-test/scripts/test_live_api.py` |
| JSON scenarios | via `ci_e2e.sh` | `.cursor/skills/autonocraft-game-test/scripts/run_scenario.py --all` |

## Manual-only scripts

| Script | Owner | When to run | Rationale |
|--------|-------|-------------|-----------|
| `tests/verify_terrain_slabs.py` | Rendering maintainers | Before terrain/slab rendering PRs | Requires visible game window |
| `tests/capture_structure_gallery.py` | Art / structures | Gallery asset refresh | Visual capture for review |
| `tests/interact.py` | Contributors | Ad-hoc HTTP debugging | CLI helper, not a test suite |
| `tests/live_villager_e2e.py` | Village maintainers | Deep villager simulation smoke (optional) | **Deprecated** — overlaps `test_live_api.py` village checks; unique lumber/recruit/build flow kept for manual diagnosis only |

## Deprecated: `tests/live_villager_e2e.py`

Superseded by required CI path `scripts/ci_e2e.sh` → `test_live_api.py` + `run_scenario.py`.
Starter village presence, population, and field checks are covered by `test_live_api.py`.
The legacy script retains a longer lumber → recruit → farm_plot build flow for manual
debugging when CI E2E is insufficient.

## Deferred platform coverage

| Gap | Status | Workaround |
|-----|--------|------------|
| Agent E2E on Windows CI | Deferred | Run `scripts/ci_e2e.ps1` locally on Windows before agent PRs |
| Agent E2E on macOS CI | Deferred | Run `scripts/ci_e2e.sh` locally on macOS; never set `DYLD_LIBRARY_PATH` on GitHub Actions macOS runners |

## Visual / screenshot checks

Use the game-test skill scenarios or:

```bash
dotnet run --project src/Autonocraft -- --skip-menu
python3 tests/interact.py wait
python3 tests/interact.py screenshot my_view.png
```

Not required on every PR unless changing HUD, layout, or rendering.
