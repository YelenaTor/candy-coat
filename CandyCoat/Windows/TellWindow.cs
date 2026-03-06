using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using CandyCoat.Data;
using CandyCoat.UI;
using ECommons.DalamudServices;
using Una.Drawing;

namespace CandyCoat.Windows;

public class TellWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

    private string _filter = string.Empty;
    private TellConversation? _selectedConversation;
    private string _inputBuffer = string.Empty;
    private string _notesBuffer = string.Empty;
    private bool _scrollToBottom = false;
    private int _lastMessageCount = 0;

    private const float LeftPanelWidth = 190f;
    private const float InputRowHeight = 38f;

    // Una.Drawing root
    private Node? _root;

    public TellWindow(Plugin plugin)
        : base("Candy Tells##CandyTellsWindow",
               ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 380),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Size = new Vector2(700, 500);
        SizeCondition = ImGuiCond.FirstUseEver;

        plugin.TellService.OnTellReceived += OnTellReceived;
    }

    public void Dispose()
    {
        _plugin.TellService.OnTellReceived -= OnTellReceived;
        _root?.Dispose();
        _root = null;
    }

    private void OnTellReceived()
    {
        if (_selectedConversation != null)
            _scrollToBottom = true;
    }

    private void BuildRoot()
    {
        _root?.Dispose();
        // Outer shell: sidebar column + content column side-by-side.
        // All actual content is drawn as ImGui overlays.
        _root = CandyUI.WindowRoot(
            CandyUI.Sidebar(),
            CandyUI.ContentPanel()
        );
    }

    public override void Draw()
    {
        // Sync selection from TellService (e.g. opened via ChatTwo IPC or TellAutoOpen)
        var svcSelected = _plugin.TellService.SelectedConversation;
        if (svcSelected != null && svcSelected != _selectedConversation)
        {
            _selectedConversation = svcSelected;
            _notesBuffer = _selectedConversation.Notes;
            _scrollToBottom = true;
        }

        if (_root == null) BuildRoot();

        var region = ImGui.GetContentRegionAvail();
        _root!.Style.Size = new Size((int)region.X, (int)region.Y);

        var pos = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        _root.Render(ImGui.GetWindowDrawList(), pos);
        ImGui.Dummy(region);

        DrawOverlays();
    }

    private void DrawOverlays()
    {
        ImGui.SetCursorPos(new Vector2(0, 0));

        var contentRegion = ImGui.GetContentRegionAvail();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.08f, 0.07f, 0.11f, 0.95f));
        using (var left = ImRaii.Child("##TellLeft", new Vector2(LeftPanelWidth, contentRegion.Y), true))
        {
            ImGui.PopStyleColor();
            if (left) DrawConversationList();
        }

        ImGui.SameLine(0, 0);

        using (var right = ImRaii.Child("##TellRight", new Vector2(0, contentRegion.Y), false))
        {
            if (right) DrawRightPanel();
        }
    }

    // ─── Left Panel ───────────────────────────────────────────────────────────

    private void DrawConversationList()
    {
        var cfg = _plugin.Configuration;

        // Filter bar
        ImGui.SetNextItemWidth(LeftPanelWidth - 16f);
        ImGui.InputTextWithHint("##tellfilter", "Filter...", ref _filter, 64);
        ImGui.Separator();
        ImGui.Spacing();

        // Sort: pinned first, then by LastActivity descending
        var sorted = cfg.TellHistory
            .Where(c => string.IsNullOrEmpty(_filter) ||
                        c.PlayerName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastActivity)
            .ToList();

        bool listModified = false;
        foreach (var conv in sorted)
        {
            bool isSelected = _selectedConversation?.PlayerName == conv.PlayerName;
            bool hasUnread = conv.UnreadCount > 0;

            var patron = cfg.Patrons.FirstOrDefault(p => p.Name == conv.PlayerName);
            string tierPart = string.Empty;
            if (patron != null)
            {
                var tier = cfg.GetTier(patron);
                tierPart = tier switch
                {
                    PatronTier.Elite   => "\u2605 ",
                    PatronTier.Regular => "\u25c6 ",
                    _                  => "\u25cb ",
                };
            }

            var displayName = (conv.IsPinned ? "\u25c6 " : "") + tierPart + conv.PlayerName;
            if (hasUnread) displayName += $" [{conv.UnreadCount}]";

            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(1f, 0.7f, 0.9f, 0.3f));
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1f, 0.7f, 0.9f, 0.4f));
            }
            if (hasUnread) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.95f, 1f, 1f));

            if (ImGui.Selectable($"{displayName}##conv_{conv.PlayerName}", isSelected))
            {
                _selectedConversation = conv;
                _notesBuffer = conv.Notes;
                _scrollToBottom = true;
                _plugin.TellService.SelectConversation(conv);
            }

            if (hasUnread) ImGui.PopStyleColor();
            if (isSelected) ImGui.PopStyleColor(2);

            // Right-click context menu
            if (ImGui.BeginPopupContextItem($"##convCtx_{conv.PlayerName}"))
            {
                if (ImGui.MenuItem(conv.IsPinned ? "Unpin" : "Pin"))
                {
                    conv.IsPinned = !conv.IsPinned;
                    cfg.Save();
                }
                if (ImGui.MenuItem("Clear History"))
                {
                    conv.Messages.Clear();
                    cfg.Save();
                }
                if (ImGui.MenuItem("Delete Conversation"))
                {
                    if (_selectedConversation?.PlayerName == conv.PlayerName)
                    {
                        _selectedConversation = null;
                        _plugin.TellService.ClearSelection();
                    }
                    cfg.TellHistory.Remove(conv);
                    cfg.Save();
                    listModified = true;
                }
                ImGui.EndPopup();
            }

            if (listModified) break;
        }

        // Quick Replies
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled("QUICK REPLIES");
        ImGui.Spacing();

        var macros = GetRoleMacros();
        var displayed = macros.Take(4).ToList();
        if (displayed.Count == 0)
        {
            ImGui.TextDisabled("No macros set up.");
        }
        else
        {
            float fullW = ImGui.GetContentRegionAvail().X;
            float halfW = (fullW - ImGui.GetStyle().ItemSpacing.X) / 2f;
            for (int i = 0; i < displayed.Count; i++)
            {
                if (i % 2 == 1) ImGui.SameLine();
                if (ImGui.Button($"{displayed[i].Title}##qr{i}", new Vector2(halfW, 0)))
                    _inputBuffer = displayed[i].Text;
            }
        }
    }

    // ─── Right Panel ──────────────────────────────────────────────────────────

    private void DrawRightPanel()
    {
        if (_selectedConversation == null)
        {
            var avail = ImGui.GetContentRegionAvail();
            var label = "Select a conversation";
            var labelSize = ImGui.CalcTextSize(label);
            ImGui.SetCursorPos(new Vector2(
                (avail.X - labelSize.X) / 2f,
                (avail.Y - labelSize.Y) / 2f));
            ImGui.TextDisabled(label);
            return;
        }

        DrawConversationHeader();

        // Notes bar
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##tellnotes", "Notes about this person...", ref _notesBuffer, 256))
        {
            _selectedConversation.Notes = _notesBuffer;
            _plugin.Configuration.Save();
        }

        ImGui.Separator();

        DrawMessageArea();
        DrawInputArea();
    }

    private void DrawConversationHeader()
    {
        var conv = _selectedConversation!;
        var cfg = _plugin.Configuration;

        // Patron tier icon
        var patron = cfg.Patrons.FirstOrDefault(p => p.Name == conv.PlayerName);
        if (patron != null)
        {
            var tier = cfg.GetTier(patron);
            var icon = tier switch
            {
                PatronTier.Elite   => "\u2605",
                PatronTier.Regular => "\u25c6",
                _                  => "\u25cb",
            };
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f), icon);
            ImGui.SameLine(0, 4f);
        }

        ImGui.TextColored(new Vector4(1f, 0.7f, 0.9f, 1f), conv.PlayerName);

        // Easter egg: subtle moon icon for "Sephy" (The 13th Floor owner, requested this feature)
        if (conv.PlayerName.StartsWith("Sephy", StringComparison.OrdinalIgnoreCase))
        {
            ImGui.SameLine(0, 6f);
            ImGui.TextColored(new Vector4(0.7f, 0.6f, 1f, 0.75f), "\ud83c\udf19");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Owner of The 13th Floor\nThis window was built for her. \u2665");
        }

        // Right-aligned action buttons
        float btnW1 = 70f, btnW2 = 72f, btnW3 = 52f;
        float totalBtnW = btnW1 + btnW2 + btnW3 + ImGui.GetStyle().ItemSpacing.X * 2;
        float rightEdge = ImGui.GetContentRegionMax().X;
        ImGui.SameLine(rightEdge - totalBtnW);

        if (ImGui.Button("Session##tellSess", new Vector2(btnW1, 0)))
        {
            _plugin.SessionManager.StartCapture(conv.PlayerName);
            _plugin.SessionWindow.IsOpen = true;
        }
        ImGui.SameLine();
        if (ImGui.Button("Booking##tellBook", new Vector2(btnW2, 0)))
            _plugin.MainWindow.OpenBookingsTab();
        ImGui.SameLine();
        if (ImGui.Button("Export##tellExp", new Vector2(btnW3, 0)))
            ExportConversation(conv);

        ImGui.Separator();
    }

    private void DrawMessageArea()
    {
        var conv = _selectedConversation!;
        int msgCount = conv.Messages.Count;
        if (msgCount != _lastMessageCount)
        {
            _lastMessageCount = msgCount;
            _scrollToBottom = true;
        }

        using var msgArea = ImRaii.Child("##TellMessages", new Vector2(0, -InputRowHeight), false);
        if (!msgArea) return;

        DateTime? lastDate = null;
        bool anyMarkedRead = false;

        foreach (var msg in conv.Messages)
        {
            // Date separator
            var msgDate = msg.Timestamp.Date;
            if (lastDate != msgDate)
            {
                lastDate = msgDate;
                var dateStr = $"\u2500\u2500 {msg.Timestamp:dddd, MMM d} \u2500\u2500";
                var dateWidth = ImGui.CalcTextSize(dateStr).X;
                var available = ImGui.GetContentRegionAvail().X;
                ImGui.SetCursorPosX(MathF.Max(0, (available - dateWidth) / 2f));
                ImGui.TextDisabled(dateStr);
            }

            if (!msg.IsOutgoing)
            {
                // Incoming — left-aligned
                ImGui.TextColored(new Vector4(0.7f, 0.5f, 0.9f, 1f), msg.Sender);
                ImGui.SameLine(0, 4f);
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 0.8f), msg.Timestamp.ToString("HH:mm"));
                ImGui.TextWrapped(msg.Content);
            }
            else
            {
                // Outgoing — right-biased (indented from left)
                float indent = ImGui.GetContentRegionAvail().X * 0.25f;
                var youLabel = "You  " + msg.Timestamp.ToString("HH:mm");
                var labelW = ImGui.CalcTextSize(youLabel).X;
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - labelW);
                ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), youLabel);
                ImGui.SetCursorPosX(indent);
                ImGui.PushTextWrapPos(0);
                ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.95f, 1f), msg.Content);
                ImGui.PopTextWrapPos();
            }

            ImGui.Spacing();

            // Mark as read on draw
            if (!msg.IsRead && !msg.IsOutgoing)
            {
                msg.IsRead = true;
                anyMarkedRead = true;
            }
        }

        if (_scrollToBottom)
        {
            ImGui.SetScrollHereY(1.0f);
            _scrollToBottom = false;
        }

        if (anyMarkedRead)
            _plugin.Configuration.Save();
    }

    private void DrawInputArea()
    {
        ImGui.SetNextItemWidth(-80f);
        bool enter = ImGui.InputText("##tellinput", ref _inputBuffer, 512,
            ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((enter || ImGui.Button("Send##tellSend", new Vector2(72, 0)))
            && !string.IsNullOrWhiteSpace(_inputBuffer) && _selectedConversation != null)
        {
            SendTell();
        }
    }

    private void SendTell()
    {
        if (string.IsNullOrWhiteSpace(_inputBuffer) || _selectedConversation == null) return;
        _plugin.TellService.SendTell(_selectedConversation.PlayerName, _inputBuffer.Trim());
        _inputBuffer = string.Empty;
        _scrollToBottom = true;
    }

    private void ExportConversation(TellConversation conv)
    {
        try
        {
            var sessionDir = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "Sessions");
            Directory.CreateDirectory(sessionDir);
            var safeName = string.Concat(conv.PlayerName.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(sessionDir, $"Tells_{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.txt");

            var sb = new StringBuilder();
            sb.AppendLine($"=== Tells with: {conv.PlayerName} ===");
            sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            foreach (var msg in conv.Messages)
            {
                var who = msg.IsOutgoing ? "You" : msg.Sender;
                sb.AppendLine($"[{msg.Timestamp:HH:mm}] [{who}]: {msg.Content}");
            }

            File.WriteAllText(path, sb.ToString());
            Svc.Log.Info($"[CandyCoat] Tells exported to {path}");
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[CandyCoat] Failed to export tells: {ex.Message}");
        }
    }

    private List<MacroTemplate> GetRoleMacros()
    {
        var cfg = _plugin.Configuration;
        return cfg.PrimaryRole switch
        {
            StaffRole.Sweetheart => cfg.SweetheartMacros,
            StaffRole.CandyHeart => cfg.CandyHeartMacros,
            StaffRole.Bartender  => cfg.BartenderMacros,
            StaffRole.Greeter    => cfg.GreeterWelcomeMacros,
            _                    => cfg.Macros
        };
    }
}
