using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DewRunHistory;

// builds and places the copy of the result screen. everything here is a pure function of
// the (root, view) pair: ResultViewHarvest is what holds state and decides when to open.
internal static class ResultViewClone
{
    const string Tag = "[DewRunHistory]";

    internal static GameObject BuildRoot(Canvas origCanvas)
    {
        var root = new GameObject("DewRunHistory Root");
        UnityEngine.Object.DontDestroyOnLoad(root);

        var cv = root.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 30000;

        var scaler = root.AddComponent<CanvasScaler>();
        var src = origCanvas == null ? null : origCanvas.GetComponent<CanvasScaler>();
        if (src == null)
        {
            Debug.LogWarning($"{Tag} harvest: original canvas has no CanvasScaler, layout may vary by resolution");
        }
        else
        {
            // copying only half the config left our canvas at 1:1 pixels while the game's
            // scaled, which breaks the layout and ties everything to whatever resolution
            // the author happened to test on
            scaler.uiScaleMode = src.uiScaleMode;
            scaler.referenceResolution = src.referenceResolution;
            scaler.screenMatchMode = src.screenMatchMode;
            scaler.matchWidthOrHeight = src.matchWidthOrHeight;
            scaler.scaleFactor = src.scaleFactor;
            scaler.referencePixelsPerUnit = src.referencePixelsPerUnit;
            scaler.physicalUnit = src.physicalUnit;
            scaler.fallbackScreenDPI = src.fallbackScreenDPI;
            scaler.defaultSpriteDPI = src.defaultSpriteDPI;
            Debug.Log($"{Tag} harvest: CanvasScaler copied -> mode={scaler.uiScaleMode} ref={scaler.referenceResolution} " +
                      $"match={scaler.matchWidthOrHeight} factor={scaler.scaleFactor}");
        }

        root.AddComponent<GraphicRaycaster>();
        return root;
    }

    // the original was hidden, and the game hides a View in three independent ways.
    // the clone inherits all three, so all three have to be undone by hand.
    internal static void Unhide(GameObject view)
    {
        var cg = view.GetComponent<CanvasGroup>() ?? view.AddComponent<CanvasGroup>();
        Debug.Log($"{Tag} harvest: CanvasGroup alpha was {cg.alpha}, opening it");
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        // the View has its own Canvas and UpdateComponentStatus does
        // _canvas.enabled = alpha > 0.0001. hidden, that Canvas comes over disabled in the
        // clone, and nothing draws no matter how correct everything else is.
        foreach (Canvas c in view.GetComponentsInChildren<Canvas>(true))
        {
            if (!c.enabled) Debug.Log($"{Tag} harvest: re-enabling Canvas '{c.name}'");
            c.enabled = true;
        }

        var own = view.GetComponent<Canvas>();
        if (own != null)
        {
            own.overrideSorting = true;
            own.sortingOrder = 30001;
        }
    }

    // the original parent was something else, so the inherited offsets are meaningless.
    // the left strip is handled by Fit, at open time, because only there do we know the
    // canvas width at the current resolution.
    internal static void Reanchor(GameObject view)
    {
        if (!(view.transform is RectTransform rt)) return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    // the screen's content is centred and has a fixed size in canvas units. pushing the
    // left edge in isn't enough: anything wider than what's left just spills back under
    // the list. scaling the whole view down means nothing can overlap.
    internal static void Fit(GameObject root, GameObject view)
    {
        try
        {
            var rootRt = root.transform as RectTransform;
            var rt = view.transform as RectTransform;
            if (rootRt == null || rt == null) return;

            float w = rootRt.rect.width;
            if (w <= 1f)
            {
                Debug.LogWarning($"{Tag} fit: canvas width is {w}, staying at 1:1");
                return;
            }

            float s = Mathf.Clamp((w - RunListPanel.TotalWidth) / w, 0.5f, 1f);
            rt.localScale = new Vector3(s, s, 1f);

            // it shrinks around the centre, leaving w*(1-s)/2 on each side. shifting right
            // by half the strip turns the left margin into exactly the list width.
            float half = RunListPanel.TotalWidth * 0.5f;
            rt.offsetMin = new Vector2(half, 0f);
            rt.offsetMax = new Vector2(half, 0f);

            Debug.Log($"{Tag} fit: canvas={w:0}x{rootRt.rect.height:0} list={RunListPanel.TotalWidth} scale={s:0.000}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} fit: {e.GetType().Name}: {e.Message}");
        }
    }

    // a child canvas with overrideSorting uses an ABSOLUTE order. inherited from the prefab
    // those are 0..90, i.e. below our backdrop at 30000, so whole blocks vanished behind it.
    // rebase into our band keeping the relative order, always below the tooltip.
    internal static void NormalizeChildCanvases(GameObject view)
    {
        try
        {
            var touched = new List<string>();
            foreach (Canvas c in view.GetComponentsInChildren<Canvas>(true))
            {
                if (c.transform == view.transform) continue;
                if (!c.overrideSorting) continue;

                int now = 30002 + Mathf.Clamp(c.sortingOrder, 0, 90);
                touched.Add($"{c.name}:{c.sortingOrder}->{now}");
                c.sortingOrder = now;
            }

            Debug.Log($"{Tag} order: child canvases rebased = " +
                      (touched.Count == 0 ? "none" : string.Join(" ", touched)));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} order: {e.GetType().Name}: {e.Message}");
        }
    }

    // inside a match the background is the game world. on the title it was the menu
    // showing through the panel, so it needs a backdrop of its own.
    internal static void CreateBackdrop(GameObject root)
    {
        var back = new GameObject("Backdrop", typeof(Image));
        back.transform.SetParent(root.transform, false);
        back.transform.SetAsFirstSibling();

        var rt = (RectTransform)back.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        back.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.06f, 0.97f);
        Debug.Log($"{Tag} harvest: backdrop created");
    }

    // "Continue" and the ready status belong to the real end-of-match screen.
    // in a history view they make no sense and wouldn't work anyway.
    internal static void HideEndOfMatchControls(GameObject view)
    {
        string[] drop = { "Continue Toggle", "Ready Status Text" };
        foreach (string name in drop)
        {
            Transform t = view.transform.Find(name);
            if (t != null)
            {
                t.gameObject.SetActive(false);
                Debug.Log($"{Tag} harvest: hid '{name}'");
            }
        }

        // the player arrows only show up when the run had more than one
        GameObject group = ResultPainter.MultiplePlayersObject;
        if (group != null) group.SetActive(true);
    }

    // the player arrows exist in the prefab, but their onClick pointed at the
    // UI_InGame_ResultView component we destroy. wire them back to our code.
    internal static void WireArrows(GameObject view, Action<int> changePlayer)
    {
        try
        {
            GameObject group = ResultPainter.MultiplePlayersObject;
            Transform scope = group != null ? group.transform : view.transform;
            Button[] buttons = scope.GetComponentsInChildren<Button>(true);
            Debug.Log($"{Tag} arrows: {buttons.Length} buttons under '{scope.name}' -> {string.Join(", ", buttons.Select(b => b.name))}");

            int wired = 0;
            foreach (Button b in buttons)
            {
                string n = b.name.ToLowerInvariant();
                bool back = n.Contains("prev") || n.Contains("left");
                bool fwd = n.Contains("next") || n.Contains("right");
                if (!back && !fwd) continue;

                b.onClick.RemoveAllListeners();
                int d = back ? -1 : 1;
                b.onClick.AddListener(() => changePlayer(d));
                wired++;
            }

            // if names didn't help and there are exactly two, assume order: first goes back
            if (wired == 0 && buttons.Length == 2)
            {
                buttons[0].onClick.RemoveAllListeners();
                buttons[0].onClick.AddListener(() => changePlayer(-1));
                buttons[1].onClick.RemoveAllListeners();
                buttons[1].onClick.AddListener(() => changePlayer(1));
                wired = 2;
                Debug.Log($"{Tag} arrows: names didn't match, wired by order");
            }

            Debug.Log($"{Tag} arrows: {wired} wired");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} arrows: {e.GetType().Name}: {e.Message}");
        }
    }
}
