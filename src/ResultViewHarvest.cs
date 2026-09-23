using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DewRunHistory;

// the result view is not a prefab. it's created inside GameLogicPackage in the PlayGame
// scene, which moves to DontDestroyOnLoad and gets destroyed by ReturnToTitleImmediately.
// so to show it outside a match we need a copy of our own.
//
// split: ResultViewClone builds and places the copy, ResultPainter paints data into it,
// and this holds the window state (which run, which player, open or not).
internal static class ResultViewHarvest
{
    const string Tag = "[DewRunHistory]";

    static GameObject _root;
    static GameObject _view;
    static bool _visible;
    static int _runIdx;
    static int _playerIdx;

    internal static bool HasCopy => _view != null;
    internal static bool IsVisible => _visible;

    internal static void Close()
    {
        if (!_visible) return;
        Toggle();
    }

    // GameLogicPackage only exists inside a match: born with the PlayGame scene and
    // destroyed by ReturnToTitleImmediately. it's the exact "am I playing" marker.
    internal static bool InMatch
    {
        get
        {
            try { return ManagerBase<GameLogicPackage>.instance != null; }
            catch { return false; }
        }
    }

    // harvesting is expensive (Instantiate of hundreds of Graphics), so only do it
    // while loading, where the hitch bothers nobody.
    internal static bool IsLoading
    {
        get
        {
            try
            {
                var tm = ManagerBase<TransitionManager>.instance;
                return tm != null && tm.state == TransitionManager.StateType.Loading;
            }
            catch { return false; }
        }
    }

    internal static void Harvest()
    {
        try
        {
            if (_view != null)
            {
                Debug.Log($"{Tag} harvest: already have a copy, skipping");
                return;
            }

            var orig = Resources.FindObjectsOfTypeAll<UI_InGame_ResultView>().FirstOrDefault();
            if (orig == null)
            {
                Debug.LogWarning($"{Tag} harvest: no result view in memory. has to happen inside a match.");
                return;
            }

            Canvas origCanvas = orig.GetComponentInParent<Canvas>();
            Debug.Log($"{Tag} harvest: found '{orig.name}', parent canvas = {(origCanvas == null ? "NONE" : origCanvas.name)}");

            _root = ResultViewClone.BuildRoot(origCanvas);

            _view = UnityEngine.Object.Instantiate(orig.gameObject, _root.transform);
            _view.name = "Result View (history)";

            // turn _root off RIGHT NOW: while it's active, any SetActive in the subtree runs
            // the cloned components' Awake, and View.Awake registers into the game's static
            // View.instances list. from there InGameUIManager decides a modal is open and
            // kills gameplay input in the middle of a match.
            _root.SetActive(false);

            ResultViewClone.Unhide(_view);
            ResultViewClone.Reanchor(_view);

            // read the component's fields before killing it: once it's dead there's no way
            // to ask it which art belongs to which result type
            ResultPainter.Capture(_view, _view.GetComponent<UI_InGame_ResultView>());

            // every View in the clone has to die, not just the root one. any survivor lands
            // in View.instances on its first Awake and starts counting as open UI for the
            // game. the clone is still inactive here, so no Awake has run yet.
            View[] views = _view.GetComponentsInChildren<View>(true);
            Debug.Log($"{Tag} harvest: destroying {views.Length} View components from the clone");
            foreach (View v in views)
            {
                UnityEngine.Object.DestroyImmediate(v);
            }

            ResultViewClone.CreateBackdrop(_root);
            ResultViewClone.HideEndOfMatchControls(_view);
            ResultViewClone.WireArrows(_view, ChangePlayer);
            ResultViewClone.NormalizeChildCanvases(_view);

            // the list hangs off _root, not _view: _view gets scaled and shifted to give up
            // the strip, and the list would shrink along with it and leave its reserved spot.
            RunListPanel.Create(_root.transform, ResultPainter.FontSource, SelectRun, Close);

            _view.SetActive(true);
            _visible = false;

            int items = _view.GetComponentsInChildren<MonoBehaviour>(true).Count(mb => mb is IGameResultStatItem);
            Debug.Log($"{Tag} harvest: copy built with {items} IGameResultStatItem, left off.");
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} harvest failed: {e}");
        }
    }

    internal static void Toggle()
    {
        // history is a before-you-start thing. inside a match the game already shows you
        // everything, and keeping this closed during play makes it impossible for the
        // window to interfere with input.
        if (InMatch)
        {
            Debug.Log($"{Tag} toggle: ignored, history only opens outside a match");
            return;
        }

        if (_view == null)
        {
            Debug.LogWarning($"{Tag} toggle: no copy yet. play a match once this session.");
            return;
        }

        _visible = !_visible;
        _root.SetActive(_visible);
        Debug.Log($"{Tag} toggle: visible={_visible}");

        if (_visible)
        {
            // the resolution may have changed since the harvest, so the fit is recomputed
            // every time it opens, never baked into whatever monitor built it.
            ResultViewClone.Fit(_root, _view);
            TooltipLayer.BringToFront(_root.GetComponent<Canvas>());
            Render();
        }
        else
        {
            TooltipLayer.Restore();
        }
    }

    internal static void ChangeRun(int delta)
    {
        if (!_visible) return;
        _runIdx += delta;
        _playerIdx = 0;
        Render();
    }

    // click on the side list
    static void SelectRun(int idx)
    {
        if (!_visible) return;
        _runIdx = idx;
        _playerIdx = 0;
        Render();
    }

    internal static void ChangePlayer(int delta)
    {
        if (!_visible) return;
        _playerIdx += delta;
        Render();
    }

    internal static void Render()
    {
        try
        {
            List<DewGameResult> all = RunArchive.LoadAll();
            if (all.Count == 0)
            {
                Debug.LogWarning($"{Tag} render: archive is empty");
                return;
            }

            List<DewGameResult> runs = all.Where(ResultPainter.IsWorthShowing).ToList();
            int hidden = all.Count - runs.Count;

            // if the filter ate everything, show everything: an empty screen is worse
            // than a messy list
            if (runs.Count == 0)
            {
                runs = all;
                hidden = 0;
            }

            _runIdx = ((_runIdx % runs.Count) + runs.Count) % runs.Count;
            DewGameResult r = runs[_runIdx];

            int nPlayers = Mathf.Max(1, r.players?.Count ?? 1);
            _playerIdx = ((_playerIdx % nPlayers) + nPlayers) % nPlayers;

            ResultPainter.Paint(r, _playerIdx, nPlayers);
            RunListPanel.Fill(runs, _runIdx, hidden);

            string who = r.players != null && _playerIdx < r.players.Count ? r.players[_playerIdx].playerProfileName : "?";
            Debug.Log($"{Tag} render: run {_runIdx + 1}/{runs.Count} {r.result} | player {_playerIdx + 1}/{nPlayers} ({who})");
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} render failed: {e}");
        }
    }

    // polled while we don't have a copy. stays quiet until the view exists.
    internal static void TryHarvestQuietly()
    {
        try
        {
            if (Resources.FindObjectsOfTypeAll<UI_InGame_ResultView>().Length == 0) return;
            Debug.Log($"{Tag} view showed up, harvesting on its own");
            Harvest();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} auto harvest: {e.GetType().Name}: {e.Message}");
        }
    }
}
