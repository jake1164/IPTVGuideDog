#!/usr/bin/env python3
import argparse, re, os
from urllib.request import urlopen, Request
from urllib.parse import urlparse, parse_qs

# ---------------------------
# Regexes
# ---------------------------
GROUP_PATTERNS = [
    re.compile(r'group-title="([^"]+)"', re.IGNORECASE),
    re.compile(r'tvg-group="([^"]+)"', re.IGNORECASE),
]
NAME_FROM_EXTINF = re.compile(r'#EXTINF[^,]*,(?P<name>.*)$', re.IGNORECASE)

# ---------------------------
# I/O helpers
# ---------------------------
def read_lines(src: str):
    if src.startswith(("http://", "https://")):
        req = Request(src, headers={"User-Agent": "curl/8"})
        with urlopen(req, timeout=60) as r:
            data = r.read()
        try:
            return data.decode("utf-8", errors="ignore").splitlines()
        except UnicodeDecodeError:
            return data.decode("latin-1", errors="ignore").splitlines()
    else:
        with open(src, "r", encoding="utf-8", errors="ignore") as f:
            return f.read().splitlines()

def write_atomic(path: str, text: str):
    tmp = f"{path}.tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        f.write(text)
    os.replace(tmp, path)

# ---------------------------
# Parsing helpers
# ---------------------------
def extract_group(extinf_line: str):
    for pat in GROUP_PATTERNS:
        m = pat.search(extinf_line)
        if m:
            return m.group(1).strip()
    return None

def extract_title(extinf_line: str):
    m = NAME_FROM_EXTINF.search(extinf_line or "")
    return m.group('name').strip() if m else ""

def iter_entries(lines):
    """
    Yields tuples: (block_lines, url, group)
      - For channel entries: block_lines is the lines from #EXTINF to just before the URL, url is the media URL, group is parsed group
      - For other lines (headers, comments): url is None
    """
    i, n = 0, len(lines)
    while i < n:
        line = lines[i]
        if line.startswith("#EXTINF"):
            block = [line]
            i += 1
            # Collect metadata/comment lines following the EXTINF
            while i < n and lines[i].startswith("#") and not lines[i].startswith("#EXTINF"):
                block.append(lines[i]); i += 1
            # Skip blank/comment lines up to the URL
            while i < n and (not lines[i] or lines[i].startswith("#")):
                block.append(lines[i]); i += 1
            url = None
            if i < n:
                url = lines[i].strip(); i += 1
            grp = extract_group(block[0])
            yield (block, url, grp)
        else:
            if line.strip():
                yield ([line], None, None)
            i += 1

# ---------------------------
# URL-based type detection (optional, strict segments)
# ---------------------------
_KIND_SEGMENTS = {
    "live": "live",
    "lives": "live",
    "movie": "movie",
    "movies": "movie",
    "series": "series",
    "tv": "series",  # some providers use /tv/
}

def detect_kind_by_url(url: str) -> str:
    if not url:
        return "unknown"
    try:
        p = urlparse(url)
        segs = [s for s in (p.path or "").split("/") if s]
        for seg in segs:
            key = seg.lower()
            if key in _KIND_SEGMENTS:
                return _KIND_SEGMENTS[key]
        qs = parse_qs(p.query or "")
        tvals = qs.get("type") or qs.get("kind") or []
        for val in tvals:
            key = (val or "").lower()
            if key in _KIND_SEGMENTS:
                return _KIND_SEGMENTS[key]
        return "unknown"
    except Exception:
        return "unknown"

# ---------------------------
# Group listing
# ---------------------------
def list_groups(src: str, type_filter: str = None):
    kind_set = None
    if type_filter:
        kinds = [k.strip().lower() for k in type_filter.split(",") if k.strip()]
        valid = {"live", "movie", "series", "unknown"}
        bad = [k for k in kinds if k not in valid]
        if bad:
            raise AssertionError(f"Unknown --type value(s): {', '.join(bad)} (valid: live,movie,series,unknown)")
        kind_set = set(kinds)

    lines = read_lines(src)
    groups = set()
    for block, url, grp in iter_entries(lines):
        if url is None:
            continue
        if kind_set:
            kind = detect_kind_by_url(url)
            if kind not in kind_set:
                continue
        if grp:
            groups.add(grp)
    return sorted(groups, key=lambda g: g.lower())

def emit_drop_template(groups):
    header = [
        "######  This is a DROP list. Put a '#' in front of any group you want to KEEP. ######",
        "######  Lines without '#' will be DROPPED. Blank lines are ignored.              ######",
        "",
    ]
    return "\n".join(header + groups) + "\n"

# ---------------------------
# Filtering
# ---------------------------
def load_group_file(path: str, comment_prefix: str = "#", ignore_case: bool = False):
    groups = []
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        for raw in f:
            line = raw.strip()
            if not line or line.startswith(comment_prefix):
                continue
            groups.append(line.lower() if ignore_case else line)
    return set(groups)

def normalize(name: str, ignore_case: bool):
    return name.lower() if (ignore_case and isinstance(name, str)) else name

def filter_playlist(src: str, out_path: str, keep=None, drop=None,
                    keep_file=None, drop_file=None, ignore_case=False,
                    type_filter=None):
    # Build keep/drop sets
    def from_inline(csv):
        if not csv: return set()
        return set([normalize(x.strip(), ignore_case) for x in csv.split(",") if x.strip()])

    keep_set = from_inline(keep)
    drop_set = from_inline(drop)

    if keep_file:
        keep_set |= load_group_file(keep_file, ignore_case=ignore_case)
    if drop_file:
        drop_set |= load_group_file(drop_file, ignore_case=ignore_case)

    # Optional type filter
    kind_set = None
    if type_filter:
        kinds = [k.strip().lower() for k in type_filter.split(",") if k.strip()]
        valid = {"live", "movie", "series", "unknown"}
        bad = [k for k in kinds if k not in valid]
        if bad:
            raise AssertionError(f"Unknown --type value(s): {', '.join(bad)} (valid: live,movie,series,unknown)")
        kind_set = set(kinds)

    # Enforce either keep or drop mode (not both), unless neither provided (no-op)
    if keep_set and drop_set:
        raise AssertionError("Use either keep or drop lists, not both.")

    lines = read_lines(src)
    out = []
    wrote_header = False

    for block, url, grp in iter_entries(lines):
        if url is None:
            # Preserve header if present before first included entry
            if not wrote_header and block and block[0].startswith("#EXTM3U"):
                out.extend(block); wrote_header = True
            continue

        gnorm = normalize(grp or "", ignore_case)
        include = True

        # Apply URL-kind filter first (strict)
        if kind_set:
            kind = detect_kind_by_url(url)
            if kind not in kind_set:
                include = False

        # Apply keep/drop group filters
        if include and keep_set:
            include = (gnorm in keep_set)
        elif include and drop_set:
            include = (gnorm not in drop_set)

        if include:
            if not wrote_header:
                out.append("#EXTM3U"); wrote_header = True
            out.extend(block); out.append(url)

    text = "\n".join(out) + "\n"
    write_atomic(out_path, text)
    kept = sum(1 for line in out if line.startswith("#EXTINF"))
    return kept

# ---------------------------
# CLI
# ---------------------------
def main():
    p = argparse.ArgumentParser(description="M3U group lister & filter")
    sub = p.add_subparsers(dest="cmd", required=True)

    # 1) Create a DROP list template
    dl = sub.add_parser("make-drop-list", help="Print a DROP list template (edit: add '#' to groups you want to KEEP)")
    dl.add_argument("src", help="M3U URL or file path")
    dl.add_argument("--type", dest="type_filter",
                    help="Limit to URL kinds {live,movie,series,unknown} (comma-separated, optional)")

    # 2) Plain list of group names (if you want raw names only)
    lg = sub.add_parser("list-groups", help="Print just the unique group names (sorted)")
    lg.add_argument("src", help="M3U URL or file path")
    lg.add_argument("--type", dest="type_filter",
                    help="Limit to URL kinds {live,movie,series,unknown} (comma-separated, optional)")

    # 3) Filter using a keep/drop config
    flt = sub.add_parser("filter", help="Filter by URL-kind and/or groups and write a new M3U")
    flt.add_argument("src", help="M3U URL or file path")
    flt.add_argument("out", help="Output M3U path")
    g = flt.add_mutually_exclusive_group()
    g.add_argument("--keep", help="Comma-separated groups to keep")
    g.add_argument("--drop", help="Comma-separated groups to drop")
    flt.add_argument("--keep-file", help="Path to file of groups to keep (one per line, '#' comments allowed)")
    flt.add_argument("--drop-file", help="Path to file of groups to drop (one per line, '#' comments allowed)")
    flt.add_argument("--ignore-case", action="store_true", help="Case-insensitive group matching")
    flt.add_argument("--type", dest="type_filter",
                     help="URL-kind filter {live,movie,series,unknown} (comma-separated, optional)")

    args = p.parse_args()

    if args.cmd == "make-drop-list":
        groups = list_groups(args.src, type_filter=getattr(args, "type_filter", None))
        print(emit_drop_template(groups), end="")
        return

    if args.cmd == "list-groups":
        groups = list_groups(args.src, type_filter=getattr(args, "type_filter", None))
        for g in groups:
            print(g)
        return

    # filter
    kept = filter_playlist(
        args.src, args.out,
        keep=args.keep, drop=args.drop,
        keep_file=args.keep_file, drop_file=args.drop_file,
        ignore_case=args.ignore_case,
        type_filter=args.type_filter
    )
    print(f"Wrote {args.out} with {kept} channels")

if __name__ == "__main__":
    main()
