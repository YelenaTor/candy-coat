# Una.Drawing UI Refactor Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace Candy Coat's entire ImGui+OtterGui UI with Una.Drawing's retained-mode node system, producing a visually polished, themeable interface decoupled from Umbra.

**Architecture:** Each window builds a root `Node` tree once, then mutates node properties (style, value, visibility) in `BeforeDraw` lambdas for live data. ImGui input widgets (InputText, Combo, etc.) are rendered as overlays on top of `InputSpacer` nodes that reserve layout space. `DrawingLib.Setup()` / `Dispose()` are wired into `Plugin.cs`.

**Tech Stack:** Una.Drawing (git submodule `una-xiv/drawing`), SkiaSharp 3.118.0-preview.1.2, Dalamud API 14, .NET 10 Preview

---

## Una.Drawing Quick Reference

### Node creation
```csharp
var node = new Node {
    Id        = "my-node",
    NodeValue = "Label text",
    Tooltip   = "Hover tooltip",
    Overflow  = false,          // clip children to bounds
    ChildNodes = [ child1, child2 ],
    Style = new() {
        Size            = new(200, 40),   // 0 = auto
        Padding         = new(8, 12),     // EdgeSize(top/bottom, left/right)
        Margin          = new(4),
        Anchor          = Anchor.TopLeft,
        Flow            = Flow.Horizontal,
        Gap             = 8,
        BackgroundColor = new Color(0xFF2D1B35),  // 0xAARRGGBB
        BorderColor     = new(0xFF7B4FA0),
        BorderRadius    = 6,
        Color           = new Color("CandyText"),  // named color
        FontSize        = 14,
        TextAlign       = Anchor.MiddleCenter,
        IsVisible       = true,
        Opacity         = 1.0f,
    },
    Stylesheet = new() {
        { ".hover", new Style { BackgroundColor = new(0xFF3D2B45) } },
    },
};
node.OnClick      += _ => { /* handle */ };
node.OnMouseEnter += _ => node.AddClass("hover");
node.OnMouseLeave += _ => node.RemoveClass("hover");
node.BeforeDraw   += _ => { node.NodeValue = liveData; };
```

### Rendering (in Window.Draw())
```csharp
// Only root nodes (ParentNode == null) can call Render
_rootNode.Render(ImGui.GetWindowDrawList(), Vector2.Zero);
// Then draw ImGui overlays on top:
DrawOverlays();
```

### Color format
`new Color(0xAARRGGBB)` — alpha is the HIGH byte. `0xFF` = fully opaque.
Named colors: `Color.AssignByName("CandyText", 0xFFFFD6F0)` — all nodes using that named color auto-repaint when `DrawingLib.ThemeVersion` increments.

### Font indices (registered by DrawingLib.Setup)
- 0 = NotoSans  1 = Inconsolata  2 = FontAwesome Solid  3 = Arial  4 = GameGlyphs

### AutoSize
```csharp
AutoSize = (AutoSize.Grow, AutoSize.Fit)  // Horizontal grows to fill parent
```

### Node mutation vs rebuild
- **Mutate** (`BeforeDraw` lambda): change `NodeValue`, `Style.Color`, `Style.IsVisible`, `Style.Opacity` — efficient, only repaints affected node
- **Rebuild** (`BuildNode()`): call when the number or type of children changes structurally

### Text input overlay pattern
```csharp
// In node tree: reserve space
var inputSpacer = new Node { Id="name-input", Style = new() { Size = new(200, 28) } };
// In DrawOverlays(): position ImGui widget over spacer
var pos  = inputSpacer.Bounds.ContentRect;
ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y));
ImGui.SetNextItemWidth(pos.Width);
ImGui.InputText("##name", ref _nameBuffer, 64);
```

---

## Phase 0: Foundation

### Task 0.1: Add Una.Drawing submodule and update csproj

**Files:**
- Create: `CandyCoat/Una.Drawing/` (submodule)
- Modify: `CandyCoat/CandyCoat.csproj`

**Step 1: Create the candy-coat-testing branch**
```bash
git checkout -b candy-coat-testing
git push -u origin candy-coat-testing
```

**Step 2: Add the submodule**
```bash
git submodule add https://github.com/una-xiv/drawing.git CandyCoat/Una.Drawing
git submodule update --init --recursive
```

**Step 3: Update CandyCoat.csproj**

Remove OtterGui PackageReference (if present). Add:
```xml
<ItemGroup>
  <ProjectReference Include="Una.Drawing\Una.Drawing.csproj" />
</ItemGroup>
```

**Step 4: Verify build**
```
dotnet build CandyCoat/CandyCoat.csproj
```
Expected: 0 errors (Una.Drawing and SkiaSharp resolve via submodule).

**Step 5: Commit**
```bash
git add .gitmodules CandyCoat/Una.Drawing CandyCoat/CandyCoat.csproj
git commit -m "feat(phase0): add una-xiv/drawing submodule, update csproj"
```

---

### Task 0.2: Wire DrawingLib into Plugin.cs

**Files:**
- Modify: `CandyCoat/Plugin.cs`

**Step 1: Add DrawingLib setup at top of constructor (after PluginInterface is available)**
```csharp
using Una.Drawing;
// ...
DrawingLib.Setup(PluginInterface);
```

**Step 2: Add DrawingLib disposal in Dispose()**
```csharp
DrawingLib.Dispose();
```

**Step 3: Build and verify no errors**
```
dotnet build CandyCoat/CandyCoat.csproj
```

**Step 4: Commit**
```bash
git add CandyCoat/Plugin.cs
git commit -m "feat(phase0): wire DrawingLib.Setup/Dispose into Plugin"
```

---

### Task 0.3: Delete StyleManager, remove OtterGui

**Files:**
- Delete: `CandyCoat/UI/StyleManager.cs`
- Modify: all files that call `StyleManager.PushStyles()` / `PopStyles()`

**Step 1: Find all usages**
```bash
grep -r "StyleManager" CandyCoat/ --include="*.cs" -l
```

**Step 2: Remove all `StyleManager.PushStyles()` / `PopStyles()` calls** from each window's `Draw()` method. Una.Drawing handles its own rendering; ImGui base windows need only `ImGui.Begin`/`End`.

**Step 3: Delete `CandyCoat/UI/StyleManager.cs`**

**Step 4: If OtterGui is a submodule, remove it:**
```bash
git submodule deinit -f CandyCoat/OtterGui
git rm -f CandyCoat/OtterGui
rm -rf .git/modules/CandyCoat/OtterGui
```
Remove the `<ProjectReference>` for OtterGui from `CandyCoat.csproj`.
Fix any remaining `using OtterGui` imports (replace with plain ImGui equivalents).

**Step 5: Build and fix any compile errors**
```
dotnet build CandyCoat/CandyCoat.csproj
```

**Step 6: Commit**
```bash
git add -A
git commit -m "feat(phase0): remove StyleManager and OtterGui"
```

---

## Phase 1: CandyTheme — Named Color System

### Task 1.1: Create CandyTheme.cs

**Files:**
- Create: `CandyCoat/UI/CandyTheme.cs`

**Content:**
```csharp
using Una.Drawing;

namespace CandyCoat.UI;

/// <summary>
/// Registers all Candy Coat named colors with Una.Drawing's theme system.
/// Call CandyTheme.Apply() once after DrawingLib.Setup().
/// To update colors: change values and increment DrawingLib.ThemeVersion.
/// </summary>
internal static class CandyTheme
{
    // Backgrounds
    public const string BgWindow      = "CandyBgWindow";      // #1A0F20 — deep dark purple
    public const string BgSidebar     = "CandyBgSidebar";     // #140C1C — darker sidebar
    public const string BgCard        = "CandyBgCard";        // #2D1B35 — card surface
    public const string BgCardHover   = "CandyBgCardHover";   // #3D2B45 — card hover
    public const string BgInput       = "CandyBgInput";       // #201228 — input fields
    public const string BgTabActive   = "CandyBgTabActive";   // #7B4FA0 — active tab
    public const string BgTabInactive = "CandyBgTabInactive"; // #2D1B35 — inactive tab

    // Borders
    public const string BorderCard    = "CandyBorderCard";    // #7B4FA0 — purple border
    public const string BorderDivider = "CandyBorderDivider"; // #3D2045 — subtle divider
    public const string BorderFocus   = "CandyBorderFocus";   // #FFB6D9 — pink focus ring

    // Text
    public const string TextPrimary   = "CandyTextPrimary";   // #FFD6F0 — soft pink-white
    public const string TextSecondary = "CandyTextSecondary"; // #B89FBF — muted lavender
    public const string TextMuted     = "CandyTextMuted";     // #7A6080 — dim
    public const string TextAccent    = "CandyTextAccent";    // #FF9DD6 — bright pink
    public const string TextSuccess   = "CandyTextSuccess";   // #9DFFA0 — green
    public const string TextWarning   = "CandyTextWarning";   // #FFD770 — amber
    public const string TextDanger    = "CandyTextDanger";    // #FF7070 — red

    // Interactive
    public const string BtnPrimary    = "CandyBtnPrimary";    // #B060D0 — purple button
    public const string BtnHover      = "CandyBtnHover";      // #C070E0 — button hover
    public const string BtnGhost      = "CandyBtnGhost";      // #3D2045 — ghost button
    public const string BtnGhostHover = "CandyBtnGhostHover"; // #4D3055 — ghost hover

    // Status
    public const string StatusOnline  = "CandyStatusOnline";  // #60C870 — green dot
    public const string StatusAway    = "CandyStatusAway";    // #FFD060 — amber dot
    public const string StatusOffline = "CandyStatusOffline"; // #7A6080 — grey dot

    public static void Apply()
    {
        Color.AssignByName(BgWindow,      new Color(0xFF1A0F20));
        Color.AssignByName(BgSidebar,     new Color(0xFF140C1C));
        Color.AssignByName(BgCard,        new Color(0xFF2D1B35));
        Color.AssignByName(BgCardHover,   new Color(0xFF3D2B45));
        Color.AssignByName(BgInput,       new Color(0xFF201228));
        Color.AssignByName(BgTabActive,   new Color(0xFF7B4FA0));
        Color.AssignByName(BgTabInactive, new Color(0xFF2D1B35));
        Color.AssignByName(BorderCard,    new Color(0xFF7B4FA0));
        Color.AssignByName(BorderDivider, new Color(0xFF3D2045));
        Color.AssignByName(BorderFocus,   new Color(0xFFFFB6D9));
        Color.AssignByName(TextPrimary,   new Color(0xFFFFD6F0));
        Color.AssignByName(TextSecondary, new Color(0xFFB89FBF));
        Color.AssignByName(TextMuted,     new Color(0xFF7A6080));
        Color.AssignByName(TextAccent,    new Color(0xFFFF9DD6));
        Color.AssignByName(TextSuccess,   new Color(0xFF9DFFA0));
        Color.AssignByName(TextWarning,   new Color(0xFFFFD770));
        Color.AssignByName(TextDanger,    new Color(0xFFFF7070));
        Color.AssignByName(BtnPrimary,    new Color(0xFFB060D0));
        Color.AssignByName(BtnHover,      new Color(0xFFC070E0));
        Color.AssignByName(BtnGhost,      new Color(0xFF3D2045));
        Color.AssignByName(BtnGhostHover, new Color(0xFF4D3055));
        Color.AssignByName(StatusOnline,  new Color(0xFF60C870));
        Color.AssignByName(StatusAway,    new Color(0xFFFFD060));
        Color.AssignByName(StatusOffline, new Color(0xFF7A6080));

        DrawingLib.ThemeVersion++;
    }
}
```

**Step 2: Call `CandyTheme.Apply()` in Plugin.cs after `DrawingLib.Setup()`:**
```csharp
DrawingLib.Setup(PluginInterface);
CandyTheme.Apply();
```

**Step 3: Build**
```
dotnet build CandyCoat/CandyCoat.csproj
```

**Step 4: Commit**
```bash
git add CandyCoat/UI/CandyTheme.cs CandyCoat/Plugin.cs
git commit -m "feat(phase1): add CandyTheme named color system"
```

---

## Phase 2: CandyUI Node Factory

### Task 2.1: Create CandyUI.cs

**Files:**
- Create: `CandyCoat/UI/CandyUI.cs`

This is the component library. All window code uses these factories — never constructs raw Nodes directly (except in CandyUI itself). Full factory content:

```csharp
using System;
using System.Numerics;
using System.Collections.Generic;
using Una.Drawing;

namespace CandyCoat.UI;

internal static class CandyUI
{
    // -------------------------------------------------------------------------
    // Root window node — fills ImGui client area
    // -------------------------------------------------------------------------
    public static Node WindowRoot(params Node[] children) => new() {
        Id = "WindowRoot",
        ChildNodes = children,
        Style = new() {
            Size    = new(0, 0),
            AutoSize = (AutoSize.Grow, AutoSize.Grow),
            Flow    = Flow.Horizontal,
            BackgroundColor = new Color(CandyTheme.BgWindow),
        },
    };

    // -------------------------------------------------------------------------
    // Two-panel layout: sidebar + content
    // -------------------------------------------------------------------------
    public static Node Sidebar(params Node[] children) => new() {
        Id = "Sidebar",
        ChildNodes = children,
        Style = new() {
            Size            = new(200, 0),
            AutoSize        = (AutoSize.None, AutoSize.Grow),
            Flow            = Flow.Vertical,
            BackgroundColor = new Color(CandyTheme.BgSidebar),
            BorderColor     = new Color(CandyTheme.BorderDivider),
            Padding         = new(8),
            Gap             = 4,
        },
    };

    public static Node ContentPanel(params Node[] children) => new() {
        Id = "Content",
        ChildNodes = children,
        Style = new() {
            AutoSize        = (AutoSize.Grow, AutoSize.Grow),
            Flow            = Flow.Vertical,
            Padding         = new(12),
            Gap             = 8,
        },
    };

    // -------------------------------------------------------------------------
    // Card — rounded bordered box
    // -------------------------------------------------------------------------
    public static Node Card(string id, params Node[] children) => new() {
        Id = id,
        ChildNodes = children,
        Style = new() {
            AutoSize        = (AutoSize.Grow, AutoSize.Fit),
            Flow            = Flow.Vertical,
            Padding         = new(10, 12),
            Gap             = 6,
            BackgroundColor = new Color(CandyTheme.BgCard),
            BorderColor     = new Color(CandyTheme.BorderCard),
            BorderRadius    = 6,
            BorderWidth     = 1,
        },
    };

    // -------------------------------------------------------------------------
    // Buttons
    // -------------------------------------------------------------------------
    public static Node Button(string id, string label, Action onClick) {
        var node = new Node {
            Id        = id,
            NodeValue = label,
            Style = new() {
                Size            = new(0, 28),
                AutoSize        = (AutoSize.Fit, AutoSize.None),
                Padding         = new(0, 12),
                BackgroundColor = new Color(CandyTheme.BtnPrimary),
                BorderRadius    = 4,
                Color           = new Color(CandyTheme.TextPrimary),
                FontSize        = 13,
                TextAlign       = Anchor.MiddleCenter,
            },
            Stylesheet = new() {
                { ".hover", new Style { BackgroundColor = new Color(CandyTheme.BtnHover) } },
            },
        };
        node.OnClick      += _ => onClick();
        node.OnMouseEnter += _ => node.AddClass("hover");
        node.OnMouseLeave += _ => node.RemoveClass("hover");
        return node;
    }

    public static Node GhostButton(string id, string label, Action onClick) {
        var node = new Node {
            Id        = id,
            NodeValue = label,
            Style = new() {
                Size            = new(0, 26),
                AutoSize        = (AutoSize.Fit, AutoSize.None),
                Padding         = new(0, 10),
                BackgroundColor = new Color(CandyTheme.BtnGhost),
                BorderRadius    = 4,
                BorderColor     = new Color(CandyTheme.BorderCard),
                BorderWidth     = 1,
                Color           = new Color(CandyTheme.TextSecondary),
                FontSize        = 12,
                TextAlign       = Anchor.MiddleCenter,
            },
            Stylesheet = new() {
                { ".hover", new Style { BackgroundColor = new Color(CandyTheme.BtnGhostHover),
                                        Color           = new Color(CandyTheme.TextPrimary) } },
            },
        };
        node.OnClick      += _ => onClick();
        node.OnMouseEnter += _ => node.AddClass("hover");
        node.OnMouseLeave += _ => node.RemoveClass("hover");
        return node;
    }

    public static Node SmallButton(string id, string label, Action onClick) {
        var node = new Node {
            Id        = id,
            NodeValue = label,
            Style = new() {
                Size            = new(0, 22),
                AutoSize        = (AutoSize.Fit, AutoSize.None),
                Padding         = new(0, 8),
                BackgroundColor = new Color(CandyTheme.BtnGhost),
                BorderRadius    = 3,
                BorderColor     = new Color(CandyTheme.BorderDivider),
                BorderWidth     = 1,
                Color           = new Color(CandyTheme.TextMuted),
                FontSize        = 11,
                TextAlign       = Anchor.MiddleCenter,
            },
            Stylesheet = new() {
                { ".hover", new Style { Color = new Color(CandyTheme.TextPrimary) } },
            },
        };
        node.OnClick      += _ => onClick();
        node.OnMouseEnter += _ => node.AddClass("hover");
        node.OnMouseLeave += _ => node.RemoveClass("hover");
        return node;
    }

    // -------------------------------------------------------------------------
    // Text nodes
    // -------------------------------------------------------------------------
    public static Node Label(string id, string text, int fontSize = 13) => new() {
        Id        = id,
        NodeValue = text,
        Style = new() {
            AutoSize  = (AutoSize.Fit, AutoSize.Fit),
            Color     = new Color(CandyTheme.TextPrimary),
            FontSize  = fontSize,
            TextAlign = Anchor.MiddleLeft,
        },
    };

    public static Node SectionHeader(string id, string text) => new() {
        Id        = id,
        NodeValue = text,
        Style = new() {
            AutoSize  = (AutoSize.Grow, AutoSize.Fit),
            Color     = new Color(CandyTheme.TextAccent),
            FontSize  = 14,
            TextAlign = Anchor.MiddleLeft,
            Padding   = new(0, 0, 4, 0),
        },
    };

    public static Node Muted(string id, string text, int fontSize = 11) => new() {
        Id        = id,
        NodeValue = text,
        Style = new() {
            AutoSize  = (AutoSize.Fit, AutoSize.Fit),
            Color     = new Color(CandyTheme.TextMuted),
            FontSize  = fontSize,
            TextAlign = Anchor.MiddleLeft,
        },
    };

    // -------------------------------------------------------------------------
    // Layout helpers
    // -------------------------------------------------------------------------
    public static Node Separator(string id) => new() {
        Id = id,
        Style = new() {
            Size            = new(0, 1),
            AutoSize        = (AutoSize.Grow, AutoSize.None),
            Margin          = new(4, 0),
            BackgroundColor = new Color(CandyTheme.BorderDivider),
        },
    };

    public static Node Row(string id, float gap = 8, params Node[] children) => new() {
        Id         = id,
        ChildNodes = children,
        Style = new() {
            AutoSize = (AutoSize.Grow, AutoSize.Fit),
            Flow     = Flow.Horizontal,
            Gap      = gap,
        },
    };

    public static Node Column(string id, float gap = 6, params Node[] children) => new() {
        Id         = id,
        ChildNodes = children,
        Style = new() {
            AutoSize = (AutoSize.Grow, AutoSize.Fit),
            Flow     = Flow.Vertical,
            Gap      = gap,
        },
    };

    public static Node ScrollBox(string id, float height, params Node[] children) => new() {
        Id         = id,
        ChildNodes = children,
        Overflow   = false,
        Style = new() {
            Size     = new(0, (int)height),
            AutoSize = (AutoSize.Grow, AutoSize.None),
            Flow     = Flow.Vertical,
            Gap      = 4,
        },
    };

    // -------------------------------------------------------------------------
    // Sidebar components
    // -------------------------------------------------------------------------
    public static Node SidebarItem(string id, string label, bool active, Action onClick) {
        var node = new Node {
            Id        = id,
            NodeValue = label,
            Style = new() {
                AutoSize        = (AutoSize.Grow, AutoSize.Fit),
                Padding         = new(5, 10),
                BorderRadius    = 4,
                BackgroundColor = active ? new Color(CandyTheme.BgTabActive) : new Color(0x00000000),
                Color           = active ? new Color(CandyTheme.TextPrimary) : new Color(CandyTheme.TextSecondary),
                FontSize        = 13,
                TextAlign       = Anchor.MiddleLeft,
            },
        };
        if (!active) {
            node.Stylesheet = new() {
                { ".hover", new Style { BackgroundColor = new Color(CandyTheme.BgCard),
                                        Color           = new Color(CandyTheme.TextPrimary) } },
            };
            node.OnMouseEnter += _ => node.AddClass("hover");
            node.OnMouseLeave += _ => node.RemoveClass("hover");
        }
        node.OnClick += _ => onClick();
        return node;
    }

    public static Node SidebarDrawer(string id, string label, bool expanded, Action onToggle, params Node[] children) {
        var header = new Node {
            Id        = $"{id}-header",
            NodeValue = (expanded ? "v " : "> ") + label,
            Style = new() {
                AutoSize  = (AutoSize.Grow, AutoSize.Fit),
                Padding   = new(5, 8),
                Color     = new Color(CandyTheme.TextAccent),
                FontSize  = 13,
                TextAlign = Anchor.MiddleLeft,
            },
        };
        header.OnClick += _ => onToggle();

        var body = new Node {
            Id         = $"{id}-body",
            ChildNodes = children,
            Style = new() {
                IsVisible = expanded,
                AutoSize  = (AutoSize.Grow, AutoSize.Fit),
                Flow      = Flow.Vertical,
                Gap       = 2,
                Padding   = new(0, 0, 0, 8),
            },
        };

        return new Node {
            Id         = id,
            ChildNodes = [header, body],
            Style = new() {
                AutoSize = (AutoSize.Grow, AutoSize.Fit),
                Flow     = Flow.Vertical,
            },
        };
    }

    // -------------------------------------------------------------------------
    // Tab bar (horizontal)
    // -------------------------------------------------------------------------
    public static Node TabContainer(string id, string[] tabs, int activeTab, Action<int> onSelect, Node activeContent) {
        var tabNodes = new List<Node>();
        for (int i = 0; i < tabs.Length; i++) {
            int idx = i;
            bool active = idx == activeTab;
            var tab = new Node {
                Id        = $"{id}-tab-{i}",
                NodeValue = tabs[i],
                Style = new() {
                    Padding         = new(5, 12),
                    BackgroundColor = active ? new Color(CandyTheme.BgTabActive) : new Color(CandyTheme.BgTabInactive),
                    Color           = active ? new Color(CandyTheme.TextPrimary) : new Color(CandyTheme.TextSecondary),
                    FontSize        = 12,
                    TextAlign       = Anchor.MiddleCenter,
                    BorderRadius    = 4,
                },
            };
            if (!active) {
                tab.Stylesheet = new() {
                    { ".hover", new Style { BackgroundColor = new Color(CandyTheme.BgCard),
                                            Color           = new Color(CandyTheme.TextPrimary) } },
                };
                tab.OnMouseEnter += _ => tab.AddClass("hover");
                tab.OnMouseLeave += _ => tab.RemoveClass("hover");
            }
            tab.OnClick += _ => onSelect(idx);
            tabNodes.Add(tab);
        }

        var tabBar = new Node {
            Id         = $"{id}-bar",
            ChildNodes = tabNodes.ToArray(),
            Style = new() {
                AutoSize = (AutoSize.Grow, AutoSize.Fit),
                Flow     = Flow.Horizontal,
                Gap      = 4,
                Padding  = new(0, 0, 4, 0),
            },
        };

        return new Node {
            Id         = id,
            ChildNodes = [tabBar, activeContent],
            Style = new() {
                AutoSize = (AutoSize.Grow, AutoSize.Grow),
                Flow     = Flow.Vertical,
                Gap      = 0,
            },
        };
    }

    // -------------------------------------------------------------------------
    // Status badge (colored dot + label)
    // -------------------------------------------------------------------------
    public static Node StatusBadge(string id, string label, string colorName) {
        var dot = new Node {
            Id    = $"{id}-dot",
            Style = new() {
                Size            = new(8, 8),
                BackgroundColor = new Color(colorName),
                BorderRadius    = 4,
                Margin          = new(0, 4, 0, 0),
            },
        };
        var text = new Node {
            Id        = $"{id}-text",
            NodeValue = label,
            Style = new() {
                AutoSize  = (AutoSize.Fit, AutoSize.Fit),
                Color     = new Color(colorName),
                FontSize  = 12,
                TextAlign = Anchor.MiddleLeft,
            },
        };
        return Row(id, 4, dot, text);
    }

    // -------------------------------------------------------------------------
    // InputSpacer — reserves space for an ImGui overlay widget
    // -------------------------------------------------------------------------
    public static Node InputSpacer(string id, int width, int height = 28) => new() {
        Id = id,
        Style = new() {
            Size = new(width, height),
        },
    };
}
```

**Step 2: Build**
```
dotnet build CandyCoat/CandyCoat.csproj
```

**Step 3: Commit**
```bash
git add CandyCoat/UI/CandyUI.cs
git commit -m "feat(phase2): add CandyUI node factory"
```

---

## Phase 3: MainWindow + Interface Updates

### Task 3.1: Update ITab and IToolboxPanel interfaces

**Files:**
- Modify: `CandyCoat/Windows/Tabs/ITab.cs`
- Modify: `CandyCoat/Windows/SRT/IToolboxPanel.cs`

**ITab:**
```csharp
using Una.Drawing;

internal interface ITab
{
    string Name { get; }
    // Called once; return root node for this tab's content area.
    Node BuildNode();
    // Called every frame after Render() — draw ImGui input overlays here.
    void DrawOverlays() { }
    void Dispose() { }
}
```

**IToolboxPanel:**
```csharp
using Una.Drawing;

internal interface IToolboxPanel
{
    string Name  { get; }
    StaffRole Role { get; }
    Node BuildNode();          // content tab
    Node BuildSettingsNode();  // settings tab (shown in OwnerPanel settings)
    void DrawOverlays() { }
    void DrawSettingsOverlays() { }
    void Dispose() { }
}
```

**Step: Build, fix any implementor compile errors (add stub `BuildNode`/`BuildSettingsNode` returning `new Node()` as placeholder)**

**Commit:**
```bash
git add CandyCoat/Windows/Tabs/ITab.cs CandyCoat/Windows/SRT/IToolboxPanel.cs
git commit -m "feat(phase3): update ITab and IToolboxPanel for Una.Drawing"
```

---

### Task 3.2: Rewrite MainWindow

**Files:**
- Modify: `CandyCoat/Windows/MainWindow.cs`

**Architecture:**
```
WindowRoot (horizontal)
  Sidebar (vertical, 200px)
    Logo node
    Separator
    SidebarDrawer "Dashboard" (collapsible, lists ITab items)
    SidebarDrawer "Staff Toolbox" (collapsible, lists IToolboxPanel items)
    Separator
    SidebarItem "My Profile"
    Footer: StatusBadge, GhostButton "Settings", GhostButton "Cosmetics"
  ContentPanel (grow)
    [active tab node OR active panel node]
```

**Key implementation notes:**
- `_rootNode` is built once in the constructor after all tabs/panels are initialized
- Tab/panel switching calls `RebuildContent()` which swaps the content child: `_contentPanel.ChildNodes = [_activeTab.BuildNode()]`
- Sidebar active state: `BeforeDraw` lambdas on each sidebar item update `Style.BackgroundColor` + `Style.Color` based on `_activeSectionEnum`
- In `Draw()`:
  ```csharp
  // Resize root to match ImGui window
  _rootNode.Style.Size = new((int)ImGui.GetContentRegionAvail().X,
                              (int)ImGui.GetContentRegionAvail().Y);
  _rootNode.Render(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos());
  _activeTab?.DrawOverlays();
  ```
- Remove all `ImGui.BeginChild` / `ImGui.Columns` / `OtterGui` calls

**Step: Implement, build, fix errors. This is the most complex step — take it tab by tab.**

**Commit:**
```bash
git add CandyCoat/Windows/MainWindow.cs
git commit -m "feat(phase3): rewrite MainWindow with Una.Drawing root node"
```

---

## Phase 4: Dashboard Tabs (ITab implementations)

Migrate each tab one at a time. Pattern for each:

```csharp
internal class OverviewTab : ITab
{
    public string Name => "Overview";
    private Node? _root;

    public Node BuildNode()
    {
        _root = CandyUI.Column("overview-root", 8,
            CandyUI.SectionHeader("ov-title", "Overview"),
            CandyUI.Card("ov-welcome",
                CandyUI.Label("ov-welcome-text", "Welcome to Candy Coat!")),
            // ... etc
        );
        // Wire BeforeDraw for live data
        return _root;
    }
}
```

**Tabs to migrate (one commit each):**
1. `OverviewTab.cs`
2. `BookingsTab.cs` (has date inputs — use InputSpacer + DrawOverlays)
3. `LocatorTab.cs`
4. `SessionTab.cs`
5. `WaitlistTab.cs` (has text inputs)
6. `StaffTab.cs`
7. `CosmeticDrawer.cs` — color pickers need ImGui overlays; complex

**For each tab:**
- Step 1: Implement `BuildNode()` returning the node tree
- Step 2: Move all ImGui input widgets to `DrawOverlays()` using `InputSpacer` positioning
- Step 3: Wire `BeforeDraw` lambdas for live data (booking list, locator results, etc.)
- Step 4: Build + fix compile errors
- Step 5: Commit `feat(phase4): migrate [TabName] to Una.Drawing`

---

## Phase 5: SRT Panels (IToolboxPanel implementations)

Same pattern as tabs, but each panel has both `BuildNode()` (content) and `BuildSettingsNode()`.

**Panels to migrate (one commit each):**
1. `SweetheartPanel.cs`
2. `CandyHeartPanel.cs`
3. `BartenderPanel.cs`
4. `GambaPanel.cs` — has number inputs, dropdown for game type
5. `DJPanel.cs` — setlist textarea needs InputSpacer
6. `ManagementPanel.cs` — most complex; room board grid
7. `GreeterPanel.cs`
8. `OwnerPanel.cs` — venue registration card, staff roster

**For each panel:**
- Step 1: Implement `BuildNode()` with tab bar via `CandyUI.TabContainer()`
- Step 2: Implement `BuildSettingsNode()`
- Step 3: Move inputs to `DrawOverlays()` / `DrawSettingsOverlays()`
- Step 4: Build + fix
- Step 5: Commit `feat(phase5): migrate [PanelName] to Una.Drawing`

---

## Phase 6: Settings Panel

**Files:**
- Modify: `CandyCoat/Windows/MainWindow.cs` (DrawGeneralSettings extracted)
- Create: `CandyCoat/UI/SettingsPanel.cs`

Settings currently in `MainWindow.DrawGeneralSettings()`. Extract to its own `SettingsPanel` class implementing the same `BuildNode()` / `DrawOverlays()` pattern, returned when user clicks "Settings" in sidebar footer.

**Commit:**
```bash
git commit -m "feat(phase6): migrate Settings panel to Una.Drawing"
```

---

## Phase 7: Secondary Windows

Migrate each floating window. These are simpler — smaller node trees, fewer inputs.

**Windows to migrate:**
1. `ProfileWindow.cs` — status badge, labels, copy button
2. `TellWindow.cs` — text input needs InputSpacer
3. `PatronDetailsWindow.cs` — glamourer integration buttons
4. `SessionWindow.cs` — start/stop timer display
5. `CosmeticWindow.cs` — color pickers (heavy ImGui overlay usage)
6. `PatronAlertOverlay.cs` — stacked alert cards; each card is a `Node` with auto-dismiss timer updating `Style.Opacity`

**Pattern for floating windows:**
```csharp
public override void Draw()
{
    if (_root == null) _root = BuildRoot();
    _root.Style.Size = new((int)ImGui.GetContentRegionAvail().X,
                            (int)ImGui.GetContentRegionAvail().Y);
    _root.Render(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos());
    DrawOverlays();
}
```

**One commit per window:**
`feat(phase7): migrate [WindowName] to Una.Drawing`

---

## Phase 8: Setup Wizard

**Files:**
- Modify: `CandyCoat/Windows/SetupWindow.cs`
- Modify: each `SetupStep*.cs`

The wizard is a sequence of steps each with a center-aligned card. Steps share `WizardState`. The outer `SetupWindow` owns the root node; steps return a `Node` for the card content area.

**Step interface update:**
```csharp
// Each step exposes:
Node BuildStepNode(WizardState state);
void DrawOverlays(WizardState state);
```

**SetupWindow root structure:**
```
WindowRoot (vertical, centered)
  Card "wizard-card" (600px wide, auto height)
    [step.BuildStepNode(state)]
    Separator
    Row "nav-row"
      GhostButton "Back" (visible if step > 0)
      Spacer (grow)
      Button "Next" / "Finish"
```

**Steps to migrate (one commit each):**
1. `SetupStep0_Welcome.cs`
2. `SetupStep1_CharacterProfile.cs` — has text inputs
3. `SetupStep2_ModeSelection.cs`
4. `SetupStep3_RoleSelection.cs` — password input
5. `SetupStep4_VenueKey.cs` — register/validate flows, async state
6. `SetupStep4_Finish.cs`
7. `SetupStepCheckSync.cs`

**Commit per step:**
`feat(phase8): migrate SetupStep[N] to Una.Drawing`

---

## Phase 9: Cleanup + Release

### Task 9.1: Remove .temp directory reference code

Delete `.temp/` contents (git-ignored) and any leftover TODO comments.

### Task 9.2: Version bump to v0.17.0

**Files:**
- Modify: `CandyCoat/CandyCoat.csproj` — bump version
- Modify: `CHANGELOG.md` — add v0.17.0 entry
- Modify: `README.md` — update version badge

**Commit:**
```bash
git add -A
git commit -m "feat(v0.17.0): Una.Drawing UI refactor complete"
git tag v0.17.0
git push origin candy-coat-testing
git push origin v0.17.0
```

### Task 9.3: Open PR candy-coat-testing -> master

After QA on the testing branch:
```bash
# Merge to master
git checkout master
git merge candy-coat-testing
git push origin master
```

---

## Edge Cases & Watch-outs

| Issue | Mitigation |
|-------|-----------|
| Umbra `/una-drawing` command conflict | `DrawingLib.Setup()` returns bool — if false (command taken by Umbra), log warning and continue. It's a debug command only. |
| Async glyph download | `DrawingLib.Setup()` is async internally. Render may show missing glyphs for <1s on first load. Acceptable. |
| `Node.Render()` on non-root | Only nodes with `ParentNode == null` can call `Render()`. All factories return detached nodes; caller attaches via `ChildNodes`. Never call `Render()` on a node that has been added as a child. |
| Root node sizing | Una.Drawing does not auto-resize to ImGui window. Must set `_rootNode.Style.Size` every frame before `Render()`. |
| Text input on game thread | `DrawOverlays()` runs inside `Draw()` which is on the game/framework thread. Safe for ImGui calls. |
| Thread safety for `BeforeDraw` lambdas | `BeforeDraw` fires during `Render()` — game thread. Read from `Configuration` or local caches; never await inside. |
| Disposal | Each window/panel that owns a node tree should call child disposal if Una.Drawing exposes it. Otherwise, just null the reference — SkiaSharp textures are GC-collected. |
| OtterGui removal | If OtterGui is deeply used for `OtterGui.Raii` or table helpers, audit each usage. Most can be replaced with `ImRaii` (already in Dalamud ImGui bindings) or Una.Drawing layout nodes. |

---

## Migration Checklist

- [ ] Phase 0: Submodule + csproj + DrawingLib init + StyleManager/OtterGui removal
- [ ] Phase 1: CandyTheme named colors
- [ ] Phase 2: CandyUI factory
- [ ] Phase 3: ITab/IToolboxPanel interfaces + MainWindow
- [ ] Phase 4: All 7 dashboard tabs
- [ ] Phase 5: All 8 SRT panels
- [ ] Phase 6: Settings panel
- [ ] Phase 7: All 6 secondary windows
- [ ] Phase 8: Setup wizard (7 steps)
- [ ] Phase 9: Cleanup + v0.17.0 release + PR
