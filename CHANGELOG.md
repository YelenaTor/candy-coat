~*~ Changelog ~*~

all notable changes to the candy coat project will be documented here <3

.: 0.5.0 :. - 2026-02-22
[ Added ]
* Support & Feedback ::
  + Added a dedicated section to the Settings tab for reporting bugs, crashes, and suggestions.
[ Fixed ]
* CI/CD ::
  + Completely overhauled the release pipeline to ensure flat ZIP structures and array-based `repo.json` formatting.
  + Fixed nested directory and stray ZIP inclusion issues.

.: 0.4.10 :. - 2026-02-22
[ Fixed ]
* CI/CD ::
  + Completely resolved nested ZIP structure by implementing a temporary staging process during the build workflow.
  + Specifically excluded stray `latest.zip` files from the final plugin bundles.

.: 0.4.9 :. - 2026-02-22
[ Fixed ]
* CI/CD ::
  + Resolved "Empty ZIP" issue by explicitly specifying the build output directory in the release workflow, ensuring all plugin binaries are correctly included in the archive.

.: 0.4.8 :. - 2026-02-22
[ Fixed ]
* CI/CD ::
  + Fixed nested ZIP structure issue where the plugin files were being double-zipped.

.: 0.4.7 :. - 2026-02-22
[ Fixed ]
* CI/CD ::
  + Fixed `repo.json` format to ensure it is always an array, satisfying Dalamud's custom repository requirements.

.: 0.4.6 :. - 2026-02-21
[ Added ]
* Support & Feedback ::
  + Added a dedicated section to the Settings tab for reporting bugs, crashes, and suggestions.
  + Added direct links and format guidance for staff members.


.: 0.4.5 :. - 2026-02-21
[ Added ]
* Patron UI Overhaul ::
  + Renamed "Favorite" status to "Regular" across the entire plugin.
  + Added an "Eye" button to regulars in the locator list to instantly target them in-game.
  + Overhauled "Add Patron" UI with separate fields for First Name, Last Name, and World.
  + Added a "Detect Targeted" button to auto-fill patron details from your current game target.
  + Integrated a new "Add as Regular" option into the in-game character right-click context menu.
[ Fixed ]
* CI/CD ::
  + Hardened the release pipeline to use internal job artifacts, resolving intermittent GitHub Pages deployment failures.

.: 0.4.3 :. - 2026-02-21
[ Fixed ]
* API 14 Validation ::
  + Registered mandatory `OpenMainUi` and `OpenConfigUi` callbacks to resolve Dalamud validation warnings.
  + Improved UI integration with the Dalamud plugin list.

.: 0.4.2 :. - 2026-02-21
[ Fixed ]
* Critical Crash ::
  + Resolved a fatal CLR crash (MissingMethodException) during plugin initialization caused by a binary mismatch in `TerraFX.Interop.Windows` with Dalamud API 14.
  + Replaced unstable TerraFX MessageBox calls with a secure P/Invoke implementation.
  + Fixed `FindWindowEx` signature mismatch in `WindowFunctions`.

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
