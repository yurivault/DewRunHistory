using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace DewRunHistory;

// F9 diagnostic. answers things you can only know at runtime, and it's the first thing
// to run when a game update breaks something:
// 1. how many runs are saved and whether they can be read back
// 2. whether the result view prefab comes out of DewResources outside a match
// 3. whether any live or inactive instance of it exists in the current scene
// 4. what the title and lobby menus actually look like, for placing our entry
internal static class HistoryProbe
{
    const string Tag = "[DewRunHistory]";

    internal static void Run()
    {
        Debug.Log($"{Tag} ===== probe start | scene={SceneManager.GetActiveScene().name} =====");
        ProbeSavedRuns();
        ProbeResourcePrefab();
        ProbeSceneInstances();
        ProbeMenus();
        Debug.Log($"{Tag} ===== probe end =====");
    }

    static void ProbeSavedRuns()
    {
        try
        {
            List<DewGameResult> runs = DewSave.profileMain?.lastGameResults;
            if (runs == null)
            {
                Debug.Log($"{Tag} runs: profileMain or lastGameResults is null");
                return;
            }

            Debug.Log($"{Tag} runs: {runs.Count} saved, favourites={DewSave.profileMain.favoriteGameResults?.Count ?? -1}, " +
                      $"archived={RunArchive.Count()}");

            for (int i = 0; i < Math.Min(runs.Count, 3); i++)
            {
                DewGameResult r = runs[i];
                Debug.Log($"{Tag} run[{i}] id={r.runId} result={r.result} diff={r.difficulty} limbo={r.limboDepth} " +
                          $"{Mathf.RoundToInt(r.elapsedGameTimeSeconds / 60f)}min players={r.players?.Count ?? -1} " +
                          $"worlds={r.visitedWorlds} locations={r.visitedLocations}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} runs: failed: {e}");
        }
    }

    static void ProbeResourcePrefab()
    {
        // GetByType would be the ideal path: the console's ResLoad shows the database is
        // indexed by Type, so if the view were in there this would resolve straight away.
        // it doesn't, which is exactly why the mod harvests from the scene instead.
        try
        {
            var byType = DewResources.GetByType<UI_InGame_ResultView>(default);
            Debug.Log($"{Tag} prefab GetByType<UI_InGame_ResultView> => {(byType == null ? "NULL" : byType.name)}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} prefab GetByType failed: {e.GetType().Name}: {e.Message}");
        }

        try
        {
            var all = DewResources.FindAllByType<UI_InGame_ResultView>(default)?.ToList();
            Debug.Log($"{Tag} prefab FindAllByType => {(all == null ? "NULL" : all.Count.ToString())}");
            if (all != null)
            {
                foreach (var v in all.Take(3))
                {
                    Debug.Log($"{Tag}    -> {(v == null ? "null" : v.name)}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} prefab FindAllByType failed: {e.GetType().Name}: {e.Message}");
        }

        try
        {
            var sub = DewResources.FindOneByTypeSubstring<UI_InGame_ResultView>("ResultView", default);
            Debug.Log($"{Tag} prefab FindOneByTypeSubstring('ResultView') => {(sub == null ? "NULL" : sub.name)}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} prefab FindOneByTypeSubstring failed: {e.GetType().Name}: {e.Message}");
        }
    }

    // survey of where the menu entry can go, so we don't guess at structure
    static void ProbeMenus()
    {
        try
        {
            var title = Resources.FindObjectsOfTypeAll<UI_Title_TitleView>().FirstOrDefault();
            if (title == null)
            {
                Debug.Log($"{Tag} menu: UI_Title_TitleView is not loaded here");
            }
            else
            {
                var group = AccessTools.Field(typeof(UI_Title_TitleView), "mainButtonsGroup")?.GetValue(title) as CanvasGroup;
                Debug.Log($"{Tag} menu: TitleView='{title.name}' mainButtonsGroup={(group == null ? "NULL" : group.name)}");
                if (group != null)
                {
                    foreach (Transform child in group.transform)
                    {
                        var lbl = child.GetComponentInChildren<TMPro.TMP_Text>(true);
                        Debug.Log($"{Tag} menu:    child '{child.name}' button={child.GetComponent<Button>() != null} text='{(lbl == null ? "" : lbl.text)}'");
                    }
                }
            }

            var common = Resources.FindObjectsOfTypeAll<UI_Common_MenuView>().FirstOrDefault();
            Debug.Log($"{Tag} menu: UI_Common_MenuView={(common == null ? "NULL" : common.name)}");
            if (common != null)
            {
                foreach (Transform child in common.transform)
                {
                    Debug.Log($"{Tag} menu:    common '{child.name}' buttons={child.GetComponentsInChildren<Button>(true).Length}");
                }

                // drill into Menu List View, where the lobby entry would go
                Transform list = common.transform.Find("Menu List View");
                if (list != null)
                {
                    foreach (Button b in list.GetComponentsInChildren<Button>(true))
                    {
                        var lbl = b.GetComponentInChildren<TMPro.TMP_Text>(true);
                        Debug.Log($"{Tag} menu:      list '{b.name}' parent='{b.transform.parent.name}' " +
                                  $"text='{(lbl == null ? "" : lbl.text)}' persistent={b.onClick.GetPersistentEventCount()}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} menu: {e.GetType().Name}: {e.Message}");
        }
    }

    static void ProbeSceneInstances()
    {
        // FindObjectsOfTypeAll picks up inactive objects and things loaded but hidden
        try
        {
            var found = Resources.FindObjectsOfTypeAll<UI_InGame_ResultView>();
            Debug.Log($"{Tag} instances in memory: {found.Length}");
            foreach (var v in found.Take(3))
            {
                string scene = v.gameObject.scene.IsValid() ? v.gameObject.scene.name : "(prefab/no scene)";
                Debug.Log($"{Tag}    -> {v.name} scene={scene} active={v.gameObject.activeInHierarchy}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} instances: failed: {e}");
        }

        // how many pieces implement the interface that draws saved data
        try
        {
            var items = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .Where(mb => mb is IGameResultStatItem)
                .ToList();
            Debug.Log($"{Tag} IGameResultStatItem in memory: {items.Count}");
            foreach (var g in items.GroupBy(x => x.GetType().Name))
            {
                Debug.Log($"{Tag}    -> {g.Key} x{g.Count()}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} stat items: failed: {e}");
        }
    }
}
