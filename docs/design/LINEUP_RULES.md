# Lineup Rules (User-Facing Model)

## Lineup = “Channel List for a Device/Room”
Users can create multiple lineups (profiles), each with its own endpoint:
- /m3u/default
- /m3u/livingroom
- /m3u/mancave

## Cloning
When creating a new lineup, the user selects a lineup to clone from.
Cloning copies:
- enabled groups/channels
- group start numbers
- pinned channel numbers
- ordering and overrides

## Dynamic Groups (Auto-update)
Each lineup shows a list of groups with a checkbox:
- Checked = this group is included in the lineup
- Unchecked = excluded

When a group is checked:
- channels in that provider group are automatically added/removed as they appear/disappear upstream
- this reduces user babysitting for volatile sports/PPV content

This is called “Dynamic Group” (or “Auto-update group”) in the UI.

## New Channels Inbox
Newly discovered channels are NOT automatically added to lineups by default.
They appear in a “New Channels” list so the user can review.

Exception:
- if a channel belongs to an enabled Dynamic Group in a lineup, it is auto-added to that lineup.
