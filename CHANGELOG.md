~*~ Changelog ~*~

all notable changes to the candy coat project will be documented here <3

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
