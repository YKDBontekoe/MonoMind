#!/usr/bin/env python3
"""Bump VERSION, update CHANGELOG.md, and prepare release notes from git history."""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION_FILE = ROOT / "VERSION"
CHANGELOG_FILE = ROOT / "CHANGELOG.md"

COMMIT_RE = re.compile(
    r"^(?P<type>\w+)(?:\((?P<scope>[^)]+)\))?(?P<breaking>!)?: (?P<subject>.+)$"
)
SKIP_PREFIXES = ("chore(release):", "chore:", "chore(", "Merge ", "Merge pull request")


@dataclass(frozen=True)
class Commit:
    sha: str
    subject: str
    body: str


@dataclass(frozen=True)
class ParsedCommit:
    commit: Commit
    change_type: str
    scope: str | None
    breaking: bool
    subject: str


def run_git(*args: str) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def read_version() -> str:
    return VERSION_FILE.read_text(encoding="utf-8").strip()


def write_version(version: str) -> None:
    VERSION_FILE.write_text(f"{version}\n", encoding="utf-8")


def parse_version(version: str) -> tuple[int, int, int]:
    match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)", version)
    if not match:
        raise ValueError(f"Invalid semver: {version}")
    return int(match.group(1)), int(match.group(2)), int(match.group(3))


def bump_version(current: str, level: str) -> str:
    major, minor, patch = parse_version(current)
    if level == "major":
        return f"{major + 1}.0.0"
    if level == "minor":
        return f"{major}.{minor + 1}.0"
    return f"{major}.{minor}.{patch + 1}"


def latest_tag() -> str | None:
    try:
        tag = run_git("describe", "--tags", "--abbrev=0", "--match", "v*.*.*")
    except subprocess.CalledProcessError:
        return None
    return tag if tag else None


def commits_since(ref: str | None) -> list[Commit]:
    range_spec = f"{ref}..HEAD" if ref else "HEAD"
    log = run_git(
        "log",
        range_spec,
        "--pretty=format:%H%x1f%s%x1f%b%x1e",
    )
    if not log:
        return []

    commits: list[Commit] = []
    for entry in log.split("\x1e"):
        entry = entry.strip()
        if not entry:
            continue
        parts = entry.split("\x1f", 2)
        if len(parts) < 2:
            continue
        sha, subject = parts[0], parts[1]
        body = parts[2] if len(parts) > 2 else ""
        if subject.startswith(SKIP_PREFIXES):
            continue
        commits.append(Commit(sha=sha, subject=subject, body=body))
    return commits


def parse_commit(commit: Commit) -> ParsedCommit | None:
    match = COMMIT_RE.match(commit.subject)
    if not match:
        return None

    change_type = match.group("type").lower()
    scope = match.group("scope")
    breaking = bool(match.group("breaking")) or "BREAKING CHANGE" in commit.body
    subject = match.group("subject").strip()
    return ParsedCommit(
        commit=commit,
        change_type=change_type,
        scope=scope,
        breaking=breaking,
        subject=subject,
    )


def release_level(commits: list[Commit]) -> str:
    level = "patch"
    for commit in commits:
        parsed = parse_commit(commit)
        if parsed is None:
            continue
        if parsed.breaking:
            return "major"
        if parsed.change_type == "feat" and level != "major":
            level = "minor"
    return level


def format_subject(parsed: ParsedCommit) -> str:
    if parsed.scope:
        return f"**{parsed.scope}:** {parsed.subject}"
    return parsed.subject


def group_entries(commits: list[Commit]) -> dict[str, list[str]]:
    groups: dict[str, list[str]] = {
        "Added": [],
        "Changed": [],
        "Fixed": [],
        "Other": [],
    }

    for commit in commits:
        parsed = parse_commit(commit)
        if parsed is None:
            groups["Other"].append(commit.subject)
            continue

        entry = format_subject(parsed)
        if parsed.change_type == "feat":
            groups["Added"].append(entry)
        elif parsed.change_type == "fix":
            groups["Fixed"].append(entry)
        elif parsed.change_type in {"perf", "refactor"}:
            groups["Changed"].append(entry)
        else:
            groups["Other"].append(entry)

    return {name: items for name, items in groups.items() if items}


def render_section(version: str, release_date: str, commits: list[Commit]) -> str:
    lines = [f"## [{version}] - {release_date}", ""]
    groups = group_entries(commits)
    if not groups:
        lines.extend(["### Changed", "- Maintenance release.", ""])
        return "\n".join(lines)

    for heading, entries in groups.items():
        lines.append(f"### {heading}")
        for entry in entries:
            lines.append(f"- {entry}")
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def insert_changelog_section(section: str) -> None:
    text = CHANGELOG_FILE.read_text(encoding="utf-8")
    marker = "\n## ["
    idx = text.find(marker)
    if idx == -1:
        updated = text.rstrip() + "\n\n" + section
    else:
        updated = text[:idx] + "\n" + section + text[idx:]
    CHANGELOG_FILE.write_text(updated, encoding="utf-8")


def extract_changelog_section(version: str) -> str:
    text = CHANGELOG_FILE.read_text(encoding="utf-8")
    header_re = re.compile(
        rf"^## \[{re.escape(version)}\] - \d{{4}}-\d{{2}}-\d{{2}}\s*$",
        re.MULTILINE,
    )
    match = header_re.search(text)
    if not match:
        return f"# Autonocraft {version}\n\nSee repository history for details.\n"

    rest = text[match.end() :]
    next_header = rest.find("\n## [")
    body = rest if next_header == -1 else rest[:next_header]
    return f"# Autonocraft {version}\n{body.strip()}\n"


def plan_release() -> tuple[str, str, list[Commit]] | None:
    current = read_version()
    tag = latest_tag()
    commits = commits_since(tag)
    if not commits:
        return None

    new_version = bump_version(current, release_level(commits))
    return new_version, current, commits


def cmd_check() -> int:
    return 0 if plan_release() else 1


def cmd_bump() -> int:
    planned = plan_release()
    if planned is None:
        print("No releasable commits since last tag.", file=sys.stderr)
        return 1

    new_version, _, commits = planned
    section = render_section(new_version, date.today().isoformat(), commits)
    write_version(new_version)
    insert_changelog_section(section)
    print(new_version)
    return 0


def cmd_extract(version: str) -> int:
    print(extract_changelog_section(version), end="")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("check", help="Exit 0 when a release bump is needed")
    sub.add_parser("bump", help="Bump VERSION and prepend CHANGELOG.md")
    extract_parser = sub.add_parser("extract", help="Print changelog section for a version")
    extract_parser.add_argument("version", help="Semver without leading v")

    args = parser.parse_args()
    if args.command == "check":
        return cmd_check()
    if args.command == "bump":
        return cmd_bump()
    if args.command == "extract":
        return cmd_extract(args.version)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
