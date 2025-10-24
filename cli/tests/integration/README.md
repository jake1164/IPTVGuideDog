# IPTV CLI Integration Harness

This directory contains a Docker-based integration harness that exercises the new IPTV CLI and compares its output with the legacy Python filter.

## Overview

The container performs the following high-level steps:

1. Mounts this repository and the caller's `.env` file at runtime (no secrets are baked into the image).
2. Builds the CLI (`dotnet build cli/iptv/iptv.csproj`).
3. Downloads or copies a playlist/EPG feed (defaults to samples in this folder, but real URLs can be supplied through environment variables).
4. Runs `legacy/m3u-filter.py` with `legacy/sample_drop_list.txt` to generate a baseline playlist.
5. Executes a scenario matrix that touches every CLI flag and config-file override.
6. Compares CLI output against the baseline and captures diffs/logs under `test-output/`.

The matrix definition lives in `matrix.json`. You can extend it to add more scenarios or tweak the existing ones without rebuilding the container. Configuration files are rendered from `fixtures/test-config.template.yaml` at runtime so each run uses an isolated output directory.

## Building the Test Image

```bash
docker build -t iptv-cli-integration -f cli/tests/integration/Dockerfile .
```

## Running the Suite

```bash
docker run --rm \
  --env-file /path/to/.env \
  -e PLAYLIST_URL="http://example.com/get.php?username=%USER%&password=%PASS%&type=m3u_plus" \
  -v "$(pwd)":/workspace \
  iptv-cli-integration
```

- Mount the repo root at `/workspace` (required).
- Mount or point to your `.env` file at runtime so credentials stay outside the image.
- Optionally provide `PLAYLIST_URL` / `EPG_URL` (or the corresponding `*_TEMPLATE` variants). If omitted, the suite uses the sample data in `data/`.
- Set `TEST_OUTPUT_DIR` to change where artifacts are written (defaults to `/workspace/test-output`).
- Use `DOTNET_CONFIGURATION=Release` for a release build if desired.
- Set `MATRIX_PATH=/workspace/cli/tests/integration/custom-matrix.json` to point at a different scenario list.

### Running Against Multiple Providers

- Provide a comma-separated list of provider-specific `.env` files via `PROVIDER_ENV_FILES`. The entrypoint executes the entire matrix once per file, writing artifacts under `TEST_OUTPUT_DIR/<provider-name>/`.
- Provider names are derived from the env file basename (e.g., `providers/sample.env` → `sample`).
- Example:
  ```bash
  docker run --rm \
    -v "$(pwd)":/workspace \
    --env-file /workspace/providers/base.env \
    -e PROVIDER_ENV_FILES="/workspace/providers/providerA.env,/workspace/providers/providerB.env" \
    iptv-cli-integration
  ```
- To run a single provider override, set `PROVIDER_ENV_FILE=/workspace/providers/providerA.env`.

## Outputs

Results are written under `test-output/` (or the directory specified by `TEST_OUTPUT_DIR`):

- `artifacts/` – fetched playlist/EPG and baseline output.
- `scenarios/<name>/` – per-scenario stdout/stderr logs and generated files.
- `diffs/` – unified diffs between CLI output and the legacy baseline for scenarios that request comparison.

Any failing scenario is reported at the end of the run, with pointers to the relevant logs.

## Feeding Real Data

The harness replaces `%USER%`/`%PASS%` (and `${USER}`/`${PASS}`) in `PLAYLIST_URL`, `PLAYLIST_TEMPLATE`, `EPG_URL`, or `EPG_TEMPLATE` using values from the mounted `.env`. Provide fully expanded URLs if your provider requires additional tokens.

## Extending the Matrix

- Add new scenarios to `matrix.json`. Use `{playlist_url}`, `{epg_url}`, `{drop_file}`, `{scenario_dir}`, and `{output_root}` placeholders to reference runtime paths.
- To compare against the baseline playlist, set `compareToBaseline` in the scenario to the relative path of the generated M3U.
- Update `fixtures/test-config.template.yaml` or add new templates to test different profiles/overrides. The generated file lives at `runtime/test-config.yaml`.

## Legacy Script Requirements

The entrypoint fails fast if `legacy/m3u-filter.py` is missing or empty. Ensure your legacy filter is present when you invoke the container so the baseline comparison remains meaningful.

## Automating on GitHub

- A sample workflow is provided in `.github/workflows/integration-tests.yml`. It builds the Docker image and runs the suite weekly against the bundled sample data.
- To run against real providers on GitHub, store provider credentials and URLs as repository secrets, have a preparatory step write per-provider `.env` files, and set `PROVIDER_ENV_FILES` before launching the container.
