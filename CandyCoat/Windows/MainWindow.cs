using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui.Widgets;
using ECommons.DalamudServices;

namespace CandyCoat.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string goatImagePath;
    private readonly Plugin plugin;

    private readonly CandyCoat.IPC.GlamourerIpc glamourer;
    private CandyCoat.Data.Patron? selectedPatron = null;

    public MainWindow(Plugin plugin, string goatImagePath)
        : base("Candy Coat - Sugar##CandyCoatMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.goatImagePath = goatImagePath;
        this.plugin = plugin;
        this.glamourer = new CandyCoat.IPC.GlamourerIpc();
    }

    public void Dispose()
    {
        glamourer.Dispose();
    }

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("CandyCoatTabBar");
        if (!tabBar) return;

        DrawOverviewTab();
        DrawBookingsTab();
        DrawLocatorTab();
        DrawSessionTab();

        if (selectedPatron != null)
        {
            DrawPatronDetails();
        }
    }

    // ... (Existing DrawOverviewTab code) ...
    private void DrawBookingsTab()
    {
        using var tab = ImRaii.TabItem("Bookings");
        if (!tab) return;

        ImGui.TextUnformatted("New Booking");
        ImGui.Separator();

        // Input Form
        var inputWidth = 200f;
        
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Patron:");
        ImGui.SameLine(80);
        ImGui.SetNextItemWidth(inputWidth);
        ImGui.InputText("##NewBookingName", ref newBookingName, 100);
        
        ImGui.SameLine();
        ImGui.Text("Service:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(inputWidth);
        ImGui.InputText("##NewBookingService", ref newBookingService, 100);

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Room:");
        ImGui.SameLine(80);
        ImGui.SetNextItemWidth(inputWidth);
        ImGui.InputText("##NewBookingRoom", ref newBookingRoom, 50);

        ImGui.SameLine();
        ImGui.Text("Gil:");
        ImGui.SameLine(368); // Align with Service input
        ImGui.SetNextItemWidth(inputWidth);
        ImGui.InputInt("##NewBookingGil", ref newBookingGil, 0);

        if (ImGui.Button("Add Booking"))
        {
            if (!string.IsNullOrWhiteSpace(newBookingName))
            {
                var newBooking = new SamplePlugin.Data.Booking
                {
                    PatronName = newBookingName,
                    Service = newBookingService,
                    Room = newBookingRoom,
                    Gil = newBookingGil,
                    Timestamp = DateTime.Now,
                    State = SamplePlugin.Data.BookingState.Active
                };
                plugin.Configuration.Bookings.Add(newBooking);
                plugin.Configuration.Save();
                
                // Reset inputs
                newBookingName = string.Empty;
                newBookingService = string.Empty;
                newBookingRoom = string.Empty;
                newBookingGil = 0;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Booking List
        using var table = ImRaii.Table("BookingsTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable);
        if (!table) return;

        ImGui.TableSetupColumn("Patron", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Service", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Room", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("Gil", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, 100f);
        ImGui.TableHeadersRow();

        foreach (var booking in plugin.Configuration.Bookings)
        {
            ImGui.TableNextRow();
            
            ImGui.TableNextColumn();
            if (ImGui.Selectable(booking.PatronName, selectedPatron?.Name == booking.PatronName))
            {
                // Find or create patron
                var patron = plugin.Configuration.Patrons.Find(p => p.Name == booking.PatronName);
                if (patron == null)
                {
                    // Assuming 'PatronName' might be 'Name' or 'Name @World' in future?
                    // For now, initializing World as empty or separate input would be better.
                    // But to keep it simple and compile, we just set a default.
                    // TODO: Parse World from input or ask user.
                    patron = new CandyCoat.Data.Patron { Name = booking.PatronName, World = "Unknown" };
                    plugin.Configuration.Patrons.Add(patron);
                    plugin.Configuration.Save();
                }
                selectedPatron = patron;
            }
            
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(booking.Service);
            
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(booking.Room);
            
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{booking.Gil:N0}");
            
            ImGui.TableNextColumn();
            var stateColor = booking.State switch
            {
                CandyCoat.Data.BookingState.Active => new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                CandyCoat.Data.BookingState.Inactive => new Vector4(0.5f, 0.5f, 0.5f, 1.0f),
                CandyCoat.Data.BookingState.CompletedPaid => new Vector4(0.0f, 0.8f, 1.0f, 1.0f),
                CandyCoat.Data.BookingState.CompletedUnpaid => new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
                _ => Vector4.One
            };
            ImGui.TextColored(stateColor, booking.State.ToString());

            // Context Menu to change state or delete
            if (ImGui.BeginPopupContextItem($"BookingContext{booking.Id}"))
            {
                if (ImGui.Selectable("Mark Active")) { booking.State = CandyCoat.Data.BookingState.Active; plugin.Configuration.Save(); }
                if (ImGui.Selectable("Mark Completed (Paid)")) { booking.State = CandyCoat.Data.BookingState.CompletedPaid; plugin.Configuration.Save(); }
                if (ImGui.Selectable("Mark Completed (Unpaid)")) { booking.State = CandyCoat.Data.BookingState.CompletedUnpaid; plugin.Configuration.Save(); }
                if (ImGui.Selectable("Mark Inactive")) { booking.State = CandyCoat.Data.BookingState.Inactive; plugin.Configuration.Save(); }
                ImGui.Separator();
                if (ImGui.Selectable("Delete Booking")) 
                { 
                    plugin.Configuration.Bookings.Remove(booking); 
                    plugin.Configuration.Save();
                    ImGui.EndPopup(); 
                    break; // Break to avoid collection modification exception
                }
                ImGui.EndPopup();
            }
        }
    }

    private void DrawLocatorTab()
    {
        using var tab = ImRaii.TabItem("Locator");
        if (!tab) return;

        ImGui.TextUnformatted("Patron Locator");
        ImGui.Spacing();

        // Simple Add Patron for testing
        ImGui.InputText("Name to Track", ref newPatronName, 100);
        ImGui.SameLine();
        if (ImGui.Button("Track"))
        {
            if (!string.IsNullOrWhiteSpace(newPatronName) && !plugin.Configuration.Patrons.Exists(p => p.Name == newPatronName))
            {
                plugin.Configuration.Patrons.Add(new CandyCoat.Data.Patron { Name = newPatronName, World = "Unknown", IsFavorite = true });
                plugin.Configuration.Save();
                newPatronName = string.Empty;
            }
        }

        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Nearby Favorites:");
        
        // Scan for patrons
        // In a real scenario, this scanning might be better done in a framework update loop, 
        // but for a simple UI display, iterating here is acceptable for small lists.
        var nearbyPlayers = Svc.Objects;
        bool foundAny = false;

        foreach (var player in nearbyPlayers)
        {
            var patron = plugin.Configuration.Patrons.Find(p => p.Name == player.Name.ToString());
            if (patron != null && patron.IsFavorite)
            {
                foundAny = true;
                var distance = Vector3.Distance(Svc.ClientState.LocalPlayer?.Position ?? Vector3.Zero, player.Position);
                ImGui.TextColored(new Vector4(1f, 0.5f, 0.8f, 1f), $"♥ {patron.Name} is here! ({distance:F1}m away)");
            }
        }

        if (!foundAny)
        {
            ImGui.TextDisabled("No favorite patrons nearby.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Tracked Patrons:");
        foreach (var p in plugin.Configuration.Patrons)
        {
            if (ImGui.Selectable($"- {p.Name}##{p.Name}", selectedPatron == p))
            {
                selectedPatron = p;
            }

            if (ImGui.BeginPopupContextItem($"PatronContext{p.Name}"))
            {
                if (ImGui.Selectable("Remove"))
                {
                    plugin.Configuration.Patrons.Remove(p);
                    plugin.Configuration.Save();
                    if (selectedPatron == p) selectedPatron = null;
                }
                ImGui.EndPopup();
            }
        }
    }

    private void DrawPatronDetails()
    {
        if (selectedPatron == null) return;

        bool open = true;
        // Open a separate window or a modal? Or just a panel below?
        // Let's use a "child" window that pops over the bottom or is a separate window.
        // For simplicity in this layout, let's append it to the bottom or use a new Window if we could.
        // But since we are inside Draw(), let's make it a nice overlay or section.
        
        ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.FirstUseEver);
        if (ImGui.Begin($"Patron Details: {selectedPatron.Name}###PatronDetails", ref open))
        {
            using var subTabBar = ImRaii.TabBar("PatronDetailsTabs");
            if (subTabBar)
            {
                using (var infoTab = ImRaii.TabItem("Info"))
                {
                    if (infoTab)
                    {
                        var notes = selectedPatron.Notes;
                        if (ImGui.InputTextMultiline("Notes", ref notes, 2000, new Vector2(-1, 100)))
                        {
                            selectedPatron.Notes = notes;
                            plugin.Configuration.Save();
                        }

                        var hooks = selectedPatron.RpHooks;
                        if (ImGui.InputTextMultiline("RP Hooks", ref hooks, 2000, new Vector2(-1, 100)))
                        {
                            selectedPatron.RpHooks = hooks;
                            plugin.Configuration.Save();
                        }
                    }
                }

                using (var glamTab = ImRaii.TabItem("Glamour"))
                {
                    if (glamTab)
                    {
                        // Optimization: Cache design list once for this frame/tab draw
                        var allDesigns = glamourer.GetDesignList();

                        ImGui.Text("Assigned Outfits (Quick Swap)");
                        foreach (var designId in selectedPatron.QuickSwitchDesignIds.ToArray())
                        {
                             var name = allDesigns.TryGetValue(designId, out var designName) ? designName : designId.ToString();
                             
                             if (ImGui.Button($"Apply: {name}"))
                             {
                                 glamourer.ApplyDesign(designId);
                             }
                             ImGui.SameLine();
                             if (ImGui.Button($"Unlink##{designId}"))
                             {
                                 selectedPatron.QuickSwitchDesignIds.Remove(designId);
                                 plugin.Configuration.Save();
                             }
                        }

                        ImGui.Separator();
                        ImGui.Text("All Designs");
                        
                        // Use the cached list here too
                        foreach (var kvp in allDesigns)
                        {
                            if (ImGui.Selectable(kvp.Value))
                            {
                                if (!selectedPatron.QuickSwitchDesignIds.Contains(kvp.Key))
                                {
                                    selectedPatron.QuickSwitchDesignIds.Add(kvp.Key);
                                    plugin.Configuration.Save();
                                }
                            }
                        }
                    }
                }
            }
        }
        ImGui.End();

        if (!open) selectedPatron = null;
    }

    public void OpenBookingsTab()
    {
        IsOpen = true;
        // Switch tab logic
    }

    private void DrawOverviewTab()
    {
        using var tab = ImRaii.TabItem("Overview");
        if (!tab) return;

        ImGui.TextUnformatted("Welcome to Candy Coat!");
        ImGui.Spacing();
        
        ImGui.TextWrapped("Your venue assistant is ready.");
        ImGui.Spacing();
        
        if (ImGui.Button("Open Settings"))
        {
            plugin.ToggleConfigUi();
        }
    }

    // Booking Inputs
    private string newBookingName = string.Empty;
    private string newBookingService = string.Empty;
    private string newBookingRoom = string.Empty;
    private int newBookingGil = 0;

    private string newPatronName = string.Empty;
    private string _manualTargetName = string.Empty;

    private void DrawSessionTab()
    {
        using var tab = ImRaii.TabItem("Session Capture");
        if (!tab) return;

        ImGui.TextUnformatted("Session Capture Control");
        ImGui.Separator();

        var manager = plugin.SessionManager;

        if (manager.IsCapturing)
        {
            ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), $"Capturing: {manager.TargetName}");
            
            if (ImGui.Button("Stop Capture"))
            {
                manager.StopCapture();
            }
            
            ImGui.SameLine();
            ImGui.TextDisabled("(Session Window should be open)");
        }
        else
        {
            ImGui.Text("Target Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("##ManualSessionTarget", ref _manualTargetName, 100);

            if (ImGui.Button("Start Capture"))
            {
                if (!string.IsNullOrWhiteSpace(_manualTargetName))
                {
                    manager.StartCapture(_manualTargetName);
                    // Ensure window is open
                    var win = plugin.WindowSystem.GetWindow("Candy Session##CandySessionWindow");
                    if (win != null) win.IsOpen = true;
                }
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Use Current Target"))
            {
                var target = Svc.Targets.Target;
                if (target is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc)
                {
                    _manualTargetName = pc.Name.ToString();
                }
            }
        }
        
        ImGui.Separator();
        ImGui.TextWrapped("Note: You can also right-click a player in Chat to start a session.");
    }
}
