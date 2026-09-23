using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DewRunHistory;

// the result view only exists in the PlayGame scene. without this, the history only works
// after you've entered a run this session, which in practice is the same as not working.
// so we load PlayGame additively just to harvest, and clean up after ourselves.
internal static class OnDemandHarvest
{
    const string Tag = "[DewRunHistory]";

    static bool _running;
    static readonly List<string> _captured = new List<string>();

    internal static bool Running => _running;

    internal static IEnumerator Run()
    {
        if (_running) yield break;
        _running = true;

        Debug.Log($"{Tag} ondemand: loading PlayGame from '{SceneManager.GetActiveScene().name}'");
        _captured.Clear();
        Application.logMessageReceived += Capture;

        AsyncOperation op = null;
        try { op = SceneManager.LoadSceneAsync("PlayGame", LoadSceneMode.Additive); }
        catch (Exception e) { Debug.LogError($"{Tag} ondemand: LoadSceneAsync threw: {e}"); }

        if (op != null)
        {
            while (!op.isDone) yield return null;
            // two frames for the scene objects' Awake/Start to finish
            yield return null;
            yield return null;

            ResultViewHarvest.Harvest();
        }

        // cleanup: GameLogicPackage moves itself to DontDestroyOnLoad in its Start, so
        // unloading the scene is not enough to get it out of the way.
        try
        {
            var pkg = UnityEngine.Object.FindAnyObjectByType<GameLogicPackage>();
            if (pkg != null)
            {
                Debug.Log($"{Tag} ondemand: destroying GameLogicPackage");
                UnityEngine.Object.DestroyImmediate(pkg.gameObject);
            }

            var net = UnityEngine.Object.FindAnyObjectByType<NetworkLogicPackage>();
            if (net != null && !ResultViewHarvest.InMatch)
            {
                Debug.Log($"{Tag} ondemand: destroying NetworkLogicPackage");
                UnityEngine.Object.DestroyImmediate(net.gameObject);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} ondemand: cleanup failed: {e}");
        }

        Scene sc = SceneManager.GetSceneByName("PlayGame");
        if (sc.IsValid() && sc.isLoaded)
        {
            AsyncOperation un = null;
            try { un = SceneManager.UnloadSceneAsync(sc); }
            catch (Exception e) { Debug.LogError($"{Tag} ondemand: unload threw: {e}"); }
            if (un != null) { while (!un.isDone) yield return null; }
        }

        Application.logMessageReceived -= Capture;

        // the point of the exercise is knowing WHERE each error came from, not how many
        Debug.Log($"{Tag} ondemand: done. scene='{SceneManager.GetActiveScene().name}' copy={ResultViewHarvest.HasCopy} " +
                  $"errors captured={_captured.Count}");
        foreach (var g in _captured.GroupBy(x => x).OrderByDescending(g => g.Count()).Take(8))
        {
            Debug.Log($"{Tag} ondemand: [{g.Count()}x] {g.Key}");
        }

        _running = false;
    }

    static void Capture(string msg, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error) return;
        if (msg != null && msg.Contains("DewRunHistory")) return;

        // first useful line of the stack says who blew up
        string from = "(no stack)";
        if (!string.IsNullOrEmpty(stack))
        {
            from = stack.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? from;
            if (from.Length > 160) from = from.Substring(0, 160);
        }

        _captured.Add($"{msg} @ {from}");
    }
}
