# Toolbar Redesign — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace `MainWindow.cs` (ImGui window with parallax bug) with a screen-anchored Umbra-style toolbar that renders via `UiBuilder.Draw` → `GetBackgroundDrawList()`, eliminating the shifting/parallax issue entirely.

**Architecture:** A `ToolbarService` hooks `UiBuilder.Draw` and renders a thin vertical toolbar strip directly to the background draw list at a screen-absolute position. Clicking a toolbar button opens a `BalloonService`-managed panel that slides out from the toolbar. A hidden "ghost" ImGui window pins itself at the balloon position each frame so `DrawOverlays()` has a valid ImGui context for text inputs and combos. All animations are frame-delta lerp (no timers).

**Tech Stack:** Una.Drawing (node trees), `ImGui.GetBackgroundDrawList()`, `ImGui.GetMainViewport()`, frame-delta lerp animation, Dalamud `UiBuilder.Draw` hook, existing `ITab` / `IToolboxPanel` implementations (unchanged).

**Design reference:** `docs/plans/2026-03-07-toolbar-redesign-design.md`

**Build command:** `dotnet build CandyCoat/CandyCoat.csproj`

**IMPORTANT — Una.Drawing API reminders (read before writing any node code):**
- `Node.AppendChild(node)` — not array initializer
- `Color.AssignByName(string, uint)` — uint is `0xAABBGGRR` (R and B are SWAPPED vs ARGB)
- `new Color("namedColor")` — references a named color
- `Style.BorderColor` is `BorderColor?` — use `new BorderColor(new Color("name"))`
- `BorderRadius` and `StrokeWidth` are `float?`
- Stylesheet is `List<StyleDefinition>` with `$"#{id}:hover"` pseudo-class
- Only root nodes (ParentNode == null) call `Render()`
- No `Overflow` property — use fixed `Size.Height` for scroll containers
- `Style.AutoSize` tuple needs full qualifier: `(Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit)`
- `ImGuiWindowFlags.NoBringToDisplayOnFocus` does NOT exist — use `NoFocusOnAppearing | NoNav` instead

---

## Task 1: Add ToolbarAnchor enum and Configuration fields

**Files:**
- Create: `CandyCoat/Data/ToolbarAnchor.cs`
- Modify: `CandyCoat/Configuration.cs`

**Step 1: Create the enum file**

```csharp
// CandyCoat/Data/ToolbarAnchor.cs
namespace CandyCoat.Data;

public enum ToolbarAnchor
{
    Left,
    Right,
    Top,
    Bottom
}
```

**Step 2: Add fields to Configuration.cs**

In `Configuration.cs`, add after the existing fields (before the `Save()` method or at the end of the field list):

```csharp
// Toolbar layout
public ToolbarAnchor ToolbarAnchor { get; set; } = ToolbarAnchor.Left;
public bool ToolbarLocked { get; set; } = false;
public float BalloonWidth { get; set; } = 380f;
public string LastActiveEntryId { get; set; } = string.Empty;
```

**Step 3: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors

**Step 4: Commit**

```bash
git add CandyCoat/Data/ToolbarAnchor.cs CandyCoat/Configuration.cs
git commit -m "feat: add ToolbarAnchor enum and toolbar config fields"
```

---

## Task 2: Create IToolbarEntry interface

**Files:**
- Create: `CandyCoat/UI/Toolbar/IToolbarEntry.cs`

**Step 1: Create the interface**

```csharp
// CandyCoat/UI/Toolbar/IToolbarEntry.cs
using Una.Drawing;
using CandyCoat.Data;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// A single entry in the screen-anchored toolbar.
/// Produces a Una.Drawing node tree for the balloon panel content.
/// </summary>
public interface IToolbarEntry
{
    /// <summary>Unique string identifier (used for LastActiveEntryId config).</summary>
    string Id { get; }

    /// <summary>FontAwesome icon character string (e.g. FontAwesomeIcon.Heart.ToIconString()).</summary>
    string Icon { get; }

    /// <summary>Display label shown on the toolbar button (expanded state) and balloon tab strip.</summary>
    string Label { get; }

    /// <summary>Role gate. StaffRole.None = always visible regardless of EnabledRoles.</summary>
    StaffRole Role { get; }

    /// <summary>
    /// Returns the Una.Drawing node tree for this entry's balloon panel content.
    /// Called every frame while the balloon is open. Cache the node tree internally — only
    /// rebuild when data changes, not on every call.
    /// </summary>
    Node BuildPanel();

    /// <summary>
    /// Called inside the ghost ImGui window each frame while this entry's balloon is open.
    /// Place all ImGui input widgets here (InputText, Combo, Checkbox, etc.).
    /// Use InputSpacer.Bounds.ContentRect to position them.
    /// </summary>
    void DrawOverlays();

    /// <summary>
    /// Optional settings content node. Return null if no settings section needed.
    /// Default implementation returns null.
    /// </summary>
    Node? BuildSettingsPanel() => null;

    /// <summary>Optional settings ImGui overlays.</summary>
    void DrawSettingsOverlays() { }
}
```

**Step 2: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors

**Step 3: Commit**

```bash
git add CandyCoat/UI/Toolbar/IToolbarEntry.cs
git commit -m "feat: add IToolbarEntry interface for screen-anchored toolbar"
```

---

## Task 3: Create ToolbarButton node factory

**Files:**
- Create: `CandyCoat/UI/Toolbar/ToolbarButton.cs`

A self-contained Una.Drawing node representing one button on the toolbar strip. Handles its own hover glow animation.

**Step 1: Create the file**

```csharp
// CandyCoat/UI/Toolbar/ToolbarButton.cs
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Una.Drawing;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// A single icon+label button for the toolbar strip.
/// Owns its Una.Drawing node. Call Update() each frame to animate.
/// </summary>
public class ToolbarButton : IDisposable
{
    public Node Root { get; }

    private readonly Node _iconNode;
    private readonly Node _labelNode;
    private readonly Node _glowNode;

    private float _glowAlpha = 0f;
    private bool _isActive = false;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            UpdateActiveStyle();
        }
    }

    public ToolbarButton(string id, string icon, string label, Action onClick)
    {
        _iconNode = new Node
        {
            InheritTags = true,
            Style = new Style
            {
                FontSize = 16,
                Color = new Color("Toolbar.Icon"),
                TextAlign = Anchor.MiddleCenter,
                Size = new Size(36, 36),
            }
        };
        _iconNode.NodeValue = icon;

        _labelNode = new Node
        {
            InheritTags = true,
            Style = new Style
            {
                FontSize = 12,
                Color = new Color("Toolbar.Label"),
                TextAlign = Anchor.MiddleLeft,
                Padding = new EdgeSize(0, 0, 0, 4),
                Overflow = false,
            }
        };
        _labelNode.NodeValue = label;

        // Glow ring rendered behind icon
        _glowNode = new Node
        {
            Style = new Style
            {
                Size = new Size(36, 36),
                BorderRadius = 18f,
                StrokeWidth = 2f,
                BorderColor = new BorderColor(new Color("Toolbar.Glow")),
                Opacity = 0f,
            }
        };

        Root = new Node
        {
            Id = id,
            Style = new Style
            {
                Display = DisplayType.Flex,
                FlowDirection = FlowDirection.Horizontal,
                Size = new Size(0, 40),
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Padding = new EdgeSize(2, 6),
                Gap = 4,
                BorderRadius = 6f,
                Cursor = Cursor.Pointer,
            },
            Stylesheet = new(
            [
                new StyleDefinition($"#{id}:hover", new Style
                {
                    BackgroundColor = new Color("Toolbar.ButtonHover"),
                }),
            ])
        };

        Root.AppendChild(_glowNode);
        Root.AppendChild(_iconNode);
        Root.AppendChild(_labelNode);

        Root.OnClick += _ => onClick();
    }

    /// <summary>Call each frame to animate glow alpha.</summary>
    public void Update(float deltaTime)
    {
        float glowTarget = _isActive ? 1f : 0f;
        _glowAlpha += (glowTarget - _glowAlpha) * deltaTime * 22f;
        _glowNode.Style.Opacity = _glowAlpha;
    }

    /// <summary>Set label visibility (collapsed = icon only, expanded = icon + label).</summary>
    public void SetExpanded(bool expanded)
    {
        _labelNode.Style.IsHidden = !expanded;
    }

    private void UpdateActiveStyle()
    {
        _iconNode.Style.Color = new Color(_isActive ? "Toolbar.IconActive" : "Toolbar.Icon");
    }

    public void Dispose()
    {
        Root.Dispose();
    }
}
```

**Step 2: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors. If `Cursor`, `DisplayType`, `FlowDirection`, `Anchor`, or `EdgeSize` are in a different namespace, adjust the using/qualifiers to match what the Una.Drawing source in `CandyCoat/Una.Drawing/` uses.

**Step 3: Commit**

```bash
git add CandyCoat/UI/Toolbar/ToolbarButton.cs
git commit -m "feat: add ToolbarButton Una.Drawing node factory"
```

---

## Task 4: Create TabStrip node factory

**Files:**
- Create: `CandyCoat/UI/Toolbar/TabStrip.cs`

Renders the named-tab strip at the top of the balloon. Supports wrap-to-second-line and highlights the active tab.

**Step 1: Create the file**

```csharp
// CandyCoat/UI/Toolbar/TabStrip.cs
using System;
using System.Collections.Generic;
using Una.Drawing;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Horizontal tab strip at the top of the balloon panel.
/// Rebuilds its node tree when tabs change; call Rebuild() after changing Tabs.
/// </summary>
public class TabStrip : IDisposable
{
    public Node Root { get; }

    private readonly List<(string Id, string Label)> _tabs = new();
    private string _activeId = string.Empty;
    private Action<string>? _onTabClicked;

    public TabStrip()
    {
        Root = new Node
        {
            Id = "tab-strip",
            Style = new Style
            {
                Display = DisplayType.Flex,
                FlowDirection = FlowDirection.Horizontal,
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Padding = new EdgeSize(4, 8),
                Gap = 4,
                Wrap = true,  // wraps to second line when tabs overflow width
                BackgroundColor = new Color("Balloon.TabStripBg"),
                BorderColor = new BorderColor(new Color("Balloon.Separator")),
                StrokeWidth = 1f,
            }
        };
    }

    public void SetTabs(List<(string Id, string Label)> tabs, string activeId, Action<string> onTabClicked)
    {
        _tabs.Clear();
        _tabs.AddRange(tabs);
        _activeId = activeId;
        _onTabClicked = onTabClicked;
        Rebuild();
    }

    public void SetActiveTab(string id)
    {
        _activeId = id;
        Rebuild();
    }

    private void Rebuild()
    {
        // Remove all children
        while (Root.ChildNodes.Count > 0)
            Root.ChildNodes[0].Remove();

        foreach (var (tabId, label) in _tabs)
        {
            bool isActive = tabId == _activeId;
            var tabNode = new Node
            {
                Id = $"tab-{tabId}",
                Style = new Style
                {
                    FontSize = 12,
                    Color = new Color(isActive ? "Tab.Active" : "Tab.Inactive"),
                    Padding = new EdgeSize(4, 10),
                    BorderRadius = 4f,
                    BackgroundColor = new Color(isActive ? "Tab.ActiveBg" : "Tab.InactiveBg"),
                    FontStyle = isActive ? FontStyle.Bold : FontStyle.Regular,
                    Cursor = Cursor.Pointer,
                    TextAlign = Anchor.MiddleCenter,
                    // Active tab bottom border accent
                    BorderColor = isActive
                        ? new BorderColor(default, default, new Color("Accent.Pink"), default)
                        : null,
                    StrokeWidth = isActive ? 2f : 0f,
                },
                Stylesheet = new(
                [
                    new StyleDefinition($"#tab-{tabId}:hover", new Style
                    {
                        BackgroundColor = new Color("Tab.HoverBg"),
                        Color = new Color("Tab.HoverFg"),
                    })
                ])
            };
            tabNode.NodeValue = label;
            var capturedId = tabId;
            tabNode.OnClick += _ => _onTabClicked?.Invoke(capturedId);
            Root.AppendChild(tabNode);
        }
    }

    public void Dispose()
    {
        Root.Dispose();
    }
}
```

**Note:** If `BorderColor` constructor with positional sides (top, right, bottom, left) is not available in the Una.Drawing version, check `CandyCoat/Una.Drawing/` source for the actual constructor overloads. Use whichever exists.

**Step 2: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors.

**Step 3: Commit**

```bash
git add CandyCoat/UI/Toolbar/TabStrip.cs
git commit -m "feat: add TabStrip Una.Drawing node factory for balloon header"
```

---

## Task 5: Create BalloonService

**Files:**
- Create: `CandyCoat/UI/Toolbar/BalloonService.cs`

Manages the active toolbar entry's balloon panel. Runs open/close and tab-crossfade animations. Hosts the ghost ImGui window for `DrawOverlays()`.

**Step 1: Create the file**

```csharp
// CandyCoat/UI/Toolbar/BalloonService.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Una.Drawing;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Renders the balloon panel that slides out from the toolbar.
/// Call OnDraw() from ToolbarService each frame — it manages its own root node.
/// </summary>
public class BalloonService : IDisposable
{
    // Animation state
    private float _openAlpha = 0f;       // 0 = closed, 1 = fully open
    private float _openWidth = 0f;       // current animated width
    private bool _isOpen = false;

    // Tab crossfade
    private float _fadeOut = 0f;         // outgoing panel alpha (1→0)
    private float _fadeIn = 1f;          // incoming panel alpha (0→1)
    private float _scaleOut = 1f;        // outgoing panel scale
    private float _scaleIn = 1f;         // incoming panel scale
    private bool _transitioning = false;
    private Node? _outgoingPanel = null;

    private IToolbarEntry? _activeEntry = null;
    private string _activeTabId = string.Empty;

    // Nodes
    private readonly Node _balloonRoot;
    private readonly Node _contentArea;
    private readonly TabStrip _tabStrip;

    private readonly Configuration _config;

    // Balloon panel is always this tall; width is animated
    private const float BalloonHeight = 480f;
    private const float AnimSpeed = 18f;

    public IToolbarEntry? ActiveEntry => _activeEntry;
    public bool IsVisible => _openAlpha > 0.01f;

    // Screen position of the balloon — set by ToolbarService each frame before calling OnDraw
    public Vector2 BalloonScreenPos { get; set; }

    public BalloonService(Configuration config)
    {
        _config = config;

        _tabStrip = new TabStrip();

        _contentArea = new Node
        {
            Id = "balloon-content",
            Style = new Style
            {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
                Overflow = false,
            }
        };

        _balloonRoot = new Node
        {
            Id = "balloon-root",
            Style = new Style
            {
                Display = DisplayType.Flex,
                FlowDirection = FlowDirection.Vertical,
                BackgroundColor = new Color("Balloon.Bg"),
                BorderRadius = 8f,
                BorderColor = new BorderColor(new Color("Balloon.Border")),
                StrokeWidth = 1f,
                Size = new Size((int)_config.BalloonWidth, (int)BalloonHeight),
                Opacity = 0f,
                IsHidden = true,
            }
        };
        _balloonRoot.AppendChild(_tabStrip.Root);
        _balloonRoot.AppendChild(_contentArea);
    }

    /// <summary>Toggle or switch the active entry. Pass null to close.</summary>
    public void SetActiveEntry(IToolbarEntry? entry)
    {
        if (entry == null || entry == _activeEntry)
        {
            // Toggle closed
            _activeEntry = null;
            _isOpen = false;
            return;
        }

        _activeEntry = entry;
        _isOpen = true;
        RebuildContent(entry);
    }

    private void RebuildContent(IToolbarEntry entry)
    {
        // For now the balloon shows a single panel per entry.
        // OverviewEntry manages its own internal tab state and returns a full panel node.
        while (_contentArea.ChildNodes.Count > 0)
            _contentArea.ChildNodes[0].Remove();

        var panel = entry.BuildPanel();
        _contentArea.AppendChild(panel);

        // Build tab strip with a single tab (or OverviewEntry will override)
        _tabStrip.SetTabs(
            new List<(string, string)> { (entry.Id, entry.Label) },
            entry.Id,
            _ => { });
    }

    /// <summary>
    /// Call once per frame from ToolbarService BEFORE calling Render.
    /// Returns the current screen position the ghost window should be placed at.
    /// </summary>
    public void Update(float deltaTime)
    {
        float alphaTarget = _isOpen ? 1f : 0f;
        float widthTarget = _isOpen ? _config.BalloonWidth : 0f;

        _openAlpha += (alphaTarget - _openAlpha) * deltaTime * AnimSpeed;
        _openWidth  += (widthTarget - _openWidth)  * deltaTime * AnimSpeed;

        _balloonRoot.Style.Opacity = _openAlpha;
        _balloonRoot.Style.IsHidden = _openAlpha < 0.01f;
        _balloonRoot.Style.Size = new Size((int)_openWidth, (int)BalloonHeight);

        // Tab crossfade
        if (_transitioning)
        {
            _fadeOut += (0f - _fadeOut) * deltaTime * AnimSpeed;
            _scaleOut += (0.96f - _scaleOut) * deltaTime * AnimSpeed;

            if (_fadeOut < 0.05f)
            {
                // Phase 2: bring in new panel
                if (_outgoingPanel != null)
                {
                    _outgoingPanel.Remove();
                    _outgoingPanel = null;
                }
                _fadeIn += (1f - _fadeIn) * deltaTime * AnimSpeed;
                _scaleIn += (1f - _scaleIn) * deltaTime * AnimSpeed;

                if (_fadeIn > 0.95f) _transitioning = false;
            }
        }
    }

    /// <summary>
    /// Render balloon to the provided draw list at BalloonScreenPos.
    /// Called from ToolbarService after Update().
    /// </summary>
    public void Render(ImDrawListPtr drawList)
    {
        if (_balloonRoot.Style.IsHidden) return;
        _balloonRoot.Style.Size = new Size((int)_openWidth, (int)BalloonHeight);
        _balloonRoot.Render(drawList, BalloonScreenPos);
    }

    /// <summary>
    /// Open a ghost ImGui window at BalloonScreenPos and call DrawOverlays() on the active entry.
    /// Call this after ImGui.NewFrame() — typically at the end of the UiBuilder.Draw hook.
    /// </summary>
    public void DrawGhostWindow()
    {
        if (_activeEntry == null || _openAlpha < 0.01f) return;

        ImGui.SetNextWindowPos(BalloonScreenPos);
        ImGui.SetNextWindowSize(new Vector2(_openWidth, BalloonHeight));
        ImGui.SetNextWindowBgAlpha(0f);

        var flags = ImGuiWindowFlags.NoDecoration
                  | ImGuiWindowFlags.NoBackground
                  | ImGuiWindowFlags.NoMove
                  | ImGuiWindowFlags.NoSavedSettings
                  | ImGuiWindowFlags.NoFocusOnAppearing
                  | ImGuiWindowFlags.NoNav
                  | ImGuiWindowFlags.NoScrollbar
                  | ImGuiWindowFlags.NoScrollWithMouse;

        if (ImGui.Begin("##candy_balloon_ghost", flags))
        {
            _activeEntry.DrawOverlays();
        }
        ImGui.End();
    }

    public void Dispose()
    {
        _tabStrip.Dispose();
        _balloonRoot.Dispose();
    }
}
```

**Step 2: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors. Fix any Una.Drawing API mismatches by checking `CandyCoat/Una.Drawing/` source.

**Step 3: Commit**

```bash
git add CandyCoat/UI/Toolbar/BalloonService.cs
git commit -m "feat: add BalloonService for sliding balloon panel with ghost ImGui window"
```

---

## Task 6: Create ToolbarService

**Files:**
- Create: `CandyCoat/UI/Toolbar/ToolbarService.cs`

The main rendering service. Hooks `UiBuilder.Draw`, positions the toolbar strip on screen, renders toolbar buttons, forwards clicks to `BalloonService`.

**Step 1: Create the file**

```csharp
// CandyCoat/UI/Toolbar/ToolbarService.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Una.Drawing;
using CandyCoat.Data;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Screen-anchored toolbar service. Hook into UiBuilder.Draw.
/// Renders toolbar strip + balloon via Una.Drawing to GetBackgroundDrawList().
/// No ImGui window — position is viewport-absolute, never parallaxes.
/// </summary>
public class ToolbarService : IDisposable
{
    private readonly IDalamudPluginInterface _pi;
    private readonly Configuration _config;
    private readonly BalloonService _balloon;

    private readonly List<(IToolbarEntry Entry, ToolbarButton Button)> _buttons = new();

    // Toolbar animation
    private float _toolbarWidth = 44f;   // collapsed width
    private bool _isHovered = false;

    private const float CollapsedWidth = 44f;
    private const float ExpandedWidth  = 160f;
    private const float ToolbarPad     = 8f;
    private const float AnimSpeed      = 18f;

    // Root node for the entire toolbar strip
    private readonly Node _toolbarRoot;

    public ToolbarService(IDalamudPluginInterface pi, Configuration config)
    {
        _pi = pi;
        _config = config;
        _balloon = new BalloonService(config);

        _toolbarRoot = new Node
        {
            Id = "toolbar-root",
            Style = new Style
            {
                Display = DisplayType.Flex,
                FlowDirection = FlowDirection.Vertical,
                BackgroundColor = new Color("Toolbar.Bg"),
                BorderRadius = 8f,
                BorderColor = new BorderColor(new Color("Toolbar.Border")),
                StrokeWidth = 1f,
                Padding = new EdgeSize(6),
                Gap = 2,
            }
        };

        _pi.UiBuilder.Draw += OnDraw;
    }

    /// <summary>Register all toolbar entries. Call once after construction.</summary>
    public void SetEntries(List<IToolbarEntry> entries)
    {
        // Clean up existing buttons
        foreach (var (_, btn) in _buttons) btn.Dispose();
        _buttons.Clear();
        while (_toolbarRoot.ChildNodes.Count > 0)
            _toolbarRoot.ChildNodes[0].Remove();

        foreach (var entry in entries)
        {
            // Skip role-gated entries if role not enabled
            if (entry.Role != StaffRole.None && !_config.EnabledRoles.HasFlag(entry.Role))
                continue;

            var entry_captured = entry;
            var btn = new ToolbarButton(
                id: $"btn-{entry.Id}",
                icon: entry.Icon,
                label: entry.Label,
                onClick: () => OnButtonClicked(entry_captured));

            _buttons.Add((entry, btn));
            _toolbarRoot.AppendChild(btn.Root);
        }
    }

    private void OnButtonClicked(IToolbarEntry entry)
    {
        // Toggle: clicking active button closes balloon
        bool wasActive = _balloon.ActiveEntry == entry;
        foreach (var (_, btn) in _buttons)
            btn.IsActive = false;

        if (!wasActive)
        {
            _balloon.SetActiveEntry(entry);
            foreach (var (e, b) in _buttons)
                b.IsActive = e == entry;
        }
        else
        {
            _balloon.SetActiveEntry(null);
        }
    }

    private void OnDraw()
    {
        float delta = ImGui.GetIO().DeltaTime;

        // Toolbar screen position (top-left of toolbar strip)
        var viewport = ImGui.GetMainViewport();
        var toolbarPos = CalculateToolbarPos(viewport);

        // Hover detection (simple AABB against mouse pos)
        var mousePos = ImGui.GetIO().MousePos;
        _isHovered = mousePos.X >= toolbarPos.X && mousePos.X <= toolbarPos.X + _toolbarWidth
                  && mousePos.Y >= toolbarPos.Y && mousePos.Y <= toolbarPos.Y + GetToolbarHeight();

        // Animate toolbar width
        float widthTarget = _isHovered ? ExpandedWidth : CollapsedWidth;
        _toolbarWidth += (widthTarget - _toolbarWidth) * delta * AnimSpeed;

        // Update button expanded state
        bool expanded = _toolbarWidth > CollapsedWidth + 10f;
        foreach (var (_, btn) in _buttons)
        {
            btn.SetExpanded(expanded);
            btn.Update(delta);
        }

        // Update toolbar root size
        _toolbarRoot.Style.Size = new Size((int)_toolbarWidth, (int)GetToolbarHeight());

        // Balloon screen position (to the right of toolbar for Left anchor)
        var balloonPos = CalculateBalloonPos(toolbarPos);
        _balloon.BalloonScreenPos = balloonPos;
        _balloon.Update(delta);

        // Render to background draw list (no ImGui window)
        var dl = ImGui.GetBackgroundDrawList();
        _toolbarRoot.Render(dl, toolbarPos);
        _balloon.Render(dl);

        // Ghost window for DrawOverlays()
        _balloon.DrawGhostWindow();
    }

    private Vector2 CalculateToolbarPos(ImGuiViewportPtr viewport)
    {
        // Left anchor: toolbar on left edge, vertically centered
        // Adapt for other anchors based on _config.ToolbarAnchor
        return _config.ToolbarAnchor switch
        {
            ToolbarAnchor.Left   => new Vector2(viewport.Pos.X + ToolbarPad,
                                                viewport.Pos.Y + (viewport.Size.Y - GetToolbarHeight()) / 2f),
            ToolbarAnchor.Right  => new Vector2(viewport.Pos.X + viewport.Size.X - _toolbarWidth - ToolbarPad,
                                                viewport.Pos.Y + (viewport.Size.Y - GetToolbarHeight()) / 2f),
            ToolbarAnchor.Top    => new Vector2(viewport.Pos.X + (viewport.Size.X - _toolbarWidth) / 2f,
                                                viewport.Pos.Y + ToolbarPad),
            ToolbarAnchor.Bottom => new Vector2(viewport.Pos.X + (viewport.Size.X - _toolbarWidth) / 2f,
                                                viewport.Pos.Y + viewport.Size.Y - GetToolbarHeight() - ToolbarPad),
            _ => Vector2.Zero
        };
    }

    private Vector2 CalculateBalloonPos(Vector2 toolbarPos)
    {
        const float gap = 6f;
        return _config.ToolbarAnchor switch
        {
            ToolbarAnchor.Left   => toolbarPos with { X = toolbarPos.X + _toolbarWidth + gap },
            ToolbarAnchor.Right  => toolbarPos with { X = toolbarPos.X - _config.BalloonWidth - gap },
            ToolbarAnchor.Top    => toolbarPos with { Y = toolbarPos.Y + GetToolbarHeight() + gap },
            ToolbarAnchor.Bottom => toolbarPos with { Y = toolbarPos.Y - 480f - gap },
            _ => toolbarPos
        };
    }

    private float GetToolbarHeight()
    {
        return _buttons.Count * 44f + 16f; // 44px per button + padding
    }

    public void Dispose()
    {
        _pi.UiBuilder.Draw -= OnDraw;
        _balloon.Dispose();
        foreach (var (_, btn) in _buttons) btn.Dispose();
        _toolbarRoot.Dispose();
    }
}
```

**Step 2: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors.

**Step 3: Commit**

```bash
git add CandyCoat/UI/Toolbar/ToolbarService.cs
git commit -m "feat: add ToolbarService — screen-anchored toolbar via UiBuilder.Draw"
```

---

## Task 7: Create OverviewEntry

**Files:**
- Create: `CandyCoat/UI/Toolbar/OverviewEntry.cs`

Wraps all `ITab` panel instances. Manages its own internal tab state and tab strip. The balloon's tab strip delegates to `OverviewEntry` for the tab list.

**Step 1: Create the file**

```csharp
// CandyCoat/UI/Toolbar/OverviewEntry.cs
using System.Collections.Generic;
using Una.Drawing;
using CandyCoat.Data;
using CandyCoat.Windows.Tabs;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Toolbar entry that hosts all ITab panels under one balloon button.
/// Internally manages which tab is active and owns the balloon tab strip.
/// </summary>
public class OverviewEntry : IToolbarEntry
{
    public string Id    => "overview";
    public string Icon  => "\uF015"; // FontAwesome home icon — adjust to actual FontAwesomeIcon char
    public string Label => "Overview";
    public StaffRole Role => StaffRole.None; // Always visible

    private readonly List<ITab> _tabs;
    private int _activeIndex = 0;
    private readonly TabStrip _tabStrip;

    // Outer wrapper returned as the panel — contains tab strip + active tab content
    private readonly Node _panelRoot;
    private readonly Node _tabContentArea;

    public OverviewEntry(List<ITab> tabs)
    {
        _tabs = tabs;
        _tabStrip = new TabStrip();

        _tabContentArea = new Node
        {
            Id = "overview-content",
            Style = new Style
            {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
            }
        };

        _panelRoot = new Node
        {
            Id = "overview-panel",
            Style = new Style
            {
                Display = DisplayType.Flex,
                FlowDirection = FlowDirection.Vertical,
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
            }
        };
        _panelRoot.AppendChild(_tabStrip.Root);
        _panelRoot.AppendChild(_tabContentArea);

        BuildTabList();
    }

    private void BuildTabList()
    {
        var tabDefs = new List<(string, string)>();
        foreach (var tab in _tabs)
            tabDefs.Add((tab.Name, tab.Name));

        _tabStrip.SetTabs(tabDefs, _tabs[_activeIndex].Name, OnTabSelected);
        RefreshContent();
    }

    private void OnTabSelected(string tabName)
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            if (_tabs[i].Name == tabName)
            {
                _activeIndex = i;
                break;
            }
        }
        RefreshContent();
    }

    private void RefreshContent()
    {
        while (_tabContentArea.ChildNodes.Count > 0)
            _tabContentArea.ChildNodes[0].Remove();

        var node = _tabs[_activeIndex].BuildNode();
        _tabContentArea.AppendChild(node);

        _tabStrip.SetActiveTab(_tabs[_activeIndex].Name);
    }

    public Node BuildPanel() => _panelRoot;

    public void DrawOverlays()
    {
        _tabs[_activeIndex].DrawOverlays();
    }
}
```

**Note:** `ITab.BuildNode()` must exist on all tab implementations. Verify this in `CandyCoat/Windows/Tabs/` — from project history it should be there. If `ITab` interface doesn't declare `BuildNode()`, add it to `ITab.cs`:
```csharp
Una.Drawing.Node BuildNode();
```

**Step 2: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors.

**Step 3: Commit**

```bash
git add CandyCoat/UI/Toolbar/OverviewEntry.cs
git commit -m "feat: add OverviewEntry wrapping all ITab panels under one toolbar button"
```

---

## Task 8: Create SrtEntry adapter and SettingsEntry

**Files:**
- Create: `CandyCoat/UI/Toolbar/SrtEntry.cs`
- Create: `CandyCoat/UI/Toolbar/SettingsEntry.cs`

**Step 1: Create SrtEntry.cs**

A thin generic adapter that wraps any `IToolboxPanel` as an `IToolbarEntry`. No changes to existing panel files.

```csharp
// CandyCoat/UI/Toolbar/SrtEntry.cs
using Una.Drawing;
using CandyCoat.Data;
using CandyCoat.Windows.SRT;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Adapts an IToolboxPanel to IToolbarEntry.
/// The panel's BuildNode() becomes BuildPanel().
/// The panel's DrawOverlays() passes through unchanged.
/// </summary>
public class SrtEntry : IToolbarEntry
{
    private readonly IToolboxPanel _panel;
    private readonly string _icon;

    public string Id    => _panel.Name.ToLowerInvariant().Replace(" ", "-");
    public string Label => _panel.Name;
    public StaffRole Role => _panel.Role;
    public string Icon  => _icon;

    public SrtEntry(IToolboxPanel panel, string icon)
    {
        _panel = panel;
        _icon  = icon;
    }

    public Node BuildPanel() => _panel.BuildNode();
    public void DrawOverlays() => _panel.DrawOverlays();
    public Node? BuildSettingsPanel() => _panel.BuildSettingsNode();
    public void DrawSettingsOverlays() => _panel.DrawSettingsOverlays();
}
```

**Note:** `IToolboxPanel` must expose `BuildNode()`, `DrawOverlays()`, `BuildSettingsNode()`, `DrawSettingsOverlays()`. Verify these exist in `IToolboxPanel.cs`. If not, add them to the interface (the implementations should already have them per project history).

**Step 2: Create SettingsEntry.cs**

```csharp
// CandyCoat/UI/Toolbar/SettingsEntry.cs
using Una.Drawing;
using CandyCoat.Data;
using CandyCoat.UI;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// Toolbar entry for the Settings panel (cog icon at the bottom of the toolbar).
/// </summary>
public class SettingsEntry : IToolbarEntry
{
    public string Id    => "settings";
    public string Icon  => "\uF013"; // FontAwesome cog — adjust to actual glyph char
    public string Label => "Settings";
    public StaffRole Role => StaffRole.None;

    private readonly SettingsPanel _settingsPanel;

    public SettingsEntry(SettingsPanel settingsPanel)
    {
        _settingsPanel = settingsPanel;
    }

    public Node BuildPanel() => _settingsPanel.BuildNode();
    public void DrawOverlays() => _settingsPanel.DrawOverlays();
}
```

**Step 3: Verify IToolboxPanel interface has required methods**

Read `CandyCoat/Windows/SRT/IToolboxPanel.cs`. If `BuildNode()`, `DrawOverlays()`, `BuildSettingsNode()`, `DrawSettingsOverlays()` are missing, add them with default interface implementations:

```csharp
// In IToolboxPanel.cs, add:
Una.Drawing.Node BuildNode();
Una.Drawing.Node? BuildSettingsNode() => null;
void DrawOverlays() { }
void DrawSettingsOverlays() { }
```

**Step 4: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors.

**Step 5: Commit**

```bash
git add CandyCoat/UI/Toolbar/SrtEntry.cs CandyCoat/UI/Toolbar/SettingsEntry.cs CandyCoat/Windows/SRT/IToolboxPanel.cs
git commit -m "feat: add SrtEntry adapter and SettingsEntry for toolbar integration"
```

---

## Task 9: Add theme colors for toolbar

**Files:**
- Modify: `CandyCoat/UI/CandyTheme.cs`

The toolbar + balloon node styles reference named colors that must exist in the theme. Add them.

**Step 1: Read CandyTheme.cs**

Read `CandyCoat/UI/CandyTheme.cs` to see existing `AssignByName` calls.

**Step 2: Add toolbar colors**

Add the following `Color.AssignByName` calls in `CandyTheme.Apply()`. Color format is `0xAABBGGRR` (alpha-blue-green-red, R and B are SWAPPED vs standard ARGB):

```csharp
// Toolbar strip
Color.AssignByName("Toolbar.Bg",          0xFF2A1A2E); // dark purple
Color.AssignByName("Toolbar.Border",      0xFF5C3070); // muted purple
Color.AssignByName("Toolbar.Icon",        0xFFCCAACE); // soft lavender
Color.AssignByName("Toolbar.IconActive",  0xFFFF9EBF); // pink
Color.AssignByName("Toolbar.Label",       0xFFCCAACE); // soft lavender
Color.AssignByName("Toolbar.ButtonHover", 0xFF3D2050); // hover bg
Color.AssignByName("Toolbar.Glow",        0xFFFF9EBF); // pink glow ring

// Balloon panel
Color.AssignByName("Balloon.Bg",          0xFF231535); // deep purple
Color.AssignByName("Balloon.Border",      0xFF5C3070); // muted purple
Color.AssignByName("Balloon.TabStripBg",  0xFF1E1030); // slightly darker
Color.AssignByName("Balloon.Separator",   0xFF4A2860); // divider line

// Tab strip
Color.AssignByName("Tab.Active",          0xFFFF9EBF); // pink text
Color.AssignByName("Tab.ActiveBg",        0xFF3D1E50); // highlighted bg
Color.AssignByName("Tab.Inactive",        0xFFAA88BB); // muted
Color.AssignByName("Tab.InactiveBg",      0x00000000); // transparent
Color.AssignByName("Tab.HoverBg",         0xFF2F1840);
Color.AssignByName("Tab.HoverFg",         0xFFDDBBEE);
```

**Step 3: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors.

**Step 4: Commit**

```bash
git add CandyCoat/UI/CandyTheme.cs
git commit -m "feat: add toolbar and balloon named colors to CandyTheme"
```

---

## Task 10: Wire ToolbarService into Plugin.cs

**Files:**
- Modify: `CandyCoat/Plugin.cs`

Replace `MainWindow` with `ToolbarService`. Keep all other windows.

**Step 1: Read Plugin.cs fully**

Re-read `CandyCoat/Plugin.cs` to see the complete current state before editing.

**Step 2: Add ToolbarService field**

Replace:
```csharp
public MainWindow MainWindow { get; init; }
```
With:
```csharp
public ToolbarService ToolbarService { get; init; }
```

Add the using:
```csharp
using CandyCoat.UI.Toolbar;
```

**Step 3: Replace MainWindow construction**

Replace the `MainWindow = new MainWindow(...)` block with:
```csharp
// Build tab list for OverviewEntry
var bookingsTab = new BookingsTab(plugin: this, venueService: VenueService);
var locatorTab  = new LocatorTab(plugin: this, venueService: VenueService);
bookingsTab.OnPatronSelected += (p) => PatronDetailsWindow.OpenFor(p);
locatorTab.OnPatronSelected  += (p) => PatronDetailsWindow.OpenFor(p);

var overviewTabs = new List<ITab>
{
    new OverviewTab(this),
    bookingsTab,
    locatorTab,
    new SessionTab(this),
    new WaitlistTab(WaitlistManager),
    new StaffTab(ShiftManager),
};

// Build SRT panels
var srtPanels = new List<IToolboxPanel>
{
    new SweetheartPanel(this),
    new CandyHeartPanel(this),
    new BartenderPanel(this),
    new GambaPanel(this),
    new DJPanel(this),
    new ManagementPanel(this),
    new OwnerPanel(this),
    new GreeterPanel(this),
};

// Icon glyphs — use FontAwesomeIcon.XXXX.ToIconString() for each
// or the raw Unicode char. Adjust to real FontAwesome glyphs.
var srtIcons = new Dictionary<string, string>
{
    ["Sweetheart"]  = "\uF004", // heart
    ["CandyHeart"]  = "\uF0A0", // adjust
    ["Bartender"]   = "\uF000", // adjust
    ["Gamba"]       = "\uF11B", // gamepad
    ["DJ"]          = "\uF001", // music
    ["Management"]  = "\uF0E8", // sitemap
    ["Owner"]       = "\uF521", // crown
    ["Greeter"]     = "\uF2B9", // adjust
};

var entries = new List<IToolbarEntry>();
entries.Add(new OverviewEntry(overviewTabs));
foreach (var panel in srtPanels)
{
    var icon = srtIcons.TryGetValue(panel.Name, out var ico) ? ico : "\uF111";
    entries.Add(new SrtEntry(panel, icon));
}

var settingsPanel = new SettingsPanel(this);
entries.Add(new SettingsEntry(settingsPanel));

ToolbarService = new ToolbarService(PluginInterface, Configuration);
ToolbarService.SetEntries(entries);
```

**Step 4: Remove MainWindow from WindowSystem**

Remove:
```csharp
WindowSystem.AddWindow(MainWindow);
```

Remove from `Dispose()`:
```csharp
MainWindow.Dispose();
```

**Step 5: Update OnSetupComplete, ToggleMainUi, ToggleConfigUi, OnMainCommand**

These methods currently call `MainWindow.Toggle()` or set `MainWindow.IsOpen`. With the screen-anchored toolbar, the toolbar is always rendering when the plugin is loaded (no open/close needed for the toolbar strip itself). Update these:

```csharp
public void OnSetupComplete()
{
    // Toolbar is always visible once setup is complete — nothing to toggle
}

public void ToggleMainUi()
{
    if (!Configuration.IsSetupComplete) { SetupWindow.IsOpen = true; return; }
    // Toolbar is always on screen; /candy could toggle the last active balloon
    // For now: no-op (toolbar is persistent)
}

public void ToggleConfigUi()
{
    if (!Configuration.IsSetupComplete) { SetupWindow.IsOpen = true; return; }
    // Could open Settings balloon — implement later if desired
}

private void OnMainCommand(string command, string args)
{
    if (!Configuration.IsSetupComplete) { SetupWindow.IsOpen = true; return; }
    // /candy toggles the last used entry balloon or shows overview
    // For now: no-op — user interacts via toolbar
}
```

**Step 6: Add ToolbarService.Dispose() call**

In `Dispose()`:
```csharp
ToolbarService?.Dispose();
```

**Step 7: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors. This is the most complex wiring step — resolve any constructor argument mismatches by reading the actual panel constructors in `CandyCoat/Windows/SRT/`.

**Step 8: Commit**

```bash
git add CandyCoat/Plugin.cs
git commit -m "feat: wire ToolbarService into Plugin.cs, replace MainWindow"
```

---

## Task 11: Delete MainWindow.cs

**Files:**
- Delete: `CandyCoat/Windows/MainWindow.cs`

**Step 1: Verify no remaining references**

```bash
grep -r "MainWindow" CandyCoat/ --include="*.cs" -l
```

Expected: only `Plugin.cs` (the field is now `ToolbarService`) — no remaining `MainWindow` type references. If any remain, fix them first.

**Step 2: Delete the file**

```bash
rm "CandyCoat/Windows/MainWindow.cs"
```

**Step 3: Build**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors.

**Step 4: Commit**

```bash
git add -u CandyCoat/Windows/MainWindow.cs
git commit -m "refactor: delete MainWindow.cs — replaced by screen-anchored ToolbarService"
```

---

## Task 12: Version bump, changelog, and release commit

**Files:**
- Modify: `CandyCoat/CandyCoat.csproj`
- Modify: `CandyCoat/CandyCoat.json`
- Modify: `CHANGELOG.md`
- Modify: `README.md`

**Step 1: Bump version**

In `CandyCoat.csproj`, update `<AssemblyVersion>` and `<Version>` to `0.18.0.0`.
In `CandyCoat.json`, update `"AssemblyVersion"` to `"0.18.0.0"`.

**Step 2: Update CHANGELOG.md**

Add at the top (after the `# Changelog` header):

```markdown
## [0.18.0] — 2026-03-07

### Changed
- **Complete UI overhaul**: replaced `MainWindow` (ImGui window with parallax/shift bug) with a screen-anchored toolbar rendered directly to the background draw list — no more shifting when moving the window
- Toolbar anchors to the left screen edge by default; configurable to Right, Top, or Bottom in Settings
- Balloon panel slides out from toolbar with smooth animation when a button is clicked
- All SRT panels and Overview tabs now live in the toolbar balloon — no separate window needed
- Tab strip with named tabs at the top of each balloon panel
- Toolbar collapses to icon-only when not hovered, expands with labels on hover
```

**Step 3: Update README.md**

Update the version badge/mention from `0.17.0` to `0.18.0` and add a brief note about the new toolbar UI.

**Step 4: Final build check**

```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors, 0 warnings (or pre-existing warnings only).

**Step 5: Commit and tag**

```bash
git add CandyCoat/CandyCoat.csproj CandyCoat/CandyCoat.json CHANGELOG.md README.md
git commit -m "feat(v0.18.0): screen-anchored toolbar UI, eliminate parallax bug"
git tag v0.18.0
git push origin master
git push origin v0.18.0
```

---

## Appendix: Known API Pitfalls

| Pitfall | Correct approach |
|---------|-----------------|
| `Node.ChildNodes = new[] { ... }` | Use `node.AppendChild(child)` for each child |
| `Color.AssignByName("x", 0xAARRGGBB)` | Byte order is `0xAABBGGRR` (R and B swapped) |
| `new Color(0xFF...)` | Use `new Color("namedColor")` for named, or `Color.AssignByName` for new |
| `ImGuiWindowFlags.NoBringToDisplayOnFocus` | Does not exist — use `NoFocusOnAppearing \| NoNav` |
| `_root.Render(dl, Vector2.Zero)` | Use actual screen position — `Vector2.Zero` causes misalignment |
| `node.Style.BorderColor = new Color(...)` | `BorderColor` is `BorderColor?` struct: `new BorderColor(new Color(...))` |
| `new Stylesheet { ... }` | `Stylesheet` is `List<StyleDefinition>` — use collection initializer or `new([...])` |
| Calling `Render()` on a non-root node | Only root nodes (ParentNode == null) can call `Render()` |
| `DrawingLib.ThemeVersion` | Doesn't exist — `Color.ThemeVersion` increments internally per `AssignByName` |
