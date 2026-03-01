using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ECommons.DalamudServices;
using CandyCoat.Data;

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
    public ConcurrentDictionary<string, CosmeticProfile> Cosmetics { get; } = new();
    public List<SyncedBooking> Bookings { get; private set; } = new();

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
        if (_isAwake || IsWaking) return;
        // DEV: hardcoded to localhost while self-hosted; re-enable ApiUrl check when permanently deployed
        // if (string.IsNullOrEmpty(_plugin.Configuration.ApiUrl)) return;

        IsWaking = true;
        _cts = new CancellationTokenSource();

        // DEV: hardcoded base URL; swap to _plugin.Configuration.ApiUrl when hosted
        _httpClient.BaseAddress = new Uri("http://localhost:5000/");
        // DEV: auth disabled — remove these comments and restore the key header when hosted
        // _httpClient.DefaultRequestHeaders.Remove("X-Venue-Key");
        // _httpClient.DefaultRequestHeaders.Add("X-Venue-Key", _plugin.Configuration.VenueKey);

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

        // Unblock the UI immediately — data arrives on the first poll tick
        _isAwake = true;
        IsWaking = false;
        LastError = null;
        Svc.Log.Info("[SyncService] Awake and connected.");

        // Start loops (FastPoll fires within 3 s and fills all data)
        _fastLoop      = RunLoopAsync(FastPollAsync,  FastPollMs,  _cts.Token);
        _slowLoop      = RunLoopAsync(SlowPollAsync,  SlowPollMs,  _cts.Token);
        _heartbeatLoop = RunLoopAsync(HeartbeatAsync, HeartbeatMs, _cts.Token);

        // Background initial fetch so first open isn't empty for 3 s
        _ = FetchAllAsync();
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
        var ct = _cts?.Token ?? CancellationToken.None;
        try
        {
            Rooms = await GetAsync<List<SyncedRoom>>("api/rooms", ct) ?? new();
            OnlineStaff = await GetAsync<List<SyncedStaff>>("api/staff/online", ct) ?? new();
            Patrons = await GetAsync<List<SyncedPatron>>("api/patrons", ct) ?? new();
            PatronNotes = await GetAsync<List<SyncedPatronNote>>("api/notes", ct) ?? new();
            Earnings = await GetAsync<List<SyncedEarning>>("api/earnings", ct) ?? new();
            Menu = await GetAsync<List<SyncedMenuItem>>("api/menu", ct) ?? new();
            GambaPresets = await GetAsync<List<SyncedGambaPreset>>("api/gamba/presets", ct) ?? new();
            Bookings = await GetAsync<List<SyncedBooking>>("api/bookings", ct) ?? new();
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
        var ct = _cts?.Token ?? CancellationToken.None;
        try
        {
            Rooms = await GetAsync<List<SyncedRoom>>("api/rooms", ct) ?? Rooms;
            OnlineStaff = await GetAsync<List<SyncedStaff>>("api/staff/online", ct) ?? OnlineStaff;

            var newCosmetics = await GetAsync<List<SyncedCosmeticEnvelope>>("api/cosmetics", ct);
            if (newCosmetics != null)
            {
                foreach (var env in newCosmetics)
                {
                    var profile = await TryDecompressCosmeticAsync(env.BrotliBlob);
                    if (profile != null) Cosmetics[env.CharacterHash] = profile;
                }
            }

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
        var ct = _cts?.Token ?? CancellationToken.None;
        try
        {
            var sinceEarnings = _lastEarningsSync.ToString("o");
            var sinceNotes = _lastNotesSync.ToString("o");

            var newEarnings = await GetAsync<List<SyncedEarning>>($"api/earnings?since={sinceEarnings}", ct);
            if (newEarnings?.Count > 0)
            {
                var updatedEarnings = new List<SyncedEarning>(Earnings);
                updatedEarnings.AddRange(newEarnings);
                Earnings = updatedEarnings;
                _lastEarningsSync = DateTime.UtcNow;
            }

            var newNotes = await GetAsync<List<SyncedPatronNote>>($"api/notes?since={sinceNotes}", ct);
            if (newNotes?.Count > 0)
            {
                var updatedNotes = new List<SyncedPatronNote>(PatronNotes);
                updatedNotes.AddRange(newNotes);
                PatronNotes = updatedNotes;
                _lastNotesSync = DateTime.UtcNow;
            }

            Patrons = await GetAsync<List<SyncedPatron>>("api/patrons", ct) ?? Patrons;
            Bookings = await GetAsync<List<SyncedBooking>>("api/bookings", ct) ?? Bookings;
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

    public async Task UpsertBookingAsync(SyncedBooking booking) =>
        await PostAsync("api/bookings", booking);

    public async Task DeleteBookingAsync(Guid bookingId) =>
        await DeleteAsync($"api/bookings/{bookingId}");

    public async Task PushCosmeticsAsync(CosmeticProfile profile)
    {
        try
        {
            var json = JsonConvert.SerializeObject(profile);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            using var ms = new System.IO.MemoryStream();
            using (var bs = new System.IO.Compression.BrotliStream(ms, System.IO.Compression.CompressionLevel.Optimal))
            {
                await bs.WriteAsync(bytes, 0, bytes.Length);
            }
            var compressedBytes = ms.ToArray();
            
            var name = _plugin.Configuration.CharacterName ?? "Unknown";
            var world = _plugin.Configuration.HomeWorld ?? "Unknown";
            var hash = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{name}@{world}"));
            
            var payload = new SyncedCosmeticEnvelope
            {
                CharacterHash = hash,
                BrotliBlob = compressedBytes,
                LastUpdatedUtc = DateTime.UtcNow
            };
            
            await PostAsync("api/cosmetics", payload);
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SyncService] PushCosmeticsAsync failed: {ex.Message}");
        }
    }

    // ─── Private helpers ───

    private static async Task<CosmeticProfile?> TryDecompressCosmeticAsync(byte[] blob)
    {
        try
        {
            using var ms = new System.IO.MemoryStream(blob);
            using var bs = new System.IO.Compression.BrotliStream(ms, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new System.IO.StreamReader(bs);
            var json = await reader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<CosmeticProfile>(json);
        }
        catch { return null; }
    }

    // ─── HTTP helpers ───

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonConvert.DeserializeObject<T>(json);
    }

    private async Task SendBodyAsync<T>(HttpMethod method, string path, T body)
    {
        var json = JsonConvert.SerializeObject(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(method, path) { Content = content };
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private Task PostAsync<T>(string path, T body) => SendBodyAsync(HttpMethod.Post, path, body);
    private Task PutAsync<T>(string path, T body)  => SendBodyAsync(HttpMethod.Put,  path, body);

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

    /// <summary>
    /// Fire-and-forget upsert of a character profile to the global profiles table.
    /// Non-fatal: logs a warning on failure.
    /// </summary>
    public void UpsertProfileAsync(string profileId, string characterName, string homeWorld, string mode,
        bool hasGlamourer = false, bool hasChatTwo = false)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var body = new
                {
                    profileId,
                    characterName,
                    homeWorld,
                    mode,
                    hasGlamourerIntegrated = hasGlamourer,
                    hasChatTwoIntegrated = hasChatTwo
                };
                await PostAsync("api/profile", body);
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[SyncService] UpsertProfileAsync failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Fire-and-forget upsert of venue config (manager password) to the API.
    /// Non-fatal: logs a warning on failure.
    /// </summary>
    public void UpsertVenueConfigAsync(string managerPw)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await PutAsync("api/config", new { managerPw });
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[SyncService] UpsertVenueConfigAsync failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// One-shot unauthenticated health check during setup wizard.
    /// Returns true if the API responds successfully.
    /// </summary>
    public static async Task<bool> CheckHealthAsync(string apiUrl)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"{apiUrl.TrimEnd('/')}/api/health";
            var response = await client.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// One-shot unauthenticated profile lookup during setup wizard.
    /// Creates a temporary HttpClient — do not call in a hot loop.
    /// Returns null if not found or on error.
    /// </summary>
    public static async Task<GlobalProfileLookupResult?> LookupProfileAsync(string apiUrl, string profileId)
    {
        if (string.IsNullOrWhiteSpace(apiUrl) || string.IsNullOrWhiteSpace(profileId))
            return null;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = $"{apiUrl.TrimEnd('/')}/api/profile/{Uri.EscapeDataString(profileId)}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<GlobalProfileLookupResult>(json);
        }
        catch
        {
            return null;
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

public class SyncedBooking
{
    public Guid Id { get; set; }
    public string PatronName { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public int Gil { get; set; }
    public string State { get; set; } = "Active";
    public string StaffName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SyncedCosmeticEnvelope
{
    public string CharacterHash { get; set; } = string.Empty;
    public byte[] BrotliBlob { get; set; } = Array.Empty<byte>();
    public DateTime LastUpdatedUtc { get; set; }
}

public class GlobalProfileLookupResult
{
    public string ProfileId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string HomeWorld { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public bool HasGlamourerIntegrated { get; set; }
    public bool HasChatTwoIntegrated { get; set; }
}
