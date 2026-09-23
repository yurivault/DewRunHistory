using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DewRunHistory;

// the game only keeps the last 20 runs in DewProfile.lastGameResults and prunes the rest.
// this copies each result into a folder of our own before that happens, so the history is
// unbounded and survives reinstalling the mod.
internal static class RunArchive
{
    const string Tag = "[DewRunHistory]";

    internal static string Dir => Path.Combine(Application.persistentDataPath, "RunHistory");

    internal static void Save(DewGameResult r)
    {
        if (r == null) return;

        try
        {
            Directory.CreateDirectory(Dir);
            string path = PathOf(r);
            // ConsumeGameResult can rewrite the same run (it finds it by runId and swaps),
            // so overwriting is the correct behaviour here
            File.WriteAllText(path, DewPersistence.ToJson(r, DewPersistence.GetNewSettings()));
            _cache = null;
            Debug.Log($"{Tag} archived run {r.runId} ({r.result}) as {Path.GetFileName(path)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} failed to archive {r?.runId}: {e}");
        }
    }

    // grab the 20 the game still has and write the missing ones. runs once per session.
    internal static void Backfill()
    {
        try
        {
            List<DewGameResult> runs = DewSave.profileMain?.lastGameResults;
            if (runs == null || runs.Count == 0)
            {
                Debug.Log($"{Tag} backfill: nothing to import yet");
                return;
            }

            Directory.CreateDirectory(Dir);
            int added = 0;
            foreach (DewGameResult r in runs)
            {
                if (r == null || string.IsNullOrEmpty(r.runId)) continue;
                if (File.Exists(PathOf(r))) continue;
                Save(r);
                added++;
            }

            Debug.Log($"{Tag} backfill: {added} new out of {runs.Count}, archive total = {Count()}");
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} backfill failed: {e}");
        }
    }

    internal static int Count()
    {
        try
        {
            return Directory.Exists(Dir) ? Directory.GetFiles(Dir, "*.json").Length : 0;
        }
        catch
        {
            return -1;
        }
    }

    static List<DewGameResult> _cache;

    // reading and deserializing every file on each player switch drowned the game in I/O
    // badly enough to look frozen. only reload when the archive changes.
    internal static List<DewGameResult> LoadAll()
    {
        if (_cache != null) return _cache;

        var output = new List<DewGameResult>();
        try
        {
            if (!Directory.Exists(Dir)) return output;

            foreach (string f in Directory.GetFiles(Dir, "*.json"))
            {
                try
                {
                    var r = DewPersistence.FromJson<DewGameResult>(File.ReadAllText(f), DewPersistence.GetNewSettings());
                    if (r != null) output.Add(r);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{Tag} unreadable file {Path.GetFileName(f)}: {e.Message}");
                }
            }

            output = output.OrderByDescending(x => x.startTimestamp).ToList();
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} LoadAll failed: {e}");
        }

        _cache = output;
        Debug.Log($"{Tag} archive read from disk: {output.Count} runs (cached)");
        return _cache;
    }

    static string PathOf(DewGameResult r)
    {
        string id = new string((r.runId ?? "no-id").Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        return Path.Combine(Dir, $"{r.startTimestamp}_{id}.json");
    }
}
