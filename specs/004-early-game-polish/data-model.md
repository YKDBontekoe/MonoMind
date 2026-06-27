# Data Model: Early Game Polish

## Opening Guidance

Short, player-facing guidance shown during the first minutes of a new world.
This is primarily derived from existing progress and world state rather than a
new persistent entity.

**Fields**

- `headline`: Concise first-goal message shown to the player
- `detail`: Optional follow-up text that explains the next step
- `dismissible`: Whether the player can close the prompt and continue
- `active`: Whether the opening guidance should currently be shown

**Relationships**

- Driven by `PlayerStatistics.EarlyGuideStage`
- Contextualized by the active village, if one exists
- May reference the current starter milestone or nearby point of interest

**Validation Rules**

- Must not block normal movement or interaction after dismissal
- Must not repeat unnecessarily for returning players in the same world
- Must remain readable at supported window sizes

## Starter Goal

The first intended objective for a fresh world, used to create early momentum.

**Fields**

- `stage`: Current opening stage or milestone number
- `trigger`: Event that advances the stage
- `completionCondition`: What counts as progress for the player
- `reward`: Acknowledgement or next-step cue shown when completed

**Relationships**

- Uses existing early-guide progression and starter village flow
- Can be completed independently of later survival or village goals

**Validation Rules**

- Must be understandable without prior knowledge
- Must be completable shortly after spawn with nearby resources
- Must produce a visible acknowledgement when completed

## Starting Area

The immediate region around the player spawn point, including the starter
settlement and nearby resources or landmarks.

**Fields**

- `spawnAnchor`: The center position where the player first enters the world
- `landmark`: The visible or discoverable point of interest near spawn
- `resourceAccess`: The nearby materials or interactions that support the first goal
- `explorationPath`: The obvious short route a player can follow from spawn

**Relationships**

- Built around the starter settlement created during new-world startup
- May include curated world features that make the opening area feel intentional

**Validation Rules**

- Must include at least one clear point of interest near spawn
- Must provide enough nearby resources to support the starter goal
- Must still feel coherent if the player explores away from the suggested route

## Opening Reward

The acknowledgement or cue shown when the first milestone is completed.

**Fields**

- `message`: Text confirming progress
- `nextStep`: The next suggested action
- `visibility`: How prominently the reward is shown

**Relationships**

- Triggered by the starter goal and early-guide progression
- May be surfaced as toast text, HUD copy, or another lightweight prompt

**Validation Rules**

- Must be clear enough to understand in a single glance
- Must not create a hard pause or modal lock

## Persistence Source

The existing save-backed state that determines whether the opening guidance
should show and how far the player has progressed.

**Fields**

- `earlyGuideStage`
- `playerDeaths` and other existing stats used by the guide logic
- Existing world save metadata for returning-player behavior

**Relationships**

- Stored through existing player statistics save/load behavior
- Shared with agent-visible state through `/state`

**Validation Rules**

- Must round-trip through existing saves
- Must remain compatible with current agent state serialization
