# Quickstart: Early Game Polish

## Prerequisites

- Repo dependencies available for the current workspace
- A local build of `src/Autonocraft`
- Optional: `python3` for scripted agent validation

## Validation Commands

### 1. Run the required integration suite

```bash
dotnet run --project src/Autonocraft -- --test
```

Expected result:

- The command exits with code `0`
- The new-world startup, starter settlement, and opening guidance checks pass
- `EarlyGameTests`, `EarlyGameSpawnTests`, and `EarlyGamePresentationTests`
  report `PASSED`

### 2. Manually verify a fresh-world start

```bash
dotnet run --project src/Autonocraft
```

Expected result:

- The main menu opens normally
- Starting a new world shows a clear opening goal
- The starter settlement or equivalent opening landmark is visible or reachable near spawn
- The player can dismiss the opening guidance and continue playing
- A short plank path and lantern marker lead from spawn toward the Town Heart

### 3. Confirm the opening flow is readable in-game

While the world is running, use the in-game viewport and observe:

- The opening message is short and easy to understand
- The prompt does not block movement or interaction after dismissal
- The text remains readable at the supported window size you are using
- Returning to a world with completed early-guide progress does not replay the opening prompt

### 4. Verify the agent-facing regression path

```bash
python3 tests/interact.py wait
python3 tests/interact.py state
python3 tests/interact.py screenshot test_output/early-game-polish.png
```

Expected result:

- `/health` becomes ready once the game is playing
- `/state` still reports the expected early-game fields
- The screenshot shows a nonblank world start with the opening presentation visible if it is still active

### 5. Confirm returning-player behavior

Load the same save again and verify:

- The opening guidance does not repeat in a way that feels intrusive
- The save still loads into the same world state
- The player can continue directly into normal play

## Notes

- If the feature touches starter settlement placement, opening guidance, or
  world startup timing, rerun the required integration suite before concluding.
- Keep `--skip-menu` and `--structure-gallery` as regression checks even if the
  feature is focused on new-world starts.
