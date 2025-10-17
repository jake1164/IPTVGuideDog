# Environment Variable Usage (.env file)

This guide explains how to use `.env` files for credential management with the IPTV CLI.

---

## Quick Start

### 1. Create a `.env` file

In your working directory (or next to your config file), create a file named `.env`:

```env
USER=your_username
PASS=your_password
```

**Important:** The `.env` file is automatically ignored by git (added to `.gitignore`).

---

## Using Credentials in Commands

### PowerShell Example

```powershell
iptv groups --playlist-url "http://host/get.php?username=%USER%&password=%PASS%&type=m3u_plus&output=ts" --out-groups groups.txt --verbose
```

### Bash/Linux Example

```bash
iptv groups --playlist-url "http://host/get.php?username=%USER%&password=%PASS%&type=m3u_plus&output=ts" --out-groups groups.txt --verbose
```

### Windows CMD Example

```cmd
iptv groups --playlist-url "http://host/get.php?username=%USER%&password=%PASS%&type=m3u_plus&output=ts" --out-groups groups.txt --verbose
```

---

## How It Works

1. **The CLI reads `.env` from:**
   - Current working directory (zero-config mode)
   - Same directory as `--config` file (config mode)

2. **Only `USER` and `PASS` are recognized** (case-insensitive).

3. **Substitution format:** `%USER%` and `%PASS%` (percent-delimited).

4. **Substitution happens only in URL strings** before fetching.

5. **Shell-safe:** The `%...%` format is **not** expanded by any common shell (PowerShell, Bash, Zsh, CMD).

---

## Verbose Mode

Use `--verbose` to see what's happening:

```
[VERBOSE] .env file found: C:\path\to\.env
[VERBOSE] Keys found: USER, PASS
[VERBOSE] Playlist URL: replaced USER, PASS
```

**Note:** Actual credential values are never printed in logs.

---

## Why `%USER%` and `%PASS%`?

We use percent-delimited format because:

- ? **PowerShell** does not expand `%...%` (unlike `$...` or `${...}`)
- ? **Bash/Zsh** does not expand `%...%` 
- ? **CMD** does not expand `%...%` in this context (URL query strings)
- ? Works consistently across all platforms

**Avoided formats:**
- ? `$USER` / `$PASS` — Expanded by PowerShell and Bash
- ? `${USER}` / `${PASS}` — Expanded by PowerShell (braced variable syntax)

---

## Config File Mode

When using a config file, place `.env` in the same directory:

```
/etc/iptv/
  ??? config.yml
  ??? .env
```

Example config with placeholders:

```yaml
profiles:
  default:
    inputs:
      playlist:
        url: "http://host/get.php?username=%USER%&password=%PASS%&type=m3u_plus&output=ts"
      epg:
        url: "http://host/xmltv.php?username=%USER%&password=%PASS%"
```

Command:

```bash
iptv run --config /etc/iptv/config.yml --profile default
```

---

## Security Best Practices

1. **Never commit `.env` to version control** (already in `.gitignore`).
2. **Set appropriate file permissions:**
   - Linux/macOS: `chmod 600 .env` (read/write for owner only)
   - Windows: Use file properties to restrict access
3. **Use separate credentials** for testing vs. production.
4. **Rotate credentials regularly.**

### Automatic Credential Redaction

The CLI automatically redacts sensitive information from all logs and error messages:

- **URLs are sanitized:** Only the scheme, host, port, and path are shown in logs (query strings are removed)
- **Example:** `http://host/get.php?username=user&password=pass` becomes `http://host/get.php`
- **No credentials are ever printed** in verbose mode, error messages, or diagnostic output

This ensures that even if logs are shared or visible to others, your credentials remain secure.

---

## Troubleshooting

### "username=&password=" in error messages
**Problem:** Placeholders were not replaced.

**Solution:** 
- Verify `.env` exists in the correct directory (use `--verbose` to see where CLI is looking)
- Check that `.env` contains `USER=...` and `PASS=...` lines
- Ensure you're using `%USER%` and `%PASS%` (with percent signs, not `$` or `${...}`)

### Credentials not substituted
**Problem:** `.env` file not found or in wrong location.

**Solution:** 
- Verify `.env` is in current directory (or config directory).
- Use `--verbose` to see where the CLI is looking for `.env`.
- Check that `.env` contains `USER=...` and `PASS=...` lines.

### File permission errors
**Problem:** `.env` file is not readable.

**Solution:** Check file permissions and ownership.

---

## Example `.env` File

```env
# IPTV Provider Credentials
USER=myusername
PASS=mypassword123

# Lines starting with # are comments and ignored
# Blank lines are also ignored
```

---

## Alternative: Embedded Credentials

If you prefer, you can embed credentials directly in URLs (not recommended for production):

```bash
iptv groups --playlist-url "http://host/get.php?username=U&password=P&type=m3u_plus&output=ts"
```

However, using `.env` files is more secure and maintainable.
