# Changelog

All notable changes to Autonocraft are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Releases are created automatically when changes land on `main` (after CI passes).

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
