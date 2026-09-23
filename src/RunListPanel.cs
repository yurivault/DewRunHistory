using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewRunHistory;

// side panel, one row per run. it sits BESIDE the result screen, not on top of it:
// the result screen gives up TotalWidth on the left and re-centres in what's left.
internal static class RunListPanel
{
    const string Tag = "[DewRunHistory]";
    const float Margin = 36f;
    const float Width = 430f;
    const float RowHeight = 52f;

    // how much the result screen has to give up on the left so the list covers nothing
    internal const float TotalWidth = Margin + Width + 24f;

    static GameObject _panel;
    static RectTransform _content;
    static TextMeshProUGUI _header;
    static TMP_FontAsset _font;
    static Material _material;

    static readonly List<Image> _rows = new List<Image>();
    static readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();
    static Action<int> _onPick;
    static Action _onClose;

    internal static bool Exists => _panel != null;

    internal static void Create(Transform parent, TextMeshProUGUI fontFrom, Action<int> onPick, Action onClose)
    {
        try
        {
            if (_panel != null) return;

            _onPick = onPick;
            _onClose = onClose;
            if (fontFrom != null)
            {
                _font = fontFrom.font;
                _material = fontFrom.fontSharedMaterial;
            }

            _panel = NewRect("Run List (history)", parent);
            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(Margin, 70f);
            rt.offsetMax = new Vector2(Margin + Width, -70f);

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.09f, 0.9f);

            GameObject head = NewRect("Header", _panel.transform);
            var hrt = (RectTransform)head.transform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.offsetMin = new Vector2(16f, -86f);
            hrt.offsetMax = new Vector2(-58f, -10f);
            _header = Label(head, 22f, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.88f, 1f));

            CreateCloseButton();

            GameObject scroll = NewRect("Scroll", _panel.transform);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(10f, 10f);
            srt.offsetMax = new Vector2(-10f, -90f);

            var sr = scroll.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f;

            GameObject viewport = NewRect("Viewport", scroll.transform);
            var vrt = (RectTransform)viewport.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            GameObject content = NewRect("Content", viewport.transform);
            _content = (RectTransform)content.transform;
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3f;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = vrt;
            sr.content = _content;

            Debug.Log($"{Tag} list: panel built (total reserved width = {TotalWidth})");
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} list: failed to build: {e}");
        }
    }

    static void CreateCloseButton()
    {
        GameObject bt = NewRect("Close", _panel.transform);
        var brt = (RectTransform)bt.transform;
        brt.anchorMin = new Vector2(1f, 1f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(1f, 1f);
        brt.anchoredPosition = new Vector2(-12f, -12f);
        brt.sizeDelta = new Vector2(36f, 36f);

        var img = bt.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.09f);

        var button = bt.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(() => _onClose?.Invoke());

        GameObject x = NewRect("X", bt.transform);
        var xrt = (RectTransform)x.transform;
        xrt.anchorMin = Vector2.zero;
        xrt.anchorMax = Vector2.one;
        xrt.offsetMin = Vector2.zero;
        xrt.offsetMax = Vector2.zero;
        var t = Label(x, 22f, TextAlignmentOptions.Center, new Color(0.9f, 0.92f, 1f));
        t.text = "X";
        t.raycastTarget = false;
    }

    internal static void Fill(List<DewGameResult> runs, int selected, int hidden)
    {
        try
        {
            if (_panel == null || _content == null) return;

            for (int i = _rows.Count; i < runs.Count; i++)
            {
                int idx = i;
                GameObject row = NewRect($"Run {i}", _content);
                var rrt = (RectTransform)row.transform;
                rrt.sizeDelta = new Vector2(0f, RowHeight);

                var img = row.AddComponent<Image>();
                img.color = Color.clear;

                var bt = row.AddComponent<Button>();
                bt.targetGraphic = img;
                bt.onClick.AddListener(() => _onPick?.Invoke(idx));

                GameObject lbl = NewRect("Label", row.transform);
                var lrt = (RectTransform)lbl.transform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(12f, 2f);
                lrt.offsetMax = new Vector2(-12f, -2f);

                var txt = Label(lbl, 17f, TextAlignmentOptions.Left, Color.white);
                txt.raycastTarget = false;

                var le = row.AddComponent<LayoutElement>();
                le.preferredHeight = RowHeight;
                le.minHeight = RowHeight;

                _rows.Add(img);
                _labels.Add(txt);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < runs.Count;
                _rows[i].transform.parent.gameObject.SetActive(used);
                if (!used) continue;

                DewGameResult r = runs[i];
                _rows[i].color = i == selected
                    ? new Color(0.35f, 0.55f, 0.95f, 0.55f)
                    : new Color(1f, 1f, 1f, 0.05f);

                string when = DateTimeOffset.FromUnixTimeSeconds(r.startTimestamp).LocalDateTime.ToString("dd/MM/yy HH:mm");
                int mins = Mathf.RoundToInt(r.elapsedGameTimeSeconds / 60f);
                int players = r.players?.Count ?? 0;

                _labels[i].text = $"<b>{when}</b>   <color=#{ResultColor(r.result)}>{ResultLabel(r.result)}</color>\n" +
                                  $"<size=14><color=#9AA6C4>{mins} min   -   {players} player{(players == 1 ? "" : "s")}   -   {PrettyDifficulty(r.difficulty)}</color></size>";
            }

            if (_header != null)
            {
                string extra = hidden > 0 ? $"   <size=13>({hidden} short runs hidden)</size>" : "";
                _header.text = $"<b>Run History</b>   <size=16><color=#9AA6C4>{runs.Count} runs{extra}</color></size>\n" +
                               "<size=14><color=#9AA6C4>Click a run to open   -   arrows switch player</color></size>";
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} list: failed to fill: {e}");
        }
    }

    // "diffNightmare" means nothing to a player, the game only ever shows the icon
    static string PrettyDifficulty(string d)
    {
        if (string.IsNullOrEmpty(d)) return "?";
        return d.StartsWith("diff", StringComparison.OrdinalIgnoreCase) ? d.Substring(4) : d;
    }

    static string ResultLabel(DewGameResult.ResultType t)
    {
        switch (t)
        {
            case DewGameResult.ResultType.GameOver: return "Game Over";
            case DewGameResult.ResultType.Conceded: return "Abandoned";
            case DewGameResult.ResultType.PureWhiteDream: return "Pure White Dream";
            case DewGameResult.ResultType.StarlessPath: return "Starless Path";
            default: return "Unknown Fate";
        }
    }

    static string ResultColor(DewGameResult.ResultType t)
    {
        switch (t)
        {
            case DewGameResult.ResultType.PureWhiteDream: return "FFD98A";
            case DewGameResult.ResultType.StarlessPath: return "C9A0FF";
            case DewGameResult.ResultType.GameOver: return "FF9B9B";
            default: return "8C93A8";
        }
    }

    static GameObject NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static TextMeshProUGUI Label(GameObject target, float size, TextAlignmentOptions align, Color color)
    {
        var t = target.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        if (_material != null) t.fontSharedMaterial = _material;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Truncate;
        return t;
    }
}
