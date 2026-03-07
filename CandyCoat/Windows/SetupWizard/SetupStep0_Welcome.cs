using CandyCoat.UI;
using Una.Drawing;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep0_Welcome
{
    private bool _getStartedClicked;

    // ─── Una.Drawing node ────────────────────────────────────────────────────

    public Node BuildStepNode(WizardState state)
    {
        _getStartedClicked = false;

        var btn = CandyUI.Button("step0-btn", "Get Started", () => _getStartedClicked = true);
        btn.Style.Size     = new Size(0, 40);
        btn.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit);

        return CandyUI.Column("step0-content", 8,
            new Node
            {
                Id        = "step0-title",
                NodeValue = "Welcome to Candy Coat",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextAccent),
                    FontSize  = 16,
                    TextAlign = Anchor.MiddleCenter,
                },
            },
            new Node
            {
                Id        = "step0-desc1",
                NodeValue = "Candy Coat is your in-game venue assistant for Sugar Venue.",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextPrimary),
                    FontSize  = 13,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            new Node
            {
                Id        = "step0-desc2",
                NodeValue = "This short setup will create a Character Profile for Venue-Sync — a unique ID that identifies you across all worlds and instances.",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextSecondary),
                    FontSize  = 12,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            new Node
            {
                Id        = "step0-desc3",
                NodeValue = "Setup takes less than a minute.",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextMuted),
                    FontSize  = 12,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            btn
        );
    }

    // ─── Step navigation (ref int cannot be captured in closure) ─────────────

    public void DrawOverlays(WizardState state, ref int step)
    {
        if (_getStartedClicked)
        {
            step = 1;
            _getStartedClicked = false;
        }
    }
}
