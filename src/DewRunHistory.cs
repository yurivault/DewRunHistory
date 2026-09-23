using System;
using DewRunHistory.patch;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DewRunHistory;

public class DewRunHistory : ModBehaviour
{
    internal static DewRunHistory Instance;

    bool _backfilled;
    float _nextScan;
    float _nextMenuCheck;

    private void Start()
    {
        Instance = this;
        ConsumeGameResult_Patch.Apply(harmony);
        Debug.Log($"[{mod.metadata.id}] loaded. Run History entry is in the title menu. F9 = probe.");
    }

    private void OnDestroy()
    {
        // the tooltip box is on loan to our canvas while the window is open. if the mod
        // goes away without giving it back, the game loses tooltips until restart.
        TooltipLayer.Restore();
        harmony.UnpatchAll(harmony.Id);
    }

    private void Update()
    {
        try
        {
            // profileMain may not be ready during the mod's Start
            if (!_backfilled && DewSave.profileMain != null)
            {
                _backfilled = true;
                RunArchive.Backfill();
            }

            // the result view only exists inside a match and is destroyed on the way back
            // to the title. harvesting is expensive (Instantiate of hundreds of Graphics),
            // so only do it while loading, where the hitch bothers nobody.
            if (!ResultViewHarvest.HasCopy && ResultViewHarvest.IsLoading && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1f;
                ResultViewHarvest.TryHarvestQuietly();
            }

            // the title menu is rebuilt every time you come back to it
            if (!ResultViewHarvest.InMatch && Time.unscaledTime >= _nextMenuCheck)
            {
                _nextMenuCheck = Time.unscaledTime + 2f;
                TitleMenuEntry.Ensure();
            }

            // Esc is deliberately not handled here. the game's own back stack closes the
            // window (see EscapeHook), otherwise the key closed it AND leaked through to
            // the title, which read it as "back" and opened the quit dialog.
            EscapeHook.Ensure(this);

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f9Key.wasPressedThisFrame) HistoryProbe.Run();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DewRunHistory] update failed: {e}");
        }
    }
}
