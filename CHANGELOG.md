# Changelog

All notable changes to Autonocraft are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Releases are created automatically when changes land on `main` (after CI passes).

## [0.2.14] - 2026-06-23

### Other
- Expand jungle fern placement test coverage
- Refine swing and crack overlay animations

## [0.2.13] - 2026-06-22

### Changed
- replace ScanConcentric with fast two-phase biome scanner

### Other
- add diagnostics to Mining and Placing test and clean up test runner
- Improved pipeline
- Make test sharding dynamic

## [0.2.12] - 2026-06-22

### Other
- Split coverage unit test shards

## [0.2.11] - 2026-06-22

### Other
- Fix formatting drift
- Refine village flow and early settlement
- Bump softprops/action-gh-release from 3.0.0 to 3.0.1
- Bump actions/checkout from 6 to 7

## [0.2.10] - 2026-06-19

### Other
- Address review findings for villager feedback, menu polish, and test reliability.
- Revamp the pre-game main menu with a root hub, shared navigation chrome, and integration tests so players can reach play, saves, and settings consistently.

## [0.2.9] - 2026-06-19

### Other
- specify and agent skills

## [0.2.8] - 2026-06-19

### Other
- Delete test_output/playthrough directory
- Fix CI failures: assembly rules, fluid spread test, and atlas sync.
- Address PR #44 review feedback on determinism, lifecycle, and safety.
- Fix gameplay lag spikes and expand procedural structures.

## [0.2.7] - 2026-06-17

### Other
- Fix biome review findings for crafting tags, cave decor, flora, spawns, and tests.
- Expand terrain variety with surface and cave biomes, ocean ice tied to snowy peaks, and matching blocks, structures, and animal spawns.

## [0.2.6] - 2026-06-17

### Other
- Improve world foliage and tree generation
- Bump actions/checkout from 4 to 6
- Bump actions/setup-dotnet from 4 to 5
- Bump dorny/test-reporter from 1 to 3
- Bump softprops/action-gh-release from 2.6.2 to 3.0.0
- Bump actions/upload-artifact from 4 to 7
- Bump actions/cache from 4 to 5

## [0.2.5] - 2026-06-17

### Other
- Bump MonoGame.Framework.DesktopGL from 3.8.2.1105 to 3.8.4.1

## [0.2.4] - 2026-06-17

### Other
- Refactor large types, add C# atlas build, and sync Voronoi terrain textures.

## [0.2.3] - 2026-06-17

### Other
- Reorganize source files into logical package subdirectories.

## [0.2.2] - 2026-06-17

### Other
- Fix CI matrix job names to show platform labels.
- Wire multi-assembly solution and fix CI build.
- Extract multi-assembly layout with Core, Engine, and domain libraries.

## [0.2.1] - 2026-06-16

### Other
- Improve animal and villager models with panic flee behavior.

## [0.2.0] - 2026-06-16

### Added
- implement slabbing, connected textures, and voronoi texture updates
- implement survival overhaul with physical 3D drops, view bobbing adjustments, 3D hand/tool perspective rendering, block cracks, sprinting, and Q key dropping

### Other
- Address review findings: formatting, atlas seed, column cache, and dead code.
- Fix terrain slab placement, rendering, and stair collision.
- Fix release workflow push when main advances during CI.
- Align HUD hotbar slots with rounded inventory slot design using reusable UiDrawingUtils
- Delete scripts/configure_github_repo.sh
- Allow release bot to bypass main rules and push directly.
- Fix release workflow for PR-protected main branch.
- Fix review findings for quartz, wolf atlas, lava jump, and procedural gaps.
- massive creative expansion adding blocks, tools, animals, custom physics, and under-lava rendering overlays
- Fix water draw performance and villager-triggered chunk unloads.
- Fix see-through and unstable water rendering\n\n- Update DepthStencilState for water terrain pass from DepthRead to Default so water writes to the depth buffer.\n- Also update cutout terrain pass to prevent similar depth sorting issues with transparent blocks.
- Fixes
- Fixes
- Improve starting village and villager visuals
- Add automatic versioning, changelog, and GitHub Releases on main.
- Fix review findings for item drops, camera timing, and rendering.
- Fix review findings and whitespace formatting for CI.
- Smooth distant chunk streaming to prevent frame spikes.
- Speed up world generation, chunk loading, and runtime perf tooling.
- Add missing village panel classes required by VillageScreen refactor.
- Address code review findings from the mega-refactor PR.
- Extract game subsystems and schedulers to shrink AutonocraftGame and improve chunk meshing performance.
- Remove E2E workflow from CI pipeline.
- Fix code review issues across crafting, inventory, and UI input.
- Many improvements
- Add EarlyGameGuide, survival tests, and documentation.
- Improve Founder's Hamlet onboarding and village food sharing.
- Add survival systems: hunger, food, night threats, and death stakes.
- Fix Linux CI agent HTTP test and dotnet format whitespace.
- improvements
- Refactor village system into job handlers and services with save v7, new professions, and clearer player guidance.
- Expand village simulation and make creative mode resource-free.
- Add villager visual identity and interactive settlement UX.
- Fix steady FPS drop in grassy areas by optimizing flora and terrain rendering.
- Upgrade flora visuals with richer sprites, batched alpha-tested rendering, and wind sway.
- Improve atmospheric lighting and fix terrain AO brightness artifacts.
- Fix macOS E2E SIGBUS from Homebrew SDL override on CI.
- Fix Windows E2E timeouts for agent API startup.
- Complete world load once chunks are playable, not fully upgraded.
- Fix world load stall on the last few meshing chunks.
- Fix initial load stalling on final meshing chunks.
- Fix E2E timeouts when agent game runs in background.
- Fix integration test permission denied after artifact download.
- Fix whitespace formatting in ProceduralTextureSynth.cs.
- Add player statistics dashboard and fully procedural texture atlas.
- Fix nullable GraphicsDevice warnings in block interaction and crafting.
- Fix cross-platform atlas validation in CI.
- Expand GitHub Actions into multi-OS CI, E2E, and release pipelines.
- Fix chunk rendering reliability and reduce periodic frame hitches.
- Improve chunk mesh rebuild scheduling in VoxelWorld.
- Expand Autonocraft with villages, crafting, fluids, and agent API.
- Initial commit: Autonocraft voxel game codebase.

## [0.1.0] - 2026-06-16

### Added
- Initial automated release pipeline with semantic versioning and changelog generation.
