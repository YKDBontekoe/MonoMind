# Quickstart: Revamped Main Menu UI Validation

## Prerequisites

- .NET 10 SDK installed
- Working tree contains the main menu revamp changes

## 1. Run Required Integration Tests

```bash
dotnet run --project src/Autonocraft -- --test
```

Expected result:

```text
ALL TESTS PASSED SUCCESSFULLY! (EXIT CODE: 0)
```

Mandatory because this feature touches UI, settings persistence, save/load entry
paths, and navigation.

Focused menu tests (added with this feature):

- Menu navigation layer transitions (RootHub ↔ SaveBrowser ↔ overlays)
- Settings cancel does not mutate saved settings
- Layout bounds at 1280×720 and 800×600
- Continue eligibility when fixture saves exist

Existing test that must remain green:

- `RunGameSettingsRoundTrip` (settings.json round-trip)

Optional focused filter after `MenuTests` exists:

```bash
dotnet test tests/Autonocraft.Tests -c Release --filter "FullyQualifiedName~Menu"
```

## 2. Manual Root Hub Walkthrough

Terminal:

```bash
dotnet run --project src/Autonocraft
```

Verify:

1. **Root Hub** shows title, tagline, and primary actions (Continue if saves exist,
   Play/Browse, New World, Settings, Quit).
2. **Keyboard only**: Tab or arrow keys + Enter reach every primary action.
3. **Escape** on Root Hub does not quit silently; Quit button is visible and works.
4. **Settings** opens overlay; change render distance; Save; reopen and confirm
   persistence.
5. **Settings** cancel: change a slider; Back/Escape; reopen — values unchanged.
6. **Play/Browse** opens save browser; **Back** returns to Root Hub.

See [menu-ui-contract.md](contracts/menu-ui-contract.md) for required behaviors.

## 3. Manual Save Browser Walkthrough

From Root Hub → Play/Browse (or if implementation lands directly on browser for
returning players, verify list is refreshed):

1. Select a save — detail pane shows name and summary stats.
2. **Load** enters world; exit to main menu — slot list refreshed.
3. **New World** → pick world type and seed → Create → loading → gameplay.
4. **Delete** — first click arms confirmation; second click deletes; cancel path
   clears confirmation.
5. **Rename** — edit name, Enter confirms, Escape cancels.
6. **Structure Gallery** — loads gallery world (same as pre-revamp behavior).
7. Induce or simulate load failure — inline error appears with guidance.

## 4. Resolution Layout Check

Run windowed at **1280×720** and **800×600** (resize or CI default):

- No overlapping primary buttons or clipped titles on Root Hub, Save Browser,
  Settings, and New World Setup.
- All footer actions reachable (scroll if implemented on small height).

## 5. Bypass Paths (Regression)

Verify automation entry points unchanged:

```bash
dotnet run --project src/Autonocraft -- --skip-menu
```

Expected: immediate world load, no Root Hub blocking.

```bash
dotnet run --project src/Autonocraft -- --structure-gallery --agent-port 5001
```

Expected: structure gallery world loads; agent port available per AGENTS.md.

## 6. Transition Timing Check

Step through: Root Hub → Save Browser → New World → Back → Settings → Back →
Load.

Expected: each transition completes in under one second of blocking animation
(SC-007).

## Success Checklist

| Criterion | How to verify |
|-----------|---------------|
| SC-001 | Primary actions within two interactions from Root Hub |
| SC-002 | Keyboard-only walkthrough completes |
| SC-003 | Layout pass at 800×600 and 1280×720 |
| SC-004 | Ten-trial save/settings/delete flows without data loss |
| SC-006 | `--test` exit code 0; bypass CLIs unchanged |
| SC-007 | Subjective <1s transitions on standard navigation |

## References

- [spec.md](../spec.md) — user stories and requirements
- [data-model.md](../data-model.md) — navigation state and entities
- [menu-navigation-contract.md](contracts/menu-navigation-contract.md) — layer rules for tests
