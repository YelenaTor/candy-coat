# Toolbar Redesign — Design Document
**Date:** 2026-03-07
**Status:** Approved
**Target version:** v0.18.0

---

## Problem Statement

The current `MainWindow.cs` hosts all Una.Drawing node rendering inside a draggable ImGui window. Because Una.Drawing computes layout per-frame from `ImGui.GetWindowPos()`, the entire UI parallaxes/shifts when the window is dragged — a fundamental incompatibility between retained-mode node layout and ImGui's immediate-mode window dragging. The only correct fix is to eliminate the ImGui window entirely.

---

## 1. Architecture Overview

**Umbra-style screen-anchored rendering** — no ImGui window for the main UI.

- A `UiBuilder.Draw` hook renders everything via `ImGui.GetBackgroundDrawList()`
- Position is calculated from `ImGui.GetMainViewport()` — screen-absolute, never shifts
- A hidden "ghost" ImGui window (pinned, invisible, zero decoration) is placed at the active balloon panel's screen position to give `DrawOverlays()` a valid ImGui context for `InputText`, `Combo`, etc.
- `SetupWindow`, `ProfileWindow`, `CosmeticWindow` remain regular ImGui windows (untouched)

---

## 2. Toolbar Component

**Shape:** Thin vertical strip anchored to the left edge of the screen by default.

**Anchor options (user-configurable):**
- Left edge: strip runs top→bottom, lock button at very bottom
- Right edge: strip runs top→bottom, lock button at very bottom
- Top edge: strip runs left→right, lock button at far right
- Bottom edge: strip runs left→right, lock button at far right

**Collapse behaviour:**
- Collapsed: only icon buttons visible (no labels)
- Expanded: icon + label visible, triggered by mouse hover over toolbar area
- Lerp-animated width expand/collapse (~120ms, speed factor ~18f)
- **Balloon persists** when toolbar collapses — closing balloon requires clicking the active button again or pressing Escape

**Toolbar buttons (top to bottom):**

| Button | Opens |
|--------|-------|
| Overview | Balloon → Overview drawer (all ITab panels) |
| Sweetheart | Balloon → Sweetheart panel |
| CandyHeart | Balloon → CandyHeart panel |
| Bartender | Balloon → Bartender panel |
| Gamba | Balloon → Gamba panel |
| DJ | Balloon → DJ panel |
| Management | Balloon → Management panel |
| Owner | Balloon → Owner panel |
| *(spacer)* | — |
| Settings cog | Balloon → Settings panel |
| Lock | Toggles toolbar anchor lock (prevents drag repositioning) |

Only buttons whose role is enabled in `Configuration.EnabledRoles` are shown. Overview is always shown.

**Hover glow:** Active/hovered buttons get a soft pink glow ring rendered around the icon via Una.Drawing `BorderColor` + animated `StrokeWidth`.

---

## 3. Balloon Panel

**Shape:** Rounded rectangle that slides out from the toolbar edge when a button is clicked.

**Layout (top to bottom inside balloon):**
```
┌─────────────────────────────────────┐
│  [Sweetheart] [CandyHeart] [Gamba]  │  ← Tab strip (names)
│  ─────────────────────────────────  │  ← separator
│                                     │
│         Active panel content        │
│                                     │
└─────────────────────────────────────┘
```

**Tab strip** (approved "Option D + B + C"):
- Horizontal strip of named tab buttons at the top of the balloon
- Wraps to a second line when tabs overflow the balloon width
- User-resizable balloon width (drag handle on the far edge from toolbar)
- "Overview" balloon contains all ITab panels as named tabs
- Single-panel balloons (SRT) show a single tab — strip still present, consistent UI
- Active tab name is bold + underline accent; inactive tabs are muted

**Open/close animation:**
- Slides out from toolbar edge (direction opposite to anchor edge)
- Width lerps from 0 → target width (~120ms, speed ~18f)
- Opacity lerps 0 → 1 simultaneously
- Close: reverse — slide + fade out, then hide

**Tab content transition:**
- Blur-fade crossfade between tab panels (~120ms total)
- Phase 1 (60ms): outgoing panel fades out + subtle scale 1.0→0.96
- Phase 2 (60ms): incoming panel fades in + scale 0.96→1.0
- Implemented via two overlapping Una.Drawing nodes with `Opacity` lerp

---

## 4. Animation System

All animations use lerp: `value += (target - value) * deltaTime * speed`

| Animation | Speed factor | ~Duration |
|-----------|-------------|-----------|
| Toolbar expand/collapse | 18f | ~120ms |
| Balloon open/close | 18f | ~120ms |
| Tab content crossfade | 18f | ~120ms |
| Button glow pulse | 6f | continuous |
| Hover glow appear | 22f | ~100ms |

`deltaTime` = `ImGui.GetIO().DeltaTime` (called each frame in the `UiBuilder.Draw` hook).

No timers. No coroutines. No `Task.Delay`. Pure frame-delta lerp.

---

## 5. Input Handling

Una.Drawing has no native input widgets. All interactive inputs (text fields, combos, checkboxes) remain as ImGui calls inside `DrawOverlays()`.

**Ghost window pattern:**
```csharp
// Pinned invisible ImGui window at balloon screen position
ImGui.SetNextWindowPos(balloonScreenPos);
ImGui.SetNextWindowSize(balloonSize);
ImGui.Begin("##candy_ghost",
    ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground |
    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav |
    ImGuiWindowFlags.NoBringToDisplayOnFocus | ImGuiWindowFlags.NoScrollbar);
activeEntry.DrawOverlays();
ImGui.End();
```

The ghost window is only opened when the balloon is visible. Its position and size update every frame to match the balloon. It is completely invisible (no title bar, no background, no border) — it exists purely to give ImGui a coordinate space for `InputText` etc.

---

## 6. File Structure

### New files
```
CandyCoat/UI/Toolbar/
  IToolbarEntry.cs         ← interface: Id, Icon, Label, Role, BuildPanel() → Node, DrawOverlays(), DrawSettings()
  ToolbarService.cs        ← UiBuilder.Draw hook; screen anchor logic; toolbar node tree; hover detection
  BalloonService.cs        ← Active entry state; balloon node tree; tab strip; animation state; ghost window
  ToolbarButton.cs         ← Single toolbar button node factory (icon, label, glow ring)
  TabStrip.cs              ← Tab strip node factory (named tabs, wrap logic, active indicator)
  OverviewEntry.cs         ← IToolbarEntry wrapping all ITab panels under one balloon
  SettingsEntry.cs         ← IToolbarEntry wrapping SettingsPanel
```

### Deleted
```
CandyCoat/Windows/MainWindow.cs      ← replaced entirely by ToolbarService + BalloonService
CandyCoat/UI/SettingsPanel.cs        ← folded into SettingsEntry
```

### Unchanged
```
CandyCoat/Windows/SetupWindow.cs
CandyCoat/Windows/ProfileWindow.cs
CandyCoat/Windows/CosmeticWindow.cs
CandyCoat/Windows/SessionWindow.cs
CandyCoat/Windows/PatronDetailsWindow.cs
CandyCoat/Windows/PatronAlertOverlay.cs
CandyCoat/Windows/SRT/              ← all panels converted to IToolbarEntry (minimal change)
CandyCoat/Windows/Tabs/             ← all tabs wrapped by OverviewEntry (no change to tab code)
CandyCoat/UI/CandyTheme.cs
CandyCoat/UI/CandyUI.cs
```

---

## 7. IToolbarEntry Interface

```csharp
public interface IToolbarEntry
{
    string Id { get; }
    string Icon { get; }         // FontAwesomeIcon char
    string Label { get; }
    StaffRole Role { get; }      // StaffRole.None = always visible
    Node BuildPanel();           // Una.Drawing node tree for balloon content
    void DrawOverlays();         // ImGui inputs (called inside ghost window)
    void DrawSettings() { }      // Optional settings section (default no-op)
}
```

Each existing `IToolboxPanel` becomes `IToolbarEntry` — `BuildPanel()` returns the same Una.Drawing node tree that `BuildNode()` currently returns. `DrawOverlays()` is unchanged.

Each `ITab` is wrapped by `OverviewEntry` which forwards to the active tab's `BuildPanel()` / `DrawOverlays()`.

---

## 8. Configuration Additions

```csharp
// Toolbar positioning
public ToolbarAnchor ToolbarAnchor { get; set; } = ToolbarAnchor.Left;
public bool ToolbarLocked { get; set; } = false;
public float BalloonWidth { get; set; } = 380f;
public string LastActiveEntryId { get; set; } = "";
```

```csharp
public enum ToolbarAnchor { Left, Right, Top, Bottom }
```

---

## 9. Migration Summary

| Before | After |
|--------|-------|
| `MainWindow.cs` + ImGui window | `ToolbarService` + `BalloonService` via `UiBuilder.Draw` |
| `IToolboxPanel.BuildNode()` | `IToolbarEntry.BuildPanel()` |
| `ITab.BuildNode()` | Wrapped by `OverviewEntry` |
| `SettingsPanel.cs` | `SettingsEntry.cs` |
| StyleManager-wrapped `Draw()` | No StyleManager (Una.Drawing handles styling) |
| `WindowSystem.AddWindow(mainWindow)` | `UiBuilder.Draw += toolbarService.OnDraw` |

No changes required to service layer, data models, IPC, or API.
