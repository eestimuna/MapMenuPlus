using System;
using System.Reflection;
using UnityEngine;

namespace MapMenuPlus
{
    /// <summary>
    /// Finds the game's Levels page (LevelsPanelView): a Harmony postfix on its Activate (found by name) remembers the
    /// page when the game opens it; as a backup the page is also searched for a few times per level. The patch only
    /// remembers the page - it changes nothing in the game's method.
    /// </summary>
    internal static class Patches
    {
        internal static object LastActivated;      // the LevelsPanelView (as its own interop type)
        internal static bool Applied { get; private set; }
        private static float _nextScan;
        private static int _scansThisLevel;
        private static MethodInfo _find;

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Type t = Levels.PanelType;
            if (t == null) { Log.Warn("No Levels page class: the new map menu cannot attach (the game's menu stays as it is)."); return; }
            foreach (string name in new[] { "Activate", "OnEnable", "Start" })
            {
                try
                {
                    MethodInfo original = null;
                    foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        if (m.Name == name && m.GetParameters().Length == 0) { original = m; break; }
                    if (original == null) continue;
                    MethodInfo post = typeof(Patches).GetMethod(nameof(ActivatePostfix), BindingFlags.NonPublic | BindingFlags.Static);
                    harmony.Patch(original, postfix: new HarmonyLib.HarmonyMethod(post));
                    Applied = true;
                    Log.Info("Levels page hook: " + t.Name + "." + name + " patched.");
                    return;
                }
                catch (Exception e)
                {
                    Log.Error("Patching " + t.Name + "." + name + " failed", e);
                }
            }
            Log.Warn("No Activate method on the Levels page: using the backup search only.");
        }

        private static void ActivatePostfix(object __instance)
        {
            try { if (__instance != null) LastActivated = __instance; }
            catch { }
        }

        internal static void Reset()
        {
            LastActivated = null;
            _nextScan = Time.realtimeSinceStartup + 3f;
            _scansThisLevel = 0;
        }

        /// <summary>The Levels page (open or not) as a Component of its own type, or null while none is known.</summary>
        internal static Component Find(float now)
        {
            object o = LastActivated;
            Component c = null;
            try
            {
                c = R.Cast(o, Levels.PanelType) as Component;   // as the page's own type (its members are read by name)
                if (c != null && c.gameObject != null) return c;
            }
            catch { }
            LastActivated = null;
            // searching the whole level costs a little time: with the hook working only twice per level (to build the
            // menu before you open it), without the hook every 10 seconds
            if (now < _nextScan) return null;
            if (Applied && _scansThisLevel >= 2) return null;
            _scansThisLevel++;
            _nextScan = now + (Applied ? 12f : 10f);
            Type t = Levels.PanelType;
            if (t == null) return null;
            try
            {
                Component pick = null;
                if (_find == null)
                    foreach (MethodInfo m in typeof(UnityEngine.Object).GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        ParameterInfo[] ps = m.GetParameters();
                        if (m.Name == "FindObjectsOfType" && m.IsGenericMethodDefinition && ps.Length == 1 && ps[0].ParameterType == typeof(bool))
                        {
                            _find = m.MakeGenericMethod(t);
                            break;
                        }
                    }
                if (_find == null) { Log.Warn("No FindObjectsOfType<T>(bool): backup search off."); _scansThisLevel = 99; return null; }
                var all = _find.Invoke(null, new object[] { true }) as System.Collections.IEnumerable;
                if (all == null) return null;
                foreach (object found in all)
                {
                    var comp = R.Cast(found, t) as Component;
                    if (comp == null) continue;
                    if (pick == null || (comp.gameObject.activeInHierarchy && !pick.gameObject.activeInHierarchy)) pick = comp;
                }
                if (pick != null)
                {
                    LastActivated = pick;
                    Log.Info("Levels page found by search: " + pick.gameObject.name);
                }
                return pick;
            }
            catch (Exception e) { Log.Error("Searching for the Levels page failed", e); }
            return null;
        }
    }
}
