# Candy Coat

[![Build](https://github.com/YelenaTor/candy-coat/actions/workflows/release.yml/badge.svg)](https://github.com/YelenaTor/candy-coat/actions/workflows/release.yml)
[![Latest Release](https://img.shields.io/github/v/release/YelenaTor/candy-coat?style=flat&label=release&color=f4a7c3)](https://github.com/YelenaTor/candy-coat/releases/latest)
[![License](https://img.shields.io/badge/license-Proprietary-c084fc?style=flat)](./LICENSE.md)
[![.NET](https://img.shields.io/badge/.NET-10%20Preview-7c3aed?style=flat)](https://dotnet.microsoft.com/)
[![Dalamud API](https://img.shields.io/badge/Dalamud%20API-14-f472b6?style=flat)](https://github.com/goatcorp/Dalamud)

**A private venue operations plugin for FFXIV — built for Sugar Venue staff.**
**Current version: v0.18.0**

Candy Coat is a comprehensive in-game assistant that handles the full operational stack for adult-friendly FFXIV venues: booking management, patron CRM, shift tracking, earnings logging, team sync, and a role-gated staff toolbox — all from a screen-anchored toolbar interface.

As of v0.16.0, the plugin supports multi-venue registration. Venue Owners can register their venue through the setup wizard to get a unique Venue Key to share with their staff. Staff enter the key during setup to connect to their venue's Backstage API.

---

## Features

### Booking Management
- Create bookings with patron name, service, room, and gil amount
- Live sortable booking table with per-row timers and urgency pulses (< 5 min warning)
- Right-click context menu to mark bookings Active / Completed (Paid) / Completed (Unpaid) / Inactive
- Team booking view — see all active bookings from all synced staff in real time
- Trade detection: when a trade matching a booking is received, auto-mark as Completed (Paid)

### Patron CRM
- Full patron profiles: world, visit count, total gil spent, RP hooks, favorite drink, allergies, notes
- Status flags: Neutral, Regular, Warning, Blacklisted — with proximity chat alerts for flagged patrons
- **Loyalty Tiers**: automatically assign Guest / Regular / Elite based on configurable visit and gil thresholds
- Glamourer quick-swap: link saved designs to patron profiles and apply them in one click
- First-run profile scraper from native FFXIV Search Info

### Patron Locator
- Per-frame nearby player scanner with Regular/Elite detection alerts
- When a tracked Regular enters the zone, surfaces a CRM summary: last visit, spend, notes
- Eye-target button to focus any nearby regular in-game

### Patron Entry Alerts
- When a tracked Regular or Elite patron enters the housing instance, a dismissible card overlay appears top-right
- Shows name, loyalty tier badge, distance, visit count, and favourite drink
- Alert method: in-window card, chat message, or both — configurable per staff member
- Auto-dismisses after a configurable duration; "Target" button focuses the patron in-game
- Danger-status patrons (Warning/Blacklisted) always alert regardless of Regular-Only filter

### VIP Package Tracking (v0.15.0)
A full membership subscription layer sitting above the existing loyalty tier system:
- **Owners** define reusable VIP package templates in the Owner Panel: name, tier (Bronze / Silver / Gold / Platinum), duration (Monthly / One-Time / Permanent), Gil price, description, and a free-form perks list
- **Staff** assign packages to patrons via the new 💎 VIP tab in PatronDetailsWindow; the actual Gil paid can be overridden from the template price
- **Monthly** packages automatically set a one-month expiry; **Permanent** and **One-Time** packages never expire
- **Active subscription** shows package name, tier badge, expiry with colour-coded days-remaining, paid amount, perks list, and Renew / Remove actions
- **Entry alert cards**: active VIP patrons get a gold-tinted alert card with 💎 package name; expired VIP patrons get a muted grey card with "VIP EXPIRED"
- **Chat alerts**: active VIP patrons include package name and days left; expired VIP patrons are tagged `[VIP EXPIRED]`
- **Locator tab**: all patron list rows show a gold 💎 badge with hover tooltip for active VIP patrons

### Candy Tells (v0.14.0)
- Floating `✉ Messages` window that captures all incoming and outgoing /tell traffic as persistent conversations
- Discord-like conversation list: sorted by last activity, with patron tier icons, unread count badges, pin/unpin, and right-click context menu
- Per-conversation notes bar for personal observations about each player
- Scrollable message area with date separators; outgoing messages visually right-biased
- **Quick Replies**: first 4 macros from your active role shown as one-click buttons to pre-fill the input
- **Patron integration**: known patrons display their loyalty tier (★ Elite, ◆ Regular, ○ Guest) in the conversation list and header
- Action buttons in the header: one-click session start, one-click booking creation, conversation export to `.txt`
- ChatTwo context menu: right-click a player → "Open in Candy Tells" alongside "Start Candy Session"
- Settings: suppress tells from main chat, auto-open on incoming message, per-conversation message cap
- History persists across relog and plugin reload

### Session Capture
- Start a named chat session to log all messages with a specific patron
- Pop-out session window with live message feed
- ChatTwo IPC context menu: right-click a player in chat to start a session instantly
- Export session log to clipboard or save as `.txt` file to the plugin config directory

### Waitlist
- Queue management with active wait timers per entry
- One-click tell notification to the next patron in line

### Shift & Earnings Tracking
- Clock in / out with shift duration tracking
- Automatic gil trade detection via system message hook — attributes earnings to the current shift
- Earnings ledger with role breakdown; backend sync for team-wide visibility

### Screen-Anchored Toolbar (v0.18.0)
The main UI is now a compact icon strip anchored to the screen edge — not a draggable ImGui window. No more UI parallaxing or shifting as the game window moves.

- Anchors to Left / Right / Top / Bottom edge — configurable in Settings
- Collapses to icon-only when not hovered; expands with labels on hover
- Click any button to open its balloon panel, which slides out with a smooth animation
- Each balloon has a named tab strip at the top; the Overview balloon groups all dashboard tabs
- Each SRT role gets its own dedicated toolbar button — only enabled roles are shown
- All inputs (text fields, combos) work via an invisible ghost ImGui window pinned to the balloon

### Staff Role Toolbox (SRT)
Role-gated panels — each staff member only sees panels matching their assigned role(s).

| Panel | Role | Purpose |
|-------|------|---------|
| Sweetheart | `Sweetheart` | SFW companion session timer, patron profile card, upcoming bookings, comfort emote shortcuts, macro bank, glamourer presets, earnings/tip logging |
| Candy Heart | `CandyHeart` | Courtesan session timer, patron profile card, upcoming bookings, social emote shortcuts, dual session/tip earnings, macro bank |
| Bartender | `Bartender` | Drink service menu management |
| Gamba | `Gamba` | Gambling round management with **auto-capture** of `/random` and `/dice` chat rolls |
| DJ | `DJ` | Music and performance tooling |
| Greeter | `CandyHeart` | Door queue, patron lookup, welcome macros, escort handoff |
| Management | `Management` | Shift overview, live floor board, room board, incident log, patron flagging, capacity |
| Owner | `Owner` | Venue-wide admin: room editor, staff roster, loyalty tier thresholds, role cosmetic defaults |

Both companion panels (Sweetheart, Candy Heart) share a unified panel layout: session timer → patron profile inline card (tier, RP hooks, favourite drink, allergies) → upcoming bookings → macros → role-flavoured emote shortcuts → earnings → notes → history → staff ping.

All SRT panels include the **Staff Ping widget** — select any online staff member and send a templated or freeform coordinated alert (Room Ready, Needs Escort, Incident Here, Help Needed).

### Nameplate Cosmetics
- Per-character nameplate customization: glow, drop shadow, gradient colors, role badge icon
- Live ImGui draw-list preview in the Cosmetic Drawer tab
- **Role Identity System**: Owners can assign a default badge and glow color per staff role — automatically applied to all online staff nameplates via backend sync
- Cosmetic profiles are Brotli-compressed and synced to the backend, visible to all connected staff

### Backend Sync
Permanently hosted API (`CandyCoat.API`) on an OCI VM behind Caddy with automatic TLS. All write operations are fire-and-forget — no polling or blocking:
- **Staff heartbeat**: fires on clock-in, recording active presence in the shared staff table
- **Profile sync**: `UpsertProfileAsync` on first setup; `LastSeen` updated on each run
- **Write ops**: bookings, earnings, patrons, cosmetics, and notes push on create/update
- Auth via `X-Venue-Key` header — single shared venue key, no user accounts

---

## Requirements

| Dependency | Notes |
|-----------|-------|
| [Dalamud](https://github.com/goatcorp/Dalamud) | API 14 — runs within the Dalamud framework |
| [Glamourer](https://github.com/Ottermandias/Glamourer) | Required for quick-swap functionality |
| [ChatTwo](https://github.com/cottonvibes/ChatTwo) | Required for session capture context menus |
| Una.Drawing | Bundled — retained-mode node rendering library |
| ECommons | Bundled — Dalamud utility library |

The backend API is permanently hosted and configured automatically on first run. All write operations are fire-and-forget; panels degrade gracefully if the API is unreachable.

---

## Installation

> This plugin is private and distributed via the Sugar Venue internal Dalamud custom repository.

1. Add the Sugar Venue custom repo to your Dalamud plugin sources
2. Install **Candy Coat** from the plugin list
3. Open the interface with `/candy`
4. Complete the first-run setup wizard (identity → dependencies → config → finish)

For local development / testing, build with:
```
dotnet build CandyCoat/CandyCoat.csproj
```
and load `CandyCoat.dll` directly from the Dalamud dev tools.

---

## Tech Stack

- **.NET 10 Preview** — `net10.0-windows7.0`
- **Dalamud API 14** — `Dalamud.NET.Sdk/14.0.1`
- **ImGui** via `Dalamud.Bindings.ImGui`
- **Una.Drawing** — retained-mode node UI rendering (submodule)
- **ECommons** — Dalamud service utilities
- **Backend**: ASP.NET Core 10 minimal API · PostgreSQL (Npgsql/EF Core) · Docker

---

## Project Structure

```
CandyCoat/               Plugin source
  Plugin.cs              Entry point and DI wiring
  Configuration.cs       Serializable IPluginConfiguration (all state)
  Windows/
    Tabs/                ITab implementations (BookingsTab, LocatorTab, etc.)
    SRT/                 IToolboxPanel implementations (role-gated panels)
  Services/              Business logic (VenueService, SyncService, etc.)
  Data/                  Data models and enums
  UI/
    Toolbar/             Screen-anchored toolbar (ToolbarService, BalloonService, IToolbarEntry)
                         NameplateRenderer, CosmeticRenderer, CandyTheme, SettingsPanel
  IPC/                   Glamourer and ChatTwo IPC wrappers
CandyCoat.API/           Self-hosted backend
  Program.cs             Minimal API endpoints
  Models/                EF Core entity models
  Data/                  VenueDbContext
  Migrations/            EF Core migrations
```

---

## Contributing

This project is **not open to external contributions**. See [CONTRIBUTING.md](./CONTRIBUTING.md).

---

## License

Proprietary — Sugar Venue. See [LICENSE.md](./LICENSE.md) for terms.

---

> **Coming soon:** Candy Coat will be rebranded as **Backstage** for public availability on the official Dalamud plugin repository. The rebrand will bring a generalized venue-agnostic configuration system, a public plugin listing, and an open-source release under a permissive license. Stay tuned.
