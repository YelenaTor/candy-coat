~*~ Changelog ~*~

all notable changes to the candy coat project will be documented here <3

.: 0.4.1 :. - 2026-02-20
[ Added ]
* Management Access Control ::
  + Gated sensitive features (Dashboard Analytics and full Blackbook assignment) behind a Management Mode passcode (`YXIII`).
  + Regular staff can now only `Favorite` patrons, ensuring data security and preventing accidental/malicious blacklisting.
  + Added an unlocking mechanism to the Settings tab that securely enables management mode.

.: 0.4.0 :. - 2026-02-20
[ Added ]
* Venue Management Suite ::
  + The Blackbook: Added `PatronStatus` (Favorite, Warning, Blacklisted) and Locator proximity chat alerts.
  + Dashboard Analytics: New UI displaying daily earnings and top spenders, reading from trade logs.
  + Waitlist Queue: `WaitlistTab` added to manage waiting patrons with active timers and 1-click tells.
  + Shift Management: `StaffTab` added for clocking in/out, recording shift duration and income.
  + Automated Macros: `SettingsTab` now allows creating reusable text macros with `{name}` insertion, executable from the Patron Profile.
  + Profile Scraper: Re-hooked `CharacterInspect` via `FFXIVClientStructs` to scrape native Search Info into Patron Notes.
* API 14 / .NET 10 Migration ::
  + Upgraded to Dalamud API v14 and .NET 10 frameworks.
  + Corrected `AtkUnitBasePtr` and `IChatGui` delegate errors.

.: 0.3.1 :. - 2026-02-19
[ Changed ]
* Refactoring ::
  + Renamed namespace to `CandyCoat`
  + Optimized `MainWindow` checks
  + Added `World` to Patron data
  + Filtered chat capture to relevant types
  + Removed legacy staff key features

.: 0.3.0 :. - 2026-02-188
[ Changed ]
* Renaming :: project structure updated to candy coat

[ Added ]
* First Time Setup Wizard ::
  + new onboarding flow for new users ^__^
  + Identity Setup :: set your character name and world
  + Dependency Check :: automatically checks for Glamourer and ChatTwo
  + Configuration :: enable/disable features during setup

.: 0.2.0 :. - 2026-02-18
[ Added ]
* Session Capture ::
  + new "session capture" tab in main ui
  + Pop-out Session Window :: isolates chat between staff and a specific patron
  + Context Menu Integration :: right-click a player in chat -> "start candy session"
  + ChatTwo IPC support for context menu items

.: 0.1.0 :. - 2026-02-18
[ Added ]
* Booking System :: complete ui for adding and managing bookings
* Patron Locator :: functionality to track and detect favorite patrons
* Client Profiles :: detailed view for patrons including persistent notes
* Glamourer Integration ::
  + ipc wrapper for glamourer
  + ability to link designs to profiles
  + "quick swap" buttons to apply designs instantly
* UI Refactor :: converted entire ui to use OtterGui for a premium feel

[ Removed ]
* Legacy sample commands (/cset, /cbook)

.: 0.0.1 :. - Initial Start
* project scaffolding based on dalamud sample plugin
