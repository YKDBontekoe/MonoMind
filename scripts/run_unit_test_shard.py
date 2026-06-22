#!/usr/bin/env python3
from __future__ import annotations

import argparse
import collections
import os
import re
import subprocess
import sys
from pathlib import Path


TEST_ROOT_FILTER = "FullyQualifiedName~Autonocraft.Tests.Unit"
CLASS_PATTERN = re.compile(r"^(Autonocraft\.Tests\.Unit\.[^.]+)\.")


def run_command(args: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    try:
        return subprocess.run(
            args,
            cwd=cwd,
            check=True,
            text=True,
            capture_output=True,
        )
    except subprocess.CalledProcessError as exc:
        if exc.stdout:
            sys.stderr.write(exc.stdout)
        if exc.stderr:
            sys.stderr.write(exc.stderr)
        raise


def discover_unit_tests(repo_root: Path) -> list[tuple[str, int]]:
    result = run_command(
        [
            "dotnet",
            "test",
            "tests/Autonocraft.Tests",
            "--configuration",
            "Release",
            "--no-build",
            "--list-tests",
            "--filter",
            TEST_ROOT_FILTER,
        ],
        repo_root,
    )

    counts: collections.Counter[str] = collections.Counter()
    for line in (result.stdout + "\n" + result.stderr).splitlines():
        match = CLASS_PATTERN.match(line.strip())
        if match:
            counts[match.group(1)] += 1

    if not counts:
        raise RuntimeError("No unit test classes were discovered from dotnet test --list-tests.")

    return sorted(counts.items())


def shard_classes(
    classes: list[tuple[str, int]], shard_index: int, shard_count: int
) -> list[tuple[str, int]]:
    if shard_index < 0 or shard_index >= shard_count:
        raise ValueError(
            f"Shard index {shard_index} is out of range for shard count {shard_count}."
        )

    if shard_count > len(classes):
        raise ValueError(
            f"Shard count {shard_count} exceeds discovered class count {len(classes)}."
        )

    allocations: list[list[tuple[str, int]]] = [[] for _ in range(shard_count)]
    totals = [0 for _ in range(shard_count)]

    for class_name, test_count in sorted(classes, key=lambda item: (-item[1], item[0])):
        target_index = min(range(shard_count), key=lambda index: (totals[index], index))
        allocations[target_index].append((class_name, test_count))
        totals[target_index] += test_count

    return sorted(allocations[shard_index])


def build_filter(class_names: list[str]) -> str:
    return "|".join(f"FullyQualifiedName~{class_name}" for class_name in class_names)


def main() -> int:
    parser = argparse.ArgumentParser(description="Run a dynamically balanced unit-test shard.")
    parser.add_argument("--shard-index", type=int, required=True)
    parser.add_argument("--shard-count", type=int, required=True)
    parser.add_argument(
        "--collect-coverage",
        action="store_true",
        help='Add the "XPlat Code Coverage" collector.',
    )
    parser.add_argument(
        "--results-directory",
        default=None,
        help="Pass through a custom results directory.",
    )
    parser.add_argument(
        "--logger",
        action="append",
        default=[],
        help="Additional dotnet test logger arguments. May be supplied multiple times.",
    )
    parser.add_argument(
        "--verbosity",
        default="minimal",
        help="dotnet test verbosity level.",
    )
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[1]
    classes = discover_unit_tests(repo_root)
    assigned = shard_classes(classes, args.shard_index, args.shard_count)

    if not assigned:
        raise RuntimeError(
            f"Shard {args.shard_index} of {args.shard_count} did not receive any unit test classes."
        )

    class_names = [class_name for class_name, _ in assigned]
    total_cases = sum(test_count for _, test_count in assigned)
    print(
        f"Running shard {args.shard_index + 1}/{args.shard_count}: "
        f"{len(class_names)} classes, {total_cases} discovered tests",
        file=sys.stderr,
    )
    for class_name, test_count in assigned:
        print(f"  - {class_name} ({test_count})", file=sys.stderr)

    command = [
        "dotnet",
        "test",
        "tests/Autonocraft.Tests",
        "--configuration",
        "Release",
        "--no-build",
        "--verbosity",
        args.verbosity,
        "--filter",
        build_filter(class_names),
    ]

    for logger in args.logger:
        command.extend(["--logger", logger])

    if args.collect_coverage:
        command.extend(["--collect", "XPlat Code Coverage"])

    if args.results_directory:
        command.extend(["--results-directory", args.results_directory])

    completed = subprocess.run(command, cwd=repo_root)
    return completed.returncode


if __name__ == "__main__":
    raise SystemExit(main())
