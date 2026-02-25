using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using ECommons.DalamudServices;

namespace CandyCoat.Windows.SRT;

public class GambaPanel : IToolboxPanel
{
    public string Name => "Gamba";
    public StaffRole Role => StaffRole.Gamba;

    private readonly Plugin _plugin;

    // Game state
    private int _selectedPresetIndex = 0;
    private readonly List<GambaPlayer> _players = new();
    private readonly List<GambaRollEntry> _rollHistory = new();
    private string _newPlayerName = string.Empty;
    private int _newPlayerBet = 50000;
    private int _manualRoll = 0;

    // Bank
    private int _bankIn = 0;
    private int _bankOut = 0;

    // Preset editor
    private string _newPresetName = string.Empty;
    private string _newPresetRules = string.Empty;
    private string _newPresetAnnounce = string.Empty;

    public GambaPanel(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void DrawContent()
    {
        ImGui.TextColored(new Vector4(0.4f, 1f, 0.6f, 1f), "🎲 Gamba Toolbox");
        ImGui.Separator();
        ImGui.Spacing();

        DrawGameSelector();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawPlayerBets();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawRolls();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawPayoutCalculator();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawHouseBank();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawAnnounceMacros();
    }

    private void DrawGameSelector()
    {
        ImGui.Text("Game Select");
        var presets = _plugin.Configuration.GambaPresets;

        if (presets.Count == 0)
        {
            ImGui.TextDisabled("No game presets. Add below or in Owner panel.");

            // Inline add
            ImGui.SetNextItemWidth(100);
            ImGui.InputTextWithHint("##gpn", "Name", ref _newPresetName, 50);
            ImGui.SameLine();
            if (ImGui.Button("+ Add Preset"))
            {
                if (!string.IsNullOrWhiteSpace(_newPresetName))
                {
                    presets.Add(new GambaGamePreset
                    {
                        Name = _newPresetName,
                        Rules = "Set rules here...",
                        AnnounceMacro = $"🎲 {_newPresetName} starting! /tell me to join!",
                        DefaultMultiplier = 2.0f,
                    });
                    _plugin.Configuration.Save();
                    _newPresetName = string.Empty;
                }
            }
            return;
        }

        var names = presets.Select(p => p.Name).ToArray();
        ImGui.SetNextItemWidth(200);
        ImGui.Combo("##GamePreset", ref _selectedPresetIndex, names, names.Length);

        if (_selectedPresetIndex >= 0 && _selectedPresetIndex < presets.Count)
        {
            var preset = presets[_selectedPresetIndex];
            ImGui.TextDisabled($"Multiplier: {preset.DefaultMultiplier}x");

            if (ImGui.TreeNode("Rules"))
            {
                ImGui.TextWrapped(preset.Rules);
                if (ImGui.Button("Paste Rules to /say"))
                {
                    // Split long rules into lines
                    foreach (var line in preset.Rules.Split('\n'))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            Svc.Commands.ProcessCommand($"/say {line.Trim()}");
                    }
                }
                ImGui.TreePop();
            }
        }
    }

    private void DrawPlayerBets()
    {
        ImGui.Text("Player Bets");
        ImGui.Spacing();

        // Add player
        if (ImGui.Button("Add Target##GP"))
        {
            var t = Svc.Targets.Target;
            if (t != null) _newPlayerName = t.Name.ToString();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(130);
        ImGui.InputTextWithHint("##PlayerName", "Name", ref _newPlayerName, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("##Bet", ref _newPlayerBet, 10000);
        ImGui.SameLine();
        if (ImGui.Button("Add"))
        {
            if (!string.IsNullOrWhiteSpace(_newPlayerName) && _newPlayerBet > 0)
            {
                _players.Add(new GambaPlayer { Name = _newPlayerName, Bet = _newPlayerBet });
                _bankIn += _newPlayerBet;
                _newPlayerName = string.Empty;
            }
        }

        if (_players.Count == 0)
        {
            ImGui.TextDisabled("No players.");
            return;
        }

        if (ImGui.BeginTable("##BetTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Bet", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableHeadersRow();

            for (int i = 0; i < _players.Count; i++)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text(_players[i].Name);
                ImGui.TableNextColumn(); ImGui.Text($"{_players[i].Bet:N0}");
                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"X##{i}"))
                {
                    _bankIn -= _players[i].Bet;
                    _players.RemoveAt(i);
                    break;
                }
            }
            ImGui.EndTable();
        }
    }

    private void DrawRolls()
    {
        ImGui.Text("Rolls");
        if (ImGui.Button("/random"))
            Svc.Commands.ProcessCommand("/random");
        ImGui.SameLine();
        if (ImGui.Button("/dice"))
            Svc.Commands.ProcessCommand("/dice");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("##ManRoll", ref _manualRoll);
        ImGui.SameLine();
        if (ImGui.Button("Log Roll"))
        {
            _rollHistory.Add(new GambaRollEntry
            {
                PlayerName = _players.Count > 0 ? _players[0].Name : "?",
                Roll = _manualRoll,
            });
        }

        // History
        if (_rollHistory.Count > 0)
        {
            ImGui.TextDisabled("Roll History:");
            foreach (var r in _rollHistory.AsEnumerable().Reverse().Take(10))
            {
                ImGui.BulletText($"{r.PlayerName}: {r.Roll} ({r.Timestamp:HH:mm:ss})");
            }
        }
    }

    private void DrawPayoutCalculator()
    {
        ImGui.Text("Payout Calculator");
        var presets = _plugin.Configuration.GambaPresets;
        float mult = _selectedPresetIndex >= 0 && _selectedPresetIndex < presets.Count
            ? presets[_selectedPresetIndex].DefaultMultiplier : 2.0f;

        ImGui.SliderFloat("Multiplier##Calc", ref mult, 1.0f, 10.0f, "%.1fx");

        foreach (var p in _players)
        {
            var payout = (int)(p.Bet * mult);
            ImGui.Text($"{p.Name}: {p.Bet:N0} × {mult:F1} = {payout:N0} Gil");
        }

        if (_players.Count > 0 && ImGui.Button("Pay Winner (Log)"))
        {
            var winner = _players[0]; // Player picks who won
            var payout = (int)(winner.Bet * mult);
            _bankOut += payout;
            _plugin.Configuration.Earnings.Add(new EarningsEntry
            {
                Role = StaffRole.Gamba,
                Type = EarningsType.GamePayout,
                PatronName = winner.Name,
                Description = $"Payout ({mult:F1}x)",
                Amount = -payout, // Negative = house pays out
            });
            _plugin.Configuration.Save();
        }
    }

    private void DrawHouseBank()
    {
        ImGui.Text("House Bank");
        var net = _bankIn - _bankOut;
        var color = net >= 0 ? new Vector4(0.2f, 1f, 0.2f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f);

        ImGui.Text($"Bets In:     {_bankIn:N0} Gil");
        ImGui.Text($"Payouts Out: {_bankOut:N0} Gil");
        ImGui.TextColored(color, $"Net P/L:     {net:N0} Gil");

        if (ImGui.Button("Reset Bank"))
        {
            _bankIn = 0;
            _bankOut = 0;
        }
    }

    private void DrawAnnounceMacros()
    {
        ImGui.Text("Announce");
        var presets = _plugin.Configuration.GambaPresets;
        if (_selectedPresetIndex >= 0 && _selectedPresetIndex < presets.Count)
        {
            var p = presets[_selectedPresetIndex];
            if (ImGui.Button("Shout Announce"))
                Svc.Commands.ProcessCommand($"/shout {p.AnnounceMacro}");
        }

        if (_players.Count > 0)
        {
            if (ImGui.Button("Clear All Players"))
            {
                _players.Clear();
                _rollHistory.Clear();
            }
        }
    }
}
