using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using ECommons.DalamudServices;

namespace CandyCoat.Windows.SRT;

public class BartenderPanel : IToolboxPanel
{
    public string Name => "Bartender";
    public StaffRole Role => StaffRole.Bartender;

    private readonly Plugin _plugin;

    // Order queue
    public enum OrderStatus { Pending, Making, Served }
    private readonly List<(string Patron, string Drink, int Price, DateTime Time, OrderStatus Status)> _orders = new();
    private string _newOrderPatron = string.Empty;
    private int _selectedDrinkIndex = -1;
    private string _customDrink = string.Empty;

    // Tab system
    private readonly Dictionary<string, int> _tabs = new();

    // Macro input
    private string _newMacroTitle = string.Empty;
    private string _newMacroText = string.Empty;

    public BartenderPanel(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void DrawContent()
    {
        ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.4f, 1f), "🍸 Bartender Toolbox");
        ImGui.Separator();
        ImGui.Spacing();

        DrawDrinkMenu();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawOrderQueue();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawTabSystem();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        DrawRPMacros();
    }

    private void DrawDrinkMenu()
    {
        ImGui.Text("Drink Menu");
        var drinks = _plugin.Configuration.ServiceMenu
            .Where(s => s.Category == ServiceCategory.Drink).ToList();

        if (drinks.Count == 0)
        {
            ImGui.TextDisabled("No drinks defined. Add in Owner > Menu Editor.");
            return;
        }

        for (int i = 0; i < drinks.Count; i++)
        {
            var d = drinks[i];
            if (ImGui.Selectable($"  {d.Name} — {d.Price:N0} Gil##drink{i}", _selectedDrinkIndex == i))
                _selectedDrinkIndex = i;

            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(d.Description))
                ImGui.SetTooltip(d.Description);
        }

        if (_selectedDrinkIndex >= 0 && _selectedDrinkIndex < drinks.Count)
        {
            if (ImGui.Button("Paste to Chat"))
            {
                var d = drinks[_selectedDrinkIndex];
                Svc.Commands.ProcessCommand($"/say {d.Name} — {d.Description}");
            }
        }
    }

    private void DrawOrderQueue()
    {
        ImGui.Text("Order Queue");
        ImGui.Spacing();

        // New order
        var target = Svc.Targets.Target;
        if (target != null && string.IsNullOrEmpty(_newOrderPatron))
        {
            if (ImGui.Button("Use Target##OQ"))
                _newOrderPatron = target.Name.ToString();
            ImGui.SameLine();
        }
        ImGui.SetNextItemWidth(130);
        ImGui.InputTextWithHint("##OrdPatron", "Patron", ref _newOrderPatron, 100);
        ImGui.SameLine();

        // Drink selection: from menu or custom
        var drinks = _plugin.Configuration.ServiceMenu
            .Where(s => s.Category == ServiceCategory.Drink).ToList();
        var drinkNames = drinks.Select(d => d.Name).Append("Custom...").ToArray();
        var drinkIdx = _selectedDrinkIndex;
        if (drinkIdx < 0 || drinkIdx >= drinkNames.Length) drinkIdx = 0;
        ImGui.SetNextItemWidth(140);
        ImGui.Combo("##DrinkPick", ref drinkIdx, drinkNames, drinkNames.Length);

        ImGui.SameLine();
        if (ImGui.Button("Add Order"))
        {
            if (!string.IsNullOrWhiteSpace(_newOrderPatron))
            {
                string drink;
                int price;
                if (drinkIdx < drinks.Count)
                {
                    drink = drinks[drinkIdx].Name;
                    price = drinks[drinkIdx].Price;
                }
                else
                {
                    drink = string.IsNullOrWhiteSpace(_customDrink) ? "Custom Drink" : _customDrink;
                    price = 0;
                }
                _orders.Add((_newOrderPatron, drink, price, DateTime.Now, OrderStatus.Pending));

                // Add to tab
                if (!_tabs.ContainsKey(_newOrderPatron))
                    _tabs[_newOrderPatron] = 0;
                _tabs[_newOrderPatron] += price;

                _newOrderPatron = string.Empty;
                _customDrink = string.Empty;
            }
        }

        if (drinkIdx >= drinks.Count)
        {
            ImGui.SetNextItemWidth(200);
            ImGui.InputTextWithHint("##CustomDrink", "Custom drink name", ref _customDrink, 100);
        }

        // Order table
        ImGui.Spacing();
        if (_orders.Count == 0)
        {
            ImGui.TextDisabled("No orders queued.");
            return;
        }

        for (int i = 0; i < _orders.Count; i++)
        {
            var (patron, drink, price, time, status) = _orders[i];
            var elapsed = DateTime.Now - time;
            var statusColor = status switch
            {
                OrderStatus.Pending => new Vector4(1f, 0.8f, 0.2f, 1f),
                OrderStatus.Making => new Vector4(0.4f, 0.8f, 1f, 1f),
                OrderStatus.Served => new Vector4(0.2f, 1f, 0.2f, 1f),
                _ => Vector4.One,
            };

            ImGui.PushID($"ord{i}");
            ImGui.TextColored(statusColor, $"[{status}]");
            ImGui.SameLine();
            ImGui.Text($"{patron}: {drink}");
            ImGui.SameLine();
            ImGui.TextDisabled($"({elapsed.Minutes}m ago)");
            ImGui.SameLine();

            if (status == OrderStatus.Pending && ImGui.SmallButton("Making"))
                _orders[i] = (patron, drink, price, time, OrderStatus.Making);
            if (status == OrderStatus.Making)
            {
                if (ImGui.SmallButton("Served"))
                    _orders[i] = (patron, drink, price, time, OrderStatus.Served);
            }
            if (status == OrderStatus.Served && ImGui.SmallButton("Clear"))
            {
                _orders.RemoveAt(i);
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }
    }

    private void DrawTabSystem()
    {
        ImGui.Text("Open Tabs");
        if (_tabs.Count == 0)
        {
            ImGui.TextDisabled("No open tabs.");
            return;
        }

        foreach (var (patron, total) in _tabs.ToList())
        {
            ImGui.Text($"{patron}: {total:N0} Gil");
            ImGui.SameLine();
            if (ImGui.SmallButton($"Close Tab##{patron}"))
            {
                _plugin.Configuration.Earnings.Add(new EarningsEntry
                {
                    Role = StaffRole.Bartender,
                    Type = EarningsType.Drink,
                    PatronName = patron,
                    Description = "Tab close",
                    Amount = total,
                });
                _plugin.Configuration.Save();
                _tabs.Remove(patron);
                break;
            }
        }
    }

    private void DrawRPMacros()
    {
        ImGui.Text("RP Emote Macros");
        var macros = _plugin.Configuration.BartenderMacros;

        foreach (var m in macros)
        {
            if (ImGui.Button($"{m.Title}##{m.Title}"))
            {
                var patron = _orders.Count > 0 ? _orders[0].Patron : Svc.Targets.Target?.Name.ToString() ?? "";
                var drink = _orders.Count > 0 ? _orders[0].Drink : "a drink";
                var msg = m.Text.Replace("{patron}", patron).Replace("{drink}", drink);
                Svc.Commands.ProcessCommand($"/em {msg}");
            }
            ImGui.SameLine();
            ImGui.TextDisabled(m.Text.Length > 35 ? m.Text[..35] + "..." : m.Text);
        }

        ImGui.SetNextItemWidth(80);
        ImGui.InputTextWithHint("##BT", "Title", ref _newMacroTitle, 50);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##BM", "{patron} {drink}", ref _newMacroText, 200);
        ImGui.SameLine();
        if (ImGui.Button("+##AddBM"))
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
