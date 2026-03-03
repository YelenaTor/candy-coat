using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ECommons.DalamudServices;
using CandyCoat.Data;

namespace CandyCoat.Services;

/// <summary>
/// Manages synchronization between the plugin and the Backstage API.
/// Write operations are fire-and-forget. Read caches are refreshed via fast polling.
/// </summary>
public class SyncService : IDisposable
{
    private readonly Plugin _plugin;
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _pollCts = new();
    private readonly Task _pollTask;
    private int _connectedState = 1;
    private int _consecutivePollFailures;

    private const int FastPollIntervalSeconds = 3;
    private const int OfflineFailureThreshold = 2;

    // Synced data cache
    public List<SyncedRoom> Rooms { get; private set; } = new();
    public List<SyncedStaff> OnlineStaff { get; private set; } = new();
    public List<SyncedPatron> Patrons { get; private set; } = new();
    public List<SyncedPatronNote> PatronNotes { get; private set; } = new();
    public List<SyncedEarning> Earnings { get; private set; } = new();
    public List<SyncedMenuItem> Menu { get; private set; } = new();
    public List<SyncedGambaPreset> GambaPresets { get; private set; } = new();
    public ConcurrentDictionary<string, CosmeticProfile> Cosmetics { get; } = new();
    public List<SyncedBooking> Bookings { get; private set; } = new();

    public bool IsConnected => Volatile.Read(ref _connectedState) == 1;

    private sealed class SyncedCosmeticEnvelopeRead
    {
        [JsonProperty("characterHash")]
        public string CharacterHash { get; set; } = string.Empty;

        [JsonProperty("brotliBlob")]
        public byte[] BrotliBlob { get; set; } = Array.Empty<byte>();

        [JsonProperty("lastUpdatedUtc")]
        public DateTime LastUpdatedUtc { get; set; }
    }

    public SyncService(Plugin plugin)
    {
        _plugin = plugin;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(5);

        var apiUrl = !string.IsNullOrEmpty(_plugin.Configuration.ApiUrl)
            ? _plugin.Configuration.ApiUrl
            : PluginConstants.ProductionApiUrl;
        _httpClient.BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/");

        if (!string.IsNullOrEmpty(_plugin.Configuration.VenueKey))
            _httpClient.DefaultRequestHeaders.Add("X-Venue-Key", _plugin.Configuration.VenueKey);

        _pollTask = Task.Run(() => PollFastLoopAsync(_pollCts.Token));
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
            if (!TryEncodeCosmeticProfile(profile, out var compressedBytes))
            {
                Svc.Log.Error("[SyncService] PushCosmeticsAsync failed: unable to encode cosmetic profile.");
                return;
            }
            
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

    private async Task PollFastLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await RefreshFastCachesAsync(token);
                MarkConnected();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                RegisterPollFailure(ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(FastPollIntervalSeconds), token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RefreshFastCachesAsync(CancellationToken token)
    {
        var staffTask = FetchOnlineStaffAsync(token);
        var cosmeticsTask = FetchCosmeticsAsync(token);
        await Task.WhenAll(staffTask, cosmeticsTask);

        OnlineStaff = staffTask.Result;
        ApplyCosmetics(cosmeticsTask.Result);
    }

    private async Task<List<SyncedStaff>> FetchOnlineStaffAsync(CancellationToken token)
    {
        using var response = await _httpClient.GetAsync("api/staff/online", token);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(token);
        return JsonConvert.DeserializeObject<List<SyncedStaff>>(json) ?? [];
    }

    private async Task<List<SyncedCosmeticEnvelopeRead>> FetchCosmeticsAsync(CancellationToken token)
    {
        using var response = await _httpClient.GetAsync("api/cosmetics", token);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(token);
        return JsonConvert.DeserializeObject<List<SyncedCosmeticEnvelopeRead>>(json) ?? [];
    }

    private void ApplyCosmetics(List<SyncedCosmeticEnvelopeRead> envelopes)
    {
        // Build the incoming set first — never clear the live dictionary entirely,
        // as the draw thread reads it every frame and would see empty plates.
        var incoming = new Dictionary<string, CosmeticProfile>(envelopes.Count);
        foreach (var envelope in envelopes)
        {
            if (string.IsNullOrWhiteSpace(envelope.CharacterHash))
                continue;

            if (!TryDecodeCosmeticProfile(envelope.BrotliBlob, out var profile))
            {
                Svc.Log.Warning($"[SyncService] Skipping corrupt cosmetic profile for hash '{envelope.CharacterHash}'.");
                continue;
            }

            incoming[envelope.CharacterHash] = profile;
        }

        // Remove stale keys, upsert new/updated ones — no full-empty window.
        foreach (var key in Cosmetics.Keys.ToList())
        {
            if (!incoming.ContainsKey(key))
                Cosmetics.TryRemove(key, out _);
        }
        foreach (var kvp in incoming)
            Cosmetics[kvp.Key] = kvp.Value;
    }

    private void MarkConnected()
    {
        Interlocked.Exchange(ref _consecutivePollFailures, 0);
        Interlocked.Exchange(ref _connectedState, 1);
    }

    private void RegisterPollFailure(Exception ex)
    {
        var failures = Interlocked.Increment(ref _consecutivePollFailures);
        if (failures >= OfflineFailureThreshold)
            Interlocked.Exchange(ref _connectedState, 0);

        if (failures == OfflineFailureThreshold)
            Svc.Log.Warning($"[SyncService] Polling degraded after {failures} consecutive failures: {ex.Message}");
    }

    private static bool TryEncodeCosmeticProfile(CosmeticProfile profile, out byte[] compressedBytes)
    {
        compressedBytes = Array.Empty<byte>();
        try
        {
            var json = JsonConvert.SerializeObject(profile);
            var bytes = Encoding.UTF8.GetBytes(json);
            using var outStream = new MemoryStream();
            using (var brotli = new BrotliStream(outStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                brotli.Write(bytes, 0, bytes.Length);
            }

            compressedBytes = outStream.ToArray();
            return compressedBytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDecodeCosmeticProfile(byte[] compressedBytes, out CosmeticProfile profile)
    {
        profile = new CosmeticProfile();
        try
        {
            if (compressedBytes == null || compressedBytes.Length == 0)
                return false;

            using var input = new MemoryStream(compressedBytes);
            using var brotli = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            brotli.CopyTo(output);

            var json = Encoding.UTF8.GetString(output.ToArray());
            var decoded = JsonConvert.DeserializeObject<CosmeticProfile>(json);
            if (decoded == null)
                return false;

            profile = decoded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ─── HTTP helpers ───

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

    /// <summary>
    /// Fire-and-forget upsert of a character profile to the global profiles table.
    /// Non-fatal: logs a warning on failure. Pass venueId to register the venue in the profile.
    /// </summary>
    public void UpsertProfileAsync(string profileId, string characterName, string homeWorld, string mode,
        string venueId = "", bool hasGlamourer = false, bool hasChatTwo = false)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                Guid? venueGuid = null;
                if (!string.IsNullOrEmpty(venueId) && Guid.TryParse(venueId, out var vg))
                    venueGuid = vg;

                var body = new
                {
                    profileId,
                    characterName,
                    homeWorld,
                    mode,
                    hasGlamourerIntegrated = hasGlamourer,
                    hasChatTwoIntegrated   = hasChatTwo,
                    venueId                = venueGuid
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
    /// Updates the X-Venue-Key default header on the HttpClient.
    /// Call after setup completes for non-Sugar venue installs so API calls use the correct key.
    /// </summary>
    public void UpdateVenueKey(string key)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Venue-Key");
        if (!string.IsNullOrEmpty(key))
            _httpClient.DefaultRequestHeaders.Add("X-Venue-Key", key);
    }

    /// <summary>
    /// Validates a venue key against the API. Returns the VenueId and VenueName on success.
    /// Creates a temporary HttpClient — do not call in a hot loop.
    /// </summary>
    public async Task<(bool Valid, Guid VenueId, string VenueName)> ValidateVenueKeyAsync(string key)
    {
        try
        {
            var apiUrl = !string.IsNullOrEmpty(_plugin.Configuration.ApiUrl)
                ? _plugin.Configuration.ApiUrl
                : PluginConstants.ProductionApiUrl;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url     = $"{apiUrl.TrimEnd('/')}/api/venues/validate";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Venue-Key", key);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return (false, Guid.Empty, string.Empty);

            var json   = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<VenueValidateResult>(json);
            return result != null
                ? (true, result.VenueId, result.VenueName)
                : (false, Guid.Empty, string.Empty);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[SyncService] ValidateVenueKeyAsync failed: {ex.Message}");
            return (false, Guid.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Registers a new venue with the API. Returns the VenueId, VenueKey, and VenueName on success.
    /// Creates a temporary HttpClient — do not call in a hot loop.
    /// </summary>
    public async Task<(bool Success, Guid VenueId, string VenueKey, string VenueName)> RegisterVenueAsync(
        string venueName, string ownerProfileId)
    {
        try
        {
            var apiUrl = !string.IsNullOrEmpty(_plugin.Configuration.ApiUrl)
                ? _plugin.Configuration.ApiUrl
                : PluginConstants.ProductionApiUrl;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url  = $"{apiUrl.TrimEnd('/')}/api/venues/register";
            var body = JsonConvert.SerializeObject(new { venueName, ownerProfileId });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode) return (false, Guid.Empty, string.Empty, string.Empty);

            var json   = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<VenueRegisterResult>(json);
            return result != null
                ? (true, result.VenueId, result.VenueKey, result.VenueName)
                : (false, Guid.Empty, string.Empty, string.Empty);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"[SyncService] RegisterVenueAsync failed: {ex.Message}");
            return (false, Guid.Empty, string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Fire-and-forget staff presence heartbeat. Call on clock-in and periodically while clocked in.
    /// </summary>
    public void SendHeartbeatAsync()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var cfg = _plugin.Configuration;
                var body = new
                {
                    CharacterName = cfg.CharacterName,
                    HomeWorld     = cfg.HomeWorld,
                    Role          = cfg.PrimaryRole.ToString(),
                    IsOnline      = true,
                    IsDnd         = false,
                    ShiftStart    = _plugin.ShiftManager.CurrentShift?.StartTime
                };
                await PostAsync("api/staff/heartbeat", body);
            }
            catch (Exception ex)
            {
                Svc.Log.Warning($"[SyncService] SendHeartbeatAsync failed: {ex.Message}");
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
        _pollCts.Cancel();
        try
        {
            _pollTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Best-effort shutdown; disposal continues.
        }
        _pollCts.Dispose();
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

internal sealed class VenueValidateResult
{
    [JsonProperty("venueId")]   public Guid   VenueId   { get; set; }
    [JsonProperty("venueName")] public string VenueName { get; set; } = string.Empty;
}

internal sealed class VenueRegisterResult
{
    [JsonProperty("venueId")]   public Guid   VenueId   { get; set; }
    [JsonProperty("venueKey")]  public string VenueKey  { get; set; } = string.Empty;
    [JsonProperty("venueName")] public string VenueName { get; set; } = string.Empty;
}
