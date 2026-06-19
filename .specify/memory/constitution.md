<!--
Sync Impact Report
Version change: template -> 1.0.0
Modified principles:
- PRINCIPLE_1_NAME placeholder -> I. Code Quality and Architecture
- PRINCIPLE_2_NAME placeholder -> II. Testing Is Required Evidence
- PRINCIPLE_3_NAME placeholder -> III. User Experience Consistency
- PRINCIPLE_4_NAME placeholder -> IV. Performance Budgets
- PRINCIPLE_5_NAME placeholder -> V. Maintainability and Operability
Added sections:
- Autonocraft Technical Standards
- Development Workflow and Quality Gates
Removed sections:
- None
Templates requiring updates:
- ✅ .specify/templates/plan-template.md
- ✅ .specify/templates/spec-template.md
- ✅ .specify/templates/tasks-template.md
- ✅ .specify/templates/commands/*.md (no command templates present)
Runtime guidance reviewed:
- ✅ README.md
- ✅ AGENTS.md
Follow-up TODOs:
- None
-->
# Autonocraft Constitution

## Core Principles

### I. Code Quality and Architecture
Code MUST follow the existing project boundaries in `src/Autonocraft.*`, keep
domain behavior outside UI/rendering code when a lower-level project already owns
that concern, and prefer existing services, registries, serializers, and test
helpers before introducing new abstractions. Changes MUST be small enough to
review by behavior and MUST avoid unrelated rewrites, duplicated domain rules,
and hidden coupling across projects.

Rationale: Autonocraft spans gameplay, world simulation, rendering, persistence,
and agent automation; quality depends on clear ownership and predictable change
scope.

### II. Testing Is Required Evidence
Every behavior change MUST include executable verification appropriate to its
risk. Changes touching player physics, movement, gravity, collision, world
generation, chunking, blocks, inventory, tools, skills, animals, combat,
crafting, fluids, structures, saves, villagers, villages, UI, or rendering MUST
run the headless integration suite before completion:
`dotnet run --project src/Autonocraft -- --test`. Narrow pure-domain changes MAY
use focused unit tests first, but the selected verification MUST be named in the
plan and completion notes. Failing tests MUST block delivery unless the failure
is explicitly documented as unrelated and reproducible before the change.

Rationale: Gameplay regressions are often cross-system; tests are the evidence
that changes preserve world state, user actions, and simulation rules.

### III. User Experience Consistency
Player-facing and agent-facing workflows MUST remain consistent with existing
controls, HUD conventions, save/load behavior, hotbar indexing, dev commands,
and HTTP API semantics. New UI MUST be readable at supported window sizes, avoid
overlapping text or controls, and preserve keyboard/controller expectations
already documented in README.md and AGENTS.md. Agent API additions MUST provide
stable JSON fields, clear readiness/failure behavior, and backwards-compatible
command semantics unless a breaking change is explicitly approved.

Rationale: Autonocraft is used both interactively and through automation; UX
consistency keeps manual play, screenshots, and scripted tests reliable.

### IV. Performance Budgets
Features that affect rendering, chunk streaming, world generation, AI loops,
fluid updates, saves, or entity simulation MUST state their performance risk and
verification method before implementation. Plans MUST define measurable budgets
when user-visible latency, frame time, memory, chunk counts, mesh sizes, save
size, or startup/load time could change. Implementations MUST avoid unbounded
per-frame allocation, synchronous expensive work in hot loops, and broad scans
when indexed or chunk-local access is practical.

Rationale: Voxel worlds amplify small inefficiencies; performance requirements
must be explicit before gameplay smoothness or automation stability degrades.

### V. Maintainability and Operability
Code MUST be understandable through names, structure, and tests before comments
are added. Serialization, configuration, and public API changes MUST preserve
backward compatibility or include a migration path. Runtime diagnostics, dev
commands, and agent HTTP endpoints SHOULD be extended when they materially
improve verification or support, but MUST not expose secrets or unstable
internal state as a public contract.

Rationale: The project relies on long-lived saves, CI quality gates, and agent
driven inspection; maintainability includes the ability to diagnose issues
without destabilizing public behavior.

## Autonocraft Technical Standards

Autonocraft is a .NET 10 MonoGame DesktopGL project. New implementation work
MUST use the existing solution structure, project dependency rules, and local
tooling unless a plan documents why a new dependency or project boundary is
necessary. Procedural atlas changes MUST keep `atlas_layout.json` and
`src/Autonocraft.AtlasBuild` validation in sync. Save format changes MUST include
round-trip coverage and version handling. Agent API changes MUST be testable
through the documented HTTP server semantics and preserve port/readiness
expectations.

## Development Workflow and Quality Gates

Plans MUST include a Constitution Check covering code ownership, test evidence,
UX impact, and performance impact. Specs MUST define measurable success criteria
for user-visible behavior and any relevant performance or consistency targets.
Tasks MUST include the tests, validation commands, and documentation updates
needed to satisfy this constitution.

Before completion, contributors MUST report the verification commands they ran
and their result. For changes in the domains named by Principle II, the
integration suite is mandatory. CI quality gates remain authoritative for pull
requests and MUST not be weakened to pass a feature.

## Governance

This constitution supersedes conflicting informal practices. Amendments require
an update to this file, a Sync Impact Report, and review of dependent Spec Kit
templates and runtime guidance. Version changes follow semantic versioning:
MAJOR for removed or incompatible principles, MINOR for new principles or
material governance expansions, and PATCH for clarifications that do not change
required behavior. Compliance is reviewed during planning, implementation, code
review, and final verification.

**Version**: 1.0.0 | **Ratified**: 2026-06-19 | **Last Amended**: 2026-06-19
