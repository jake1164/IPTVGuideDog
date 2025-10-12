# IPTV CLI – Commands & Usage

This document defines the command surface and runtime behavior for the `iptv` CLI. It references the shared configuration described separately in **Config – Schema**.

---

## Subcommands

```
iptv groups   [options]
iptv run      [options]
```

---

## Command: `iptv groups`

Create or refresh the **group selection file** used by filtering.

### Flags

* `--playlist-url <url>`  (or `--config ... --profile ...`)
* `--out <path>` (optional). If omitted, groups are written to **stdout** for piping.
* `--verbose`
* `--live`

### Note on `--live` flag

- `--live` (optional): when provided, only **live** streams are enumerated (simple heuristics: stream URL contains a `/live/` path segment or `type=live` query). This helps you export a group file that reflects live channels only.

### Behavior

* Downloads groups and writes a plain text file, one group per line. 

### Examples

```bash
iptv groups \
  --playlist-url "https://host/get.php?username=U&password=P&type=m3u_plus&output=ts" \
  --out ./groups.txt \
  --preserve-comments \
  --verbose
  --live
# Edit groups.txt → comment to KEEP, leave bare to DROP
```

```bash
iptv groups --config /etc/iptv/config.yml --profile default --out /config/groups.txt --verbose --live
```

---

## Command: `iptv run`

One-shot pipeline: **fetch → filter → write**.

### Flags

* **Inputs**

  * `--playlist-url <url>` (required if `--config` not provided)
  * `--epg-url <url>` (optional)
* **Filtering**

  * `--groups-file <path>` (optional) — text file; one group per line:
  * `--live` (optional) - details in Note
    * **Commented (`#`) = KEEP**
    * **Uncommented = DROP**
* **Outputs**

  * `--out-playlist <path>` (recommended; use `-` for **stdout**)
  * `--out-epg <path>` (required if `--epg-url` provided; use `-` for **stdout**)
* **Runtime**

  * `--config <path>` (optional)
  * `--profile <name>` (default `default`)
  * `--verbose`

#### Note on `--live`

- `--live` (optional): when provided, only **live** streams are processed (same heuristics as above). This filter is applied **before** reading and applying the groups file.
#### Note on Stdout behavior
- If **only a playlist** is being produced and `--out-playlist` is **omitted**, the playlist is written to **stdout**.
- If an **EPG** is also being produced, at least one of `--out-playlist` or `--out-epg` must be provided; alternatively, pass `--out-playlist -` or `--out-epg -` to write that artifact to **stdout**.

### Behavior

1. If `--config` is present, load profile; then apply any flag overrides.
2. If a suitable `.env` exists (see Modes of use), substitute `$USER`/`$PASS` **inside URL strings/fields only**.
3. Fetch playlist (and EPG if provided).
4. Parse M3U/XMLTV.
5. if `--live` is provided only stream urls containting `/live/` OR `type=live` are kept, everything else (series/vod) are dropped. 
5. If `--groups-file` provided: **drop** any groups that are not commented (commented lines are kept).
6. Write outputs atomically to `--out-playlist`/`--out-epg`.
7. Log newly discovered groups and summarize channel changes **only for kept groups**.

### Examples

```bash
iptv run \
  --playlist-url "https://host/get.php?username=U&password=P&type=m3u_plus&output=ts" \
  --epg-url "https://host/xmltv.php?username=U&password=P" \
  --groups-file ./groups.txt \
  --out-playlist ./out/playlist.m3u \
  --out-epg ./out/epg.xml \
  --verbose
  --live
```

**Config-driven:**

```bash
iptv run --config /etc/iptv/config.yml --profile default --verbose --live
# If /etc/iptv/.env exists, `$USER` and `$PASS` are substituted only in URL fields.
```

---

## Environment behavior (strict)
* **Only** `$USER` and `$PASS` are recognized, and **only** when a `.env` file exists.
* **Zero-config mode:** if `./.env` exists, `$USER`/`$PASS` are substituted **inside URL strings only** before fetching.
* **Config mode:** if a `.env` file exists in the **same directory as the `--config` file**, `$USER`/`$PASS` are substituted **inside URL fields only** before fetching.
* If no `.env` is present, no substitution occurs.
* Embedding credentials directly in URLs is fully supported.

---

## Exit codes

* `0` success
* `2` config/validation error
* `3` network error
* `4` auth error
* `5` IO error
* `6` parse error

---

## Minimal cron example

```bash
*/30 * * * * root iptv run --config /etc/iptv/config.yml --profile default --live
```
