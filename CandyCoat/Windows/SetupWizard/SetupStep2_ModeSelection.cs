using CandyCoat.UI;
using Una.Drawing;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep2_ModeSelection
{
    private bool _staffSelected;

    // ─── Una.Drawing node ────────────────────────────────────────────────────

    public Node BuildStepNode(WizardState state)
    {
        _staffSelected = false;

        // Staff card — clickable, hover highlight with ice-blue border
        var staffCard = new Node
        {
            Id         = "step2-staff-card",
            Stylesheet = new Stylesheet([
                new Stylesheet.StyleDefinition(
                    "#step2-staff-card:hover",
                    new Style
                    {
                        BackgroundColor = new Color(CandyTheme.BgCardHover),
                        BorderColor     = new BorderColor(new Color(CandyTheme.BorderFocus)),
                    }
                ),
            ]),
            Style = new Style
            {
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                BackgroundColor = new Color(CandyTheme.BgCard),
                BorderColor     = new BorderColor(new Color(CandyTheme.BorderCard)),
                BorderWidth     = new EdgeSize(1),
                BorderRadius    = 6,
                Padding         = new EdgeSize(12),
                Flow            = Flow.Vertical,
                Gap             = 6,
            },
        };
        staffCard.AppendChild(new Node
        {
            Id        = "step2-staff-title",
            NodeValue = "Staff",
            Style     = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextAccent),
                FontSize  = 14,
                TextAlign = Anchor.MiddleCenter,
            },
        });
        staffCard.AppendChild(new Node
        {
            Id        = "step2-staff-desc",
            NodeValue = "Venue staff member — access role toolboxes, shifts, and patron tools.",
            Style     = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextSecondary),
                FontSize  = 12,
                TextAlign = Anchor.MiddleLeft,
            },
        });
        staffCard.OnClick += _ => _staffSelected = true;

        // Patron card — visually disabled (muted colours, no hover/click)
        var patronCard = new Node
        {
            Id    = "step2-patron-card",
            Style = new Style
            {
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                BackgroundColor = new Color(CandyTheme.BgCard),
                BorderColor     = new BorderColor(new Color(CandyTheme.BorderDivider)),
                BorderWidth     = new EdgeSize(1),
                BorderRadius    = 6,
                Padding         = new EdgeSize(12),
                Flow            = Flow.Vertical,
                Gap             = 6,
            },
        };
        patronCard.AppendChild(new Node
        {
            Id        = "step2-patron-title",
            NodeValue = "Patron",
            Style     = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextMuted),
                FontSize  = 14,
                TextAlign = Anchor.MiddleCenter,
            },
        });
        patronCard.AppendChild(new Node
        {
            Id        = "step2-patron-desc",
            NodeValue = "Venue guest access — coming in a future update.",
            Style     = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextMuted),
                FontSize  = 12,
                TextAlign = Anchor.MiddleLeft,
            },
        });
        patronCard.AppendChild(new Node
        {
            Id        = "step2-patron-soon",
            NodeValue = "Coming Soon",
            Style     = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextWarning),
                FontSize  = 11,
                TextAlign = Anchor.MiddleLeft,
            },
        });

        return CandyUI.Column("step2-content", 8,
            CandyUI.Muted("step2-subtitle", "Step 3 of 5 — Choose Your Mode"),
            new Node
            {
                Id        = "step2-desc",
                NodeValue = "How will you be using Candy Coat?",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextPrimary),
                    FontSize  = 13,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            CandyUI.Row("step2-cards-row", 12, staffCard, patronCard)
        );
    }

    // ─── Step navigation (ref int cannot be captured in closure) ─────────────

    public void DrawOverlays(WizardState state, ref int step)
    {
        if (_staffSelected)
        {
            state.UserMode = "Staff";
            step = 4;
            _staffSelected = false;
        }
    }
}
