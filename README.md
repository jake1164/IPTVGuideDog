# IPTVGuideDog 🐕

**Your open-source IPTV companion** — intelligently filters massive M3U playlists and EPG data down to just the channels you care about.

---

## 🎯 Why IPTVGuideDog?

IPTV providers often deliver **thousands of channels** across dozens of groups — sports, movies, international content, VOD libraries, and more. Most users only watch a fraction of these channels, but managing huge playlists is cumbersome:

- **Slow loading times** in media players
- **Difficult navigation** through irrelevant content  
- **Manual editing** is error-prone and tedious
- **No easy way to filter** live streams vs. VOD content
- **Credentials scattered** across URLs and scripts

**IPTVGuideDog solves these problems** with a powerful, flexible CLI that:

✅ **Filters playlists by channel groups** — keep what you want, drop the rest  
✅ **Supports live-only filtering** — isolate live streams from VOD/series  
✅ **Manages credentials securely** — `.env` files keep secrets out of git and auto-substitute `%VAR%` tokens in URLs  
✅ **Preserves EPG data** — filtered guide matches your filtered channels  
✅ **Works cross-platform** — PowerShell, Bash, CMD, Docker  
✅ **Automatable** — perfect for cron jobs and scheduled updates  

---

## 🚀 What the CLI Offers

### **Two Powerful Commands**

#### 1️⃣ `iptv groups` — Discover & Curate
Extract all channel groups from your playlist into a simple text file, then decide what to keep:

```bash
iptv groups --playlist-url "https://provider.com/playlist.m3u" --out-groups groups.txt --live
```

**Edit `groups.txt`** — comment lines with `#` to **KEEP**, leave uncommented to **DROP**:
```
#Sports           ← Keep
#News             ← Keep
Movies            ← Drop
International     ← Drop
##Documentary     ← Newly added (marked with ##)
```

**Features:**
- **Live-only mode** (`--live`) — only enumerate live streams, ignore VOD/series
- **Incremental updates** — new groups are automatically added and marked with `##`
- **Automatic backups** — never lose your curation work
- **Version tracking** — file format validation prevents conflicts

---

#### 2️⃣ `iptv run` — Fetch, Filter & Export
One-shot pipeline that downloads your playlist and EPG, applies group filters, and writes clean outputs:

```bash
iptv run \
  --playlist-url "https://provider.com/get.php?username=%USER%&password=%PASS%&type=m3u_plus" \
  --epg-url "https://provider.com/xmltv.php?username=%USER%&password=%PASS%" \
  --groups-file groups.txt \
  --out-playlist filtered.m3u \
  --out-epg filtered.xml \
  --live
```

**Features:**
- **Group filtering** — apply your curated `groups.txt` selections
- **Live-only mode** — filter out VOD/series, keep only live streams
- **Secure credential handling** — `.env` files auto-substitute any `%VAR%` tokens inside playlist/EPG URLs
- **Atomic writes** — ensures output files are always valid (no partial writes)
- **Verbose logging** — track exactly what's being filtered and why
- **Config profiles** — store common setups in YAML for reuse

---

### **Credential Management Made Easy**

Stop embedding passwords in URLs or scripts! Use `.env` files:

**`.env` file:**
```env
PRIMARY_USER=your_primary_username
PRIMARY_PASS=your_primary_password
SECONDARY_TOKEN=another_secret
```

> Place `.env` next to your `config.yaml` (for example `iptv/scripts/.env`) when you run `iptv` with `--config`. Otherwise, the CLI reads `.env` from your current working directory.

**Command:**
```bash
iptv run --playlist-url "https://host/get.php?username=%USER%&password=%PASS%&type=m3u_plus" --out-playlist out.m3u
```

- ✅ **Shell-safe** — `%VAR%` placeholders work in PowerShell, Bash, CMD, Zsh  
- ✅ **Git-ignored** — credentials never leave your machine  
- ✅ **Deterministic search order** — `.env` lives next to your config when `--config` is used; otherwise the CLI reads the working directory  
- ✅ **Multi-provider ready** — define as many keys as you want (`%PRIMARY_USER%`, `%SECONDARY_TOKEN%`, etc.) and they are auto-substituted inside playlist/EPG URLs

📖 **[Full details in env_usage.md](docs/env_usage.md)**

---

### **Config-Driven Workflows**

For advanced users, define reusable profiles in `config.yaml`:

```yaml
profiles:
  default:
    inputs:
      playlist:
        url: "${PROVIDER_PLAYLIST_URL}"
      epg:
        url: "${PROVIDER_EPG_URL}"
    output:
      playlistPath: "/data/out/playlist.m3u"
      epgPath: "/data/out/epg.xml"
```

Then run:
```bash
iptv run --config config.yaml --profile default --live
```

📖 **[Full config schema in config_spec.md](docs/config_spec.md)**

### Config Quick Start (multi-profile automation)

**1. Lay out secrets and directories**

```bash
mkdir -p iptv/scripts /var/lib/iptv/m3u
cat <<'EOF' > iptv/scripts/.env
PRIMARY_USER=alice@example.com
PRIMARY_PASS=super-secret
SECONDARY_USER=bob@example.com
SECONDARY_TOKEN=another-secret-token
EOF
```

Keeping `.env` next to `iptv/scripts/config.yaml` ensures the CLI picks it up automatically whenever you pass `--config iptv/scripts/config.yaml`.

**2. Create `iptv/scripts/config.yaml` with multiple profiles**

```yaml
profiles:
  primary:
    inputs:
      playlist:
        url: "https://provider-a.example/get.php?username=%PRIMARY_USER%&password=%PRIMARY_PASS%&type=m3u_plus"
      epg:
        url: "https://provider-a.example/xmltv.php?username=%PRIMARY_USER%&password=%PRIMARY_PASS%"
    filters:
      groupsFile: "/var/lib/iptv/m3u/primary.groups.txt"
    output:
      playlistPath: "/var/lib/iptv/m3u/primary.m3u"
      epgPath: "/var/lib/iptv/m3u/primary.xml"

  secondary:
    inputs:
      playlist:
        url: "https://provider-b.example/api/m3u?user=%SECONDARY_USER%&token=%SECONDARY_TOKEN%"
      epg:
        url: "https://provider-b.example/api/xmltv?user=%SECONDARY_USER%&token=%SECONDARY_TOKEN%"
    filters:
      dropListFile: "/var/lib/iptv/m3u/secondary.remove.txt"
    output:
      playlistPath: "/var/lib/iptv/m3u/secondary.m3u"
      epgPath: "/var/lib/iptv/m3u/secondary.xml"
```

**3. Run each profile manually (great for testing)**

```bash
iptv run --config iptv/scripts/config.yaml --profile primary --live
iptv run --config iptv/scripts/config.yaml --profile secondary --live
```

**4. Automate refreshes with cron**

```bash
*/30 * * * * cd /opt/IPTVGuideDog && iptv run --config iptv/scripts/config.yaml --profile primary --live
15 * * * * cd /opt/IPTVGuideDog && iptv run --config iptv/scripts/config.yaml --profile secondary --live
```

**5. Or wire it up to systemd timers**

```ini
# /etc/systemd/system/iptv-primary.service
[Unit]
Description=Refresh IPTVGuideDog primary profile
After=network-online.target

[Service]
Type=oneshot
WorkingDirectory=/opt/IPTVGuideDog
ExecStart=/usr/local/bin/iptv run --config /opt/IPTVGuideDog/iptv/scripts/config.yaml --profile primary --live

# /etc/systemd/system/iptv-primary.timer
[Unit]
Description=Run IPTVGuideDog primary profile every 30 minutes

[Timer]
OnUnitActiveSec=30min
Persistent=true

[Install]
WantedBy=timers.target
```

Enable the timer with `sudo systemctl enable --now iptv-primary.timer`. Repeat the service/timer pair for other profiles (e.g., `secondary`) and the CLI will continue to load `iptv/scripts/.env` because it sits next to the config file.

---

## 🛠️ Getting Started

### **Installation**

```bash
# Clone the repository
git clone https://github.com/jake1164/IPTVGuideDog.git
cd IPTVGuideDog

# Build the CLI (requires .NET 8+)
dotnet build src/IPTVGuideDog.Cli

# Run the CLI
dotnet run --project src/IPTVGuideDog.Cli -- --help
```

Or create an alias for convenience:
```bash
# PowerShell
Set-Alias iptv "dotnet run --project src/IPTVGuideDog.Cli --"

# Bash/Zsh
alias iptv="dotnet run --project src/IPTVGuideDog.Cli --"
```

---

### **Publishing as a Single Binary**

For easier distribution, you can publish the CLI as a self-contained single executable that includes the .NET runtime. Users won't need to have .NET installed on their machines.

#### **Publish Commands**

**For Windows (x64):**
```bash
dotnet publish src/IPTVGuideDog.Cli/IPTVGuideDog.Cli.csproj -c Release -r win-x64 -o ./publish/win-x64
```

**For Linux (x64):**
```bash
dotnet publish src/IPTVGuideDog.Cli/IPTVGuideDog.Cli.csproj -c Release -r linux-x64 -o ./publish/linux-x64
```

**For macOS (Intel):**
```bash
dotnet publish src/IPTVGuideDog.Cli/IPTVGuideDog.Cli.csproj -c Release -r osx-x64 -o ./publish/osx-x64
```

**For macOS (Apple Silicon):**
```bash
dotnet publish src/IPTVGuideDog.Cli/IPTVGuideDog.Cli.csproj -c Release -r osx-arm64 -o ./publish/osx-arm64
```

The single executable will be located in the specified output directory (e.g., `./publish/win-x64/`). The binary is:
- ✅ **Self-contained** — includes the .NET runtime
- ✅ **Single file** — one executable, no dependencies
- ✅ **Trimmed & compressed** — optimized for size
- ✅ **Ready to distribute** — just copy and run

You can then run the executable directly:
```bash
# Windows
./publish/win-x64/IPTVGuideDog.Cli.exe --help

# Linux/macOS
./publish/linux-x64/IPTVGuideDog.Cli --help
```

---

### **Quick Start: 3-Step Workflow**

#### **Step 1: Create a `.env` file** (optional, for credential security)
```env
USER=your_username
PASS=your_password
```

#### **Step 2: Generate a groups file**
```bash
iptv groups \
  --playlist-url "https://provider.com/get.php?username=%USER%&password=%PASS%&type=m3u_plus" \
  --out-groups groups.txt \
  --live
```

**Edit `groups.txt`** — add `#` before groups you want to keep.

#### **Step 3: Run the filter**
```bash
iptv run \
  --playlist-url "https://provider.com/get.php?username=%USER%&password=%PASS%&type=m3u_plus" \
  --epg-url "https://provider.com/xmltv.php?username=%USER%&password=%PASS%" \
  --groups-file groups.txt \
  --out-playlist filtered.m3u \
  --out-epg filtered.xml \
  --live
```

**Done!** Load `filtered.m3u` and `filtered.xml` into your favorite media player.

---

### **Automate with Cron (Linux/macOS)**

Keep your playlist fresh automatically:

```bash
# Update every 30 minutes
*/30 * * * * cd /path/to/IPTVGuideDog && iptv run --config config.yaml --profile default --live
```

📖 **[See cli_spec.md for full command reference](docs/cli_spec.md)**

---

## 🌐 Use Cases

### **Who Should Use IPTVGuideDog?**

- **Cord-cutters** managing large IPTV subscriptions  
- **Home media enthusiasts** running Plex, Jellyfin, or Emby  
- **System administrators** deploying IPTV in multi-user environments  
- **Developers** integrating IPTV filtering into automation pipelines  
- **Anyone** overwhelmed by 5,000+ channel playlists  

### **Common Scenarios**

- 📺 **Filter out VOD libraries** — keep only live TV channels  
- 🌍 **Remove international content** — focus on local channels  
- 📍 **Filter out-of-region channels** — drop "US West Coast" feeds if you're on the East Coast, or remove UK regional channels you don't need
- ⚽ **Sports-only playlist** — isolate sports groups for dedicated apps  
- 🏠 **Per-room filtering** — different playlists for different family members  
- 🤖 **Scheduled updates** — automate with cron/Task Scheduler  

---

## 🗺️ Roadmap

### **Current Focus**
- ✅ **CLI tool** (stable, production-ready)
- ✅ **Group-based filtering** with `.env` credential support
- ✅ **Live-only filtering** for separating live streams from VOD
- ✅ **EPG synchronization** with filtered playlists

### **Coming Soon**
- 🚧 **Self-hosted Docker web UI** — manage playlists through a browser
  - Web-based group selection interface
  - Multi-profile management
  - Real-time playlist previews
  - Scheduled updates with built-in cron
  - Docker Compose deployment for easy self-hosting

### **Future Ideas**
- 🔮 **Advanced filtering** — regex-based title/description filters
- 🔮 **Channel deduplication** — merge duplicate channels across groups
- 🔮 **Multi-provider support** — combine playlists from multiple sources
- 🔮 **API mode** — REST API for third-party integrations

💡 **Have a feature request?** [Open an issue](https://github.com/jake1164/IPTVGuideDog/issues) or contribute a PR!

---

## 📚 Documentation

- **[CLI Commands & Usage](docs/cli_spec.md)** — Full command reference
- **[Config Schema](docs/config_spec.md)** — YAML/JSON config structure  
- **[Environment Variables](docs/env_usage.md)** — `.env` file usage guide  
- **[Groups File Format](docs/groups_file_format.md)** — File format specification
- **[Version Management](docs/version_management.md)** — How to manage build versions

---

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

Built with ❤️ for the IPTV community. If IPTVGuideDog helps you, consider ⭐ starring the repo!

---

**Made with .NET 8 | Cross-platform | Open Source**
