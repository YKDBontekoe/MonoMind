# Agent State Contract

## Purpose

Defines the additive state the agent HTTP view may expose so scripted tools can observe the improved villager onboarding flow.

## Contract

### Additive State Expectations

- The existing state payload remains valid for current consumers.
- Optional fields may describe the current next action, blocked reason, and villager activity summary.
- Any recruit failure surfaced through the agent path should provide a clear message and remediation hint.

### Suggested Fields

- `village.nextAction`
- `village.blockedReason`
- `villagers[].activity`
- `villagers[].needsAttention`
- recruit / assign result message fields when an action fails

### Compatibility Rules

- Existing fields must not be removed or renamed as part of this feature.
- Consumers that do not know about the new fields must continue to work.
- New fields must mirror the same plain-language meaning shown in the UI.
