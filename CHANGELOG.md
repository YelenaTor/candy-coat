# Changelog

All notable changes to Candy Coat are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [0.20.0] - 2026-03-08

### Changed
- **Window migrations**: completed remaining ImGui → Una.Drawing migrations across `ProfileWindow`, `SessionWindow`, `CosmeticWindow`, and setup wizard steps 0, 2, and 4
  - `ProfileWindow`: Close button now a Una.Drawing `GhostButton` node — no raw ImGui button
  - `SetupStep0_Welcome`: "Get Started" button is now a `CandyUI.Button` node embedded in the step layout
  - `SetupStep2_ModeSelection`: Staff/Patron cards are now Una.Drawing nodes with `:hover` highlight and `BorderFocus` on hover
  - `SetupStep4_Finish`: Summary card, Launch button, and integration toggles (Glamourer/ChatTwo) all Una.Drawing — no `ImRaii.Child` boxes or styled `ImGui.Button`
  - `SessionWindow`: log area positions via `InputSpacer.Bounds.ContentRect`; log spacer grows to fill window on resize
  - `CosmeticWindow`: footer (enable checkbox, Re-draw button, Auto re-draw checkbox) positioned via `InputSpacer` bounds — footer layout part of the Una.Drawing tree
- **TellWindow Discord rework**: completely rebuilt with Discord-inspired two-panel layout
  - Left sidebar (160px): conversation items rendered as Una.Drawing nodes — avatar circle with first letter, name + preview column, unread count badge; hover highlight; right-click context menu for Pin/Unpin/Clear/Delete
  - Right panel: Una.Drawing shell; header row shows player name + tier icon + Session/Export action buttons; message thread stays ImGui child (rich text coloring); quick replies rendered as Una.Drawing `SmallButton` nodes above the input row; Send button is a `CandyUI.Button` node
  - Notes field: ImGui input overlay anchored to `InputSpacer` bounds in right panel
  - Root rebuilt every Draw() to keep conversation list in sync with `TellHistory`

### Fixed
- **API diagnosis (Phase 3)**: Neon Backstage DB confirmed healthy (compute active, `SELECT 1` passing). OCI VM `candy-api.service` identified as likely crashed. Any push to `CandyCoat.API/**` on `master` triggers automatic redeploy via GitHub Actions `deploy-api.yml`
- `Rect` coordinate system: corrected `Bounds.ContentRect` access to use `X1/Y1` (Una.Drawing `Rect` uses `X1,Y1,X2,Y2` not `X,Y,Width,Height`)
- Origin offset: `DrawOverlays()` methods that use `InputSpacer` bounds now subtract `ImGui.GetWindowPos() + GetWindowContentRegionMin()` to convert screen-space bounds to ImGui cursor coordinates

---

## [0.19.0] - 2026-03-07

### Changed
- **Backstage retheme**: replaced pink/purple palette with dark charcoal + steel blue + ice blue throughout all panels, toolbar, and balloon
- **UDT XML migration**: all Una.Drawing node layouts migrated to embedded XML template resources under `UI/Templates/` — cleaner separation of layout and logic
- **Balloon expanded to 480px**: wider panel for better content readability (was 340px)
- **Balloon title bar**: replaced single-tab TabStrip at top of balloon with a simple title bar showing the active entry name — less visual noise

### Fixed
- **FontAwesome icons**: toolbar buttons now correctly render FA glyphs (was showing `[X]` boxes) — `Style.Font = 2` points to FontRegistry slot pre-loaded by `DrawingLib.Setup()`
- **Settings panel positioning**: removed incorrect 216px sidebar offset from `SettingsPanel.DrawOverlays()` — settings content now fills the balloon ghost window correctly without clipping

---

## [0.18.0] - 2026-03-07

### Changed
- **Complete UI overhaul**: replaced `MainWindow` (ImGui window with parallax/shift bug) with a screen-anchored toolbar rendered directly to the background draw list — no more UI shifting when the game window moves
- Toolbar anchors to the left screen edge by default; configurable to Right, Top, or Bottom in Settings
- Balloon panel slides out from toolbar with smooth lerp animation when a button is clicked
- All SRT panels and Overview tabs now accessible via the toolbar balloon — no separate draggable window
- Tab strip with named tabs at the top of each balloon panel
- Toolbar collapses to icon-only when not hovered, expands with labels on hover
- Overview button groups all dashboard tabs (Overview, Bookings, Locator, Session, Waitlist, Staff)
- Each SRT role gets its own dedicated toolbar button

### Fixed
- Fixed parallax/shifting bug where the entire UI would shift while dragging the ImGui window

### Removed
- `MainWindow.cs` removed — replaced entirely by `ToolbarService` + `BalloonService`

---

## [0.17.0] - 2026-03-07

### Changed
- Complete UI rework: replaced ImGui+OtterGui layout system with Una.Drawing retained-mode node rendering
- Added CandyTheme named color system for consistent theming across all panels
- Added CandyUI node factory (WindowRoot, Sidebar, ContentPanel, Card, Button, GhostButton, TabContainer, StatusBadge, ScrollBox, InputSpacer, etc.)
- Migrated MainWindow sidebar and content panel to Una.Drawing
- Migrated all 6 dashboard tabs (Overview, Bookings, Locator, Session, Waitlist, Staff) to Una.Drawing
- Migrated all 8 SRT panels (Sweetheart, CandyHeart, Bartender, Gamba, DJ, Management, Greeter, Owner) to Una.Drawing
- Migrated Settings panel, ProfileWindow, TellWindow, PatronDetailsWindow, SessionWindow, CosmeticWindow, PatronAlertOverlay to Una.Drawing
- Migrated setup wizard (SetupWindow + all step classes) to Una.Drawing

### Removed
- OtterGui dependency (removed submodule and ProjectReference)
- StyleManager.cs (replaced by CandyTheme named colors)

---

## [0.16.2] — 2026-03-04

### Changed
- **OwnerPanel — collapsible headers replaced with tab bars** — matches all other SRT panels; tabs are Revenue | Patrons | Manage | Ping
- **OwnerPanel — emoji buttons replaced with text labels** — Copy/Show/Hide buttons in the Venue Registration card now use plain text that renders correctly for all users; VIP Packages header emoji removed; connected indicator changed to U+25CF (BMP-safe filled circle)
- **Candy Tells — send fix** — replaced `Svc.Commands.ProcessCommand` (only handles Dalamud-registered plugin commands) with `ECommons.Automation.Chat.ExecuteCommand` which routes through `UIModule.ProcessChatBoxEntry`, the game's actual chat processor; `/tell` now dispatches correctly from both the Send button and Enter key

---

## [0.16.1] — 2026-03-03

### Changed
- **SRT panels — collapsible headers replaced with tab bars** — all six role toolboxes (Sweetheart, CandyHeart, Bartender, Gamba, DJ, Greeter) now use `ImRaii.TabBar` matching the ManagementPanel style; reduces vertical clutter and keeps sections consistently accessible
  - **Sweetheart**: Session | Patron | Tools | Earnings
  - **CandyHeart**: Session | Patron | Tools | Earnings
  - **Bartender**: Menu | Tabs | Macros | Ping
  - **Gamba**: Rolls | Payout | Bank | Announce
  - **DJ**: Set | Engage | Tips | Ping
  - **Greeter**: Queue | Tells | Tools | Ping
- **Candy Tells — send button fix** — replaced `Plugin.CommandManager.ProcessCommand` with `Svc.Commands.ProcessCommand` so `/tell` routes through the game's native command handler correctly
- **Candy Tells — cross-world sender name fix** — outgoing tells now extract `Name@World` via `PlayerPayload` from the `SeString`, ensuring cross-world conversations are tracked under the correct key

---

## [0.16.0] — 2026-03-03

### Added
- **Multi-Venue Registration System** — the plugin can now authenticate against any registered venue, not just Sugar
  - **`VenueEntity` table** seeded in Neon with Sugar's pre-existing row (deterministic ID from MD5 of the venue key, matching all existing data foreign keys — zero disruption to existing Sugar data)
  - **DB-backed auth middleware** — API now resolves venue from the `Venues` table on every authenticated request; `VenueId` is injected via `ctx.Items` rather than computed per-request
  - **`POST /api/venues/register`** (public) — any client can register a new venue; returns a random VenueId + VenueKey; first and only time the key is returned
  - **`GET /api/venues/validate`** (public, key in header) — validates a venue key and returns VenueId + VenueName without side effects; used by the setup wizard
  - **`POST /api/profile` VenueId support** — accepts optional `venueId` (Guid?); appends to `RegisteredVenues` JSON array in the global profile record
- **Setup Wizard — all roles now complete a venue step** (step 5 of 6) instead of Owners only
  - **Owner path**: "Register Your Venue" form → POST `/api/venues/register` → returns VenueId + VenueKey; key shown with masked display, reveal toggle, and copy button; "Next →" gated on successful registration
  - **Staff path**: "Enter Venue Key" password input → GET `/api/venues/validate` → confirms venue name; "Next →" gated on successful validation; error shown for invalid keys
  - **Returning setup**: if VenueId already in config, step shows confirmed state immediately (no re-entry required)
  - Both paths are non-blocking (async via `Task.Run`); spinner/disabled state shown during in-flight API call
- **`SyncService` new methods**: `ValidateVenueKeyAsync`, `RegisterVenueAsync`, `UpdateVenueKey`
- **`UpsertProfileAsync` updated** to accept `string venueId` parameter; includes it in the POST body for `RegisteredVenues` tracking
- **`MigrateConfig()` backfill** — on every load, existing Sugar installs get `VenueId = SugarVenueId` written to config; triggers a one-time profile upsert to populate `RegisteredVenues` server-side
- **OwnerPanel → Settings: Venue Registration card** — shows Venue Name, Venue ID (copy button), and masked Venue Key (reveal + copy buttons) read from config; info hint to share the key with staff
- **`PluginConstants.SugarVenueId`** — hardcoded GUID matching the migration seed; single source of truth for Sugar's deterministic venue ID

### Changed
- Setup wizard "Next →" on Role Selection (step 4) always advances to step 5 (venue step) for all roles
- Setup wizard "Back" on Finish (step 6) always returns to step 5 (venue step)
- `SetupStep4_Finish` writes `cfg.VenueId` and `cfg.VenueKey` from `WizardState` (not hardcoded Sugar constants); calls `SyncService.UpdateVenueKey()` immediately so polling uses the correct key
- `WizardState` replaces `VenueKeyUnlocked` with `VenueId`, `VenueKey`, `VenueName`, `VenueConfirmed` fields

---

## [0.15.0] — 2026-03-03

### Added
- **VIP Package Tracking System** — full-featured membership subscription layer on top of the existing loyalty tier system
  - **VIP Package Templates** (Owner Panel → Settings → 💎 VIP Packages): Owners define reusable packages with name, tier (Bronze / Silver / Gold / Platinum), duration type (Monthly / One-Time / Permanent), price in Gil, description, and a free-form perks list
  - **Package assignment** (PatronDetailsWindow → 💎 VIP tab): Staff assign a package to any patron; system snapshots name, tier, and duration at purchase time and sets expiry automatically for Monthly packages (now + 1 month); Permanent and One-Time packages never expire
  - **Override price** field on assignment allows recording the actual Gil paid regardless of the package list price
  - **Active subscription view**: shows package name, tier badge, purchase date, assigned-by, expiry with colour-coded days-remaining (amber ≤ 7 days), paid amount, and perks list sourced from the package definition
  - **Expired subscription view**: highlights expiry date and days-since with "Renew for 1 month" / "Remove VIP" action buttons
  - **Entry alert overlay** (PatronAlertOverlay): VIP patrons get a gold-tinted card (`#401D0A` background) with 💎 + package name instead of tier label; expired VIP shows muted purple-grey card with "VIP EXPIRED" label; days-remaining or "Permanent" shown on row 2
  - **Chat alert** (PatronAlertService): VIP active → `[CandyCoat] 💎 Name [VIP GOLD · PackageName] is here!` with days left; VIP expired → `[CandyCoat] ♥ Name [VIP EXPIRED] is here!`
  - **Locator tab** 💎 badge: VIP patrons in both the nearby-regulars list and the tracked-patron list display a gold 💎 icon with a hover tooltip showing the package name
  - **Owner Panel — VIP package CRUD**: table view with tier colour badge, duration, price, active/disabled status; inline edit form; delete with active-subscriber warning popup; "+ Add Package" inline creation form with multiline perks field (one perk per line)
- New data models: `VipTier` (enum), `VipDurationType` (enum), `VipPackageDefinition`, `VipSubscription`
- `VipColours.GetTierColour(VipTier)` static helper — Bronze copper, Silver grey, Gold gold, Platinum purple — reused across overlay, details window, and owner panel
- `Configuration.VipPackages` — new `List<VipPackageDefinition>` field (default empty, no migration needed)
- `Patron.ActiveVip` — nullable `VipSubscription` field; serialised as part of existing patron sync flow

---

## [0.14.0] — 2026-03-03

### Added
- **Candy Tells** — floating `✉ Messages` window (`TellWindow`) for managing /tell conversations like a Discord DM list
  - Captures all incoming and outgoing /tell traffic via `IChatGui.ChatMessage` hook (`TellService`)
  - Persistent conversation history saved to config — survives relog and plugin reload
  - Left panel: filterable conversation list sorted by pinned → last activity, with patron tier icons (★/◆/○), unread badges, and right-click context menu (Pin/Unpin, Clear History, Delete)
  - Right panel: conversation header with patron tier icon and action buttons (Session, Booking, Export), per-conversation notes bar, scrollable message area with date separators, and send input with Enter-to-send support
  - Outgoing messages right-biased to visually distinguish from incoming (left-aligned)
  - Quick Replies section in left panel: first 4 macros from the user's active role, 2-per-row, click to pre-fill input
  - **Patron integration**: known patrons display loyalty tier icon (Elite ★, Regular ◆, Guest ○) in conversation list and header
  - **One-click session start**: Session button → `SessionManager.StartCapture()` + opens SessionWindow
  - **One-click booking creation**: Booking button → opens BookingsTab in MainWindow
  - **Export**: saves conversation to `Sessions/Tells_Name_YYYYMMDD_HHmm.txt` in plugin config directory
  - **Easter egg**: subtle 🌙 moon icon in conversation header when chatting with "Sephy" (owner of The 13th Floor, who requested this feature) — hover for a tooltip
- **ChatTwo "Open in Candy Tells"** context menu item alongside existing "Start Candy Session"
- **`✉ Messages [N]` footer button** in MainWindow sidebar with live unread count badge
- **Candy Tells settings section** in Settings → Integrations area:
  - Toggle: Suppress tells from in-game chat
  - Toggle: Auto-open Tells window on incoming message
  - Slider: Max messages per conversation (50–500, default 200)
  - Button: Clear all conversation history

---

## [0.13.3] — 2026-03-03

### Fixed
- **Nameplate jumps to top-left when zoomed out**: `GetNamePlateWorldPosition` writes `(0,0,0)` when a character's nameplate is not actively loaded (common at any distance beyond close range). `WorldToScreen(0,0,0)` silently succeeds and returns the screen projection of the world origin — which is top-left at any meaningful camera distance. Added zero-position guard and character-proximity sanity check before projecting; the method now returns false and the anchor chain falls through to the known-working dual-projection fallback.
- **`AnchorFreshFrameBudget` raised from 2 → 6 frames**: `OnDataUpdate` does not fire every render frame; a 2-frame window caused frequent stale misses and unintended fallthrough to the broken world-position path.

---

## [0.13.2] — 2026-03-03

### Fixed
- **Nameplate cosmetic flicker every 3 seconds**: `SyncService.ApplyCosmetics` previously called `Cosmetics.Clear()` before repopulating, creating a frame window where all cosmetic profiles were empty and vanilla nameplates briefly reappeared. Now uses a swap pattern — stale keys are surgically removed and new ones upserted, so the dictionary is never fully empty.
- **`LegacyBaseYOffset` misapplied to all anchor methods**: the 30px base Y offset was originally tuned for the dual-projection fallback (feet → head-height lift). It was incorrectly applied to native (`OnDataUpdate`) and world-position anchors, which already land at the nameplate position. Offset is now applied only to the dual-projection path.
- **Inconsistent hash computation in `OnNamePlateUpdate`**: was inlining `Convert.ToBase64String(...)` instead of using the `BuildCharacterHash` helper already defined in the same class.
- **Duplicate/misplaced XML summary comment** on `SendHeartbeatAsync` cleaned up; summary moved to the correct method (`UpsertVenueConfigAsync`).

---

## [0.13.1] — 2026-03-02

### Changed
- **CandyHeart panel — full rework**: replaced the legacy active-patron tracker with a full session timer (idle form → running timer → TIME'S UP pulsing); added Room Assignment, Patron Profile inline card, Upcoming Bookings mini-list, Courtesan Emote Shortcuts (Wink, Blow Kiss, Dote, Beckon, Smile, Kneel, Curtsey, Cheer, Laugh, Bow, Nod, Clap), dual Session + Tip earnings logging, session-scoped Patron Notes, and Patron History; removed Escort Handoff collapsible (belongs to Greeter)
- **Sweetheart panel — targeted additions**: new Patron Profile collapsible (auto-fills from active session — tier badge, RP hooks, favourite drink, allergies / limits); new Upcoming Bookings mini-list; new Emote Shortcuts collapsible with Pillow/Comfort flavour (Comfort, Smile, Blow Kiss, Kneel, Bow, Beckon, Doze, Laugh, Wave, Hug, Nuzzle, Pet); Log Earnings and Patron Notes now DefaultOpen
- Both companion panels share a unified collapsible order: Room Assignment → Patron Profile → Upcoming Bookings → Quick-Tell Macros → Emote Shortcuts → Log Earnings → Patron Notes → Patron History → Staff Ping

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
