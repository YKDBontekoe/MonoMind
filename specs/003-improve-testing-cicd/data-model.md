# Data Model: Improved Testing and CI/CD Pipelines

This feature models verification infrastructure as data — not runtime database
entities. Relationships describe how checks, domains, and automation events connect.

## Verification Check

A single automated validation step exposed on pull requests or mainline runs.

**Fields**

- `id`: Stable slug (e.g., `format`, `unit-linux`, `integration-macos`)
- `displayName`: Human-readable PR check title
- `category`: One of `build`, `unit`, `integration`, `format`, `atlas`, `coverage`, `security`, `agent-e2e`
- `required`: Boolean — blocks merge when false outcome
- `platforms`: List of OS targets (`ubuntu`, `windows`, `macos`, or `all`)
- `durationClass`: `fast` (<2 min typical) or `long` (>2 min)
- `localCommand`: Shell command(s) reproducing the check on primary dev platform
- `artifactPaths`: Output files retained on failure (logs, TRX, coverage)
- `workflowFile`: Source workflow YAML path
- `jobName`: GitHub Actions job name

**Validation Rules**

- Every `required: true` check MUST appear in branch protection or documented
  equivalent merge policy.
- `displayName` MUST encode category or platform so SC-003 is satisfied from PR UI.
- `localCommand` MUST be non-empty for required checks except pure matrix
  platform duplicates (document one canonical local command + platform notes).

## Verification Suite

Ordered collection of checks for an automation event.

**Fields**

- `eventType`: `pull_request`, `push_main`, `schedule`, `release_tag`, `workflow_dispatch`
- `checks`: Ordered list of Verification Check `id` references
- `failFast`: Whether later tiers are skipped after first required failure
- `maxDurationMinutes`: Target ceiling (20 for PR suites per SC-001)

**Relationships**

- Composed of many Verification Checks.
- Triggered by GitHub `on:` events in workflow files.

**Validation Rules**

- Fast checks (`durationClass: fast`) MUST precede long matrix jobs when `failFast` is true.
- `release_tag` suite MUST include integration validation on published binaries (existing `release.yml` behavior).

## Failure Artifact

Persisted output linked to a failed verification run.

**Fields**

- `checkId`: Reference to Verification Check
- `commitSha`: Git commit under test
- `platform`: Runner OS
- `logPath`: Primary text log
- `structuredReportPath`: Optional TRX, cobertura, or JSON summary
- `retentionDays`: Artifact retention (default 90)

**Validation Rules**

- Required checks MUST upload at least `logPath` on failure (`if: always()`).
- Artifact names MUST include platform suffix for matrix jobs.

## Protected Domain

Product area designated regression-sensitive by project policy (AGENTS.md /
constitution Principle II).

**Fields**

- `name`: Domain label (e.g., `persistence`, `villagers`, `physics`)
- `policySource`: Document reference (AGENTS.md section, constitution)
- `mappedChecks`: List of Verification Check `id` or test identifiers
- `primaryTestType`: `unit`, `integration`, `agent-e2e`, or `manual`
- `manualProcedure`: Optional link/command when not fully automated

**Relationships**

- Each Protected Domain maps to one or more Verification Checks or named tests.

**Validation Rules**

- SC-002: every Protected Domain MUST have at least one `mappedChecks` entry.
- Domains touching cross-system gameplay MUST include `integration` or
  `agent-e2e` mapping, not unit-only.

## Contributor Verification Guide

Documented local workflow mirroring required automation.

**Fields**

- `scriptPath`: `scripts/verify_local.sh` / `scripts/verify_local.ps1`
- `quickProfile`: Commands for fast pre-push (~format + atlas + unit)
- `fullProfile`: Commands adding integration and optional E2E
- `platformNotes`: Known parity gaps (e.g., macOS `DYLD_LIBRARY_PATH` local only)
- `prerequisites`: .NET 10 SDK, Python 3 for E2E

**Relationships**

- References Verification Suite check order and local commands.

**Validation Rules**

- `fullProfile` MUST cover every required check category at least once on primary platform.
- Documented exceptions MUST list platform and rationale (SC-004).

## Workflow Gate (release)

Aggregation state before version bump or release promotion.

**Fields**

- `requiredWorkflows`: List of workflow names that must succeed (`CI`, `Quality`, `CodeQL`)
- `conclusion`: `success`, `failure`, `pending`
- `triggerCommit`: SHA on main

**Validation Rules**

- SC-005: `conclusion: success` required before `version.yml` bump or `release.yml` publish.
- Scheduled runs on main MUST set `conclusion` visible in Actions UI (FR-011).

## State Transitions

```text
PR opened/updated
  → Tier 0 fast checks run
  → on pass: build matrix
  → on pass: unit + integration matrices (parallel)
  → on pass: optional/required agent E2E (ubuntu)
  → all required checks success → merge eligible

Push to main (CI + Quality + CodeQL success)
  → version.yml evaluates gate
  → on success + releasable commits → bump/tag
  → tag push → release.yml publish + integration on artifact
```
