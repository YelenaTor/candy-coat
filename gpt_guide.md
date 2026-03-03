# GPT Contributor Guide — Candy Coat

> Written by the senior developer. Read this before touching any file.
> This project is active, in-game, and production. Mistakes are visible to real users.

---

## 1. Before You Write Anything

1. **Read `CLAUDE.md` in full.** It is the authoritative reference for architecture, conventions, and file layout. Do not infer structure from the code alone.
2. **Read the files you are changing.** Do not generate code against a file you haven't read. Do not assume a method signature, field name, or enum value — look it up.
3. **Build before submitting.** Your output must compile with `dotnet build CandyCoat/CandyCoat.csproj`. Zero errors, zero warnings. Do not submit a diff you have not mentally walked for compile errors.

---

## 2. Project Rules (Non-Negotiable)

- **No project structure changes** unless explicitly requested.
- **No monolithic files.** If a class is growing, ask before adding more to it. New features belong in new files.
- **No Co-Authored-By lines** in commit messages.
- **Bump `CandyCoat.csproj` version** before every commit that ships a feature or fix.
- **Update `CHANGELOG.md` and `README.md`** as part of every release commit, without being asked.
- **No backwards-compat shims.** If something is removed, remove it. Don't leave `// removed` comments, unused `_vars`, or re-exported types.
- **No speculative features.** Implement only what was asked. Do not add error handling, helpers, or abstractions for scenarios that do not exist yet.

---

## 3. Dalamud API 14 Rules

This project targets `Dalamud.NET.Sdk/14.0.1`. The API surface is stable but specific.

### What's safe to use
- `Svc.*` from `ECommons.DalamudServices` — this is the correct access point for everything (objects, targets, game GUI, chat, commands, etc.)
- `Plugin.NamePlateGui.OnNamePlateUpdate` — confirmed available, used in production
- `Plugin.NamePlateGui.OnDataUpdate` — confirmed available in 14.0.1 (verified via successful build)
- `INamePlateUpdateHandler.PlayerCharacter` — safe
- `INamePlateUpdateHandler.NamePlateObjectAddress` — confirmed field on the handler in 14.0.1
- `Svc.GameGui.WorldToScreen()` — safe world-to-screen projection
- `handler.RemoveField(NamePlateStringField.*)` — correct way to suppress vanilla nameplate fields
- `pc.Struct()` from `ECommons.GameFunctions` — returns the native game object pointer

### What to avoid
- `PluginLog.*` — deprecated. Use `Svc.Log.*` only.
- `ImGuiWindowFlags.NoBringToDisplayOnFocus` — does NOT exist in this ImGui binding.
- Do not assume a Dalamud API exists without checking. When uncertain, grep the codebase for prior usage — if it's not already used, flag it rather than assume.

### FFXIVClientStructs
Unsafe struct access is used in `NameplateRenderer.cs`. It's acceptable here because:
- The codebase already uses this pattern
- All pointer dereferences must have null checks
- Always check `ptr == null` and check that the value is non-zero before use
- Stick to `FFXIVClientStructs.FFXIV.*` — do not use undocumented or private struct paths

---

## 4. Thread Safety

This project runs on two threads: the **game/draw thread** and **background task threads**.

### The rule is simple:
| What | Which thread |
|------|-------------|
| All ImGui calls | Draw thread only |
| All `Svc.*` access | Draw thread only |
| `SyncService` poll loop | Background thread |
| `SyncService` write methods (fire-and-forget) | Background thread |
| `Configuration.*` reads | Draw thread only |

### Concurrency patterns that ARE correct here
- **Reference swap on `List<T>` properties**: assigning a new list to a property (`OnlineStaff = newList`) is safe. The draw thread captures a reference at the start of iteration and holds it. The old list continues to be iterated safely.
- **`ConcurrentDictionary` for `Cosmetics`**: correct choice. Always use `TryGetValue`, `TryRemove`, and indexer assignment — never `Clear()` on a live dictionary that the draw thread reads every frame. A full `Clear()` creates an empty-window visible to the renderer.
- **`Volatile.Read/Interlocked.Exchange`** for `bool`/`int` flags shared between threads — use these, not plain field access.

### What GPT got wrong on its first pass (learn from it)
GPT called `Cosmetics.Clear()` then repopulated the dictionary. This caused every staff member's nameplate to flicker off for one frame every 3 seconds. The fix is to build the incoming set separately, then surgically remove stale keys and upsert new ones — the dictionary is never fully emptied.

---

## 5. ImGui Patterns

- `StyleManager.PushStyles()` / `PopStyles()` wrap every `Window.Draw()` call. If you add a new window, follow this pattern.
- Two-panel layouts: three `ImRaii.Child` blocks with `SameLine(0, 0)` between them.
- Child regions: always `PopStyleColor` inside the using block immediately after `if (tier1)` check, not outside.
- Fire `Svc.Commands.ProcessCommand(cmd)` for emotes and chat commands — never raw text injection.
- `ImGui.GetTime()` is fine for pulsing effects.
- Do not call `ImGui.GetFrameCount()` outside the draw thread.

---

## 6. Async Patterns

**Fire-and-forget** (the only async pattern for write operations):
```csharp
_ = Task.Run(async () =>
{
    try { await DoSomethingAsync(); }
    catch (Exception ex) { Svc.Log.Warning($"[ServiceName] Method failed: {ex.Message}"); }
});
```

**Background poll loop** (the pattern for `SyncService`):
```csharp
while (!token.IsCancellationRequested)
{
    try { await DoWorkAsync(token); MarkConnected(); }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
    catch (Exception ex) { RegisterFailure(ex); }

    try { await Task.Delay(interval, token); }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
}
```

Rules:
- Never `await` inside a `catch` block.
- Never rethrow to the caller from a fire-and-forget.
- Always pass `CancellationToken` into `HttpClient.GetAsync` / `Task.Delay` so disposal is clean.
- `Dispose()` cancels the token, waits briefly for the poll task, then disposes resources. One second wait is acceptable for a plugin.

---

## 7. Configuration

- Save via `Configuration.Save()` — never call `Plugin.PluginInterface.SavePluginConfig(this)` directly from a panel.
- Configuration is read on the draw thread only. Never write to it from a background thread.
- `MigrateConfig()` runs on every plugin load — this is how existing installs get new fields backfilled. If you add a new config field that needs a default set on upgrade, add it there.

---

## 8. SRT Panels

All SRT panels implement `IToolboxPanel` (`Name`, `Role`, `DrawContent()`, `DrawSettings()`).

- `DrawContent()` follows: Tier 1 card (fixed height child) → spacing → collapsibles.
- Collapsible order is fixed per role — don't rearrange without a plan.
- Emote buttons fire `/emote motion` commands via `Svc.Commands.ProcessCommand`.
- Earnings use `EarningsType.Session` or `EarningsType.Tip` with the correct `StaffRole` set.
- Patron notes use `AuthorRole = StaffRole.X` so they're scoped correctly in the notes list.

---

## 9. Nameplate Renderer Specifics

The anchor resolution chain is:
1. **Native** (`OnDataUpdate` → `NamePlateObjectAddress` → `AtkResNode.ScreenX/Y`) — most accurate
2. **World position** (`GetNamePlateWorldPosition` → `WorldToScreen`) — good fallback
3. **Dual-projection** (feet + 1-unit-up → compute pxPerUnit → lift 1.7 units) — legacy fallback

**Critical rule**: `LegacyBaseYOffset` (30px) applies **only** to the dual-projection fallback. Native and world-position anchors already land at the nameplate position. Applying the offset to them pushes text 30px below where it should be.

The draw thread reads `Cosmetics` every frame. The poll thread writes it every 3 seconds. Never call `Cosmetics.Clear()`. See section 4.

---

## 10. Commit Checklist

Before submitting any commit:

- [ ] `dotnet build CandyCoat/CandyCoat.csproj` — zero errors, zero warnings
- [ ] Version bumped in `CandyCoat/CandyCoat.csproj`
- [ ] `CHANGELOG.md` updated with a clear description of what changed
- [ ] `README.md` updated if a panel, feature, or user-visible behaviour changed
- [ ] No `Co-Authored-By` lines in the commit message
- [ ] No new files created unless strictly necessary
- [ ] No speculative helpers or abstractions added
- [ ] All changed methods have been mentally walked for thread safety

---

## 11. When You're Unsure

- **Grep before assuming.** If you're not sure a method/field/event exists, search the codebase for prior usage. `grep -r "MethodName"` is your friend.
- **The build is the ground truth.** If something compiles, the API exists. If it doesn't, it doesn't.
- **Flag it.** If you're uncertain whether an approach is correct (especially for unsafe code, threading, or Dalamud API surface), say so explicitly in your response. The senior dev will decide. Do not silently guess.
- **Name things what they are.** If a constant only applies to the legacy fallback path, call it `LegacyBaseYOffset` — and then actually only apply it to the legacy path.
