using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using CandyCoat.UI;
using Una.Drawing;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStep4_Finish
{
    private bool   _launchClicked;
    private Node?  _glamBtnNode;
    private Node?  _chatBtnNode;
    private Plugin?       _pendingPlugin;
    private SetupWindow?  _pendingWindow;
    private WizardState?  _pendingState;

    // ─── Una.Drawing node ────────────────────────────────────────────────────

    public Node BuildStepNode(WizardState state)
    {
        _launchClicked = false;
        _pendingState  = state;

        // ─── Summary card (static snapshot of wizard state) ─────────────────
        var summaryCard = CandyUI.Card("step4-summary-card",
            CandyUI.Row("step4-char-row", 6,
                CandyUI.Muted("step4-char-lbl", "Character:"),
                CandyUI.Label("step4-char-val", $"{state.FirstName} {state.LastName} @ {state.HomeWorld}")),
            CandyUI.Row("step4-pid-row", 6,
                CandyUI.Muted("step4-pid-lbl", "Profile ID:"),
                CandyUI.Label("step4-pid-val", state.ProfileId),
                CandyUI.SmallButton("step4-copy-btn", "Copy",
                    () => ImGui.SetClipboardText(state.ProfileId))),
            CandyUI.Row("step4-mode-row", 6,
                CandyUI.Muted("step4-mode-lbl", "Mode:"),
                CandyUI.Label("step4-mode-val", state.UserMode)),
            CandyUI.Row("step4-role-row", 6,
                CandyUI.Muted("step4-role-lbl", "Primary Role:"),
                CandyUI.Label("step4-role-val", state.SelectedPrimaryRole.ToString()))
        );

        // ─── Launch button ───────────────────────────────────────────────────
        var launchBtn = CandyUI.Button("step4-launch-btn", "Launch Candy Coat",
            () => _launchClicked = true);
        launchBtn.Style.Size     = new Size(0, 40);
        launchBtn.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit);

        // ─── Integration toggles ─────────────────────────────────────────────
        _glamBtnNode = MakeToggleButton("step4-glam-btn",
            state.HasGlamourerIntegrated,
            () =>
            {
                state.HasGlamourerIntegrated = !state.HasGlamourerIntegrated;
                ApplyToggleStyle(_glamBtnNode!, state.HasGlamourerIntegrated);
            });

        _chatBtnNode = MakeToggleButton("step4-chat-btn",
            state.HasChatTwoIntegrated,
            () =>
            {
                state.HasChatTwoIntegrated = !state.HasChatTwoIntegrated;
                ApplyToggleStyle(_chatBtnNode!, state.HasChatTwoIntegrated);
            });

        var integrationsCard = CandyUI.Card("step4-integrations-card",
            CandyUI.SectionHeader("step4-int-header", "Integrations"),
            CandyUI.Row("step4-glam-row", 8,
                CandyUI.Label("step4-glam-lbl", "Glamourer"),
                _glamBtnNode),
            CandyUI.Row("step4-chat-row", 8,
                CandyUI.Label("step4-chat-lbl", "ChatTwo"),
                _chatBtnNode)
        );

        return CandyUI.Column("step4-content", 8,
            CandyUI.Muted("step4-subtitle", "Final Step — Summary"),
            new Node
            {
                Id        = "step4-desc",
                NodeValue = "Review your setup before launching Candy Coat.",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                    Color     = new Color(CandyTheme.TextPrimary),
                    FontSize  = 13,
                    TextAlign = Anchor.MiddleLeft,
                },
            },
            summaryCard,
            launchBtn,
            integrationsCard
        );
    }

    // ─── Launch handler (called from DrawOverlays each frame) ────────────────

    public void DrawOverlays(WizardState state, ref int step, Plugin plugin, SetupWindow window)
    {
        _pendingPlugin = plugin;
        _pendingWindow = window;

        if (_launchClicked)
        {
            _launchClicked = false;
            ExecuteLaunch(state, plugin, window);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Node MakeToggleButton(string id, bool enabled, System.Action onClick)
    {
        var btn = CandyUI.GhostButton(id, enabled ? "Enabled" : "Disabled", onClick);
        ApplyToggleStyle(btn, enabled);
        return btn;
    }

    private static void ApplyToggleStyle(Node btn, bool enabled)
    {
        btn.NodeValue = enabled ? "Enabled" : "Disabled";
        btn.Style.BackgroundColor = enabled
            ? new Color(0xFF40703A)   // dark green tint
            : new Color(CandyTheme.BtnGhost);
        btn.Style.Color = enabled
            ? new Color(CandyTheme.TextSuccess)
            : new Color(CandyTheme.TextSecondary);
    }

    private static void ExecuteLaunch(WizardState state, Plugin plugin, SetupWindow window)
    {
        var cfg = plugin.Configuration;
        cfg.CharacterName    = $"{state.FirstName} {state.LastName}";
        cfg.HomeWorld        = state.HomeWorld;
        cfg.ProfileId        = state.ProfileId;
        cfg.UserMode         = state.UserMode;
        cfg.PrimaryRole      = state.SelectedPrimaryRole;
        cfg.MultiRoleEnabled = state.MultiRoleToggle;
        cfg.EnabledRoles     = state.MultiRoleToggle
            ? (state.SelectedSecondaryRoles | state.SelectedPrimaryRole)
            : state.SelectedPrimaryRole;
        cfg.EnableGlamourer  = state.HasGlamourerIntegrated;
        cfg.EnableChatTwo    = state.HasChatTwoIntegrated;

        cfg.EnableSync = true;
        cfg.ApiUrl     = PluginConstants.ProductionApiUrl;
        cfg.VenueId    = state.VenueId != System.Guid.Empty ? state.VenueId.ToString() : cfg.VenueId;
        cfg.VenueKey   = !string.IsNullOrEmpty(state.VenueKey)  ? state.VenueKey  : cfg.VenueKey;
        cfg.VenueName  = !string.IsNullOrEmpty(state.VenueName) ? state.VenueName : cfg.VenueName;

        cfg.IsSetupComplete = true;
        cfg.Save();

        plugin.SyncService.UpdateVenueKey(cfg.VenueKey);

        if (state.VenueConfirmed && !string.IsNullOrEmpty(state.ProfileId))
        {
            plugin.SyncService.UpsertProfileAsync(
                state.ProfileId,
                cfg.CharacterName,
                cfg.HomeWorld,
                state.UserMode,
                cfg.VenueId,
                state.HasGlamourerIntegrated,
                state.HasChatTwoIntegrated);
        }

        window.IsOpen = false;
        plugin.OnSetupComplete();
    }
}
