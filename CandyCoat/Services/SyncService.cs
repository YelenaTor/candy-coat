using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ECommons.DalamudServices;

namespace CandyCoat.Services;

/// <summary>
/// Manages synchronization between the plugin and the Candy Coat API.
/// Supports sleep/wake mode: dormant when UI is closed, active when open.
/// Future: multi-venue support via venue_id routing.
/// </summary>
public class SyncService : IDisposable
{
    private readonly Plugin _plugin;
    private readonly HttpClient _httpClient;

    // Sync state
    private CancellationTokenSource? _cts;
    private Task? _fastLoop;
    private Task? _slowLoop;
    private Task? _heartbeatLoop;
    private bool _isAwake = false;
    private DateTime _lastEarningsSync = DateTime.MinValue;
    private DateTime _lastNotesSync = DateTime.MinValue;

    // Synced data cache (read by panels)
    public List<SyncedRoom> Rooms { get; private set; } = new();
    public List<SyncedStaff> OnlineStaff { get; private set; } = new();
    public List<SyncedPatron> Patrons { get; private set; } = new();
    public List<SyncedPatronNote> PatronNotes { get; private set; } = new();
    public List<SyncedEarning> Earnings { get; private set; } = new();
    public List<SyncedMenuItem> Menu { get; private set; } = new();
    public List<SyncedGambaPreset> GambaPresets { get; private set; } = new();

    // Connection state
    public bool IsConnected { get; private set; } = false;
    public bool IsWaking { get; private set; } = false;
    public string? LastError { get; private set; }

    private const int FastPollMs = 3000;   // Rooms + staff
    private const int SlowPollMs = 30000;  // Earnings + notes
    private const int HeartbeatMs = 15000; // Staff heartbeat

    public SyncService(Plugin plugin)
    {
        _plugin = plugin;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Wake up: start polling loops. Called when MainWindow opens.
    /// </summary>
    public async Task WakeAsync()
    {
        if (_isAwake || !_plugin.Configuration.EnableSync) return;
        if (string.IsNullOrEmpty(_plugin.Configuration.ApiUrl)) return;

        IsWaking = true;
        _cts = new CancellationTokenSource();

        // Set auth header
        _httpClient.BaseAddress = new Uri(_plugin.Configuration.ApiUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Remove("X-Venue-Key");
        _httpClient.DefaultRequestHeaders.Add("X-Venue-Key", _plugin.Configuration.VenueKey);

        // Test connection
        try
        {
            var response = await _httpClient.GetAsync("api/health", _cts.Token);
            IsConnected = response.IsSuccessStatusCode;
            if (!IsConnected)
            {
                LastError = $"API returned {response.StatusCode}";
                IsWaking = false;
                return;
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            LastError = ex.Message;
            IsWaking = false;
            return;
        }

        // Initial data fetch
        await FetchAllAsync();

        // Start loops
        _fastLoop = RunLoopAsync(FastPollAsync, FastPollMs, _cts.Token);
        _slowLoop = RunLoopAsync(SlowPollAsync, SlowPollMs, _cts.Token);
        _heartbeatLoop = RunLoopAsync(HeartbeatAsync, HeartbeatMs, _cts.Token);

        _isAwake = true;
        IsWaking = false;
        LastError = null;
        Svc.Log.Info("[SyncService] Awake and connected.");
    }

    /// <summary>
    /// Sleep: stop all polling. Called when MainWindow closes.
    /// </summary>
    public void Sleep()
    {
        if (!_isAwake) return;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _isAwake = false;
        IsConnected = false;
        Svc.Log.Info("[SyncService] Sleeping.");
    }

    private async Task FetchAllAsync()
    {
        try
        {
            Rooms = await GetAsync<List<SyncedRoom>>("api/rooms") ?? new();
            OnlineStaff = await GetAsync<List<SyncedStaff>>("api/staff/online") ?? new();
            Patrons = await GetAsync<List<SyncedPatron>>("api/patrons") ?? new();
            PatronNotes = await GetAsync<List<SyncedPatronNote>>("api/notes") ?? new();
            Earnings = await GetAsync<List<SyncedEarning>>("api/earnings") ?? new();
            Menu = await GetAsync<List<SyncedMenuItem>>("api/menu") ?? new();
            GambaPresets = await GetAsync<List<SyncedGambaPreset>>("api/gamba/presets") ?? new();
            _lastEarningsSync = DateTime.UtcNow;
            _lastNotesSync = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SyncService] FetchAll failed: {ex.Message}");
            IsConnected = false;
        }
    }

    private async Task FastPollAsync()
    {
        try
        {
            Rooms = await GetAsync<List<SyncedRoom>>("api/rooms") ?? Rooms;
            OnlineStaff = await GetAsync<List<SyncedStaff>>("api/staff/online") ?? OnlineStaff;
            IsConnected = true;
            LastError = null;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            LastError = ex.Message;
        }
    }

    private async Task SlowPollAsync()
    {
        try
        {
            var sinceEarnings = _lastEarningsSync.ToString("o");
            var sinceNotes = _lastNotesSync.ToString("o");

            var newEarnings = await GetAsync<List<SyncedEarning>>($"api/earnings?since={sinceEarnings}");
            if (newEarnings?.Count > 0)
            {
                Earnings.AddRange(newEarnings);
                _lastEarningsSync = DateTime.UtcNow;
            }

            var newNotes = await GetAsync<List<SyncedPatronNote>>($"api/notes?since={sinceNotes}");
            if (newNotes?.Count > 0)
            {
                PatronNotes.AddRange(newNotes);
                _lastNotesSync = DateTime.UtcNow;
            }

            Patrons = await GetAsync<List<SyncedPatron>>("api/patrons") ?? Patrons;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[SyncService] SlowPoll failed: {ex.Message}");
        }
    }

    private async Task HeartbeatAsync()
    {
        try
        {
            var heartbeat = new
            {
                CharacterName = _plugin.Configuration.CharacterName,
                HomeWorld = _plugin.Configuration.HomeWorld,
                Role = _plugin.Configuration.PrimaryRole.ToString(),
                IsDnd = false, // TODO: wire to panel DND state
                ShiftStart = (DateTime?)null,
            };
            await PostAsync("api/staff/heartbeat", heartbeat);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[SyncService] Heartbeat failed: {ex.Message}");
        }
    }

    // ─── Public write methods (panels call these) ───

    public async Task UpdateRoomAsync(SyncedRoom room) =>
        await PutAsync($"api/rooms/{room.Id}", room);

    public async Task CreateRoomAsync(SyncedRoom room) =>
        await PostAsync("api/rooms", room);

    public async Task DeleteRoomAsync(Guid roomId) =>
        await DeleteAsync($"api/rooms/{roomId}");

    public async Task LogEarningAsync(SyncedEarning earning) =>
        await PostAsync("api/earnings", earning);

    public async Task UpsertPatronAsync(SyncedPatron patron) =>
        await PostAsync("api/patrons", patron);

    public async Task AddPatronNoteAsync(SyncedPatronNote note) =>
        await PostAsync("api/notes", note);

    public async Task UpdateMenuAsync(List<SyncedMenuItem> menu) =>
        await PutAsync("api/menu", menu);

    public async Task UpdateGambaPresetsAsync(List<SyncedGambaPreset> presets) =>
        await PutAsync("api/gamba/presets", presets);

    public async Task ToggleDndAsync(string characterName, bool isDnd) =>
        await PostAsync("api/staff/dnd", new { CharacterName = characterName, IsDnd = isDnd });

    // ─── HTTP helpers ───

    private async Task<T?> GetAsync<T>(string path)
    {
        var response = await _httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(json);
    }

    private async Task PostAsync<T>(string path, T body)
    {
        var json = JsonConvert.SerializeObject(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(path, content);
        response.EnsureSuccessStatusCode();
    }

    private async Task PutAsync<T>(string path, T body)
    {
        var json = JsonConvert.SerializeObject(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(path, content);
        response.EnsureSuccessStatusCode();
    }

    private async Task DeleteAsync(string path)
    {
        var response = await _httpClient.DeleteAsync(path);
        response.EnsureSuccessStatusCode();
    }

    private static async Task RunLoopAsync(Func<Task> action, int intervalMs, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(intervalMs, ct);
                await action();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[SyncService] Loop error: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        Sleep();
        _httpClient.Dispose();
    }
}

// ─── Sync DTOs (matching API entity shapes) ───

public class SyncedRoom
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Available";
    public string OccupiedBy { get; set; } = string.Empty;
    public string PatronName { get; set; } = string.Empty;
    public DateTime? OccupiedSince { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncedStaff
{
    public string CharacterName { get; set; } = string.Empty;
    public string HomeWorld { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public bool IsDnd { get; set; }
    public DateTime? ShiftStart { get; set; }
}

public class SyncedPatron
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public string Status { get; set; } = "Neutral";
    public int VisitCount { get; set; }
    public int TotalGilSpent { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string RpHooks { get; set; } = string.Empty;
    public string FavoriteDrink { get; set; } = string.Empty;
    public string Allergies { get; set; } = string.Empty;
    public string BlacklistReason { get; set; } = string.Empty;
    public DateTime? BlacklistDate { get; set; }
    public string BlacklistFlaggedBy { get; set; } = string.Empty;
    public DateTime? LastSeen { get; set; }
}

public class SyncedPatronNote
{
    public Guid Id { get; set; }
    public string PatronName { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SyncedEarning
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string PatronName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SyncedMenuItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class SyncedGambaPreset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Rules { get; set; } = string.Empty;
    public string AnnounceMacro { get; set; } = string.Empty;
    public float DefaultMultiplier { get; set; } = 2.0f;
}
