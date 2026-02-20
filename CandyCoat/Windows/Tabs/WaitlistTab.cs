using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using OtterGui.Widgets;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Services;
using CandyCoat.Data;

namespace CandyCoat.Windows.Tabs;

public class WaitlistTab : ITab
{
    private readonly WaitlistManager _manager;
    private string _newEntryName = string.Empty;

    public string Name => "Waitlist";

    public WaitlistTab(WaitlistManager manager)
    {
        _manager = manager;
    }

    public void Draw()
    {
        using var tab = ImRaii.TabItem(Name);
        if (!tab) return;

        ImGui.TextUnformatted("Waitlist Queue");
        ImGui.Spacing();

        ImGui.InputText("Patron Name##Waitlist", ref _newEntryName, 100);
        ImGui.SameLine();
        if (ImGui.Button("Add to Queue"))
        {
            if (!string.IsNullOrWhiteSpace(_newEntryName))
            {
                _manager.AddToQueue(_newEntryName);
                _newEntryName = string.Empty;
            }
        }

        ImGui.Separator();
        ImGui.Spacing();

        if (_manager.Entries.Count == 0)
        {
            ImGui.TextDisabled("The waitlist is currently empty.");
            return;
        }

        if (ImGui.BeginTable("WaitlistTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Pos", ImGuiTableColumnFlags.WidthFixed, 30f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Time Waited", ImGuiTableColumnFlags.WidthFixed, 100f);
            ImGui.TableHeadersRow();

            for (int i = 0; i < _manager.Entries.Count; i++)
            {
                var entry = _manager.Entries[i];
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text($"#{i + 1}");

                ImGui.TableNextColumn();
                ImGui.Text(entry.PatronName);

                ImGui.TableNextColumn();
                var time = entry.TimeWaited;
                ImGui.Text($"{time.Minutes}m {time.Seconds}s");

                // Context menu for each item
                if (ImGui.BeginPopupContextItem($"WaitlistCtx{i}"))
                {
                    if (ImGui.Selectable("Remove from Queue"))
                    {
                        _manager.RemoveFromQueue(entry);
                        ImGui.EndPopup();
                        break; // Stop iteration as collection modified
                    }
                    if (ImGui.Selectable("Notify Ready (Tell)"))
                    {
                        ECommons.DalamudServices.Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry
                        {
                            Type = Dalamud.Game.Text.XivChatType.Echo,
                            Message = $"[CandyCoat Macro executed: /t {entry.PatronName} You're up!]"
                        });
                        ECommons.DalamudServices.Svc.Commands.ProcessCommand($"/t {entry.PatronName} We are ready for you! Please head to the venue.");
                        ImGui.EndPopup();
                    }
                    ImGui.EndPopup();
                }
            }
            ImGui.EndTable();
        }
        
        ImGui.Spacing();
        if (ImGui.Button("Clear All"))
        {
            _manager.ClearQueue();
        }
    }
}
