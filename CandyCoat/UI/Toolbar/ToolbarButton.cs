using System;
using System.Collections.Generic;
using Una.Drawing;

namespace CandyCoat.UI.Toolbar;

/// <summary>
/// A self-contained Una.Drawing node for one toolbar button.
/// Contains an icon node (FontAwesome glyph) and a label node.
/// The glow ring effect is applied directly to the icon node via StrokeColor/StrokeWidth
/// animation, avoiding a separate sibling node that would consume horizontal space.
/// </summary>
public sealed class ToolbarButton : IDisposable
{
    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>The root Una.Drawing node — append this to a parent node.</summary>
    public Node Root { get; }

    /// <summary>
    /// When true the glow ring animates to full opacity on the icon node.
    /// When false it fades out.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive   = value;
            _glowTarget = value ? MaxGlowAlpha : 0f;
        }
    }

    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const float GlowLerpSpeed = 6f;   // units per second
    private const float MaxGlowAlpha  = 200f;  // 0–255 range

    // -------------------------------------------------------------------------
    // Private fields
    // -------------------------------------------------------------------------

    private readonly Node _iconNode;
    private readonly Node _labelNode;

    private bool  _isActive;
    private float _glowAlpha  = 0f;
    private float _glowTarget = 0f;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public ToolbarButton(string id, string icon, string label, Action onClick)
    {
        // Icon node — FontAwesome glyph centred in a 36×36 box.
        // StrokeColor/StrokeWidth are animated here to produce the glow ring
        // effect without needing a separate sibling node in the horizontal flow.
        _iconNode = new Node
        {
            Id        = $"{id}-icon",
            NodeValue = icon,
            Style     = new Style
            {
                Size        = new Size(36, 36),
                FontSize    = 18,
                TextAlign   = Anchor.MiddleCenter,
                Color       = new Color("Toolbar.Icon"),
                Anchor      = Anchor.MiddleCenter,
                StrokeColor = new Color("Toolbar.Glow"),
                StrokeWidth = 2f,
                Opacity     = 1f,
            },
        };

        // Label node — hidden when collapsed
        _labelNode = new Node
        {
            Id        = $"{id}-label",
            NodeValue = label,
            Style     = new Style
            {
                FontSize     = 11,
                Color        = new Color("Toolbar.Label"),
                TextAlign    = Anchor.MiddleCenter,
                TextOverflow = false,
                IsVisible    = false,
                Margin       = new EdgeSize(0, 0, 0, 6),
            },
        };

        // Button wrapper — stacks icon and label horizontally
        var buttonId = $"{id}-btn";
        Root = new Node
        {
            Id    = buttonId,
            Style = new Style
            {
                Flow            = Flow.Horizontal,
                Anchor          = Anchor.MiddleLeft,
                Padding         = new EdgeSize(4),
                Gap             = 0f,
                BackgroundColor = new Color(0x00000000), // fully transparent default
            },
            Stylesheet = new Stylesheet(new List<Stylesheet.StyleDefinition>
            {
                new(
                    $"#{buttonId}:hover",
                    new Style
                    {
                        BackgroundColor = new Color("Toolbar.ButtonHover"),
                        BorderRadius    = 6f,
                    }
                ),
                new(
                    $"#{id}-icon:hover",
                    new Style
                    {
                        Color = new Color("Toolbar.IconActive"),
                    }
                ),
            }),
        };

        // Build tree: icon then label
        Root.AppendChild(_iconNode);
        Root.AppendChild(_labelNode);

        // Wire click to the provided action
        Root.OnClick += _ => onClick();

        // Make child nodes inherit hover tags from the button wrapper so
        // :hover pseudo-class selectors resolve when the wrapper is hovered
        _iconNode.InheritTags  = true;
        _labelNode.InheritTags = true;

        // Initialise glow to invisible (opacity on the stroke is not a direct
        // property, so we drive it by setting StrokeWidth to 0 when alpha is 0)
        _iconNode.Style.StrokeWidth = 0f;
    }

    // -------------------------------------------------------------------------
    // Per-frame update
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call once per frame to animate the glow alpha.
    /// <paramref name="deltaTime"/> should be the frame delta in seconds.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (Root.IsDisposed) return;

        float prev = _glowAlpha;
        _glowAlpha = Lerp(_glowAlpha, _glowTarget, Math.Clamp(GlowLerpSpeed * deltaTime, 0f, 1f));

        // Only update the style when the value meaningfully changed
        if (Math.Abs(_glowAlpha - prev) < 0.5f) return;

        // Mutate in-place — no new Style allocation.
        // Drive the glow ring by scaling StrokeWidth with the animated alpha
        // (0 = no stroke, 2 = full glow ring). This avoids a separate sibling node.
        _iconNode.Style.StrokeWidth = 2f * (_glowAlpha / MaxGlowAlpha);
    }

    // -------------------------------------------------------------------------
    // Expand / collapse
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shows or hides the text label (expanded vs collapsed toolbar state).
    /// </summary>
    public void SetExpanded(bool expanded)
    {
        if (Root.IsDisposed) return;
        // Mutate in-place — no new Style allocation
        _labelNode.Style.IsVisible = expanded;
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        if (!Root.IsDisposed)
            Root.Dispose();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
