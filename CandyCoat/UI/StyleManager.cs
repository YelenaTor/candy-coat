using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace CandyCoat.UI;

public static class StyleManager
{
    // A cute, pastel color palette
    private static readonly Vector4 PastelPink = new(1.0f, 0.7f, 0.75f, 1.0f);
    private static readonly Vector4 PastelPinkHover = new(1.0f, 0.8f, 0.85f, 1.0f);
    private static readonly Vector4 PastelPinkActive = new(1.0f, 0.6f, 0.65f, 1.0f);

    private static readonly Vector4 PastelBg = new(0.12f, 0.10f, 0.15f, 0.95f); // Dark but soft purple-ish base
    private static readonly Vector4 PastelText = new(0.95f, 0.90f, 0.95f, 1.0f);

    // Sidebar-specific
    internal static readonly Vector4 SidebarBg = new(0.08f, 0.07f, 0.11f, 0.95f);
    private static readonly Vector4 SelectableActive = new(0.35f, 0.2f, 0.45f, 1.0f);
    private static readonly Vector4 SelectableHover = new(0.25f, 0.15f, 0.35f, 1.0f);

    // Semantic status colors — muted, on-palette
    internal static readonly Vector4 SyncOk    = new(0.5f,  0.9f,  0.65f, 1.0f); // muted mint
    internal static readonly Vector4 SyncWarn  = new(1.0f,  0.85f, 0.4f,  1.0f); // soft amber
    internal static readonly Vector4 SyncError = new(1.0f,  0.45f, 0.45f, 1.0f); // rose-red

    // Section header alias (semantic clarity)
    internal static readonly Vector4 SectionHeader = new(1.0f, 0.7f, 0.75f, 1.0f); // == PastelPink

    public static void PushStyles()
    {
        // ── Base Colors (13) ──────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.WindowBg, PastelBg);
        ImGui.PushStyleColor(ImGuiCol.Text, PastelText);

        ImGui.PushStyleColor(ImGuiCol.Button, PastelPink);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, PastelPinkHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, PastelPinkActive);

        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.2f, 0.15f, 0.25f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.3f, 0.2f, 0.35f, 1.0f));

        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.2f, 0.15f, 0.25f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, PastelPinkHover);
        ImGui.PushStyleColor(ImGuiCol.TabActive, PastelPink);

        // Sidebar selectable styles
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, SelectableHover);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, SelectableActive);

        // Separator — slightly more opaque
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.3f, 0.2f, 0.35f, 0.85f));

        // ── Extended Palette (12) ─────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.FrameBg,        new Vector4(0.18f, 0.14f, 0.22f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.24f, 0.18f, 0.30f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,  new Vector4(0.28f, 0.20f, 0.36f, 1.0f));

        ImGui.PushStyleColor(ImGuiCol.TabUnfocused,       new Vector4(0.12f, 0.09f, 0.16f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, new Vector4(0.20f, 0.15f, 0.25f, 1.0f));

        ImGui.PushStyleColor(ImGuiCol.CheckMark,        PastelPink);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab,       PastelPink);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, PastelPinkActive);

        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg,            new Vector4(0.08f, 0.06f, 0.12f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab,          new Vector4(0.30f, 0.20f, 0.40f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered,   new Vector4(0.40f, 0.28f, 0.52f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive,    PastelPink);

        // ── Style Variables ───────────────────────────────────────
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
    }

    public static void PopStyles()
    {
        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(25);
    }
}
