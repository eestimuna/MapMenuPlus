using System;
using System.Collections.Generic;
using UnityEngine;
using LaserCursor = Il2CppSLZ.Bonelab.LaserCursor;

namespace MapMenuPlus
{
    /// <summary>
    /// 1.3.3 fix: "the laser's cursor goes invisible on the keyboard and on the X".
    ///
    /// BONELAB's laser cursor (LaserCursor) is only drawn inside its "cursor regions": boxes around the game's menu page
    /// (Fusion builds its own panels the same way: LaserCursor.regions = { new CursorRegion { bounds, center } }).
    /// The new spawn menu is wider than the game's page and the keyboard hangs below it, so on the X, the sidebar's
    /// left edge and the whole keyboard the cursor disappeared (the clicks still worked).
    ///
    /// This adds ONE more region to the game's laser cursor(s) while the menu is open: a box around the whole menu and
    /// the keyboard. Its centre is our own unscaled object that follows the menu, so the box is in metres. The region
    /// is removed again when the menu closes (or the level changes), so nothing is left behind.
    /// </summary>
    internal sealed class CursorArea
    {
        private const float RefindEvery = 3f;

        private GameObject _go;
        private readonly List<LaserCursor> _cursors = new List<LaserCursor>();
        private float _nextFind;
        private Vector3 _size;
        private bool _attached;
        private int _logs;

        /// <summary>
        /// Every frame while the menu is open. frame = the menu's transform; centre/size = the area in its units
        /// (x, y), depth = how far in front of / behind its plane the box reaches (in its units).
        /// </summary>
        internal void Show(float now, Transform frame, Vector2 centre, Vector2 size, float depth)
        {
            if (frame == null) return;
            try
            {
                if (_go == null) _go = new GameObject("MMP_CursorArea");
                float unit = (frame.TransformPoint(new Vector3(1f, 0f, 0f)) - frame.TransformPoint(Vector3.zero)).magnitude;
                if (unit < 1e-7f) return;
                Transform t = _go.transform;
                t.position = frame.TransformPoint(new Vector3(centre.x, centre.y, 0f));
                t.rotation = frame.rotation;
                Vector3 want = new Vector3(size.x * unit, size.y * unit, depth * unit);
                bool resized = (want - _size).sqrMagnitude > 1e-6f;
                if (resized || !_attached || now >= _nextFind)
                {
                    _size = want;
                    _nextFind = now + RefindEvery;
                    Attach();
                }
            }
            catch (Exception e)
            {
                if (_logs++ < 5) Log.Error("Cursor area update failed", e);
            }
        }

        /// <summary>The menu closed: take our region out of the game's cursors again.</summary>
        internal void Hide()
        {
            if (!_attached) return;
            _attached = false;
            foreach (LaserCursor lc in _cursors)
            {
                try { if (lc != null) SetRegions(lc, false); } catch { }
            }
            _cursors.Clear();
        }

        internal void Reset()
        {
            Hide();
            try { if (_go != null) UnityEngine.Object.Destroy(_go); } catch { }
            _go = null;
            _size = Vector3.zero;
        }

        private void Attach()
        {
            _cursors.Clear();
            foreach (LaserCursor lc in UnityEngine.Object.FindObjectsOfType<LaserCursor>(true))
                if (lc != null) _cursors.Add(lc);
            int done = 0;
            foreach (LaserCursor lc in _cursors)
            {
                try { if (SetRegions(lc, true)) done++; }
                catch (Exception e) { if (_logs++ < 5) Log.Warn("Adding the cursor area failed: " + e.Message); }
            }
            if (!_attached && _logs++ < 5)
                Log.Info("Laser cursor area added to " + done + " of " + _cursors.Count + " laser cursor(s) (" + (_size.x * 100f).ToString("0") + " x " + (_size.y * 100f).ToString("0") + " cm).");
            _attached = true;
        }

        /// <summary>Rebuilds a cursor's regions: all of the game's own ones, plus ours (when add). True when ours is in.</summary>
        private bool SetRegions(LaserCursor lc, bool add)
        {
            var old = lc.regions;
            var list = new List<LaserCursor.CursorRegion>();
            int ours = 0;
            if (old != null)
                for (int i = 0; i < old.Length; i++)
                {
                    LaserCursor.CursorRegion r = old[i];
                    Transform c = null;
                    try { c = r.center; } catch { }
                    if (c != null && _go != null && c.GetInstanceID() == _go.transform.GetInstanceID()) { ours++; continue; }
                    list.Add(r);
                }
            if (!add && ours == 0) return false;   // nothing of ours in there: leave the game's array alone
            if (add && _go != null)
                list.Add(new LaserCursor.CursorRegion { bounds = new Bounds(Vector3.zero, _size), center = _go.transform });
            lc.regions = list.ToArray();
            return add;
        }
    }
}
