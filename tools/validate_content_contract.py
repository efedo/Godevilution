#!/usr/bin/env python3
"""Validate that shipped content and project documentation share one schema contract."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCHEMA_SOURCE = ROOT / "server/src/Devilution.Diablo/DiabloContentSchema.cs"
CONTENT_ROOT = ROOT / "server/content"
GODOT_LIVE_TESTS = ROOT / "godot-client/Devilution.Client.Protocol.Tests/LiveAuthoritativeServerTests.cs"


def fail(message: str) -> None:
    print(f"content-contract: error: {message}", file=sys.stderr)
    raise SystemExit(1)


def read_schema() -> tuple[str, str, str]:
    source = SCHEMA_SOURCE.read_text(encoding="utf-8")
    current_match = re.search(
        r"Current\s*\{[^=]*\}\s*=\s*new\(\s*\"([^\"]+)\"\s*,\s*\"([^\"]+)\"\s*\)",
        source,
        re.DOTALL,
    )
    previous_match = re.search(
        r"PreviousAdditive\s*\{[^=]*\}\s*=\s*new\(\s*\"([^\"]+)\"\s*,\s*\"([^\"]+)\"\s*\)",
        source,
        re.DOTALL,
    )
    if current_match is None or previous_match is None:
        fail(f"unable to read current and previous schema from {SCHEMA_SOURCE}")
    current = (current_match.group(1), current_match.group(2))
    previous = (previous_match.group(1), previous_match.group(2))
    if current[0] != previous[0]:
        fail("current and previous schemas use different identities")
    if current == previous:
        fail("current and previous schemas are identical")
    return current[0], current[1], previous[1]


def validate_manifests(schema_id: str, current_version: str) -> int:
    manifests = sorted(CONTENT_ROOT.glob("*/content-pack.json"))
    if not manifests:
        fail(f"no first-party manifests found below {CONTENT_ROOT}")
    for manifest_path in manifests:
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            fail(f"{manifest_path.relative_to(ROOT)} is not valid JSON: {exc}")
        schema = manifest.get("schema")
        if not isinstance(schema, dict):
            fail(f"{manifest_path.relative_to(ROOT)} has no schema object")
        if schema.get("id") != schema_id or schema.get("version") != current_version:
            fail(
                f"{manifest_path.relative_to(ROOT)} declares "
                f"{schema.get('id')}@{schema.get('version')}, expected {schema_id}@{current_version}"
            )
    return len(manifests)


def validate_godot_fixture_schemas(schema_id: str, current_version: str) -> int:
    """Keep live-server overlays on the active schema contract.

    Overlays normally reference BaseContentSchemaJson, which is read from the
    validated base manifest. Any schema literal must still match the current one.
    """
    base_reference = re.compile(r'"schema"\s*:\s*\{\{BaseContentSchemaJson\}\}')
    literal = re.compile(
        r'"schema"\s*:\s*\{\s*"id"\s*:\s*"([^"]+)"\s*,\s*'
        r'"version"\s*:\s*"([^"]+)"\s*\}'
    )
    source = GODOT_LIVE_TESTS.read_text(encoding="utf-8")
    if 'server", "content", "base", "content-pack.json"' not in source:
        fail(f"{GODOT_LIVE_TESTS.relative_to(ROOT)} no longer derives BaseContentSchemaJson from the base pack")
    count = 0
    for path in sorted(GODOT_LIVE_TESTS.parent.glob("LiveAuthoritativeServerTests*.cs")):
        text = path.read_text(encoding="utf-8")
        count += len(base_reference.findall(text))
        for fixture_schema_id, fixture_version in literal.findall(text):
            if (fixture_schema_id, fixture_version) != (schema_id, current_version):
                fail(
                    f"{path.relative_to(ROOT)} declares inline schema "
                    f"{fixture_schema_id}@{fixture_version}, expected "
                    f"{schema_id}@{current_version}"
                )
            count += 1
    if count == 0:
        fail(f"no Godot live-server overlay schemas found beside {GODOT_LIVE_TESTS.relative_to(ROOT)}")
    return count


def require_text(path: Path, *fragments: str) -> None:
    text = path.read_text(encoding="utf-8")
    for fragment in fragments:
        if fragment not in text:
            fail(f"{path.relative_to(ROOT)} is missing required text: {fragment}")


def main() -> int:
    schema_id, current_version, previous_version = read_schema()
    manifest_count = validate_manifests(schema_id, current_version)
    godot_fixture_count = validate_godot_fixture_schemas(schema_id, current_version)
    # The content README owns the schema contract; status docs do not repeat it.
    require_text(
        CONTENT_ROOT / "README.md",
        f"`{schema_id}@{current_version}` schema",
        f"previous additive `{previous_version}` schema",
    )
    print(
        f"content-contract: validated {manifest_count} manifests, "
        f"{godot_fixture_count} Godot fixtures, and documentation for "
        f"{schema_id}@{current_version} (previous additive {previous_version})"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
