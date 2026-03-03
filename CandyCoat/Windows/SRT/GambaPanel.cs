using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;
using CandyCoat.UI;
using ECommons.DalamudServices;

namespace CandyCoat.Windows.SRT;

public class GambaPanel : IToolboxPanel, IDisposable
{
    public string Name => "Gamba";
    public StaffRole Role => StaffRole.Gamba;

    private static readonly Regex RollRegex = new(
        @"^(.+?)\s+rolls?\s+a\s+(\d+)\s+on\s+the",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Plugin _plugin;

    // Game state
    private int _selectedPresetIndex = 0;
    private readonly List<GambaPlayer> _players = new();
    private readonly List<GambaRollEntry> _rollHistory = new();
    private string _newPlayerName = string.Empty;
    private int _newPlayerBet = 50000;
    private int _manualRoll = 0;
    private int _bankIn = 0;
    private int _bankOut = 0;

    // Settings input
    private string _newPresetName = string.Empty;
    private string _newPresetRules = string.Empty;
    private string _newPresetAnnounce = string.Empty;

    private readonly StaffPingWidget _pingWidget;

    private static readonly Vector4 CardBg = new(0.16f, 0.12f, 0.20f, 1f);
    private static readonly Vector4 HeaderBg = new(0.22f, 0.16f, 0.28f, 1f);
    private static readonly Vector4 HeaderHover = new(0.30f, 0.22f, 0.36f, 1f);

    public GambaPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
        Svc.Chat.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        Svc.Chat.ChatMessage -= OnChatMessage;
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        var text = message.TextValue;
        var match = RollRegex.Match(text);
        if (!match.Success) return;
        if (!int.TryParse(match.Groups[2].Value, out var roll)) return;
        var rollerRaw = match.Groups[1].Value;
        var player = _players.FirstOrDefault(p => rollerRaw.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        _rollHistory.Add(new GambaRollEntry { PlayerName = player?.Name ?? rollerRaw, Roll = roll });
    }

    // ─── Features ────────────────────────────────────────────────────────────

    public void DrawContent()
    {
        // Active game round (always visible)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var tier1 = ImRaii.Child("##GBTier1", new Vector2(0, 160f), true))
        {
            ImGui.PopStyleColor();
            if (tier1) DrawGameRound();
        }

        ImGui.Spacing();

        using var tabs = ImRaii.TabBar("##GBTabs", ImGuiTabBarFlags.FittingPolicyResizeDown);
        if (!tabs) return;

        if (ImGui.BeginTabItem("Rolls##GB"))
        {
            DrawRolls();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Payout##GB"))
        {
            DrawPayoutCalculator();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Bank##GB"))
        {
            DrawHouseBank();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Announce##GB"))
        {
            DrawAnnounceMacros();
            ImGui.Spacing();
            _pingWidget.Draw();
            ImGui.EndTabItem();
        }
    }

    // ─── Settings ────────────────────────────────────────────────────────────

    public void DrawSettings()
    {
        ImGui.TextColored(StyleManager.SectionHeader, "\ud83c\udfb2 Gamba Settings");
        ImGui.TextDisabled("Manage your game presets.");
        ImGui.Spacing();

        var presets = _plugin.Configuration.GambaPresets;

        // Card: Preset Manager
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var card = ImRaii.Child("##GBPresetCard", new Vector2(0, 260f), true))
        {
            ImGui.PopStyleColor();
            if (!card) return;

            ImGui.TextColored(StyleManager.SectionHeader, "Game Preset Manager");
            ImGui.Separator();
            ImGui.Spacing();

            using (var scroll = ImRaii.Child("##GBPresetList", new Vector2(0, 120f), false))
            {
                for (int i = 0; i < presets.Count; i++)
                {
                    ImGui.PushID($"gbp{i}");
                    ImGui.Text(presets[i].Name);
                    ImGui.SameLine();
                    ImGui.TextDisabled($"x{presets[i].DefaultMultiplier:F1}");
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Del##gbpd"))
                    {
                        presets.RemoveAt(i);
                        _plugin.Configuration.Save();
                        if (_selectedPresetIndex >= presets.Count) _selectedPresetIndex = System.Math.Max(0, presets.Count - 1);
                        ImGui.PopID();
                        break;
                    }
                    ImGui.PopID();
                }
                if (presets.Count == 0) ImGui.TextDisabled("No presets yet.");
            }

            ImGui.Spacing();
            ImGui.SetNextItemWidth(120);
            ImGui.InputTextWithHint("##GBPresetN", "Preset Name", ref _newPresetName, 50);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            ImGui.InputTextWithHint("##GBPresetA", "Announce macro...", ref _newPresetAnnounce, 200);
            ImGui.SameLine();
            if (ImGui.Button("+##GBAddPreset"))
            {
                if (!string.IsNullOrWhiteSpace(_newPresetName))
                {
                    presets.Add(new GambaGamePreset { Name = _newPresetName, Rules = "Set rules here...", AnnounceMacro = string.IsNullOrWhiteSpace(_newPresetAnnounce) ? $"\ud83c\udfb2 {_newPresetName} starting! /tell me to join!" : _newPresetAnnounce, DefaultMultiplier = 2.0f });
                    _plugin.Configuration.Save();
                    _newPresetName = string.Empty;
                    _newPresetAnnounce = string.Empty;
                }
            }

            if (presets.Count > 0 && _selectedPresetIndex >= 0 && _selectedPresetIndex < presets.Count)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("Edit Rules for selected preset:");
                var rules = presets[_selectedPresetIndex].Rules;
                if (ImGui.InputTextMultiline("##GBRules", ref rules, 500, new Vector2(-1, 50)))
                {
                    presets[_selectedPresetIndex].Rules = rules;
                    _plugin.Configuration.Save();
                }
            }
        }
    }

    // ─── Private Draw Helpers ────────────────────────────────────────────────

    private void DrawGameRound()
    {
        // Game selector
        var presets = _plugin.Configuration.GambaPresets;
        if (presets.Count > 0)
        {
            var names = presets.Select(p => p.Name).ToArray();
            ImGui.SetNextItemWidth(180);
            ImGui.Combo("##GBPreset", ref _selectedPresetIndex, names, names.Length);
            if (_selectedPresetIndex >= 0 && _selectedPresetIndex < presets.Count)
                ImGui.TextDisabled($"Multiplier: {presets[_selectedPresetIndex].DefaultMultiplier}x");
        }
        else
        {
            ImGui.TextDisabled("No presets. Add in Settings.");
        }

        ImGui.Spacing();

        // Player registration
        if (ImGui.Button("Add Target##GB"))
        {
            var t = Svc.Targets.Target;
            if (t != null) _newPlayerName = t.Name.ToString();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        ImGui.InputTextWithHint("##GBPlayerName", "Name", ref _newPlayerName, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        ImGui.InputInt("##GBBet", ref _newPlayerBet, 10000);
        ImGui.SameLine();
        if (ImGui.Button("Add##GBPlayer"))
        {
            if (!string.IsNullOrWhiteSpace(_newPlayerName) && _newPlayerBet > 0)
            {
                _players.Add(new GambaPlayer { Name = _newPlayerName, Bet = _newPlayerBet });
                _bankIn += _newPlayerBet;
                _newPlayerName = string.Empty;
            }
        }

        if (_players.Count > 0)
        {
            ImGui.TextDisabled($"{_players.Count} players | Pool: {_bankIn:N0} Gil");
            if (ImGui.SmallButton("Clear All##GB")) { _players.Clear(); _rollHistory.Clear(); }
        }
    }

    private void DrawRolls()
    {
        ImGui.Spacing();
        ImGui.TextColored(StyleManager.SyncOk, "\u25cf Auto-capture active");
        ImGui.SameLine();
        if (ImGui.Button("/random##GB")) Svc.Commands.ProcessCommand("/random");
        ImGui.SameLine();
        if (ImGui.Button("/dice##GB")) Svc.Commands.ProcessCommand("/dice");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(70);
        ImGui.InputInt("##GBManRoll", ref _manualRoll);
        ImGui.SameLine();
        if (ImGui.Button("Log##GBManLog")) { _rollHistory.Add(new GambaRollEntry { PlayerName = _players.Count > 0 ? _players[0].Name : "?", Roll = _manualRoll }); }

        if (_rollHistory.Count > 0)
        {
            using var rollScroll = ImRaii.Child("GBRollHistory", new Vector2(0, 90), true);
            foreach (var r in _rollHistory.AsEnumerable().Reverse().Take(10))
                ImGui.BulletText($"{r.PlayerName}: {r.Roll} ({r.Timestamp:HH:mm:ss})");
        }
        ImGui.Spacing();
    }

    private void DrawPayoutCalculator()
    {
        ImGui.Spacing();
        var presets = _plugin.Configuration.GambaPresets;
        bool hasPreset = _selectedPresetIndex >= 0 && _selectedPresetIndex < presets.Count;
        float mult = hasPreset ? presets[_selectedPresetIndex].DefaultMultiplier : 2.0f;
        if (ImGui.SliderFloat("Multiplier##GBCalc", ref mult, 1.0f, 10.0f, "%.1fx") && hasPreset)
        {
            presets[_selectedPresetIndex].DefaultMultiplier = mult;
            _plugin.Configuration.Save();
        }
        foreach (var p in _players)
            ImGui.Text($"{p.Name}: {p.Bet:N0} \u00d7 {mult:F1} = {(int)(p.Bet * mult):N0} Gil");
        if (_players.Count > 0 && ImGui.Button("Pay Winner (Log)##GB"))
        {
            var winner = _players[0];
            var payout = (int)(winner.Bet * mult);
            _bankOut += payout;
            _plugin.Configuration.Earnings.Add(new EarningsEntry { Role = StaffRole.Gamba, Type = EarningsType.GamePayout, PatronName = winner.Name, Description = $"Payout ({mult:F1}x)", Amount = -payout });
            _plugin.Configuration.Save();
        }
        ImGui.Spacing();
    }

    private void DrawHouseBank()
    {
        ImGui.Spacing();
        var net = _bankIn - _bankOut;
        var color = net >= 0 ? StyleManager.SyncOk : StyleManager.SyncError;
        ImGui.Text($"Bets In:     {_bankIn:N0} Gil");
        ImGui.Text($"Payouts Out: {_bankOut:N0} Gil");
        ImGui.TextColored(color, $"Net P/L:     {net:N0} Gil");
        if (ImGui.Button("Reset Bank##GB")) { _bankIn = 0; _bankOut = 0; }
        ImGui.Spacing();
    }

    private void DrawAnnounceMacros()
    {
        ImGui.Spacing();
        var presets = _plugin.Configuration.GambaPresets;
        if (_selectedPresetIndex >= 0 && _selectedPresetIndex < presets.Count)
        {
            var p = presets[_selectedPresetIndex];
            if (ImGui.Button("Shout Announce##GB")) Svc.Commands.ProcessCommand($"/shout {p.AnnounceMacro}");
            if (ImGui.TreeNode("Rules Preview##GB")) { ImGui.TextWrapped(p.Rules); if (ImGui.Button("Paste Rules##GB")) { foreach (var line in p.Rules.Split('\n')) { if (!string.IsNullOrWhiteSpace(line)) Svc.Commands.ProcessCommand($"/say {line.Trim()}"); } } ImGui.TreePop(); }
        }
        ImGui.Spacing();
    }
}
