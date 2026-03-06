using System;
using System.Collections.Generic;
using Una.Drawing;

namespace CandyCoat.UI;

/// <summary>
/// Static factory for creating Una.Drawing nodes with Candy Coat theming.
/// All layout and visual properties use the CandyTheme named color system.
/// </summary>
internal static class CandyUI
{
    // -------------------------------------------------------------------------
    // Layout containers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Root window node — horizontal flow, fills all available space.
    /// </summary>
    public static Node WindowRoot(params Node[] children)
    {
        var node = new Node {
            Style = new Style {
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
                Flow            = Flow.Horizontal,
                BackgroundColor = new Color(CandyTheme.BgWindow),
            },
        };
        foreach (var child in children) node.AppendChild(child);
        return node;
    }

    /// <summary>
    /// Sidebar panel — fixed 200px wide, vertical flow, dark background.
    /// </summary>
    public static Node Sidebar(params Node[] children)
    {
        var node = new Node {
            Style = new Style {
                Size            = new Size(200, 0),
                AutoSize        = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Grow),
                Flow            = Flow.Vertical,
                BackgroundColor = new Color(CandyTheme.BgSidebar),
                Padding         = new EdgeSize(8),
                Gap             = 4,
            },
        };
        foreach (var child in children) node.AppendChild(child);
        return node;
    }

    /// <summary>
    /// Main content panel — grows to fill remaining space, vertical flow.
    /// </summary>
    public static Node ContentPanel(params Node[] children)
    {
        var node = new Node {
            Style = new Style {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
                Flow     = Flow.Vertical,
                Padding  = new EdgeSize(12),
                Gap      = 8,
            },
        };
        foreach (var child in children) node.AppendChild(child);
        return node;
    }

    // -------------------------------------------------------------------------
    // Cards
    // -------------------------------------------------------------------------

    /// <summary>
    /// A card container — rounded, bordered, themed background.
    /// </summary>
    public static Node Card(string id, params Node[] children)
    {
        var node = new Node {
            Id    = id,
            Style = new Style {
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Flow            = Flow.Vertical,
                Padding         = new EdgeSize(10, 12, 10, 12),
                Gap             = 6,
                BackgroundColor = new Color(CandyTheme.BgCard),
                BorderColor     = new BorderColor(new Color(CandyTheme.BorderCard)),
                BorderWidth     = new EdgeSize(1),
                BorderRadius    = 6,
            },
        };
        foreach (var child in children) node.AppendChild(child);
        return node;
    }

    // -------------------------------------------------------------------------
    // Buttons
    // -------------------------------------------------------------------------

    /// <summary>
    /// Primary filled button with hover styling.
    /// </summary>
    public static Node Button(string id, string label, Action onClick)
    {
        var node = new Node {
            Id        = id,
            NodeValue = label,
            Stylesheet = new Stylesheet([
                new Stylesheet.StyleDefinition(
                    $"#{id}:hover",
                    new Style { BackgroundColor = new Color(CandyTheme.BtnHover) }
                ),
            ]),
            Style = new Style {
                Size            = new Size(0, 28),
                AutoSize        = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                Padding         = new EdgeSize(0, 12, 0, 12),
                BackgroundColor = new Color(CandyTheme.BtnPrimary),
                BorderRadius    = 4,
                Color           = new Color(CandyTheme.TextPrimary),
                FontSize        = 13,
                TextAlign       = Anchor.MiddleCenter,
            },
        };
        node.OnClick += _ => onClick();
        return node;
    }

    /// <summary>
    /// Ghost button — subtle border, secondary text, hover fills bg.
    /// </summary>
    public static Node GhostButton(string id, string label, Action onClick)
    {
        var node = new Node {
            Id        = id,
            NodeValue = label,
            Stylesheet = new Stylesheet([
                new Stylesheet.StyleDefinition(
                    $"#{id}:hover",
                    new Style {
                        BackgroundColor = new Color(CandyTheme.BtnGhostHover),
                        Color           = new Color(CandyTheme.TextPrimary),
                    }
                ),
            ]),
            Style = new Style {
                Size            = new Size(0, 26),
                AutoSize        = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                Padding         = new EdgeSize(0, 10, 0, 10),
                BackgroundColor = new Color(CandyTheme.BtnGhost),
                BorderRadius    = 4,
                BorderColor     = new BorderColor(new Color(CandyTheme.BorderCard)),
                BorderWidth     = new EdgeSize(1),
                Color           = new Color(CandyTheme.TextSecondary),
                FontSize        = 12,
                TextAlign       = Anchor.MiddleCenter,
            },
        };
        node.OnClick += _ => onClick();
        return node;
    }

    /// <summary>
    /// Small compact button — muted text, minimal padding.
    /// </summary>
    public static Node SmallButton(string id, string label, Action onClick)
    {
        var node = new Node {
            Id        = id,
            NodeValue = label,
            Stylesheet = new Stylesheet([
                new Stylesheet.StyleDefinition(
                    $"#{id}:hover",
                    new Style { Color = new Color(CandyTheme.TextPrimary) }
                ),
            ]),
            Style = new Style {
                Size            = new Size(0, 22),
                AutoSize        = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                Padding         = new EdgeSize(0, 8, 0, 8),
                BackgroundColor = new Color(CandyTheme.BtnGhost),
                BorderRadius    = 3,
                BorderColor     = new BorderColor(new Color(CandyTheme.BorderDivider)),
                BorderWidth     = new EdgeSize(1),
                Color           = new Color(CandyTheme.TextMuted),
                FontSize        = 11,
                TextAlign       = Anchor.MiddleCenter,
            },
        };
        node.OnClick += _ => onClick();
        return node;
    }

    // -------------------------------------------------------------------------
    // Text
    // -------------------------------------------------------------------------

    /// <summary>
    /// Standard label — primary text color.
    /// </summary>
    public static Node Label(string id, string text, int fontSize = 13)
    {
        return new Node {
            Id        = id,
            NodeValue = text,
            Style     = new Style {
                AutoSize  = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextPrimary),
                FontSize  = fontSize,
                TextAlign = Anchor.MiddleLeft,
            },
        };
    }

    /// <summary>
    /// Section header — accent color, slightly larger font, bottom padding.
    /// </summary>
    public static Node SectionHeader(string id, string text)
    {
        return new Node {
            Id        = id,
            NodeValue = text,
            Style     = new Style {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextAccent),
                FontSize  = 14,
                TextAlign = Anchor.MiddleLeft,
                Padding   = new EdgeSize(0, 0, 4, 0),
            },
        };
    }

    /// <summary>
    /// Muted/secondary text — smaller, dimmer color.
    /// </summary>
    public static Node Muted(string id, string text, int fontSize = 11)
    {
        return new Node {
            Id        = id,
            NodeValue = text,
            Style     = new Style {
                AutoSize  = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextMuted),
                FontSize  = fontSize,
                TextAlign = Anchor.MiddleLeft,
            },
        };
    }

    // -------------------------------------------------------------------------
    // Structural
    // -------------------------------------------------------------------------

    /// <summary>
    /// A thin horizontal divider line.
    /// </summary>
    public static Node Separator(string id)
    {
        return new Node {
            Id    = id,
            Style = new Style {
                Size            = new Size(0, 1),
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Margin          = new EdgeSize(4, 0, 4, 0),
                BackgroundColor = new Color(CandyTheme.BorderDivider),
            },
        };
    }

    /// <summary>
    /// Horizontal row — children laid out left to right with a gap.
    /// </summary>
    public static Node Row(string id, float gap = 8, params Node[] children)
    {
        var node = new Node {
            Id    = id,
            Style = new Style {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Flow     = Flow.Horizontal,
                Gap      = gap,
            },
        };
        foreach (var child in children) node.AppendChild(child);
        return node;
    }

    /// <summary>
    /// Vertical column — children stacked top to bottom with a gap.
    /// </summary>
    public static Node Column(string id, float gap = 6, params Node[] children)
    {
        var node = new Node {
            Id    = id,
            Style = new Style {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Flow     = Flow.Vertical,
                Gap      = gap,
            },
        };
        foreach (var child in children) node.AppendChild(child);
        return node;
    }

    /// <summary>
    /// Scroll box — fixed height, vertical flow, content can overflow and scroll.
    /// </summary>
    public static Node ScrollBox(string id, float height, params Node[] children)
    {
        var node = new Node {
            Id    = id,
            Style = new Style {
                Size     = new Size(0, height),
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Flow     = Flow.Vertical,
                Gap      = 4,
            },
        };
        foreach (var child in children) node.AppendChild(child);
        return node;
    }

    // -------------------------------------------------------------------------
    // Sidebar items
    // -------------------------------------------------------------------------

    /// <summary>
    /// A sidebar navigation item — active state highlighted, inactive shows hover.
    /// </summary>
    public static Node SidebarItem(string id, string label, bool active, Action onClick)
    {
        Style baseStyle = active
            ? new Style {
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Padding         = new EdgeSize(5, 10, 5, 10),
                BorderRadius    = 4,
                BackgroundColor = new Color(CandyTheme.BgTabActive),
                Color           = new Color(CandyTheme.TextPrimary),
                FontSize        = 13,
                TextAlign       = Anchor.MiddleLeft,
            }
            : new Style {
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Padding         = new EdgeSize(5, 10, 5, 10),
                BorderRadius    = 4,
                BackgroundColor = new Color(0x00000000), // transparent
                Color           = new Color(CandyTheme.TextSecondary),
                FontSize        = 13,
                TextAlign       = Anchor.MiddleLeft,
            };

        var node = new Node {
            Id        = id,
            NodeValue = label,
            Style     = baseStyle,
        };

        if (!active) {
            node.Stylesheet = new Stylesheet([
                new Stylesheet.StyleDefinition(
                    $"#{id}:hover",
                    new Style {
                        BackgroundColor = new Color(CandyTheme.BgCard),
                        Color           = new Color(CandyTheme.TextPrimary),
                    }
                ),
            ]);
            node.OnClick += _ => onClick();
        }

        return node;
    }

    /// <summary>
    /// A collapsible sidebar drawer with a toggle header and an expandable body.
    /// Returns the wrapper column node.
    /// </summary>
    public static Node SidebarDrawer(
        string   id,
        string   label,
        bool     expanded,
        Action   onToggle,
        params Node[] children
    ) {
        var arrow  = expanded ? "v" : ">";
        var header = new Node {
            Id        = $"{id}-header",
            NodeValue = $"{arrow}  {label}",
            Style     = new Style {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Padding   = new EdgeSize(5, 4, 5, 4),
                Color     = new Color(CandyTheme.TextAccent),
                FontSize  = 13,
                TextAlign = Anchor.MiddleLeft,
            },
        };
        header.OnClick += _ => onToggle();

        var body = new Node {
            Id    = $"{id}-body",
            Style = new Style {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Flow      = Flow.Vertical,
                Gap       = 2,
                IsVisible = expanded,
                Padding   = new EdgeSize(0, 0, 0, 8),
            },
        };
        foreach (var child in children) body.AppendChild(child);

        var wrapper = new Node {
            Id    = id,
            Style = new Style {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Flow     = Flow.Vertical,
                Gap      = 0,
            },
        };
        wrapper.AppendChild(header);
        wrapper.AppendChild(body);
        return wrapper;
    }

    // -------------------------------------------------------------------------
    // Tab container
    // -------------------------------------------------------------------------

    /// <summary>
    /// A tab bar + content container. Tab bar shows all tab labels; active tab
    /// is highlighted. Content node is rendered below the tab bar.
    /// </summary>
    public static Node TabContainer(
        string   id,
        string[] tabs,
        int      activeTab,
        Action<int> onSelect,
        Node     activeContent
    ) {
        var tabBar = new Node {
            Id    = $"{id}-tabbar",
            Style = new Style {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Flow     = Flow.Horizontal,
                Gap      = 2,
            },
        };

        for (int i = 0; i < tabs.Length; i++) {
            int   tabIndex = i;
            bool  isActive = i == activeTab;
            string tabId   = $"{id}-tab-{i}";

            var tabNode = new Node {
                Id        = tabId,
                NodeValue = tabs[i],
                Style     = isActive
                    ? new Style {
                        AutoSize        = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                        Padding         = new EdgeSize(5, 12, 5, 12),
                        BackgroundColor = new Color(CandyTheme.BgTabActive),
                        Color           = new Color(CandyTheme.TextPrimary),
                        FontSize        = 13,
                        TextAlign       = Anchor.MiddleCenter,
                        BorderRadius    = 4,
                    }
                    : new Style {
                        AutoSize        = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                        Padding         = new EdgeSize(5, 12, 5, 12),
                        BackgroundColor = new Color(CandyTheme.BgTabInactive),
                        Color           = new Color(CandyTheme.TextSecondary),
                        FontSize        = 13,
                        TextAlign       = Anchor.MiddleCenter,
                        BorderRadius    = 4,
                    },
            };

            if (!isActive) {
                tabNode.Stylesheet = new Stylesheet([
                    new Stylesheet.StyleDefinition(
                        $"#{tabId}:hover",
                        new Style {
                            BackgroundColor = new Color(CandyTheme.BgCard),
                            Color           = new Color(CandyTheme.TextPrimary),
                        }
                    ),
                ]);
                tabNode.OnClick += _ => onSelect(tabIndex);
            }

            tabBar.AppendChild(tabNode);
        }

        var wrapper = new Node {
            Id    = id,
            Style = new Style {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
                Flow     = Flow.Vertical,
                Gap      = 6,
            },
        };
        wrapper.AppendChild(tabBar);
        wrapper.AppendChild(activeContent);
        return wrapper;
    }

    // -------------------------------------------------------------------------
    // Status badge
    // -------------------------------------------------------------------------

    /// <summary>
    /// A small colored dot + label. colorName must be a registered CandyTheme color name.
    /// </summary>
    public static Node StatusBadge(string id, string label, string colorName)
    {
        var dot = new Node {
            Id    = $"{id}-dot",
            Style = new Style {
                Size            = new Size(8, 8),
                BackgroundColor = new Color(colorName),
                BorderRadius    = 4,
            },
        };

        var text = new Node {
            Id        = $"{id}-label",
            NodeValue = label,
            Style     = new Style {
                AutoSize  = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                Color     = new Color(colorName),
                FontSize  = 12,
                TextAlign = Anchor.MiddleLeft,
            },
        };

        var row = new Node {
            Id    = id,
            Style = new Style {
                AutoSize = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                Flow     = Flow.Horizontal,
                Gap      = 5,
            },
        };
        row.AppendChild(dot);
        row.AppendChild(text);
        return row;
    }

    // -------------------------------------------------------------------------
    // Input spacer
    // -------------------------------------------------------------------------

    /// <summary>
    /// An invisible spacer node that reserves layout space for an ImGui overlay widget.
    /// </summary>
    public static Node InputSpacer(string id, int width, int height = 28)
    {
        return new Node {
            Id    = id,
            Style = new Style {
                Size = new Size(width, height),
            },
        };
    }
}
