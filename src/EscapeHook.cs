using System;
using UnityEngine;

namespace DewRunHistory;

// the game already has a back stack: GlobalUIManager.AddBackHandler(owner, priority, cb).
// GoBack() walks handlers from the highest priority down and stops at the first one that
// returns true. the title registers at -1, the lobby at 1, and the game's own modal
// windows (Settings, Constellations, Lucid Dreams) at 100. registering at 100 closes our
// window on Esc just like Recollections does, instead of leaking through to the title and
// popping up "are you sure you want to quit?".
internal static class EscapeHook
{
    const string Tag = "[DewRunHistory]";
    const int Priority = 100;

    static BackHandler _handle;
    static GlobalUIManager _owner;

    internal static void Ensure(MonoBehaviour owner)
    {
        try
        {
            var gui = ManagerBase<GlobalUIManager>.instance;
            if (gui == null) return;

            // the manager is recreated on some transitions, taking the old handle with it
            if (_handle != null && _owner == gui) return;

            _owner = gui;
            _handle = gui.AddBackHandler(owner, Priority, OnBack);
            Debug.Log($"{Tag} esc: back handler registered at priority {Priority}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} esc: {e.GetType().Name}: {e.Message}");
        }
    }

    static bool OnBack()
    {
        // false = not for us, the game keeps walking down the stack
        if (!ResultViewHarvest.IsVisible) return false;

        ResultViewHarvest.Close();
        return true;
    }
}
