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

    public static void PushStyles()
    {
        // Colors
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
        
        // Variables (Rounding)
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 6f);
    }

    public static void PopStyles()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(9);
    }
}
