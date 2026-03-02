# Changelog

All notable changes to Candy Coat are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [0.12.0] — 2026-03-02

### Added
- **Backstage API — Live on OCI**: `CandyCoat.API` deployed to Oracle Linux 9 VM behind Caddy with automatic Let's Encrypt TLS via `145.241.101.66.nip.io`
- **`PluginConstants.cs`**: single source of truth for `ProductionApiUrl` and `VenueKey` — all plugin code references constants, not strings
- **Venue Key Setup Step** (`SetupStep4_VenueKey.cs`): Owner-only wizard step 5 — password gate; non-Owner staff skip directly to finish
- **Staff Heartbeat on Clock-In**: `ShiftManager.ClockIn()` fires `SyncService.SendHeartbeatAsync()` — populates the `staff` table in Neon with active presence
- **`MigrateConfig()` on every load**: backfills `ApiUrl`, `VenueKey`, and `VenueName="Sugar"` into existing installs — no manual reconfiguration required after upgrade
- **Dev environment cleanup**: removed all hardcoded localhost references, dev bypasses, and "Local Dev" UI labels; API auth middleware enforces `X-Venue-Key` in Production

### Changed
- MainWindow sidebar label: "API: Local Dev" → green "API: Backstage • Online"
- ProfileWindow: "Local Dev Mode" → green "Connected"; venue row reads `cfg.VenueName` dynamically
- `SetupStep4_Finish.cs` always writes `ApiUrl`, `VenueKey`, and `VenueName` to config on launch
- `SetupStepCheckSync` and `SetupStep1_CharacterProfile` use `PluginConstants.ProductionApiUrl`

---

## [0.11.7] — 2026-03-01

### Added
- **Patron Entry Alerts**: dismissible card overlay (top-right) when a tracked patron enters the housing instance — shows name, tier, distance, visit count, and favourite drink
- `PatronAlertService` — cooldown-aware alert queue; dispatches to Panel, Chat, or Both
- `PatronAlertOverlay` — stacked card window; auto-dismisses; "Target" button focuses patron in-game
- `LocatorService` now fires `OnPatronArrived` event replacing inline chat prints
- Danger-status patrons (Warning/Blacklisted) always alert regardless of Regular-Only filter
- Settings: Enable/Disable, Alert Method, Regular-Only filter, Target-on-click, cooldown/dismiss durations

---

## [0.11.5] — 2026-03-01

### Changed
- Sync is now always-on: removed sync config toggle and all polling/wake/sleep infrastructure from `SyncService`
- `SyncService.IsConnected` is now a constant `true` — panels degrade gracefully with empty caches
- Removed post-wizard connection lifecycle and blocking health-check overlay from the setup flow

---

## [0.10.0] — 2026-02-27

### Added — Feature Expansion (A → I)

**A · Smart Patron Recognition**
- When a tracked Regular or Elite patron enters the zone, a CRM alert fires in chat with their visit count, total gil spent, last-seen date, and any notes on file
- `LocatorService` now caches alerted patrons per session to prevent repeat spam
- `BuildCrmSummary()` helper surfaces the right patron data concisely

**B · Live Floor Board** *(Management panel)*
- New collapsible "Live Floor" section in ManagementPanel aggregates rooms, assigned staff, patron names, timers, and nearby player count into a single real-time view
- Room rows are color-coded by occupancy status; overdue timers pulse red

**C · Booking ↔ Trade Linkage**
- `TradeMonitorService` now fires an `OnTradeDetected` event carrying patron name, amount, and whether it matched an active booking
- When a gil trade matches an open booking's amount, the booking is auto-marked CompletedPaid and a dismissible banner appears at the top of the main window
- Banners auto-dismiss per-entry with an X button

**D · Staff-to-Staff Quick Ping Widget**
- New `StaffPingWidget` shared component injected into all 7 SRT panels
- Collapsing "Staff Ping" header with online-staff dropdown (sourced from SyncService), pre-built alert templates (Room Ready, Needs Escort, Incident Here, Help Needed, Custom), and a freeform input slot
- Sends via `/t {target} [CC] {message}`

**E · Role Cosmetic Identity System**
- New `RoleDefaultCosmetic` data model — per-role badge template and glow color
- `Configuration.RoleDefaults` dictionary keyed by `StaffRole` flags enum
- `NameplateRenderer` resolves role defaults for synced staff who have no personal cosmetic profile, and blends role badge/glow onto profiles that have no role icon set
- Owner panel gains a "Role Cosmetic Defaults" section with per-role enable toggle, badge combo, and glow color picker

**F · Patron Loyalty Tier System**
- New `PatronTier` enum: `Guest`, `Regular`, `Elite`
- `Configuration.GetTier(Patron)` computes tier from visit count and total gil spent against configurable thresholds
- Thresholds (Regular/Elite × visits/gil) are editable in OwnerPanel under "Loyalty Tier Thresholds"
- LocatorTab and PatronDetailsWindow display tier with color-coded labels (gold star for Elite, pink heart for Regular)

**G · Session Export**
- "Copy" and "Save to File" buttons added to the SessionWindow header
- `SessionManager.GetExportText()` formats the full session log as readable plaintext
- `SessionManager.SaveToFile(configDir)` writes to `{ConfigDir}/Sessions/Session_{name}_{timestamp}.txt`

**H · Gamba Chat-Roll Auto-Capture**
- `GambaPanel` now subscribes to `Svc.Chat.ChatMessage` and parses `/random` and `/dice` output via regex
- Matched rolls are automatically added to the current round's roll history without manual entry
- A subtle "● Auto-capture active" indicator appears in the rolls section header
- `GambaPanel` implements `IDisposable`; chat hook is safely unregistered on panel disposal

**I · Bookings Backend Sync**
- New `BookingEntity` EF Core model and `Bookings` table in the API database (migration included)
- API gains `/api/bookings` GET, POST (upsert by Id), and DELETE endpoints
- `SyncService` fetches bookings on slow poll (30 s) into a `List<SyncedBooking> Bookings` cache
- `UpsertBookingAsync` and `DeleteBookingAsync` write methods added to `SyncService`
- BookingsTab pushes newly created bookings to the backend when connected
- New "Team Bookings (Synced)" section in BookingsTab shows all active bookings from all staff with Patron, Service, Staff, Gil, and State columns

---

## [0.9.14] — 2026-02-27

### Fixed
- Full UI and theme audit pass: corrected color inconsistencies across all tabs and SRT panels
- Standardized spacing, separator placement, and section header usage throughout

---

## [0.9.13] — 2026-02-27

### Fixed
- Nameplate: lowered render position by 30 px, fixed font size to 30 px
- Resolved font bundling issue causing missing custom fonts on first load

---

## [0.9.12] — 2026-02-27

### Fixed
- Nameplate: resolved height/zoom drift causing nameplates to shift with camera zoom
- Eliminated font blur caused by sub-pixel rendering at non-integer scales

---

## [0.9.x] — 2026-02-26 to 2026-02-27

### Added
- `CosmeticDrawerTab`: full nameplate customization UI (glow, drop shadow, gradient colors, role badge icon, SFW/NSFW tint)
- Live ImGui draw-list preview panel in CosmeticDrawerTab
- Debounce-save with backend push for cosmetic profile changes
- `CosmeticFontManager`: custom font loading pipeline with diagnostic logging
- FontDirectory path and loaded-font-count diagnostic tooltip in settings

---

## [0.9.0] — 2026-02-25

### Added
- **SRT (Staff Role Toolbox)**: role-gated panel system with `IToolboxPanel` interface
  - `SweetheartPanel`, `CandyHeartPanel`, `BartenderPanel`, `GambaPanel`, `DJPanel`, `ManagementPanel`, `OwnerPanel`
  - Each panel only visible to staff with the matching `StaffRole` flag
- **SyncService**: optional backend API sync
  - Fast poll (3 s): rooms, online staff, cosmetics
  - Slow poll (30 s): earnings, notes, patrons
  - Heartbeat (15 s): staff presence ping
  - Wake/sleep tied to MainWindow open state
- **NameplateRenderer**: custom in-game nameplates via `ImGui.GetBackgroundDrawList()`
  - Pulsing glow, drop shadow, clock-in opacity fade, SFW/NSFW tint, role icon
  - Synced cosmetic profiles keyed by `base64(Name@World)`
- `StaffRole` flags enum: `None | Sweetheart | CandyHeart | Bartender | Gamba | DJ | Management | Owner`
- `StyleManager`: unified pastel pink / dark purple color palette

---

## [0.5.0] — 2026-02-22

### Added
- Support & Feedback section in SettingsTab for bug reports and suggestions

### Fixed
- CI/CD: overhauled release pipeline — flat ZIP structures, array-based `repo.json`, resolved nested directory and stray ZIP issues

---

## [0.4.10] — 2026-02-22

### Fixed
- CI/CD: temporary staging step in build workflow resolves nested ZIP structure entirely; excluded stray `latest.zip` from plugin bundles

---

## [0.4.9] — 2026-02-22

### Fixed
- CI/CD: explicitly specify build output directory, resolving empty ZIP issue

---

## [0.4.8] — 2026-02-22

### Fixed
- CI/CD: fixed double-nested ZIP structure in release artifact

---

## [0.4.7] — 2026-02-22

### Fixed
- CI/CD: `repo.json` is now always a valid JSON array, satisfying Dalamud custom repository requirements

---

## [0.4.6] — 2026-02-21

### Added
- SettingsTab: direct links and format guidance for staff issue reporting

---

## [0.4.5] — 2026-02-21

### Added
- Patron UI overhaul: renamed "Favorite" to "Regular" across the plugin
- Eye-target button for Regulars in the Locator list
- Add Patron form now has separate First Name, Last Name, and World fields
- "Detect Targeted" button auto-fills patron details from current in-game target
- "Add as Regular" added to in-game character right-click context menu

### Fixed
- CI/CD: internal job artifacts resolve intermittent GitHub Pages deployment failures

---

## [0.4.3] — 2026-02-21

### Fixed
- Registered mandatory `OpenMainUi` and `OpenConfigUi` callbacks to resolve Dalamud API 14 validation warnings

---

## [0.4.2] — 2026-02-21

### Fixed
- Fatal CLR crash (`MissingMethodException`) during plugin initialization caused by `TerraFX.Interop.Windows` binary mismatch with Dalamud API 14
- Replaced TerraFX MessageBox calls with a P/Invoke implementation
- Fixed `FindWindowEx` signature mismatch in `WindowFunctions`

---

## [0.4.1] — 2026-02-20

### Added
- Management Mode passcode gate for Dashboard Analytics and full Blackbook assignment
- Regular staff can only mark patrons as Favorites; blacklisting requires Management Mode
- Unlock mechanism in SettingsTab

---

## [0.4.0] — 2026-02-20

### Added
- **The Blackbook**: `PatronStatus` (Favorite, Warning, Blacklisted) with proximity chat alerts
- **Dashboard Analytics**: daily earnings and top spenders from trade logs
- **Waitlist Queue**: `WaitlistTab` with timers and one-click tell notifications
- **Shift Management**: `StaffTab` for clock in/out, shift duration, and income tracking
- **Automated Macros**: reusable `{name}` template strings in SettingsTab, executable from Patron Profiles
- **Profile Scraper**: re-hooked `CharacterInspect` via FFXIVClientStructs to scrape Search Info into Patron Notes
- Upgraded to Dalamud API 14 and .NET 10

---

## [0.3.1] — 2026-02-19

### Changed
- Namespace renamed to `CandyCoat`
- Optimized `MainWindow` visibility checks
- Added `World` field to Patron data model
- Filtered chat capture to relevant message types
- Removed legacy staff key features

---

## [0.3.0] — 2026-02-18

### Changed
- Project structure and namespace renamed to Candy Coat

### Added
- First-run Setup Wizard: identity setup, Glamourer/ChatTwo dependency check, initial configuration

---

## [0.2.0] — 2026-02-18

### Added
- Session Capture tab and pop-out SessionWindow
- ChatTwo IPC context menu integration — right-click a player in chat to start a session

---

## [0.1.0] — 2026-02-18

### Added
- Booking system with full create/manage UI
- Patron Locator with proximity detection
- Client Profiles with persistent notes
- Glamourer IPC wrapper with design linking and quick-swap buttons
- OtterGui UI refactor

### Removed
- Legacy sample commands (`/cset`, `/cbook`)

---

## [0.0.1] — Initial

- Project scaffolded from Dalamud sample plugin
