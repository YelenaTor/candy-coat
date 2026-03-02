using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using CandyCoat.UI;

namespace CandyCoat.Windows;

public class ProfileWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

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

    public void Dispose() { }

    public override void Draw()
    {
        StyleManager.PushStyles();
        try
        {
            DrawContent();
        }
        finally
        {
            StyleManager.PopStyles();
        }
    }

    private void DrawContent()
    {
        var cfg     = _plugin.Configuration;
        var dimGrey = new Vector4(0.6f, 0.6f, 0.6f, 1f);
        var pink    = new Vector4(1f, 0.6f, 0.8f, 1f);

        // Character row
        ImGui.TextColored(dimGrey, "Character");
        ImGui.SameLine(90);
        ImGui.Text(string.IsNullOrEmpty(cfg.CharacterName) ? "—" : cfg.CharacterName);

        // Profile ID row
        ImGui.TextColored(dimGrey, "Profile ID");
        ImGui.SameLine(90);
        ImGui.TextColored(pink, string.IsNullOrEmpty(cfg.ProfileId) ? "—" : cfg.ProfileId);
        if (!string.IsNullOrEmpty(cfg.ProfileId))
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Copy##profCopy"))
                ImGui.SetClipboardText(cfg.ProfileId);
        }

        // Venue row
        ImGui.TextColored(dimGrey, "Venue");
        ImGui.SameLine(90);
        ImGui.Text("Sugar");

        ImGui.Separator();
        ImGui.Spacing();

        // Sync Status
        ImGui.TextColored(dimGrey, "Sync Status");
        ImGui.SameLine(90);
        DrawSyncStatus();

        ImGui.Spacing();
        ImGui.Spacing();

        if (ImGui.Button("Close", new Vector2(-1, 0)))
            IsOpen = false;
    }

    private void DrawSyncStatus()
    {
        ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.4f, 1f), "Connected");
    }
}
