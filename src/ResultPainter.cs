using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DewRunHistory;

// paints a DewGameResult into the copy of the screen. ResultViewHarvest builds the copy
// and hands over the fields it read off the UI_InGame_ResultView component before
// destroying it, because once that's dead there's no asking it which art belongs to
// which result type.
internal static class ResultPainter
{
    const string Tag = "[DewRunHistory]";

    static GameObject _view;
    static GameObject[] _gameOver, _unknownFate, _pureWhite, _starless;
    static TextMeshProUGUI _totalScoreText;
    static TextMeshProUGUI _subGameOver, _subPureWhite, _subStarless;
    static GameObject _multiplePlayers;

    static readonly FieldInfoCache F = new FieldInfoCache();

    // the side list copies its font from here so it doesn't clash with the rest
    internal static TextMeshProUGUI FontSource => _totalScoreText;
    internal static GameObject MultiplePlayersObject => _multiplePlayers;

    internal static void Capture(GameObject view, UI_InGame_ResultView comp)
    {
        _view = view;
        if (comp == null) return;

        _gameOver = F.Get<GameObject[]>(comp, "gameOverObjects");
        _unknownFate = F.Get<GameObject[]>(comp, "unknownFateObjects");
        _pureWhite = F.Get<GameObject[]>(comp, "pureWhiteDreamObjects");
        _starless = F.Get<GameObject[]>(comp, "starlessPathObjects");
        _totalScoreText = F.Get<TextMeshProUGUI>(comp, "totalScoreText");
        _subGameOver = F.Get<TextMeshProUGUI>(comp, "gameOverSubtitle");
        _subPureWhite = F.Get<TextMeshProUGUI>(comp, "pureWhiteDreamSubtitle");
        _subStarless = F.Get<TextMeshProUGUI>(comp, "starlessPathSubtitle");
        _multiplePlayers = F.Get<GameObject>(comp, "multiplePlayersObject");

        Debug.Log($"{Tag} painter: art per type -> over={_gameOver?.Length ?? -1} fate={_unknownFate?.Length ?? -1} " +
                  $"white={_pureWhite?.Length ?? -1} starless={_starless?.Length ?? -1} score={_totalScoreText != null}");
    }

    // a run under a minute is a test or a misclick. one abandoned before 5 minutes has no
    // build to look at either. the header says how many got left out.
    internal static bool IsWorthShowing(DewGameResult r)
    {
        if (r.elapsedGameTimeSeconds < 60f) return false;
        if (r.result == DewGameResult.ResultType.Conceded && r.elapsedGameTimeSeconds < 300f) return false;
        return true;
    }

    internal static int Paint(DewGameResult r, int playerIdx, int nPlayers)
    {
        float mult = ScoreMultiplier(r);
        ApplyResultArt(r);
        ApplySubtitle(r);

        double total = 0;
        int ok = 0, failed = 0;
        foreach (MonoBehaviour mb in _view.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x is IGameResultStatItem))
        {
            try
            {
                total += ((IGameResultStatItem)mb).UpdateAndGetScore(r, playerIdx, mult);
                ok++;
            }
            catch (Exception e)
            {
                // expected on the skill items: their last line uses ControlManager.instance,
                // which doesn't exist outside a match. everything before it already applied.
                failed++;
                if (failed <= 2) Debug.LogWarning($"{Tag} painter: {mb.GetType().Name}: {e.GetType().Name}");
            }

            FixKeyText(mb);
        }

        int score = Mathf.RoundToInt((float)total);
        if (_totalScoreText != null) _totalScoreText.text = score.ToString();
        ShowPlayerPosition(playerIdx + 1, nPlayers);

        Debug.Log($"{Tag} painter: mult={mult} score={score} items ok={ok} failed={failed}");
        return score;
    }

    // the original screen has no counter because the game assumes you just played and know
    // who was in the match. in a history view you don't, so show the position.
    static void ShowPlayerPosition(int current, int total)
    {
        try
        {
            if (total <= 1) return;

            var nameItem = _view.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(mb => mb is UI_InGame_Result_PlayerName);
            if (nameItem == null) return;

            var txt = F.Get<TextMeshProUGUI>(nameItem, "playerName");
            if (txt == null) return;

            txt.text = $"{txt.text}  ({current}/{total})";
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} position: {e.GetType().Name}: {e.Message}");
        }
    }

    // mirrors what the original OnShow does. it goes through GetGameOverText(), which only
    // needs GameManager.instance (gone outside a match) for the override, so the rest of
    // the logic can be reproduced in full.
    static void ApplySubtitle(DewGameResult r)
    {
        try
        {
            DewGameResult.PlayerData me = r.players?.Find(p => p.isLocalPlayer) ?? r.players?.FirstOrDefault();

            if (r.result == DewGameResult.ResultType.GameOver && _subGameOver != null)
            {
                _subGameOver.text = GameOverText(me);
            }
            else if (r.result == DewGameResult.ResultType.PureWhiteDream && _subPureWhite != null)
            {
                _subPureWhite.text = me == null
                    ? "BWAHAHAHA!"
                    : DewLocalization.GetUIValue("InGame_Result_Subtitle_PureWhiteDream_" + me.heroType);
            }
            else if (r.result == DewGameResult.ResultType.StarlessPath && _subStarless != null)
            {
                _subStarless.text = me == null
                    ? "BWAHAHAHA!"
                    : DewLocalization.GetUIValue("InGame_Result_Subtitle_StarlessPath");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} subtitle: {e.GetType().Name}: {e.Message}");
        }
    }

    static string GameOverText(DewGameResult.PlayerData me)
    {
        if (me == null) return "";

        string cause = me.causeOfDeathActor ?? "";
        foreach (string elem in new[] { "Fire", "Ice", "Dark", "Light", "Spider" })
        {
            if (cause.Contains(elem) && UnityEngine.Random.value < 0.1f)
            {
                return DewLocalization.GetUIValue($"InGame_Result_Subtitle_GameOver_{elem}_{UnityEngine.Random.Range(0, 3)}");
            }
        }

        if (UnityEngine.Random.value < 0.1f)
        {
            return DewLocalization.GetUIValue($"InGame_Result_Subtitle_GameOver_{me.heroType}_{UnityEngine.Random.Range(0, 3)}");
        }

        return DewLocalization.GetUIValue($"InGame_Result_Subtitle_GameOver_{UnityEngine.Random.Range(0, 10)}");
    }

    // the game resolves this through ControlManager.instance, which only exists in a match.
    // here we go straight to the source ControlManager reads from.
    static void FixKeyText(MonoBehaviour mb)
    {
        if (!(mb is UI_InGame_Result_HeroSkillItem)) return;

        try
        {
            var txt = F.Get<TextMeshProUGUI>(mb, "activationKeyText");
            if (txt == null) return;

            var loc = F.Get<HeroSkillLocation>(mb, "type");
            DewBinding bind = DewSave.profileMain.controls.GetSkillBinding(loc);
            txt.text = DewInput.GetReadableTextForCurrentMode(bind);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} key text: {e.GetType().Name}: {e.Message}");
        }
    }

    static void ApplyResultArt(DewGameResult r)
    {
        SetAll(_gameOver, r.result == DewGameResult.ResultType.GameOver);
        SetAll(_unknownFate, r.result == DewGameResult.ResultType.UnknownFate);
        SetAll(_pureWhite, r.result == DewGameResult.ResultType.PureWhiteDream);
        SetAll(_starless, r.result == DewGameResult.ResultType.StarlessPath);
    }

    static void SetAll(GameObject[] arr, bool on)
    {
        if (arr == null) return;
        foreach (GameObject g in arr)
        {
            if (g != null) g.SetActive(on);
        }
    }

    static float ScoreMultiplier(DewGameResult r)
    {
        try
        {
            var s = DewResources.GetByName<DewDifficultySettings>(r.difficulty, default);
            if (s != null) return s.scoreMultiplier;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} difficulty '{r.difficulty}': {e.Message}");
        }

        return 1f;
    }

    // small cache so we're not calling AccessTools on every render frame
    class FieldInfoCache
    {
        readonly Dictionary<string, System.Reflection.FieldInfo> _c = new Dictionary<string, System.Reflection.FieldInfo>();

        internal T Get<T>(object target, string field)
        {
            string k = target.GetType().Name + "." + field;
            if (!_c.TryGetValue(k, out var fi))
            {
                fi = AccessTools.Field(target.GetType(), field);
                _c[k] = fi;
            }

            if (fi == null) return default;
            object v = fi.GetValue(target);
            return v is T t ? t : default;
        }
    }
}
