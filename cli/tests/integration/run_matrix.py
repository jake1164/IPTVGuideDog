#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional


@dataclass
class ScenarioResult:
    name: str
    succeeded: bool
    details: str
    diff_path: Optional[Path] = None


def read_env_file(path: Path) -> Dict[str, str]:
    data: Dict[str, str] = {}
    if not path.exists():
        return data

    for raw in path.read_text(encoding="utf-8", errors="ignore").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in line:
            continue
        key, value = line.split("=", 1)
        key = key.strip()
        value = value.strip()
        if key and value and key not in data:
            data[key.upper()] = value
    return data


def load_env_maps(workspace: Path, provider_env: Optional[Path]) -> Dict[str, str]:
    candidates = [
        workspace / ".env",
        workspace / "cli" / ".env",
    ]
    if provider_env is not None:
        candidates.append(provider_env)
    merged: Dict[str, str] = {}
    for candidate in candidates:
        merged.update(read_env_file(candidate))
    return merged


def substitute_template(template: Optional[str], env_map: Dict[str, str]) -> Optional[str]:
    if not template:
        return None
    result = template
    for key, value in env_map.items():
        result = result.replace(f"%{key}%", value)
        result = result.replace(f"${{{key}}}", value)
    return result


def resolve_source(
    source: Optional[str],
    fallback: Path,
    destination: Path,
    description: str,
) -> Path:
    destination.parent.mkdir(parents=True, exist_ok=True)
    if not source:
        shutil.copy2(fallback, destination)
        return destination

    if source.startswith("file://"):
        local_path = Path(source[7:])
    else:
        local_path = Path(source)

    if local_path.exists():
        shutil.copy2(local_path, destination)
        return destination

    cmd = [
        "curl",
        "-fsSL",
        source,
        "-o",
        str(destination),
    ]
    completed = subprocess.run(cmd, capture_output=True, text=True)
    if completed.returncode != 0:
        raise RuntimeError(
            f"Failed to download {description} from {source}: {completed.stderr.strip()}"
        )
    return destination


def run_command(
    command: Iterable[str],
    cwd: Optional[Path],
    env: Optional[Dict[str, str]],
    stdout_path: Path,
    stderr_path: Path,
) -> subprocess.CompletedProcess[str]:
    stdout_path.parent.mkdir(parents=True, exist_ok=True)
    stderr_path.parent.mkdir(parents=True, exist_ok=True)
    completed = subprocess.run(
        list(command),
        cwd=str(cwd) if cwd else None,
        env=env,
        text=True,
        capture_output=True,
    )
    stdout_path.write_text(completed.stdout, encoding="utf-8", errors="ignore")
    stderr_path.write_text(completed.stderr, encoding="utf-8", errors="ignore")
    return completed


def unified_diff_text(lhs: Path, rhs: Path) -> str:
    import difflib

    left_lines = lhs.read_text(encoding="utf-8").splitlines(keepends=True)
    right_lines = rhs.read_text(encoding="utf-8").splitlines(keepends=True)
    diff_lines = difflib.unified_diff(
        left_lines,
        right_lines,
        fromfile=str(lhs),
        tofile=str(rhs),
    )
    return "".join(diff_lines)


def ensure_cli_outputs(paths: Iterable[Path]) -> None:
    for path in paths:
        if not path.exists():
            raise RuntimeError(f"Expected CLI output was not produced: {path}")


def apply_placeholders(
    value: str,
    *,
    scenario_dir: Path,
    output_root: Path,
    playlist_path: Path,
    epg_path: Path,
    drop_file: Path,
    baseline_path: Path,
    workspace_path: Path,
) -> str:
    text = str(value)
    return (
        text.replace("{playlist_url}", str(playlist_path))
        .replace("{epg_url}", str(epg_path))
        .replace("{drop_file}", str(drop_file))
        .replace("{baseline}", str(baseline_path))
        .replace("{scenario_dir}", str(scenario_dir))
        .replace("{output_root}", str(output_root))
        .replace("{workspace}", str(workspace_path))
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Run IPTV CLI integration matrix.")
    parser.add_argument("--workspace", required=True)
    parser.add_argument("--matrix", default=None)
    parser.add_argument("--output", required=True)
    parser.add_argument("--configuration", default="Debug")
    parser.add_argument("--provider", default=None)
    args = parser.parse_args()

    workspace = Path(args.workspace).resolve()
    cli_dir = workspace / "cli"
    integration_dir = cli_dir / "tests" / "integration"
    matrix_file = Path(args.matrix).resolve() if args.matrix else integration_dir / "matrix.json"
    provider_name = args.provider or os.environ.get("PROVIDER_NAME")
    output_root = Path(args.output).resolve()

    if not matrix_file.exists():
        raise SystemExit(f"Matrix definition missing: {matrix_file}")

    matrix = json.loads(matrix_file.read_text(encoding="utf-8"))
    defaults = matrix.get("defaults", {})

    sample_playlist = (workspace / defaults.get("samplePlaylist", "")).resolve()
    sample_epg = (workspace / defaults.get("sampleEpg", "")).resolve()
    drop_file = (workspace / defaults.get("dropFile", "")).resolve()
    legacy_args = defaults.get("legacyFilterArgs", [])

    if not sample_playlist.exists():
        raise SystemExit(f"Sample playlist missing: {sample_playlist}")
    if not sample_epg.exists():
        raise SystemExit(f"Sample EPG missing: {sample_epg}")
    if not drop_file.exists():
        raise SystemExit(f"Drop/groups file missing: {drop_file}")

    provider_env_raw = os.environ.get("PROVIDER_ENV_FILE")
    provider_env_path = None
    if provider_env_raw:
        provider_env_path = Path(provider_env_raw).resolve()
        if not provider_env_path.exists():
            raise SystemExit(f"Provider env file not found: {provider_env_path}")

    env_map = load_env_maps(workspace, provider_env_path)
    playlist_url = substitute_template(
        os.environ.get("PLAYLIST_URL")
        or os.environ.get("PLAYLIST_TEMPLATE")
        or defaults.get("playlistTemplate"),
        env_map,
    )
    epg_url = substitute_template(
        os.environ.get("EPG_URL")
        or os.environ.get("EPG_TEMPLATE")
        or defaults.get("epgTemplate"),
        env_map,
    )

    if provider_name:
        print(f"[matrix] Provider: {provider_name}")

    artifacts_dir = output_root / "artifacts"
    scenario_root = output_root / "scenarios"
    diff_root = output_root / "diffs"
    for path in (artifacts_dir, scenario_root, diff_root):
        path.mkdir(parents=True, exist_ok=True)

    raw_playlist = artifacts_dir / "raw-playlist.m3u"
    raw_epg = artifacts_dir / "raw-epg.xml"

    resolved_playlist = resolve_source(
        playlist_url,
        sample_playlist,
        raw_playlist,
        "playlist",
    )
    resolved_epg = resolve_source(
        epg_url,
        sample_epg,
        raw_epg,
        "epg",
    )

    print(f"[matrix] Playlist source: {resolved_playlist}")
    print(f"[matrix] EPG source: {resolved_epg}")

    legacy_filter = cli_dir / "tests" / "integration" / "legacy" / "m3u-filter.py"
    if not legacy_filter.exists():
        raise SystemExit(f"Legacy filter missing: {legacy_filter}")

    baseline_cfg = matrix.get("baseline", {})
    baseline_dir = artifacts_dir / "baseline"
    baseline_dir.mkdir(parents=True, exist_ok=True)
    baseline_output = baseline_dir / baseline_cfg.get("output", "legacy-filtered.m3u")
    legacy_cmd = [
        "python3",
        str(legacy_filter),
        "filter",
        str(resolved_playlist),
        str(baseline_output),
    ]
    for arg in legacy_args:
        legacy_cmd.append(str(arg))
    legacy_cmd.extend(["--drop-file", str(drop_file)])

    type_filter_key = baseline_cfg.get("typeFilterEnv", "TYPE_FILTER")
    type_filter_default = baseline_cfg.get("typeFilterDefault")
    type_filter_value = os.environ.get(type_filter_key)
    if (not type_filter_value or not type_filter_value.strip()) and type_filter_default:
        type_filter_value = str(type_filter_default).strip()
    if type_filter_value:
        legacy_cmd.extend(["--type", type_filter_value])

    legacy_stdout = scenario_root / "baseline-legacy" / "stdout.log"
    legacy_stderr = scenario_root / "baseline-legacy" / "stderr.log"
    print("[matrix] Running legacy Python filter for baseline.")
    legacy_proc = run_command(
        legacy_cmd,
        cwd=cli_dir,
        env=None,
        stdout_path=legacy_stdout,
        stderr_path=legacy_stderr,
    )
    if legacy_proc.returncode != 0:
        raise SystemExit(
            f"Legacy filter exited with {legacy_proc.returncode}. See {legacy_stderr}"
        )
    ensure_cli_outputs([baseline_output])

    build_config = args.configuration
    cli_dll = cli_dir / "iptv" / "bin" / build_config / "net10.0" / "iptv.dll"
    if not cli_dll.exists():
        raise SystemExit(f"Built CLI assembly not found: {cli_dll}")

    results: List[ScenarioResult] = []
    scenarios = matrix.get("scenarios", [])

    for scenario in scenarios:
        name = scenario["name"]
        command = scenario["command"]
        scenario_dir = scenario_root / name
        scenario_dir.mkdir(parents=True, exist_ok=True)
        processed_args: List[str] = []
        for token in scenario.get("args", []):
            processed_args.append(
                apply_placeholders(
                    token,
                    scenario_dir=scenario_dir,
                    output_root=output_root,
                    playlist_path=resolved_playlist,
                    epg_path=resolved_epg,
                    drop_file=drop_file,
                    baseline_path=baseline_output,
                    workspace_path=workspace,
                )
            )

        stdout_log = scenario_dir / "stdout.log"
        stderr_log = scenario_dir / "stderr.log"

        cmdline = ["dotnet", str(cli_dll), command] + processed_args
        print(f"[matrix] Scenario '{name}': running {' '.join(cmdline)}")
        completed = run_command(
            cmdline,
            cwd=cli_dir,
            env=dict(os.environ, DOTNET_PRINT_TELEMETRY_MESSAGE="false"),
            stdout_path=stdout_log,
            stderr_path=stderr_log,
        )

        if completed.returncode != 0:
            results.append(
                ScenarioResult(
                    name=name,
                    succeeded=False,
                    details=f"Exit code {completed.returncode}. See logs in {scenario_dir}",
                )
            )
            continue

        artifacts = scenario.get("artifacts", {})
        produced_paths: List[Path] = []
        for key, relative in artifacts.items():
            path_value = apply_placeholders(
                relative,
                scenario_dir=scenario_dir,
                output_root=output_root,
                playlist_path=resolved_playlist,
                epg_path=resolved_epg,
                drop_file=drop_file,
                baseline_path=baseline_output,
                workspace_path=workspace,
            )
            path = (scenario_dir / path_value).resolve()
            if not path.exists():
                # allow absolute paths in artifacts
                path = Path(path_value).resolve()
            produced_paths.append(path)
        try:
            ensure_cli_outputs(produced_paths)
        except RuntimeError as exc:
            results.append(
                ScenarioResult(name=name, succeeded=False, details=str(exc))
            )
            continue

        diff_target = scenario.get("compareToBaseline")
        diff_path: Optional[Path] = None
        if diff_target:
            target_value = apply_placeholders(
                diff_target,
                scenario_dir=scenario_dir,
                output_root=output_root,
                playlist_path=resolved_playlist,
                epg_path=resolved_epg,
                drop_file=drop_file,
                baseline_path=baseline_output,
                workspace_path=workspace,
            )
            candidate = (scenario_dir / target_value).resolve()
            if not candidate.exists():
                candidate = Path(target_value).resolve()
            diff_text = unified_diff_text(baseline_output, candidate)
            diff_path = diff_root / f"{name}.diff"
            diff_path.write_text(diff_text, encoding="utf-8")
            status = "identical" if not diff_text else "differs"
            details = (
                f"Compared against baseline ({status}). Diff saved to {diff_path}"
            )
        else:
            details = "Succeeded"

        results.append(
            ScenarioResult(
                name=name,
                succeeded=True,
                details=details,
                diff_path=diff_path,
            )
        )

    print("[matrix] Summary")
    failures = 0
    for result in results:
        status = "PASS" if result.succeeded else "FAIL"
        print(f"  [{status}] {result.name}: {result.details}")
        if not result.succeeded:
            failures += 1

    if failures:
        print(f"[matrix] {failures} scenario(s) failed.")
        return 1

    print("[matrix] All scenarios succeeded.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
