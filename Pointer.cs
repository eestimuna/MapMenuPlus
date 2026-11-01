using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UI;

namespace MapMenuPlus
{
    /// <summary>
    /// "Is the laser pointing at this button?" - read from the button's own state by reflection
    /// (same check the avatar menu uses in the game; a renamed member cannot break the build).
    /// </summary>
    internal static class Pointer
    {
        private static bool _probed;
        private static PropertyInfo _stateProp;
        private static PropertyInfo _insideProp;
        private static FieldInfo _insideField;

        private static void Probe()
        {
            if (_probed) return;
            _probed = true;
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type sel = typeof(Selectable);
            _stateProp = sel.GetProperty("currentSelectionState", F);
            _insideProp = sel.GetProperty("isPointerInside", F);
            _insideField = sel.GetField("isPointerInside", F);
            Log.Info("Hover probes: currentSelectionState=" + (_stateProp != null) + ", isPointerInside(prop)=" + (_insideProp != null)
                     + ", isPointerInside(field)=" + (_insideField != null));
            if (_stateProp == null && _insideProp == null && _insideField == null)
            {
                var names = new List<string>();
                foreach (MemberInfo m in sel.GetMembers(F | BindingFlags.DeclaredOnly))
                    if (m.Name.IndexOf("ointer", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("election", StringComparison.OrdinalIgnoreCase) >= 0)
                        names.Add(m.Name);
                Log.Warn("No hover members found on Selectable (the info line will not follow the pointer). Candidates: " + string.Join(", ", names));
            }
        }

        internal static bool IsOver(Selectable s)
        {
            if (s == null) return false;
            Probe();
            try
            {
                if (_insideProp != null)
                {
                    object v = _insideProp.GetValue(s);
                    if (v is bool && (bool)v) return true;
                }
                else if (_insideField != null)
                {
                    object v = _insideField.GetValue(s);
                    if (v is bool && (bool)v) return true;
                }
                if (_stateProp != null)
                {
                    object v = _stateProp.GetValue(s);
                    string n = v != null ? v.ToString() : "";
                    if (n == "Highlighted" || n == "Pressed") return true;
                }
            }
            catch { }
            return false;
        }
    }
}
