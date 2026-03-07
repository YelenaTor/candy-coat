using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using CandyCoat.UI;
using Una.Drawing;

namespace CandyCoat.Windows;

public class ProfileWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private Node? _root;

    public ProfileWindow(Plugin plugin) : base("My Profile##CandyCoatProfile")
    {
        _plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 220),
            MaximumSize = new Vector2(320, 220)
        };

        Flags |= ImGuiWindowFlags.NoCollapse;
        Flags |= ImGuiWindowFlags.NoResize;
    }

    public void Dispose()
    {
        _root?.Dispose();
        _root = null;
    }

    private void BuildRoot()
    {
        _root?.Dispose();
        var cfg = _plugin.Configuration;

        var charValue    = string.IsNullOrEmpty(cfg.CharacterName) ? "—" : cfg.CharacterName;
        var profileValue = string.IsNullOrEmpty(cfg.ProfileId)     ? "—" : cfg.ProfileId;
        var venueValue   = string.IsNullOrEmpty(cfg.VenueName)     ? "—" : cfg.VenueName;

        // Profile ID row: label + pink value + copy button
        var profileIdNode = new Node {
            Id    = "prof-id-value",
            NodeValue = profileValue,
            Style = new Style {
                AutoSize  = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextAccent),
                FontSize  = 13,
                TextAlign = Anchor.MiddleLeft,
            },
        };

        Node profileIdRow;
        if (!string.IsNullOrEmpty(cfg.ProfileId))
        {
            var copyBtn = CandyUI.SmallButton("prof-copy-btn", "Copy",
                () => ImGui.SetClipboardText(cfg.ProfileId));
            profileIdRow = CandyUI.Row("prof-id-row", 6,
                CandyUI.Muted("prof-id-label", "Profile ID"),
                profileIdNode,
                copyBtn);
        }
        else
        {
            profileIdRow = CandyUI.Row("prof-id-row", 6,
                CandyUI.Muted("prof-id-label", "Profile ID"),
                profileIdNode);
        }

        // Sync status badge
        var syncBadge = CandyUI.StatusBadge("prof-sync-badge", "Connected", CandyTheme.StatusOnline);

        // Close button — full-width ghost button at bottom
        var closeBtn = CandyUI.GhostButton("prof-close-btn", "Close", () => IsOpen = false);
        closeBtn.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit);

        var card = CandyUI.Card("profile-card",
            CandyUI.Row("prof-char-row", 6,
                CandyUI.Muted("prof-char-label", "Character"),
                CandyUI.Label("prof-char-value", charValue)),
            profileIdRow,
            CandyUI.Row("prof-venue-row", 6,
                CandyUI.Muted("prof-venue-label", "Venue"),
                CandyUI.Label("prof-venue-value", venueValue)),
            CandyUI.Separator("prof-sep"),
            CandyUI.Row("prof-sync-row", 6,
                CandyUI.Muted("prof-sync-label", "Sync Status"),
                syncBadge)
        );
        card.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow);

        _root = CandyUI.Column("profile-root", 8, card, closeBtn);
    }

    public override void Draw()
    {
        // Rebuild every frame so dynamic config values (CharacterName, ProfileId, etc.) stay fresh.
        BuildRoot();

        var region = ImGui.GetContentRegionAvail();
        _root!.Style.Size = new Size((int)region.X, (int)region.Y);

        var pos = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        _root.Render(ImGui.GetWindowDrawList(), pos);
        ImGui.Dummy(region);
    }
}
