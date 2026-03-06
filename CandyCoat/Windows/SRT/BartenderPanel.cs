using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;
using Una.Drawing;
using ECommons.DalamudServices;

namespace CandyCoat.Windows.SRT;

public class BartenderPanel : IToolboxPanel
{
    public string Name => "Bartender";
    public StaffRole Role => StaffRole.Bartender;

    private readonly Plugin _plugin;

    public enum OrderStatus { Pending, Making, Served }
    private readonly List<(string Patron, string Drink, int Price, DateTime Time, OrderStatus Status)> _orders = new();
    private string _newOrderPatron = string.Empty;
    private int _selectedDrinkIndex = -1;
    private string _customDrink = string.Empty;
    private readonly Dictionary<string, int> _tabs = new();
    private string? _pendingCloseTab = null;

    // Settings input
    private string _newMacroTitle = string.Empty;
    private string _newMacroText = string.Empty;

    private readonly StaffPingWidget _pingWidget;

    private static readonly Vector4 CardBg = new(0.16f, 0.12f, 0.20f, 1f);
    private static readonly Vector4 HeaderBg = new(0.22f, 0.16f, 0.28f, 1f);
    private static readonly Vector4 HeaderHover = new(0.30f, 0.22f, 0.36f, 1f);

    public BartenderPanel(Plugin plugin)
    {
        _plugin = plugin;
        _pingWidget = new StaffPingWidget(plugin);
    }

    // ─── Features ────────────────────────────────────────────────────────────

    public void DrawContent()
    {
        // Order Queue (always visible)
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var tier1 = ImRaii.Child("##BTTier1", new Vector2(0, 160f), true))
        {
            ImGui.PopStyleColor();
            if (tier1) DrawOrderQueue();
        }

        ImGui.Spacing();

        using var tabs = ImRaii.TabBar("##BTTabs", ImGuiTabBarFlags.FittingPolicyResizeDown);
        if (!tabs) return;

        if (ImGui.BeginTabItem("Menu##BT"))
        {
            DrawDrinkMenu();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Tabs##BT"))
        {
            DrawTabSystem();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Macros##BT"))
        {
            DrawRPMacroButtons();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Ping##BT"))
        {
            ImGui.Spacing();
            _pingWidget.Draw();
            ImGui.EndTabItem();
        }
    }

    // ─── Settings ────────────────────────────────────────────────────────────

    public void DrawSettings()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "\ud83c\udf78 Bartender Settings");
        ImGui.TextDisabled("Configure your RP emote macro bank.");
        ImGui.Spacing();

        // Card: RP Macro Bank
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        using (var card = ImRaii.Child("##BTMacroCard", new Vector2(0, 220f), true))
        {
            ImGui.PopStyleColor();
            if (!card) return;

            ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.75f, 1.0f), "RP Emote Macro Bank");
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextDisabled("Use {patron} and {drink} tokens.");

            var macros = _plugin.Configuration.BartenderMacros;

            using (var scroll = ImRaii.Child("##BTMacroList", new Vector2(0, 110f), false))
            {
                for (int i = 0; i < macros.Count; i++)
                {
                    ImGui.PushID($"btm{i}");
                    ImGui.Text(macros[i].Title);
                    ImGui.SameLine();
                    ImGui.TextDisabled(macros[i].Text.Length > 40 ? macros[i].Text[..40] + "..." : macros[i].Text);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Del##btmd"))
                    {
                        macros.RemoveAt(i);
                        _plugin.Configuration.Save();
                        ImGui.PopID();
                        break;
                    }
                    ImGui.PopID();
                }
                if (macros.Count == 0) ImGui.TextDisabled("No macros yet.");
            }

            ImGui.Spacing();
            ImGui.SetNextItemWidth(80);
            ImGui.InputTextWithHint("##BTMacroT", "Title", ref _newMacroTitle, 50);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##BTMacroM", "{patron} {drink}", ref _newMacroText, 200);
            ImGui.SameLine();
            if (ImGui.Button("+##BTAddMacro"))
            {
                if (!string.IsNullOrWhiteSpace(_newMacroTitle))
                {
                    macros.Add(new MacroTemplate { Title = _newMacroTitle, Text = _newMacroText });
                    _plugin.Configuration.Save();
                    _newMacroTitle = string.Empty;
                    _newMacroText = string.Empty;
                }
            }
        }
    }

    // ─── Private Draw Helpers ────────────────────────────────────────────────

    private void DrawOrderQueue()
    {
        ImGui.Text("Order Queue");
        ImGui.Spacing();

        var target = Svc.Targets.Target;
        if (target != null && string.IsNullOrEmpty(_newOrderPatron))
        {
            if (ImGui.Button("Use Target##BT")) _newOrderPatron = target.Name.ToString();
            ImGui.SameLine();
        }
        ImGui.SetNextItemWidth(130);
        ImGui.InputTextWithHint("##BTOrdPatron", "Patron", ref _newOrderPatron, 100);
        ImGui.SameLine();

        var drinks = _plugin.Configuration.ServiceMenu.Where(s => s.Category == ServiceCategory.Drink).ToList();
        var drinkNames = drinks.Select(d => d.Name).Append("Custom...").ToArray();
        var drinkIdx = _selectedDrinkIndex < 0 || _selectedDrinkIndex >= drinkNames.Length ? 0 : _selectedDrinkIndex;
        ImGui.SetNextItemWidth(130);
        ImGui.Combo("##BTDrinkPick", ref drinkIdx, drinkNames, drinkNames.Length);
        _selectedDrinkIndex = drinkIdx;

        ImGui.SameLine();
        if (ImGui.Button("Add Order##BT"))
        {
            if (!string.IsNullOrWhiteSpace(_newOrderPatron))
            {
                string drink;
                int price;
                if (drinkIdx < drinks.Count) { drink = drinks[drinkIdx].Name; price = drinks[drinkIdx].Price; }
                else { drink = string.IsNullOrWhiteSpace(_customDrink) ? "Custom" : _customDrink; price = 0; }
                _orders.Add((_newOrderPatron, drink, price, DateTime.Now, OrderStatus.Pending));
                if (!_tabs.ContainsKey(_newOrderPatron)) _tabs[_newOrderPatron] = 0;
                _tabs[_newOrderPatron] += price;
                _newOrderPatron = string.Empty;
                _customDrink = string.Empty;
            }
        }

        if (drinkIdx >= drinks.Count)
        {
            ImGui.SetNextItemWidth(200);
            ImGui.InputTextWithHint("##BTCustomDrink", "Custom drink name", ref _customDrink, 100);
        }

        ImGui.Spacing();
        if (_orders.Count == 0) { ImGui.TextDisabled("No orders queued."); return; }

        for (int i = 0; i < _orders.Count; i++)
        {
            var (patron, drink, price, time, status) = _orders[i];
            var elapsed = DateTime.Now - time;
            var statusColor = status switch
            {
                OrderStatus.Pending => new Vector4(1f, 0.8f, 0.2f, 1f),
                OrderStatus.Making  => new Vector4(0.4f, 0.8f, 1f, 1f),
                OrderStatus.Served  => new Vector4(0.5f, 0.9f, 0.65f, 1.0f),
                _                   => Vector4.One,
            };
            ImGui.PushID($"btord{i}");
            ImGui.TextColored(statusColor, $"[{status}]");
            ImGui.SameLine();
            ImGui.Text($"{patron}: {drink}");
            ImGui.SameLine();
            ImGui.TextDisabled($"({elapsed.Minutes}m)");
            ImGui.SameLine();
            if (status == OrderStatus.Pending && ImGui.SmallButton("Making##bt")) _orders[i] = (patron, drink, price, time, OrderStatus.Making);
            if (status == OrderStatus.Making && ImGui.SmallButton("Served##bt")) _orders[i] = (patron, drink, price, time, OrderStatus.Served);
            if (status == OrderStatus.Served && ImGui.SmallButton("Clear##bt")) { _orders.RemoveAt(i); ImGui.PopID(); break; }
            ImGui.PopID();
        }
    }

    private void DrawDrinkMenu()
    {
        ImGui.Spacing();
        var drinks = _plugin.Configuration.ServiceMenu.Where(s => s.Category == ServiceCategory.Drink).ToList();
        if (drinks.Count == 0) { ImGui.TextDisabled("No drinks defined. Add in Owner > Menu Editor."); ImGui.Spacing(); return; }
        for (int i = 0; i < drinks.Count; i++)
        {
            var d = drinks[i];
            if (ImGui.Selectable($"  {d.Name} \u2014 {d.Price:N0} Gil##btdrink{i}", _selectedDrinkIndex == i))
                _selectedDrinkIndex = i;
            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(d.Description)) ImGui.SetTooltip(d.Description);
        }
        if (_selectedDrinkIndex >= 0 && _selectedDrinkIndex < drinks.Count)
        {
            if (ImGui.Button("Paste to Chat##BT")) Svc.Commands.ProcessCommand($"/say {drinks[_selectedDrinkIndex].Name} \u2014 {drinks[_selectedDrinkIndex].Description}");
        }
        ImGui.Spacing();
    }

    private void DrawTabSystem()
    {
        ImGui.Spacing();
        if (_tabs.Count == 0) { ImGui.TextDisabled("No open tabs."); ImGui.Spacing(); return; }
        foreach (var (patron, total) in _tabs.ToList())
        {
            ImGui.Text($"{patron}: {total:N0} Gil");
            ImGui.SameLine();
            if (ImGui.SmallButton($"Close Tab##{patron}"))
            {
                _pendingCloseTab = patron;
                ImGui.OpenPopup("ConfirmCloseTab##BT");
            }
        }
        if (ImGui.BeginPopupModal("ConfirmCloseTab##BT", ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_pendingCloseTab != null && _tabs.TryGetValue(_pendingCloseTab, out var pendingTotal))
            {
                ImGui.Text($"Close {_pendingCloseTab}'s tab and log {pendingTotal:N0} Gil?");
                ImGui.Spacing();
                if (ImGui.Button("Yes, Close", new Vector2(100, 0)))
                {
                    _plugin.Configuration.Earnings.Add(new EarningsEntry { Role = StaffRole.Bartender, Type = EarningsType.Drink, PatronName = _pendingCloseTab, Description = "Tab close", Amount = pendingTotal });
                    _plugin.Configuration.Save();
                    _tabs.Remove(_pendingCloseTab);
                    _pendingCloseTab = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(80, 0))) { _pendingCloseTab = null; ImGui.CloseCurrentPopup(); }
            }
            else { _pendingCloseTab = null; ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }
        ImGui.Spacing();
    }

    private void DrawRPMacroButtons()
    {
        ImGui.Spacing();
        var macros = _plugin.Configuration.BartenderMacros;
        if (macros.Count == 0) { ImGui.TextDisabled("No macros. Add them in Settings."); ImGui.Spacing(); return; }
        foreach (var m in macros)
        {
            if (ImGui.Button($"{m.Title}##BTbtn{m.Title}"))
            {
                var patron = _orders.Count > 0 ? _orders[0].Patron : Svc.Targets.Target?.Name.ToString() ?? "";
                var drink = _orders.Count > 0 ? _orders[0].Drink : "a drink";
                Svc.Commands.ProcessCommand($"/em {m.Text.Replace("{patron}", patron).Replace("{drink}", drink)}");
            }
            ImGui.SameLine();
            ImGui.TextDisabled(m.Text.Length > 35 ? m.Text[..35] + "..." : m.Text);
        }
        ImGui.Spacing();
    }

    public Node BuildNode() => new Node { Id = "stub" };
    public Node BuildSettingsNode() => new Node { Id = "stub-settings" };
}
