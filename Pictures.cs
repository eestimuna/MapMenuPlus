using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace MapMenuPlus
{
    /// <summary>
    /// The map pictures as textures (main thread). A picture is decoded when it is wanted and its file is downloaded,
    /// at most one per frame (so opening a page never makes the game stutter). Textures have mipmaps and trilinear
    /// filtering (calm, sharp pictures in VR) and are kept for the next level; the least recently used ones are
    /// thrown away above ~50 card pictures / 6 big pictures (memory).
    /// </summary>
    internal sealed class Pictures
    {
        private const int MaxSmall = 54, MaxBig = 6;

        private sealed class Slot
        {
            internal Texture2D Tex;
            internal bool Big;
            internal int LastFrame;
        }

        private sealed class Want
        {
            internal int Frame, Order;
            internal bool Big;
        }

        private readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>();
        private readonly HashSet<string> _bad = new HashSet<string>();
        private readonly Dictionary<string, Want> _want = new Dictionary<string, Want>();
        private readonly List<string> _drop = new List<string>();
        private int _frame, _order, _decoded, _logs;
        private double _decodeMs;

        internal int Decoded { get { return _decoded; } }
        internal double DecodeMs { get { return _decoded > 0 ? _decodeMs / _decoded : 0; } }

        /// <summary>The texture of a picture if it is ready (else null; it is then decoded soon if its file is there).</summary>
        internal Texture2D Get(string url, bool big = false)
        {
            if (string.IsNullOrEmpty(url)) return null;
            Slot s;
            if (_slots.TryGetValue(url, out s) && s.Tex != null)
            {
                s.LastFrame = _frame;
                return s.Tex;
            }
            if (_bad.Contains(url)) return null;
            Want w;
            if (!_want.TryGetValue(url, out w)) _want[url] = w = new Want { Order = _order++ };
            w.Frame = _frame;
            w.Big = big;
            return null;
        }

        internal bool Failed(string url) { return url != null && (_bad.Contains(url) || ModIo.Failed(url)); }

        /// <summary>
        /// Once per frame: decodes ONE picture whose file is ready - the one the menu asked for most recently (what you
        /// look at now comes before the page you just left).
        /// </summary>
        internal bool Tick()
        {
            _frame++;
            string best = null;
            Want bw = null;
            string bestFile = null;
            _drop.Clear();
            foreach (var kv in _want)
            {
                Want w = kv.Value;
                if (_frame - w.Frame > 900 || _bad.Contains(kv.Key) || ModIo.Failed(kv.Key)) { _drop.Add(kv.Key); continue; }
                if (bw != null && (w.Frame < bw.Frame || (w.Frame == bw.Frame && w.Order > bw.Order))) continue;
                string file = ModIo.FileOf(kv.Key);
                if (file == null) continue;
                best = kv.Key;
                bw = w;
                bestFile = file;
            }
            foreach (string d in _drop) _want.Remove(d);
            if (best == null) return false;
            _want.Remove(best);
            Decode(best, bestFile, bw.Big);
            return true;
        }

        private void Decode(string url, string file, bool big)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!ImageConversion.LoadImage(tex, new Il2CppStructArray<byte>(bytes), true) || tex.width <= 8)
                {
                    UnityEngine.Object.Destroy(tex);
                    _bad.Add(url);
                    ModIo.MarkBroken(url);
                    if (_logs++ < 10) Log.Info("A picture could not be read (" + bytes.Length + " bytes): " + url);
                    return;
                }
                tex.filterMode = FilterMode.Trilinear;
                tex.anisoLevel = 4;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                tex.name = "MMP_" + Path.GetFileNameWithoutExtension(file);
                _slots[url] = new Slot { Tex = tex, Big = big, LastFrame = _frame };
                _decoded++;
                Evict(big);
            }
            catch (Exception e)
            {
                _bad.Add(url);
                if (_logs++ < 10) Log.Error("Decoding a picture failed", e);
            }
            finally
            {
                sw.Stop();
                _decodeMs += sw.Elapsed.TotalMilliseconds;
                if (sw.Elapsed.TotalMilliseconds > 25 && _logs++ < 20) Log.Info("Slow picture decode: " + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms (" + (big ? "big" : "card") + ").");
            }
        }

        /// <summary>Throws away the least recently shown textures above the limit (never one shown in the last 2 frames).</summary>
        private void Evict(bool big)
        {
            int max = big ? MaxBig : MaxSmall;
            int n = 0;
            foreach (var kv in _slots) if (kv.Value.Big == big) n++;
            while (n > max)
            {
                string oldest = null;
                int oldestFrame = int.MaxValue;
                foreach (var kv in _slots)
                    if (kv.Value.Big == big && kv.Value.LastFrame < oldestFrame && _frame - kv.Value.LastFrame > 2) { oldest = kv.Key; oldestFrame = kv.Value.LastFrame; }
                if (oldest == null) return;
                try { UnityEngine.Object.Destroy(_slots[oldest].Tex); } catch { }
                _slots.Remove(oldest);
                n--;
            }
        }

        /// <summary>BoneMenu "Download pictures again": all textures go (the menu asks for them again).</summary>
        internal void Clear()
        {
            foreach (var kv in _slots) { try { UnityEngine.Object.Destroy(kv.Value.Tex); } catch { } }
            _slots.Clear();
            _bad.Clear();
            _want.Clear();
        }
    }
}
