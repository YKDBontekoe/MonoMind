# Villager Onboarding UI Contract

## Purpose

Defines the player-facing behavior for the starter villager flow and the settlement management surface that supports it.

## Contract

### Required UI Outcomes

- The opening villager flow shows the current starter state, the next action, and any blocker in one place.
- The player can see whether they are founding, summoning settlers, or recruiting the next villager.
- The player can understand why an action is blocked without opening another screen.
- The UI remains readable and usable at supported window sizes.
- Reopening the flow always reflects the latest state.

### Required Interaction Outcomes

- Valid recruit or starter actions update counts immediately.
- Failed actions present a reason and a remediation hint on the same screen.
- Repeated clicks do not create duplicate villagers or duplicate UI state.
- The player can recover from partial or failed setup without restarting.

### Acceptance Notes

- Layout checks must cover the starter summary, primary action, roster list, and warning text.
- UI copy must use consistent language for villagers, settlers, recruitment, and starter settlement steps.
