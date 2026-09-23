using System;
using HarmonyLib;
using UnityEngine;

namespace DewRunHistory.patch;

// DewSave.ConsumeGameResult is where a result enters the profile and the 20 entry list
// gets pruned. a postfix here catches every run, including the one about to be dropped.
internal static class ConsumeGameResult_Patch
{
    const string Target = "DewSave:ConsumeGameResult";

    internal static void Apply(Harmony harmony)
    {
        try
        {
            var m = AccessTools.Method(Target);
            if (m == null)
            {
                Debug.LogWarning($"[DewRunHistory] {Target} not found, automatic archiving is off");
                return;
            }

            harmony.Patch(m, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(ConsumeGameResult_Patch), nameof(Postfix))));
            Debug.Log($"[DewRunHistory] archiver hooked on {Target}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DewRunHistory] failed to patch {Target}: {e}");
        }
    }

    static void Postfix(DewGameResult result)
    {
        RunArchive.Save(result);
    }
}
