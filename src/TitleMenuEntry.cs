using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewRunHistory;

// entry in the title menu. clones the "Mods" button and inserts it right above, the same
// trick MorePlayers uses to clone the lobby name field.
internal static class TitleMenuEntry
{
    const string Tag = "[DewRunHistory]";
    const string ButtonName = "DewRunHistory Button";

    internal static void Ensure()
    {
        try
        {
            var view = Resources.FindObjectsOfTypeAll<UI_Title_TitleView>().FirstOrDefault();
            if (view == null) return;

            var group = AccessTools.Field(typeof(UI_Title_TitleView), "mainButtonsGroup")?.GetValue(view) as CanvasGroup;
            if (group == null) return;

            // already installed on this instance of the menu?
            if (group.transform.Find(ButtonName) != null) return;

            Transform mods = group.transform.Find("Mods");
            if (mods == null)
            {
                Debug.LogWarning($"{Tag} menu: 'Mods' button not found, entry not installed");
                return;
            }

            GameObject clone = UnityEngine.Object.Instantiate(mods.gameObject, group.transform);
            clone.name = ButtonName;
            clone.transform.SetSiblingIndex(mods.GetSiblingIndex());

            // the button has more than one TMP_Text and not all of them are the label.
            // writing to all of them made "Run History" show up twice. only touch the ones
            // that had text in the original.
            TMP_Text[] orig = mods.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text[] made = clone.GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < made.Length; i++)
            {
                bool wasLabel = i < orig.Length && !string.IsNullOrWhiteSpace(orig[i].text);
                if (!wasLabel) continue;

                // DewLocalizedText rewrites the text from its key, so it has to go
                var loc = made[i].GetComponent<DewLocalizedText>();
                if (loc != null) UnityEngine.Object.DestroyImmediate(loc);

                // the button's second text is the hover tooltip, not a second label
                bool isTooltip = made[i].name.IndexOf("tooltip", StringComparison.OrdinalIgnoreCase) >= 0;
                made[i].text = isTooltip
                    ? "Browse the result screen of your previous runs, including every player's build, skills and gems."
                    : "Run History";
            }

            var bt = clone.GetComponent<Button>();
            if (bt != null)
            {
                // RemoveAllListeners does NOT drop the prefab's PERSISTENT listeners, which
                // is why the clone kept opening the mod manager. replacing the whole event
                // discards both kinds.
                int fromPrefab = bt.onClick.GetPersistentEventCount();
                bt.onClick = new Button.ButtonClickedEvent();
                bt.onClick.AddListener(Open);
                Debug.Log($"{Tag} menu: dropped {fromPrefab} prefab listener(s) on the button");
            }

            clone.SetActive(true);
            Debug.Log($"{Tag} menu: 'Run History' entry installed above Mods");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} menu: failed to install entry: {e.GetType().Name}: {e.Message}");
        }
    }

    static void Open()
    {
        if (ResultViewHarvest.HasCopy)
        {
            ResultViewHarvest.Toggle();
            return;
        }

        if (OnDemandHarvest.Running)
        {
            Debug.Log($"{Tag} menu: harvest already in progress, hold on");
            return;
        }

        var mod = DewRunHistory.Instance;
        if (mod == null)
        {
            Debug.LogWarning($"{Tag} menu: no mod instance to run the harvest on");
            return;
        }

        Debug.Log($"{Tag} menu: no copy yet, harvesting on demand");
        mod.StartCoroutine(HarvestThenOpen());
    }

    static IEnumerator HarvestThenOpen()
    {
        yield return OnDemandHarvest.Run();

        if (ResultViewHarvest.HasCopy)
        {
            ResultViewHarvest.Toggle();
        }
        else
        {
            Debug.LogWarning($"{Tag} menu: on-demand harvest could not get the screen");
        }
    }
}
