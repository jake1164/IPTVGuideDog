# IPTVGuideDog
Your open-source helper for IPTV - guiding you through big M3U/EPG playlists to the channels you actually want.

## Configuration

1. Copy `cli/config.template.yaml` to `cli/config.yaml`.
2. Replace `{{PLAYLIST_URL}}` and `{{EPG_URL}}` with your provider endpoints (you can keep `%USER%`/`%PASS%` placeholders so they expand from `.env` files at runtime).

The real `cli/config.yaml` is ignored by git so private URLs never leave your machine.
