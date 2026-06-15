# Game Test Examples

## Visual regression after rendering change

```bash
./run.sh --skip-menu &
python3 tests/interact.py wait
python3 tests/interact.py screenshot before.png
python3 tests/interact.py action set_time value=0.25
python3 tests/interact.py screenshot dusk.png
python3 tests/interact.py action dev cmd_line="time noon"
python3 tests/interact.py screenshot noon.png
```

Compare PNGs for lighting, chunk mesh, or HUD differences.

## Mine a targeted block

```bash
python3 tests/interact.py wait
python3 tests/interact.py action dev cmd_line="give tool pickaxe stone"
python3 tests/interact.py action select_slot slot=0
python3 tests/interact.py action set_creative creative=false
python3 tests/interact.py action teleport x=16 y=65 z=16

# Aim down at ground (adjust pitch/yaw from state)
python3 tests/interact.py action set_look yaw=0 pitch=45
python3 tests/interact.py state   # confirm targetBlock is set

# Hold left click ~2s (repeat click or use scenario move+click loop)
python3 tests/interact.py action click button=left
sleep 1
python3 tests/interact.py action click button=left
python3 tests/interact.py state   # check breakProgress
```

## Crafting station smoke test

```bash
python3 tests/interact.py action dev cmd_line="give OakLog 64"
python3 tests/interact.py action dev cmd_line="unlock recipe:plank"
# Teleport near a Crucible or place one via dev if available
python3 tests/interact.py action open_crucible
python3 tests/interact.py screenshot crucible_ui.png
```

## Village steward chat

```bash
python3 tests/interact.py state | grep playWithAi   # must be true
python3 tests/interact.py village_chat "Summarize village food and villagers"
```

## JSON scenario: full explore pass

```bash
python3 .cursor/skills/autonocraft-game-test/scripts/run_scenario.py \
  .cursor/skills/autonocraft-game-test/examples/explore.json
```

## Custom Python script

```python
#!/usr/bin/env python3
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] /
    ".cursor/skills/autonocraft-game-test/scripts"))
from game_client import GameClient

def main():
    g = GameClient()
    g.wait_ready(timeout=120)
    g.screenshot("step0.png")
    g.dev("fly on")
    g.teleport(32, 75, 32)
    g.set_look(180, -30)
    g.screenshot("step1.png")
    g.move(forward=3.0)
    g.release_keys()
    g.shutdown()

if __name__ == "__main__":
    main()
```
