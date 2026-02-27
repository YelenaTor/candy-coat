# Candy Coat — User Tutorial

> A complete guide to every feature of the Candy Coat venue assistant plugin.

**Open the plugin:** `/candy`

---

## Table of Contents

1. [First-Time Setup](#1-first-time-setup)
2. [Main Window Layout](#2-main-window-layout)
3. [Dashboard Tabs](#3-dashboard-tabs)
   - [Overview](#overview)
   - [Bookings](#bookings)
   - [Locator](#locator)
   - [Session Capture](#session-capture)
   - [Waitlist](#waitlist)
   - [Staff Shifts](#staff-shifts)
   - [Settings](#settings)
4. [Staff Role Toolbox (SRT)](#4-staff-role-toolbox-srt)
   - [Sweetheart Panel](#sweetheart-panel)
   - [Candy Heart Panel](#candy-heart-panel)
   - [Bartender Panel](#bartender-panel)
   - [Gamba Panel](#gamba-panel)
   - [DJ Panel](#dj-panel)
   - [Management Panel](#management-panel--protected)
   - [Owner Panel](#owner-panel--protected)
5. [Patron Details Window](#5-patron-details-window)
6. [Session Window](#6-session-window)
7. [Cosmetics Window](#7-cosmetics-window)
8. [Key Workflows](#8-key-workflows)
9. [Service Behaviors](#9-service-behaviors)
10. [Color Coding Reference](#10-color-coding-reference)
11. [Troubleshooting](#11-troubleshooting)

---

## 1. First-Time Setup

When you install Candy Coat and run `/candy` for the first time, the **Setup Wizard** opens automatically and walks you through seven steps.

| Step | What You Do |
|------|-------------|
| **1 — Identity** | Click **"Detect Current Character"** to auto-fill your name and world, or type them manually. |
| **2 — Dependencies** | Confirms whether Glamourer and ChatTwo are detected. Click **"Re-check Dependencies"** after installing either. |
| **3 — Role Selection** | Pick your staff role (Sweetheart, Candy Heart, Bartender, Gamba, DJ, Management, Owner). Owner and Management require a passcode. Enable **Multi-Role** if you hold more than one role. |
| **4 — Initial Configuration** | Toggle Glamourer and ChatTwo integrations on or off. |
| **5 — Sync Information** | Read about the optional backend sync feature. |
| **6 — Sync Configuration** | If sync is enabled, enter your **API URL** and **Venue Key** (provided by your Owner). |
| **7 — Finish** | Review your name, role, and sync status, then click **"Finish & Launch"**. |

You can re-open the wizard at any time from **Settings → Role Management**.

---

## 2. Main Window Layout

```
┌─────────────────────────────────────────────────────┐
│  Sidebar          │  Content Panel                  │
│                   │                                 │
│  [Dashboard]      │  (selected tab or SRT panel)    │
│  ─ Overview       │                                 │
│  ─ Bookings       │                                 │
│  ─ Locator        │                                 │
│  ─ Session        │                                 │
│  ─ Waitlist       │                                 │
│  ─ Shifts         │                                 │
│                   │                                 │
│  [Role Toolbox]   │                                 │
│  ─ (your panels)  │                                 │
│                   │                                 │
│  ─────────────    │                                 │
│  🟢 Synced        │                                 │
│  ✨ Cosmetics     │                                 │
│  ⚙ Settings      │                                 │
│  ☕ Ko-Fi         │                                 │
└─────────────────────────────────────────────────────┘
```

- **Sidebar** — navigate between all tabs and your role-specific SRT panels.
- **Footer** — always visible. Shows sync status, opens the Cosmetics window, navigates to Settings, and links to Ko-Fi.
- **Protected roles** (Management, Owner) show a padlock until the passcode is entered in Settings.

---

## 3. Dashboard Tabs

### Overview

A quick-glance summary of your shift and venue activity.

- **All staff:** Current clock-in status, shift duration, shift earnings, active booking count, and waitlist size.
- **Management mode (unlocked):** Today's total earnings, all-time earnings, and the top 5 spenders across all patrons.

---

### Bookings

Create and track time-boxed service sessions.

**Creating a booking:**
1. Enter the patron's name, service type, assigned room, and Gil amount.
2. Click **"Add Booking"** — the booking appears in the table and syncs to the backend if enabled.

**Booking table columns:** Patron · Service · Room · Amount · Timer · Status

**Timer behavior:**
- Counts down in real time.
- At ≤5 minutes remaining: timer pulses and a chat alert fires.
- When time expires: displays **OVERDUE** in red.

**Managing bookings:**
- Right-click any row to change its state: **Active → Completed (Paid / Unpaid) → Inactive**, or delete it.

**Team bookings (sync required):**
- A second section shows all active bookings from other online staff, including their name and status.

---

### Locator

Track known patrons and receive alerts when they appear nearby.

**Adding patrons:**
- Type a first name, last name, and world, then click **Add**.
- Or target the player in-game and click **"Detect Targeted"** to auto-fill.

**Nearby regulars panel:**
- Lists tracked patrons currently within render distance (~100 yalms).
- Shows distance in meters and their loyalty tier.
- Icons: ♥ Regular · ★ Elite · ⚠ Warning · 🚫 Blacklisted
- Click **👁** to target them in-game.

**Arrival alerts (automatic):**
- **Regular/Elite:** Green chat message with visit count, last visit date, and tier.
- **Warning/Blacklisted:** Red/yellow chat message with stored notes.
- Alerts fire once per patron per session and reset when they leave range.

**Patron list:** Click any name to open their [Patron Details Window](#5-patron-details-window). Right-click to remove from tracking.

---

### Session Capture

Record a chat conversation with a specific patron.

1. Type the patron's name or click **"Use Current Target"**.
2. Click **"Start Session"** — the [Session Window](#6-session-window) overlay opens.
3. All messages to and from that patron are captured with timestamps.
4. Click **"Stop Session"** to end capture.

**ChatTwo integration:** If ChatTwo is installed, right-clicking a player's name in chat shows a **"Start Session"** option — no need to open this tab manually.

---

### Waitlist

Manage a first-in, first-out queue of patrons waiting for service.

- **Add:** Type a patron name and press Enter or click **Add**.
- **Table columns:** Position · Name · Time Waited
- **Per-entry context menu (right-click):**
  - **Remove from Queue** — removes the entry.
  - **Notify Ready (Tell)** — sends `/t {patron} We are ready for you! Please head to the venue.`
- **Clear All** — empties the entire queue after a confirmation prompt.

---

### Staff Shifts

Track your work time and accumulated earnings.

- **Clock In / Clock Out** — large toggle buttons.
- **Active shift display:** Elapsed time (HH:MM:SS) and Gil earned so far.
- **Shift history:** Last 5 completed shifts with date, duration, and earnings.

Shift earnings are updated automatically whenever a trade is detected ([TradeMonitorService](#tradeMonitorservice)) or logged manually from an SRT panel.

---

### Settings

Configure everything that isn't role-specific.

| Section | What's Here |
|---------|-------------|
| **Role Management** | Change your primary role; unlock Owner/Management with passcode; enable multi-role. |
| **Integrations** | Toggle Glamourer and ChatTwo. |
| **Sync / API** | Enable sync, set API URL and Venue Key, test connection. |
| **Custom Macros** | Create `/t {name}` quick-tell templates usable from patron profiles. `{name}` is replaced with the patron's first name. |
| **Management Access** | Enter passcode to unlock the management analytics view. |
| **Support & Feedback** | Links to the GitHub issue tracker and Discord. |

---

## 4. Staff Role Toolbox (SRT)

SRT panels appear in the sidebar **only for your assigned roles**. Each panel is purpose-built for that role's workflow.

---

### Sweetheart Panel

For entertainers and companions.

| Section | What It Does |
|---------|--------------|
| **Session Timer** | Start/end a timed session (min 15 min). Alerts at 5 min and 2 min remaining; shows "TIME'S UP!" when expired. Auto-marks the selected room as Occupied. |
| **Room Assignment** | Dropdown of all rooms with their current status (Available / Occupied / Reserved). |
| **Service Rate Card** | Lists all "Session" category services from the owner's menu (name, price, description). |
| **Quick-Tell Templates** | Pre-built `/t {patron} {message}` macros. `{name}` substitutes the patron's first name. Add new ones with a title and message. |
| **Glamourer** | Button to open Glamourer's design window for quick appearance changes. |
| **Earnings Log** | Input a Gil amount and log it as "Session Earnings" or "Tip". Attributed to your role and the current patron. |
| **Patron Notes** | Add and view role-specific notes for the current session patron. Timestamps each entry. |
| **Patron History** | Last 10 earnings entries for the current patron. |
| **Staff Ping** | Shows online staff (sync required). |

---

### Candy Heart Panel

For greeters and the welcome team.

| Section | What It Does |
|---------|--------------|
| **Active Patrons** | Add patrons (by target or manual entry) and set their status: Chatting · Escorting · Idle. |
| **Quick-Tell Macros** | Same as Sweetheart; defaults to the first active patron. |
| **Emote Wheel** | One-click emotes grouped by mood — Flirty, Friendly, Playful, Elegant. |
| **Tips Tracker** | Log a tip amount against a patron name. |
| **Patron Notes** | Add/view notes for a specified patron. Shows 5 most recent. |
| **Room Status** | Live view of all rooms with color-coded statuses, including which staff member and patron are assigned. |

---

### Bartender Panel

For drink service.

| Section | What It Does |
|---------|--------------|
| **Drink Menu** | Displays all "Drink" category items from the menu. Click a drink, then **"Paste to Chat"** to broadcast its description to `/say`. |
| **Order Queue** | Add patron + drink; track status through Pending → Making → Served. Shows elapsed time per order. |
| **Tab System** | Open a running tab per patron (prices accumulate automatically). Close tab logs the earnings. |
| **RP Macros** | Custom emote messages with `{patron}` and `{drink}` placeholders. |

---

### Gamba Panel

For running games of chance.

| Section | What It Does |
|---------|--------------|
| **Game Selector** | Pick a preset; view multiplier and collapsible rules. **"Paste Rules to /say"** broadcasts the rules line by line. |
| **Player Bets** | Add patron names and bet amounts. Table shows all players and the total "Bets In". |
| **Roll Capture** | Automatically intercepts `/random` and `/dice` chat output and assigns results to players. Manual entry also available. Roll history shows the last 10 results with timestamps. |
| **Payout Calculator** | Adjust the multiplier (1×–10×) to preview each player's payout. **"Pay Winner (Log)"** logs the house payout as a negative earning. |
| **House Bank** | Tracks Bets In, Payouts Out, and Net P/L for the session. **"Reset Bank"** clears the counters. |
| **Announce** | Broadcasts a preset macro via shout. |

---

### DJ Panel

For music performance and crowd management.

| Section | What It Does |
|---------|--------------|
| **Performance Timer** | Start/end a set. Shows total elapsed time and per-segment time. **"Mark Segment"** records the current segment with its duration. |
| **Setlist** | Add songs; check them off as played (played songs fade to grey). |
| **Request Queue** | Add patron name + song request. Status per request: ⏳ Pending · ✓ Accepted · ▶ Played · ✗ Rejected. |
| **Stream Link** | Enter a Twitch/YouTube URL; **"Share"** broadcasts it to party chat. |
| **Crowd Engagement** | Quick buttons: "Hype!", "Requests Open", "Last Song!", "Emote". |
| **Tips Tracker** | Log DJ tips against a patron name. |
| **Performance History** | Last 10 earnings entries for the DJ role. |

---

### Management Panel *(Protected)*

Floor oversight and incident management. Requires Management or Owner role + passcode.

| Section | What It Does |
|---------|--------------|
| **Live Floor Board** | Nearby player count with capacity warning (>48). Room grid showing status, assigned staff, patron, and elapsed time. Lists unassigned online staff. |
| **Shift Overview** | Your clock-in status and shift earnings. |
| **Incident Log** | Log incidents with severity (Info / Warning / Critical), patron name, and description. Shows the 15 most recent entries color-coded by severity. |
| **Patron Flagging** | Flag a patron with a reason; automatically sets their status to Warning and appends a timestamped note. |
| **All Patron Notes** | Chronological feed of all notes from all roles — timestamp, role, author, patron, content. |
| **Venue Capacity** | Live nearby player count with warning at the 48-player limit. |

---

### Owner Panel *(Protected)*

Full venue administration. Requires Owner role + passcode.

| Section | What It Does |
|---------|--------------|
| **Venue Info** | Edit the venue name. |
| **Revenue Dashboard** | Today's and all-time earnings with a per-role breakdown and a 7-day daily chart. |
| **Service Menu Editor** | Add/remove menu items (name, description, price, category: Session / Drink / Game / Performance / Other). |
| **Room Editor** | Add/delete rooms. Color-coded live status. |
| **Blacklist Management** | Blacklist a patron with a reason; "Unban" reverts their status. |
| **Patron Analytics** | Top 5 most-visited and highest-spending patrons. |
| **All Patron Notes** | 20 most recent notes from all roles. |
| **Loyalty Tier Thresholds** | Set the minimum visit count or Gil spend to reach Regular and Elite tiers. |
| **Role Cosmetic Defaults** | Set a default badge and glow color per staff role, applied to any staff member without a personal cosmetic profile. |
| **Export** | "Copy Earnings Summary to Clipboard" — formatted revenue report (today, all-time, per-role, 7-day breakdown). |

---

## 5. Patron Details Window

Opens when you click a patron's name in the Bookings or Locator tabs.

### CRM Info Tab

| Field | Notes |
|-------|-------|
| **Name & Tier** | Displayed at the top (Guest / Regular / Elite). |
| **Status** | Staff can toggle "Regular VIP"; Management/Owner have full status control (Regular, VIP, Warning, Blacklisted). |
| **Favourite Drink** | Free text; surfaced in locator arrival alerts. |
| **Allergies** | Free text note. |
| **Notes** | Multi-line general notes. |
| **RP Hooks** | Multi-line field for roleplay context. |
| **Scrape Open Search Info** | Reads the Character Inspect addon and extracts the patron's self-description automatically. |
| **Macros** | Quick-tell buttons for each macro configured in Settings. `{name}` is substituted with the patron's first name. |

### Glamour Links Tab

- Lists all Glamourer designs linked to this patron with **Apply** and **Unlink** buttons.
- Scrollable **"All Designs"** list pulled from Glamourer IPC — click any design to link it.

---

## 6. Session Window

A floating overlay that appears when a session is active.

- **Header:** "Session with: {PatronName}"
- **Chat log:** Chronological messages with `[HH:mm]` timestamps.
  - Your messages: light purple.
  - Incoming messages: pink.
  - Auto-scrolls to the latest message.
- **Copy:** Copies the full log to clipboard as formatted text.
- **Save to File:** Exports the log as a `.txt` file with a timestamp filename, saved to the plugin config directory.

---

## 7. Cosmetics Window

Click **✨ Cosmetics** in the sidebar footer to open this dedicated window.

All changes auto-save after a **1.5-second debounce** and sync to the backend if connected (stored as a Brotli-compressed JSON blob keyed to your character).

**Live Preview** — a dark preview box at the top of the panel shows your nameplate exactly as it will appear in-game.

| Section | Options |
|---------|---------|
| **Text & Colors** | Font, base color, pulsing glow (toggle + color). |
| **Gradient Text** | Toggle, mode (Static / Sweep / Rainbow), two gradient colors, speed slider. |
| **Drop Shadow** | Toggle. |
| **Outline** | Toggle, mode (Hard / Soft), color. |
| **Aura Ring** | Toggle, color, radius, thickness. |
| **Sparkles** | Toggle, style (Orbital / Rising / Burst), color, count, speed, radius. |
| **Background** | Style (None / Solid / Gradient / Shimmer), color(s), padding. |
| **Badges** | Role icon (Slot 1, always Left) + Badge Slot 2 (Left / Right / Above). Template dropdown for each. |
| **Adjustments** | Font size override (8–40 px, default 20), X/Y position offset (±200 px). |
| **Behavior** | Auto SFW/NSFW tinting (blue in LFM zones, red in LFP). Clock-In Opacity Fade (30% opacity when not clocked in). |

> **Note:** Other staff see your customized nameplate in-game only if both you and they have sync enabled.

---

## 8. Key Workflows

### Create and Track a Booking

1. Go to **Bookings** tab.
2. Fill in patron name, service type, room, and Gil amount → **"Add Booking"**.
3. Watch the countdown. At ≤5 min: pulsing timer + chat alert.
4. When complete, right-click the row → **Completed (Paid)** or **Completed (Unpaid)**.

---

### Detect a Patron Arrival

1. Go to **Locator** tab and add the patron (or use **"Detect Targeted"**).
2. When they enter render distance, a chat alert fires automatically with their CRM summary.
3. Click their name in the **Nearby Regulars** list to open their profile.

---

### Run a Sweetheart Session

1. Open the **Sweetheart Panel**.
2. Select a room and enter the patron name (or use current target).
3. Set the session duration → **"Start Session"** — room is marked Occupied.
4. At 5 min and 2 min remaining, chat alerts fire.
5. Log earnings and notes during the session.
6. **"End Session"** — room returns to Available.

---

### Log and Respond to an Incident

1. Open **Management Panel → Incident Log**.
2. Set severity, enter patron name and description → **"Log"**.
3. If the patron needs flagging: **Patron Flagging** → name + reason → **"Flag"**.
4. The patron's status is set to Warning and a timestamped note is appended.
5. Future arrivals of that patron trigger a yellow chat alert with the stored notes.

---

### Customize Your Nameplate

1. Click **✨ Cosmetics** in the sidebar.
2. Adjust colors, effects, badges, and background in the panel.
3. Check the live preview at the top.
4. Changes save automatically after 1.5 seconds and push to the backend.

---

### Export a Revenue Report (Owner)

1. Open **Owner Panel → Export**.
2. Click **"Copy Earnings Summary to Clipboard"**.
3. Paste into a spreadsheet, Discord, or document.

---

## 9. Service Behaviors

### SyncService

Handles all backend communication. Dormant when the main window is closed; wakes when it opens.

| Poll | Interval | Data |
|------|----------|------|
| Fast | 3 seconds | Rooms, online staff |
| Slow | 30 seconds | Earnings, patron notes, patrons |
| Heartbeat | 15 seconds | Presence ping |

Auth uses the `X-Venue-Key` header. If the connection fails, a full-panel overlay shows the error and a retry button.

---

### LocatorService

Scans nearby players every ~1 second against your tracked patron list. Fires one-time arrival alerts per patron per session, then clears them when the patron leaves render distance.

---

### TradeMonitorService

Monitors incoming and outgoing gil trade chat messages via regex. On an incoming trade:

1. Updates the patron's `TotalGilSpent`.
2. Adds to today's daily earnings.
3. If a shift is active, adds to that shift's Gil earned.
4. Fires a notification banner — if a booking from the same patron is open, it shows **"✔ linked to booking"**.

---

### ShiftManager

Tracks clock-in/clock-out state. Earnings from trades and manual SRT logs are written to the active shift automatically.

---

### WaitlistManager

FIFO queue. Each entry records the patron name and the timestamp they were added. Time-waited is calculated live in the UI.

---

## 10. Color Coding Reference

| Color | Meaning |
|-------|---------|
| 🟢 Mint green | Synced, connected, available, Regular patron |
| 🔴 Rose red | Offline, error, Blacklisted patron, Critical incident |
| 🟡 Amber | Warning, near capacity, Warning patron |
| 💗 Pastel pink | Primary accent — section headers, active selections |
| ⭐ Gold | Elite tier patron |
| ♥ Pink | Regular tier patron |
| ⚠ Orange | Warning patron |
| 🚫 Red | Blacklisted patron |

---

## 11. Troubleshooting

**Sync shows "Offline" or connection fails**
- Verify the API URL and Venue Key in **Settings → Sync / API**.
- Confirm the backend server is running (Docker container or local).
- The content panel shows a detailed error message — check it before anything else.

**Patron not detected as nearby**
- The patron must be added to the Locator tab first.
- Their status must be Regular, VIP, Warning, or Blacklisted — Neutral-status patrons are not scanned.
- They must be within render distance (~100 yalms).

**Cosmetics not visible to other staff**
- Both you and the viewer need **Enable Sync** turned on.
- Changes debounce; wait 1.5 seconds after editing before the save and push happen.

**Glamourer integration not working**
- Glamourer must be installed and enabled in Dalamud.
- Run `/glamourer` once to initialize its IPC.
- Confirm **Enable Glamourer Integration** is checked in Settings.

**Gamba rolls not capturing automatically**
- Use `/random` or `/dice` (the standard game commands) — custom roll commands are not intercepted.
- Roll capture listens to chat output, so the text must actually appear in the chat log.

**Protected role panels not showing**
- Go to **Settings → Role Management** and enter the passcode to unlock Management or Owner.
- Confirm the role is enabled in the role selector.

**Session window not appearing**
- Start a session from **Session Capture** tab or via the ChatTwo right-click menu.
- If using ChatTwo, confirm it is installed, enabled, and toggled on in **Settings → Integrations**.
