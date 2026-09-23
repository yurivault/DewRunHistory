using System;
using HarmonyLib;
using UnityEngine;

namespace DewRunHistory;

// the tooltip box belongs to GlobalUIManager (tooltipBoxTransform) and lives under
// 'Global Canvas', which is ScreenSpaceCamera. our canvas is ScreenSpaceOverlay, and
// Overlay ALWAYS draws after Camera, at any sortingOrder. that's why the tooltip kept
// rendering behind the window no matter how high I pushed its order: wrong lever.
//
// the way out without touching our canvas render mode (which is what got the layout
// right) is to borrow the box into our canvas while the history is open and give it
// back on close. both canvases are the same size with the same scaler, so mirroring
// the parent 'Tooltip Zone' rect lands the game's positioning on the same pixel.
internal static class TooltipLayer
{
    const string Tag = "[DewRunHistory]";

    // above our backdrop (30000) and our view (30001), inside the same root canvas
    internal const int Order = 30100;

    static RectTransform _box;
    static Canvas _canvas;
    static Transform _originalParent;
    static int _originalIndex = -1;
    static bool _origOverride;
    static int _origOrder;
    static RectTransform _ourZone;
    static bool _moved;

    static RectTransform Resolve()
    {
        if (_box != null) return _box;

        var gui = ManagerBase<GlobalUIManager>.instance;
        if (gui == null) return null;

        _box = AccessTools.Field(typeof(GlobalUIManager), "tooltipBoxTransform")?.GetValue(gui) as RectTransform;
        if (_box == null)
        {
            Debug.LogWarning($"{Tag} tooltip: tooltipBoxTransform not found, tooltips will render behind");
            return null;
        }

        _originalParent = _box.parent;
        _originalIndex = _box.GetSiblingIndex();

        _canvas = _box.GetComponent<Canvas>();
        if (_canvas == null)
        {
            // a canvas just for the box. deliberately WITHOUT a GraphicRaycaster: raycasting
            // is per canvas, and one here would make the box steal hover from the item under
            // it and flicker.
            _canvas = _box.gameObject.AddComponent<Canvas>();
            Debug.Log($"{Tag} tooltip: added a private canvas on '{_box.name}'");
        }

        _origOverride = _canvas.overrideSorting;
        _origOrder = _canvas.sortingOrder;
        Debug.Log($"{Tag} tooltip: box='{_box.name}' parent='{NameOf(_originalParent)}' index={_originalIndex}");
        return _box;
    }

    internal static void BringToFront(Canvas ours)
    {
        try
        {
            if (ours == null) return;
            if (Resolve() == null) return;
            if (_moved) return;

            RectTransform zone = EnsureZone(ours);
            if (zone == null) return;

            _box.SetParent(zone, false);
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = Order;
            _moved = true;

            Debug.Log($"{Tag} tooltip: box moved into our canvas (order {Order})");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} tooltip: {e.GetType().Name}: {e.Message}");
        }
    }

    internal static void Restore()
    {
        try
        {
            if (!_moved || _box == null) return;

            if (_originalParent != null)
            {
                _box.SetParent(_originalParent, false);
                if (_originalIndex >= 0) _box.SetSiblingIndex(_originalIndex);
            }

            if (_canvas != null)
            {
                _canvas.overrideSorting = _origOverride;
                _canvas.sortingOrder = _origOrder;
            }

            _moved = false;
            Debug.Log($"{Tag} tooltip: box handed back to '{NameOf(_originalParent)}'");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{Tag} tooltip: {e.GetType().Name}: {e.Message}");
        }
    }

    // copy of the original 'Tooltip Zone' rect. the game positions the box in parent
    // coordinates, so the new parent has to have exactly the same geometry or the
    // tooltip shows up offset from the cursor.
    static RectTransform EnsureZone(Canvas ours)
    {
        if (_ourZone != null) return _ourZone;

        var orig = _originalParent as RectTransform;
        if (orig == null)
        {
            Debug.LogWarning($"{Tag} tooltip: original parent is not a RectTransform, can't mirror the zone");
            return null;
        }

        var go = new GameObject("Tooltip Zone (history)", typeof(RectTransform));
        _ourZone = (RectTransform)go.transform;
        _ourZone.SetParent(ours.transform, false);
        _ourZone.anchorMin = orig.anchorMin;
        _ourZone.anchorMax = orig.anchorMax;
        _ourZone.offsetMin = orig.offsetMin;
        _ourZone.offsetMax = orig.offsetMax;
        _ourZone.pivot = orig.pivot;
        _ourZone.localScale = orig.localScale;
        _ourZone.SetAsLastSibling();

        Debug.Log($"{Tag} tooltip: mirrored zone from '{orig.name}' rect={orig.rect.size:0}");
        return _ourZone;
    }

    static string NameOf(Transform t) => t == null ? "NULL" : t.name;
}
