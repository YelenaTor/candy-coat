# Candy Coat

[![Build](https://github.com/YelenaTor/candy-coat/actions/workflows/release.yml/badge.svg)](https://github.com/YelenaTor/candy-coat/actions/workflows/release.yml)
[![Latest Release](https://img.shields.io/github/v/release/YelenaTor/candy-coat?style=flat&label=release&color=f4a7c3)](https://github.com/YelenaTor/candy-coat/releases/latest)
[![License](https://img.shields.io/badge/license-Proprietary-c084fc?style=flat)](./LICENSE.md)
[![.NET](https://img.shields.io/badge/.NET-10%20Preview-7c3aed?style=flat)](https://dotnet.microsoft.com/)
[![Dalamud API](https://img.shields.io/badge/Dalamud%20API-14-f472b6?style=flat)](https://github.com/goatcorp/Dalamud)

**A private venue operations plugin for FFXIV — built for Sugar Venue staff.**

Candy Coat is a comprehensive in-game assistant that handles the full operational stack for adult-friendly FFXIV venues: booking management, patron CRM, shift tracking, earnings logging, team sync, and a role-gated staff toolbox — all from a single ImGui window.

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

### Staff Role Toolbox (SRT)
Role-gated panels — each staff member only sees panels matching their assigned role(s).

| Panel | Role | Purpose |
|-------|------|---------|
| Sweetheart | `Sweetheart` | Companion/entertainer session tooling and macro bank |
| Candy Heart | `CandyHeart` | Greeter / welcome team quick-tells and patron triage |
| Bartender | `Bartender` | Drink service menu management |
| Gamba | `Gamba` | Gambling round management with **auto-capture** of `/random` and `/dice` chat rolls |
| DJ | `DJ` | Music and performance tooling |
| Management | `Management` | Shift overview, live floor board, room board, incident log, patron flagging, capacity |
| Owner | `Owner` | Venue-wide admin: room editor, staff roster, loyalty tier thresholds, role cosmetic defaults |

All SRT panels include the **Staff Ping widget** — select any online staff member and send a templated or freeform coordinated alert (Room Ready, Needs Escort, Incident Here, Help Needed).

### Nameplate Cosmetics
- Per-character nameplate customization: glow, drop shadow, gradient colors, role badge icon
- Live ImGui draw-list preview in the Cosmetic Drawer tab
- **Role Identity System**: Owners can assign a default badge and glow color per staff role — automatically applied to all online staff nameplates via backend sync
- Cosmetic profiles are Brotli-compressed and synced to the backend, visible to all connected staff

### Backend Sync
Optional self-hosted API (Docker) with PostgreSQL. When connected:
- **Fast poll (3 s)**: rooms, online staff, cosmetics
- **Slow poll (30 s)**: earnings, patron notes, patrons, bookings
- **Heartbeat (15 s)**: staff presence ping
- Auth via `X-Venue-Key` header — single shared venue key, no user accounts

---

## Requirements

| Dependency | Notes |
|-----------|-------|
| [Dalamud](https://github.com/goatcorp/Dalamud) | API 14 — runs within the Dalamud framework |
| [Glamourer](https://github.com/Ottermandias/Glamourer) | Required for quick-swap functionality |
| [ChatTwo](https://github.com/cottonvibes/ChatTwo) | Required for session capture context menus |
| OtterGui | Bundled — used for UI helpers |
| ECommons | Bundled — Dalamud utility library |

The backend API is **optional**. All core features work offline; sync features activate automatically when a valid API URL and venue key are configured.

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
- **OtterGui** — ImGui helpers and widgets
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
  UI/                    StyleManager, NameplateRenderer, CosmeticRenderer
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
