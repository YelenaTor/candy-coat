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
    private static readonly Vector4 Green   = new(0.2f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 Red     = new(1f, 0.3f, 0.3f, 1f);

    private enum CheckState { Idle, Checking, Connected, Failed, NoUrl }
    private CheckState _state = CheckState.Idle;
    private bool _hasChecked = false;
    private Task<bool>? _checkTask;

    public void DrawContent(ref int step, WizardState state, Plugin plugin)
    {
        // Poll async task
        if (_state == CheckState.Checking && _checkTask?.IsCompleted == true)
        {
            _state = _checkTask.Result ? CheckState.Connected : CheckState.Failed;
            _checkTask = null;
        }

        // Auto-check on first draw
        if (!_hasChecked)
        {
            _hasChecked = true;
            StartCheck(plugin);
        }

        ImGui.TextColored(DimGrey, "Step 1 of 5 — Checking Sync");
        ImGui.Spacing();
        ImGui.TextWrapped("Verifying connection to the Sugar API before setup.");
        ImGui.Spacing();
        ImGui.Spacing();

        switch (_state)
        {
            case CheckState.NoUrl:
                ImGui.TextColored(Amber, "\u26a0 Sync is not configured.");
                ImGui.Spacing();
                ImGui.TextWrapped("You can set up sync later in Settings > Sync / API Configuration. You can still complete setup without it.");
                break;

            case CheckState.Checking:
                ImGui.TextColored(Amber, "\u23f3 Checking connection...");
                break;

            case CheckState.Connected:
                ImGui.TextColored(Green, "\u2714 Connected to Sugar API.");
                break;

            case CheckState.Failed:
                ImGui.TextColored(Red, "\u2718 Could not reach the API.");
                ImGui.Spacing();
                ImGui.TextWrapped("Check your API URL and Venue Key in Settings > Sync, or continue without sync.");
                ImGui.Spacing();
                if (ImGui.SmallButton("Retry##syncRetry"))
                {
                    _hasChecked = false;
                    StartCheck(plugin);
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

    private void StartCheck(Plugin plugin)
    {
        var apiUrl = plugin.Configuration.ApiUrl;
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            _state = CheckState.NoUrl;
            return;
        }
        _state = CheckState.Checking;
        _checkTask = SyncService.CheckHealthAsync(apiUrl);
    }
}
