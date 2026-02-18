# Changelog

All notable changes to the **Candy Coat** project will be documented in this file.

## [0.3.0] - 2026-02-18
### Changed
-   **Renaming:** Project structure updated to **Candy Coat**.

### Added
-   **First Time Setup Wizard:**
    -   New onboarding flow for new users.
    -   **Identity Setup:** Set your character name and world.
    -   **Dependency Check:** Automatically checks for **Glamourer** and **ChatTwo**.
    -   **Configuration:** Enable/Disable features during setup.

## [0.2.0] - 2026-02-18
### Added
-   **Session Capture:**
    -   New "Session Capture" tab in main UI.
    -   **Pop-out Session Window** that isolates chat between staff and a specific patron.
    -   **Context Menu Integration:** Right-click a player in chat -> "Start Candy Session".
    -   `ChatTwo` IPC support for context menu items.

## [0.1.0] - 2026-02-18
### Added
-   **Booking System:** Complete UI for adding and managing bookings.
-   **Patron Locator:** Functionality to track and detect favorite patrons in the venue.
-   **Client Profiles:** Detailed view for patrons including persistent "Notes" and "RP Hooks".
-   **Glamourer Integration:**
    -   IPC Wrapper for `Glamourer`.
    -   Ability to link Glamourer Designs to Client Profiles.
    -   "Quick Swap" buttons to apply designs instantly.
-   **UI Refactor:** Converted entire UI to use `OtterGui` for a premium feel.

### Removed
-   Legacy sample commands (`/cset`, `/cbook`).

## [0.0.1] - Initial Start
-   Project scaffolding based on Dalamud Sample Plugin.
