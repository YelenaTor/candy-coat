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

/// <summary>
/// Discord-style two-panel tell window.
/// Left sidebar: conversation list as Una.Drawing nodes.
/// Right panel: Una.Drawing shell with ImGui overlays for message thread and input.
/// </summary>
public class TellWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

    private string _filter = string.Empty;
    private TellConversation? _selectedConversation;
    private string _inputBuffer = string.Empty;
    private string _notesBuffer = string.Empty;
    private bool _scrollToBottom;
    private int _lastMessageCount;

    private const float SidebarWidth  = 160f;
    private const float QuickReplyH   = 36f;
    private const float InputRowH     = 38f;
    private const float NotesRowH     = 28f;

    // Una.Drawing root — rebuilt every Draw() since conversation list is dynamic.
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
        Size          = new Vector2(700, 500);
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

    // ─── Root build ───────────────────────────────────────────────────────────

    private void BuildRoot()
    {
        _root?.Dispose();

        var cfg    = _plugin.Configuration;
        var sorted = cfg.TellHistory
            .Where(c => string.IsNullOrEmpty(_filter) ||
                        c.PlayerName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastActivity)
            .ToList();

        // ── Left sidebar ──────────────────────────────────────────────────────
        var sidebar = new Node
        {
            Id    = "tell-sidebar",
            Style = new Style
            {
                Size            = new Size((int)SidebarWidth, 0),
                AutoSize        = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Grow),
                Flow            = Flow.Vertical,
                BackgroundColor = new Color(CandyTheme.BgSidebar),
                Gap             = 2,
                Padding         = new EdgeSize(6),
            },
        };

        // Filter input spacer (ImGui overlay)
        var filterSpacer = CandyUI.InputSpacer("tell-conv-filter", (int)SidebarWidth - 12, 26);
        sidebar.AppendChild(filterSpacer);
        sidebar.AppendChild(CandyUI.Separator("tell-sidebar-sep"));

        // Conversation items
        var convList = new Node
        {
            Id    = "tell-conv-list",
            Style = new Style
            {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
                Flow     = Flow.Vertical,
                Gap      = 2,
            },
        };

        bool listDirty = false;
        foreach (var conv in sorted)
        {
            bool isActive = _selectedConversation?.PlayerName == conv.PlayerName;
            convList.AppendChild(BuildConvItem(conv, isActive, ref listDirty));
        }

        if (sorted.Count == 0)
        {
            convList.AppendChild(CandyUI.Muted("tell-no-convs", "No conversations yet."));
        }

        sidebar.AppendChild(convList);

        // ── Right panel ───────────────────────────────────────────────────────
        var content = new Node
        {
            Id    = "tell-content",
            Style = new Style
            {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
                Flow     = Flow.Vertical,
                Padding  = new EdgeSize(8),
                Gap      = 4,
            },
        };

        if (_selectedConversation == null)
        {
            content.AppendChild(new Node
            {
                Id        = "tell-empty-label",
                NodeValue = "Select a conversation",
                Style     = new Style
                {
                    AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
                    Color     = new Color(CandyTheme.TextMuted),
                    FontSize  = 13,
                    TextAlign = Anchor.MiddleCenter,
                },
            });
        }
        else
        {
            content.AppendChild(BuildRightHeader(_selectedConversation));
            content.AppendChild(CandyUI.InputSpacer("tell-notes-spacer", 0, (int)NotesRowH));
            content.AppendChild(CandyUI.Separator("tell-notes-sep"));

            // Message thread spacer — grows to fill
            var msgSpacer = CandyUI.InputSpacer("tell-messages-spacer", 0, 0);
            msgSpacer.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow);
            content.AppendChild(msgSpacer);

            // Quick replies row
            var macros  = GetRoleMacros().Take(4).ToList();
            var qrRow   = BuildQuickRepliesRow(macros);
            content.AppendChild(qrRow);

            // Input row: InputSpacer + Send button
            var inputSpacer = CandyUI.InputSpacer("tell-input-spacer", 0, (int)InputRowH - 6);
            inputSpacer.Style.AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit);
            var sendBtn = CandyUI.Button("tell-send-btn", "Send", SendTell);
            sendBtn.Style.Size     = new Size(60, (int)InputRowH - 6);
            sendBtn.Style.AutoSize = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Fit);
            content.AppendChild(CandyUI.Row("tell-input-row", 6, inputSpacer, sendBtn));
        }

        // Divider between sidebar and content
        var divider = new Node
        {
            Id    = "tell-divider",
            Style = new Style
            {
                Size            = new Size(1, 0),
                AutoSize        = (Una.Drawing.AutoSize.Fit, Una.Drawing.AutoSize.Grow),
                BackgroundColor = new Color(CandyTheme.BorderDivider),
            },
        };

        _root = new Node
        {
            Id    = "tell-root",
            Style = new Style
            {
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Grow),
                Flow            = Flow.Horizontal,
                BackgroundColor = new Color(CandyTheme.BgWindow),
            },
        };
        _root.AppendChild(sidebar);
        _root.AppendChild(divider);
        _root.AppendChild(content);
    }

    private Node BuildConvItem(TellConversation conv, bool isActive, ref bool listDirty)
    {
        var cfg        = _plugin.Configuration;
        var patron     = cfg.Patrons.FirstOrDefault(p => p.Name == conv.PlayerName);
        var hasUnread  = conv.UnreadCount > 0;
        var firstLetter = conv.PlayerName.Length > 0 ? conv.PlayerName[0].ToString().ToUpper() : "?";
        string convId   = "tell-conv-" + conv.PlayerName.Replace(" ", "_");

        // Avatar circle
        var avatarColor = isActive ? CandyTheme.TextAccent : CandyTheme.TextSecondary;
        var circle = new Node
        {
            Id        = $"{convId}-circle",
            NodeValue = firstLetter,
            Style     = new Style
            {
                Size            = new Size(32, 32),
                BackgroundColor = new Color(isActive ? CandyTheme.BgTabActive : CandyTheme.BgCard),
                BorderRadius    = 16,
                Color           = new Color(avatarColor),
                FontSize        = 14,
                TextAlign       = Anchor.MiddleCenter,
            },
        };

        // Name + preview column
        var lastName = conv.PlayerName;
        if (conv.IsPinned) lastName = "\u25c6 " + lastName;
        var preview  = conv.Messages.Count > 0
            ? TruncateText(conv.Messages[^1].Content, 18)
            : "";

        var nameNode = new Node
        {
            Id        = $"{convId}-name",
            NodeValue = lastName,
            Style     = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Color     = new Color(hasUnread ? CandyTheme.TextPrimary : CandyTheme.TextSecondary),
                FontSize  = 12,
                TextAlign = Anchor.MiddleLeft,
            },
        };

        var previewNode = new Node
        {
            Id        = $"{convId}-preview",
            NodeValue = preview,
            Style     = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextMuted),
                FontSize  = 10,
                TextAlign = Anchor.MiddleLeft,
            },
        };

        var nameCol = new Node
        {
            Id    = $"{convId}-col",
            Style = new Style
            {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Flow     = Flow.Vertical,
                Gap      = 1,
            },
        };
        nameCol.AppendChild(nameNode);
        nameCol.AppendChild(previewNode);

        var item = new Node
        {
            Id         = convId,
            Stylesheet = new Stylesheet([
                new Stylesheet.StyleDefinition(
                    $"#{convId}:hover",
                    new Style { BackgroundColor = new Color(CandyTheme.BgCardHover) }
                ),
            ]),
            Style = new Style
            {
                AutoSize        = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Flow            = Flow.Horizontal,
                Gap             = 6,
                Padding         = new EdgeSize(5, 4, 5, 4),
                BorderRadius    = 4,
                BackgroundColor = isActive
                    ? new Color(CandyTheme.BgTabActive)
                    : new Color(0x00000000),
            },
        };

        item.AppendChild(circle);
        item.AppendChild(nameCol);

        // Unread badge
        if (hasUnread)
        {
            var badge = new Node
            {
                Id        = $"{convId}-badge",
                NodeValue = conv.UnreadCount.ToString(),
                Style     = new Style
                {
                    Size            = new Size(20, 20),
                    BackgroundColor = new Color(CandyTheme.BtnPrimary),
                    BorderRadius    = 10,
                    Color           = new Color(CandyTheme.TextPrimary),
                    FontSize        = 10,
                    TextAlign       = Anchor.MiddleCenter,
                },
            };
            item.AppendChild(badge);
        }

        item.OnClick += _ =>
        {
            _selectedConversation = conv;
            _notesBuffer          = conv.Notes;
            _scrollToBottom       = true;
            _plugin.TellService.SelectConversation(conv);
        };

        // Right-click context menu handled in DrawOverlays via ImGui popup
        return item;
    }

    private Node BuildRightHeader(TellConversation conv)
    {
        var cfg    = _plugin.Configuration;
        var patron = cfg.Patrons.FirstOrDefault(p => p.Name == conv.PlayerName);

        var tierIcon = patron != null
            ? cfg.GetTier(patron) switch
            {
                PatronTier.Elite   => "\u2605 ",
                PatronTier.Regular => "\u25c6 ",
                _                  => "\u25cb ",
            }
            : string.Empty;

        var nameLabel = new Node
        {
            Id        = "tell-header-name",
            NodeValue = tierIcon + conv.PlayerName,
            Style     = new Style
            {
                AutoSize  = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Color     = new Color(CandyTheme.TextAccent),
                FontSize  = 14,
                TextAlign = Anchor.MiddleLeft,
            },
        };

        // Easter egg: moon icon for Sephy
        if (conv.PlayerName.StartsWith("Sephy", StringComparison.OrdinalIgnoreCase))
        {
            nameLabel.NodeValue += " \ud83c\udf19";
        }

        var sessionBtn = CandyUI.SmallButton("tell-h-session", "Session", () =>
        {
            _plugin.SessionManager.StartCapture(conv.PlayerName);
            _plugin.SessionWindow.IsOpen = true;
        });

        var exportBtn = CandyUI.SmallButton("tell-h-export", "Export",
            () => ExportConversation(conv));

        return CandyUI.Row("tell-header", 6,
            nameLabel,
            sessionBtn,
            exportBtn
        );
    }

    private Node BuildQuickRepliesRow(List<MacroTemplate> macros)
    {
        var row = new Node
        {
            Id    = "tell-quickreplies",
            Style = new Style
            {
                AutoSize = (Una.Drawing.AutoSize.Grow, Una.Drawing.AutoSize.Fit),
                Flow     = Flow.Horizontal,
                Gap      = 4,
            },
        };

        if (macros.Count == 0)
        {
            row.AppendChild(CandyUI.Muted("tell-qr-empty", "No quick replies."));
            return row;
        }

        foreach (var (macro, i) in macros.Select((m, i) => (m, i)))
        {
            var text  = macro.Text;
            var title = macro.Title;
            row.AppendChild(CandyUI.SmallButton($"tell-qr-{i}", title,
                () => _inputBuffer = text));
        }

        return row;
    }

    // ─── Draw ─────────────────────────────────────────────────────────────────

    public override void Draw()
    {
        // Sync selection from TellService
        var svcSelected = _plugin.TellService.SelectedConversation;
        if (svcSelected != null && svcSelected != _selectedConversation)
        {
            _selectedConversation = svcSelected;
            _notesBuffer          = _selectedConversation.Notes;
            _scrollToBottom       = true;
        }

        BuildRoot();

        var region = ImGui.GetContentRegionAvail();
        _root!.Style.Size = new Size((int)region.X, (int)region.Y);

        var pos = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();
        _root.Render(ImGui.GetWindowDrawList(), pos);
        ImGui.Dummy(region);

        DrawOverlays();
    }

    // ─── Overlays ─────────────────────────────────────────────────────────────

    private void DrawOverlays()
    {
        var origin = ImGui.GetWindowPos() + ImGui.GetWindowContentRegionMin();

        // Filter input (over sidebar filter spacer)
        var filterNode = _root!.QuerySelector("#tell-conv-filter");
        if (filterNode != null)
        {
            var r = filterNode.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            ImGui.SetNextItemWidth(r.Width);
            if (ImGui.InputTextWithHint("##tellfilter", "Filter...", ref _filter, 64))
            {
                // filter change rebuilds root next frame (BuildRoot is called every Draw)
            }
        }

        if (_selectedConversation == null) return;

        // Context menus for conversation items (right-click)
        DrawConvContextMenus();

        // Notes input
        var notesNode = _root!.QuerySelector("#tell-notes-spacer");
        if (notesNode != null)
        {
            var r = notesNode.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            ImGui.SetNextItemWidth(r.Width);
            if (ImGui.InputTextWithHint("##tellnotes", "Notes about this person...",
                ref _notesBuffer, 256))
            {
                _selectedConversation.Notes = _notesBuffer;
                _plugin.Configuration.Save();
            }
        }

        // Message thread (over messages spacer)
        var msgNode = _root!.QuerySelector("#tell-messages-spacer");
        if (msgNode != null)
        {
            var r = msgNode.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            using var log = ImRaii.Child("##TellMessages",
                new Vector2(r.Width, r.Height), false);
            if (log) DrawMessageThread();
        }

        // Text input (over input spacer)
        var inputNode = _root!.QuerySelector("#tell-input-spacer");
        if (inputNode != null)
        {
            var r = inputNode.Bounds.ContentRect;
            ImGui.SetCursorPos(new Vector2(r.X1 - origin.X, r.Y1 - origin.Y));
            ImGui.SetNextItemWidth(r.Width);
            if (ImGui.InputText("##tellinput", ref _inputBuffer, 512,
                ImGuiInputTextFlags.EnterReturnsTrue))
            {
                SendTell();
            }
        }

        // Sephy tooltip (easter egg)
        var headerName = _root!.QuerySelector("#tell-header-name");
        if (headerName != null && _selectedConversation.PlayerName.StartsWith("Sephy",
            StringComparison.OrdinalIgnoreCase))
        {
            var r = headerName.Bounds.ContentRect;
            var mousePos = ImGui.GetMousePos();
            if (mousePos.X >= r.X1 && mousePos.X <= r.X2 && mousePos.Y >= r.Y1 && mousePos.Y <= r.Y2)
                ImGui.SetTooltip("Owner of The 13th Floor\nThis window was built for her. \u2665");
        }
    }

    private void DrawConvContextMenus()
    {
        var cfg      = _plugin.Configuration;
        var sorted   = cfg.TellHistory.ToList();
        bool listDirty = false;

        foreach (var conv in sorted)
        {
            string popupId = $"##convCtx_{conv.PlayerName}";
            string convId  = "tell-conv-" + conv.PlayerName.Replace(" ", "_");
            var    node    = _root!.QuerySelector($"#{convId}");
            if (node == null) continue;

            var r = node.Bounds.ContentRect;
            // Detect right-click within item bounds via ImGui
            ImGui.SetCursorPos(new Vector2(
                r.X1 - (ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMin().X),
                r.Y1 - (ImGui.GetWindowPos().Y + ImGui.GetWindowContentRegionMin().Y)));
            ImGui.InvisibleButton($"##ctx_{conv.PlayerName}", new Vector2(r.Width, r.Height));
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                ImGui.OpenPopup(popupId);

            if (ImGui.BeginPopup(popupId))
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
                    listDirty = true;
                }
                ImGui.EndPopup();
            }

            if (listDirty) break;
        }
    }

    private void DrawMessageThread()
    {
        var conv = _selectedConversation!;
        int msgCount = conv.Messages.Count;
        if (msgCount != _lastMessageCount)
        {
            _lastMessageCount = msgCount;
            _scrollToBottom   = true;
        }

        DateTime? lastDate    = null;
        bool      anyMarkedRead = false;

        foreach (var msg in conv.Messages)
        {
            // Date separator
            var msgDate = msg.Timestamp.Date;
            if (lastDate != msgDate)
            {
                lastDate = msgDate;
                var dateStr   = $"\u2500\u2500 {msg.Timestamp:dddd, MMM d} \u2500\u2500";
                var dateWidth = ImGui.CalcTextSize(dateStr).X;
                var available = ImGui.GetContentRegionAvail().X;
                ImGui.SetCursorPosX(MathF.Max(0, (available - dateWidth) / 2f));
                ImGui.TextDisabled(dateStr);
            }

            if (!msg.IsOutgoing)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.5f, 0.9f, 1f), msg.Sender);
                ImGui.SameLine(0, 4f);
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 0.8f), msg.Timestamp.ToString("HH:mm"));
                ImGui.TextWrapped(msg.Content);
            }
            else
            {
                float indent   = ImGui.GetContentRegionAvail().X * 0.25f;
                var   youLabel = "You  " + msg.Timestamp.ToString("HH:mm");
                var   labelW   = ImGui.CalcTextSize(youLabel).X;
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - labelW);
                ImGui.TextColored(new Vector4(1f, 0.6f, 0.8f, 1f), youLabel);
                ImGui.SetCursorPosX(indent);
                ImGui.PushTextWrapPos(0);
                ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.95f, 1f), msg.Content);
                ImGui.PopTextWrapPos();
            }

            ImGui.Spacing();

            if (!msg.IsRead && !msg.IsOutgoing)
            {
                msg.IsRead    = true;
                anyMarkedRead = true;
            }
        }

        if (_scrollToBottom)
        {
            ImGui.SetScrollHereY(1.0f);
            _scrollToBottom = false;
        }

        if (anyMarkedRead) _plugin.Configuration.Save();
    }

    // ─── Actions ──────────────────────────────────────────────────────────────

    private void SendTell()
    {
        if (string.IsNullOrWhiteSpace(_inputBuffer) || _selectedConversation == null) return;
        _plugin.TellService.SendTell(_selectedConversation.PlayerName, _inputBuffer.Trim());
        _inputBuffer    = string.Empty;
        _scrollToBottom = true;
    }

    private void ExportConversation(TellConversation conv)
    {
        try
        {
            var sessionDir = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "Sessions");
            Directory.CreateDirectory(sessionDir);
            var safeName = string.Concat(conv.PlayerName.Split(Path.GetInvalidFileNameChars()));
            var path     = Path.Combine(sessionDir, $"Tells_{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.txt");

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

    private static string TruncateText(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "…";
}
