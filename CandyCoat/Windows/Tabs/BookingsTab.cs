using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using OtterGui.Widgets;
using Dalamud.Interface.Utility.Raii;
using CandyCoat.Data;
using CandyCoat.Services;

namespace CandyCoat.Windows.Tabs;

public class BookingsTab : ITab
{
    private readonly Plugin _plugin;
    private readonly VenueService _venueService;
    
    // Booking Inputs
    private string newBookingName = string.Empty;
    private string newBookingService = string.Empty;
    private string newBookingRoom = string.Empty;
    private int newBookingGil = 0;

    public Action<Patron>? OnPatronSelected { get; set; }
    public Patron? SelectedPatron { get; set; }

    public string Name => "Bookings";

    public BookingsTab(Plugin plugin, VenueService venueService)
    {
        _plugin = plugin;
        _venueService = venueService;
    }

    public void Draw()
    {
        using var tab = ImRaii.TabItem(Name);
        if (!tab) return;
        DrawContent();
    }

    public void DrawContent()
    {
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
                _venueService.AddBooking(newBookingName, newBookingService, newBookingRoom, newBookingGil);
                
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

        DrawBookingsTable();
    }

    private void DrawBookingsTable()
    {
        // Booking List
        using var table = ImRaii.Table("BookingsTable", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable);
        if (!table) return;

        ImGui.TableSetupColumn("Patron", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Service", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Room", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("Gil", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("Time Left", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, 100f);
        ImGui.TableHeadersRow();

        foreach (var booking in _plugin.Configuration.Bookings.ToArray())
        {
            ImGui.TableNextRow();
            
            ImGui.TableNextColumn();
            if (ImGui.Selectable(booking.PatronName, SelectedPatron?.Name == booking.PatronName))
            {
                var patron = _venueService.EnsurePatronExists(booking.PatronName);
                SelectedPatron = patron;
                OnPatronSelected?.Invoke(patron);
            }
            
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(booking.Service);
            
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(booking.Room);
            
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{booking.Gil:N0}");

            ImGui.TableNextColumn();
            if (booking.State == BookingState.Active)
            {
                var timeRemaining = booking.EndTime - DateTime.Now;
                
                if (timeRemaining.TotalMinutes <= 0)
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "OVERDUE");
                }
                else if (timeRemaining.TotalMinutes <= 5)
                {
                    // Pulse animation for <= 5 minutes
                    float t = (float)(ImGui.GetTime() * 2.0f);
                    float pulse = (float)(Math.Sin(t) * 0.5f + 0.5f);
                    ImGui.TextColored(new Vector4(1.0f, pulse, pulse, 1.0f), $"{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}");

                    // Optional: If exactly 5 minutes remaining (within a 1 second window), play a sound or log ping
                    if (timeRemaining.TotalMinutes > 4.98 && timeRemaining.TotalMinutes <= 5.0)
                    {
                        // In Dalamud, UIGlobals.PlaySound(Chime) is often used, but to keep it compiling safely
                        // across versions, a Chat/Toast or standard Log is reliable.
                        ECommons.DalamudServices.Svc.Chat.Print($"[Candy Coat Alert] {booking.PatronName}'s session ends in 5 minutes!");
                    }
                }
                else
                {
                    ImGui.TextUnformatted($"{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}");
                }
            }
            else
            {
                ImGui.TextDisabled("--:--");
            }
            
            ImGui.TableNextColumn();
            var stateColor = booking.State switch
            {
                BookingState.Active => new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                BookingState.Inactive => new Vector4(0.5f, 0.5f, 0.5f, 1.0f),
                BookingState.CompletedPaid => new Vector4(0.0f, 0.8f, 1.0f, 1.0f),
                BookingState.CompletedUnpaid => new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
                _ => Vector4.One
            };
            ImGui.TextColored(stateColor, booking.State.ToString());

            // Context Menu to change state or delete
            if (ImGui.BeginPopupContextItem($"BookingContext{booking.Id}"))
            {
                if (ImGui.Selectable("Mark Active")) { _venueService.UpdateBookingState(booking, BookingState.Active); }
                if (ImGui.Selectable("Mark Completed (Paid)")) { _venueService.UpdateBookingState(booking, BookingState.CompletedPaid); }
                if (ImGui.Selectable("Mark Completed (Unpaid)")) { _venueService.UpdateBookingState(booking, BookingState.CompletedUnpaid); }
                if (ImGui.Selectable("Mark Inactive")) { _venueService.UpdateBookingState(booking, BookingState.Inactive); }
                ImGui.Separator();
                if (ImGui.Selectable("Delete Booking")) 
                { 
                    _venueService.RemoveBooking(booking);
                }
                ImGui.EndPopup();
            }
        }
    }
}
