using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Services;
using CandyCoat.Data;
using CandyCoat.UI;
using Una.Drawing;

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
        DrawContent();
    }

    public void DrawContent()
    {
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

        // Explicit block so EndTable fires before Clear All button
        {
            using var table = ImRaii.Table("WaitlistTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
            if (table)
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
            }
        } // EndTable called here

        ImGui.Spacing();
        if (ImGui.Button("Clear All"))
        {
            ImGui.OpenPopup("ConfirmClearAll##WL");
        }

        // Confirmation modal
        if (ImGui.BeginPopupModal("ConfirmClearAll##WL", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Clear all waitlist entries?");
            ImGui.Spacing();
            if (ImGui.Button("Yes, Clear", new Vector2(100, 0)))
            {
                _manager.ClearQueue();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(80, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    public Node BuildNode()
    {
        var root    = UdtHelper.CreateFromTemplate("waitlist-tab.xml", "waitlist-layout");
        var dynamic = root.QuerySelector("#waitlist-dynamic")!;

        // Add-entry row — live input rendered via DrawOverlays()
        var addCard = CandyUI.Card("waitlist-add-card");
        addCard.AppendChild(CandyUI.Label("waitlist-add-title", "Add to Queue", 13));

        var inputRow = CandyUI.Row("waitlist-input-row", 8);
        inputRow.AppendChild(CandyUI.InputSpacer("waitlist-name-input", 200));
        inputRow.AppendChild(CandyUI.InputSpacer("waitlist-add-btn",    120, 28));
        addCard.AppendChild(inputRow);
        dynamic.AppendChild(addCard);

        dynamic.AppendChild(CandyUI.Separator("waitlist-sep2"));

        // Queue summary card
        var queueCard = CandyUI.Card("waitlist-queue-card");
        var count = _manager.Entries.Count;

        if (count == 0)
        {
            queueCard.AppendChild(CandyUI.Muted("waitlist-empty", "The waitlist is currently empty."));
        }
        else
        {
            queueCard.AppendChild(CandyUI.Label("waitlist-count-label", $"{count} patron(s) in queue", 13));
            queueCard.AppendChild(CandyUI.Muted("waitlist-table-hint", "Queue table rendered below."));
            // Spacer for the ImGui table rendered in DrawOverlays()
            queueCard.AppendChild(CandyUI.InputSpacer("waitlist-table-spacer", 0, 200));
        }
        dynamic.AppendChild(queueCard);

        return root;
    }

    public void DrawOverlays()
    {
        // Add-entry input
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

        {
            using var table = ImRaii.Table("WaitlistTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
            if (table)
            {
                ImGui.TableSetupColumn("Pos",         ImGuiTableColumnFlags.WidthFixed, 30f);
                ImGui.TableSetupColumn("Name",        ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Time Waited", ImGuiTableColumnFlags.WidthFixed, 100f);
                ImGui.TableHeadersRow();

                for (int i = 0; i < _manager.Entries.Count; i++)
                {
                    var entry = _manager.Entries[i];
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.Text($"#{i + 1}");
                    ImGui.TableNextColumn(); ImGui.Text(entry.PatronName);
                    ImGui.TableNextColumn();
                    var time = entry.TimeWaited;
                    ImGui.Text($"{time.Minutes}m {time.Seconds}s");

                    if (ImGui.BeginPopupContextItem($"WaitlistCtx{i}"))
                    {
                        if (ImGui.Selectable("Remove from Queue"))
                        {
                            _manager.RemoveFromQueue(entry);
                            ImGui.EndPopup();
                            break;
                        }
                        if (ImGui.Selectable("Notify Ready (Tell)"))
                        {
                            ECommons.DalamudServices.Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry
                            {
                                Type    = Dalamud.Game.Text.XivChatType.Echo,
                                Message = $"[CandyCoat Macro executed: /t {entry.PatronName} You're up!]",
                            });
                            ECommons.DalamudServices.Svc.Commands.ProcessCommand(
                                $"/t {entry.PatronName} We are ready for you! Please head to the venue.");
                            ImGui.EndPopup();
                        }
                        ImGui.EndPopup();
                    }
                }
            }
        }

        ImGui.Spacing();
        if (ImGui.Button("Clear All"))
            ImGui.OpenPopup("ConfirmClearAll##WL");

        if (ImGui.BeginPopupModal("ConfirmClearAll##WL", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Clear all waitlist entries?");
            ImGui.Spacing();
            if (ImGui.Button("Yes, Clear", new Vector2(100, 0))) { _manager.ClearQueue(); ImGui.CloseCurrentPopup(); }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(80, 0))) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }
}
