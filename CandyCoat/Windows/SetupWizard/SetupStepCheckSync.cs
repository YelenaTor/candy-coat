using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using CandyCoat.Services;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class SetupStepCheckSync
{
    private static readonly Vector4 DimGrey = new(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Vector4 Pink    = new(1f, 0.6f, 0.8f, 1f);
    private static readonly Vector4 Amber   = new(1f, 0.8f, 0.2f, 1f);
    private static readonly Vector4 Red     = new(1f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 Green   = new(0.2f, 0.9f, 0.4f, 1f);

    private const float  AnimAreaHeight = 120f;

    private enum CheckState { Idle, Checking, Connected, Failed }
    private CheckState  _state      = CheckState.Idle;
    private bool        _hasChecked = false;
    private Task<bool>? _checkTask;

    // ── Ring ──
    private float       _ringAngle  = 0f;
    private const float RotSpeed    = 2.5f;            // rad/s
    private const float ArcSpan    = MathF.PI * 1.25f; // ~225°

    // ── Particles ──
    private readonly List<HeartParticle> _particles = new();
    private float       _spawnAccum      = 0f;
    private const float SpawnInterval    = 0.055f;
    private const float ParticleLifetime = 0.75f;

    // ── Connected heart ──
    private float _connectedAge = 0f;

    private struct HeartParticle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float   Age;
    }

    public void DrawContent(ref int step, WizardState state, Plugin plugin)
    {
        float dt = ImGui.GetIO().DeltaTime;

        // Poll async task
        if (_state == CheckState.Checking && _checkTask?.IsCompleted == true)
        {
            _state        = _checkTask.Result ? CheckState.Connected : CheckState.Failed;
            _checkTask    = null;
            _connectedAge = 0f;
            _particles.Clear();
        }

        // Auto-check on first draw
        if (!_hasChecked)
        {
            _hasChecked = true;
            StartCheck();
        }

        ImGui.TextColored(DimGrey, "Step 1 of 5 — Checking Sync");
        ImGui.Spacing();
        ImGui.TextWrapped("Verifying connection to the Sugar API before setup.");
        ImGui.Spacing();
        ImGui.Spacing();

        // ── Animation area ──
        var   dl     = ImGui.GetWindowDrawList();
        var   origin = ImGui.GetCursorScreenPos();
        float cw     = ImGui.GetContentRegionAvail().X;
        // Offset center slightly upward so the heart's bottom point still fits in the box
        var   center = new Vector2(origin.X + cw * 0.5f, origin.Y + AnimAreaHeight * 0.42f);

        switch (_state)
        {
            case CheckState.Checking:
                UpdateAndDrawRing(dl, center, dt);
                break;

            case CheckState.Connected:
                _connectedAge += dt;
                DrawConnectedHeart(dl, center, _connectedAge);
                break;
        }

        // Reserve the animation area in the layout
        ImGui.Dummy(new Vector2(cw, AnimAreaHeight));
        ImGui.Spacing();

        // ── Status text ──
        switch (_state)
        {
            case CheckState.Connected:
                ImGui.TextColored(Green, "\u2714 Connected to Sugar API.");
                break;

            case CheckState.Failed:
                ImGui.TextColored(Red, "\u2718 Could not reach the API.");
                ImGui.Spacing();
                ImGui.TextWrapped("Check your API settings in Settings > Sync, or continue without sync.");
                ImGui.Spacing();
                if (ImGui.SmallButton("Retry##syncRetry"))
                {
                    _particles.Clear();
                    _spawnAccum = 0f;
                    StartCheck();
                }
                break;
        }

        ImGui.Spacing();
        ImGui.Spacing();

        bool isChecking = _state == CheckState.Checking;
        if (isChecking) ImGui.BeginDisabled();
        if (ImGui.Button("Continue##syncContinue", new Vector2(120, 0)))
            step = 2;
        if (isChecking) ImGui.EndDisabled();
    }

    // ─── Loading ring with heart particles ───────────────────────────────────

    private void UpdateAndDrawRing(ImDrawListPtr dl, Vector2 center, float dt)
    {
        const float Radius = 34f;
        const float Thick  = 5f;
        const int   Segs   = 40;

        _ringAngle += dt * RotSpeed;

        float tipAngle = _ringAngle;
        var tip = new Vector2(
            center.X + MathF.Cos(tipAngle) * Radius,
            center.Y + MathF.Sin(tipAngle) * Radius);

        // Fading arc — alpha 0 at tail → 1 at tip
        for (int i = 0; i < Segs; i++)
        {
            float t  = (float)i / Segs;
            float a0 = tipAngle - ArcSpan + ArcSpan * t;
            float a1 = tipAngle - ArcSpan + ArcSpan * (t + 1f / Segs);

            var p0 = new Vector2(center.X + MathF.Cos(a0) * Radius, center.Y + MathF.Sin(a0) * Radius);
            var p1 = new Vector2(center.X + MathF.Cos(a1) * Radius, center.Y + MathF.Sin(a1) * Radius);

            dl.AddLine(p0, p1, ImGui.GetColorU32(new Vector4(1f, 0.6f, 0.8f, t)), Thick);
        }

        // Bright glowing dot at tip
        dl.AddCircleFilled(tip, Thick * 0.9f, ImGui.GetColorU32(new Vector4(1f, 0.88f, 0.94f, 1f)), 12);

        // Spawn particles from tip
        _spawnAccum += dt;
        while (_spawnAccum >= SpawnInterval)
        {
            _spawnAccum -= SpawnInterval;
            SpawnParticle(tip, tipAngle);
        }

        // Update and draw particles (reverse iterate for in-place removal)
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += dt;
            if (p.Age >= ParticleLifetime) { _particles.RemoveAt(i); continue; }

            p.Pos   += p.Vel * dt;
            p.Vel.Y += 50f * dt; // gentle gravity
            _particles[i] = p;

            float alpha = 1f - p.Age / ParticleLifetime;
            dl.AddText(p.Pos, ImGui.GetColorU32(new Vector4(1f, 0.55f, 0.78f, alpha)), "\u2665");
        }
    }

    private static void SpawnParticle(List<HeartParticle> list, Vector2 tip, float tipAngle)
    {
        // Spray outward in a ~180° cone from the tip of the arc
        float spread = tipAngle + ((float)Random.Shared.NextDouble() - 0.5f) * MathF.PI;
        float speed  = 38f + (float)Random.Shared.NextDouble() * 55f;

        list.Add(new HeartParticle
        {
            Pos = tip + new Vector2(
                ((float)Random.Shared.NextDouble() - 0.5f) * 5f,
                ((float)Random.Shared.NextDouble() - 0.5f) * 5f),
            Vel = new Vector2(MathF.Cos(spread) * speed, MathF.Sin(spread) * speed),
            Age = 0f,
        });
    }

    private void SpawnParticle(Vector2 tip, float tipAngle)
        => SpawnParticle(_particles, tip, tipAngle);

    // ─── Connected heart ─────────────────────────────────────────────────────

    private static void DrawConnectedHeart(ImDrawListPtr dl, Vector2 center, float age)
    {
        // Pop in with OutBack easing over 0.35 s, then gently pulse
        float t = Math.Min(age / 0.35f, 1f);
        float scale = t < 1f
            ? EaseOutBack(t) * 3.0f
            : 3.0f + MathF.Sin(age * 2.5f) * 0.12f;

        if (scale <= 0f) return;

        const int Points = 80;
        Span<Vector2> pts = stackalloc Vector2[Points];

        for (int i = 0; i < Points; i++)
        {
            float a = (float)i / Points * MathF.PI * 2f;
            // Parametric heart (y negated → point at bottom in screen space)
            float x =  16f * MathF.Pow(MathF.Sin(a), 3f);
            float y = -(13f * MathF.Cos(a)
                      -  5f * MathF.Cos(2f * a)
                      -  2f * MathF.Cos(3f * a)
                      -       MathF.Cos(4f * a));
            pts[i] = center + new Vector2(x, y) * scale;
        }

        uint fill = ImGui.GetColorU32(new Vector4(1f, 0.55f, 0.78f, 0.40f));
        uint line = ImGui.GetColorU32(new Vector4(1f, 0.60f, 0.80f, 1.00f));

        // Filled triangles from center
        for (int i = 0; i < Points; i++)
            dl.AddTriangleFilled(center, pts[i], pts[(i + 1) % Points], fill);

        // Outline
        for (int i = 0; i < Points; i++)
            dl.AddLine(pts[i], pts[(i + 1) % Points], line, 2.5f);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
    }

    // ─── Start check ─────────────────────────────────────────────────────────

    private void StartCheck()
    {
        _state     = CheckState.Checking;
        _checkTask = SyncService.CheckHealthAsync(PluginConstants.ProductionApiUrl);
    }
}
