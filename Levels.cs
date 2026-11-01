using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSLZ.Marrow.Warehouse;
using UnityEngine;

namespace MapMenuPlus
{
    /// <summary>
    /// Reading game objects by member NAME (reflection), so a member that is named a little differently in this game
    /// version can only switch one feature off - it can never stop the mod from building or loading.
    /// </summary>
    internal static class R
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly Dictionary<string, MemberInfo> Cache = new Dictionary<string, MemberInfo>();
        private static readonly Dictionary<Type, MethodInfo> Casts = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>();

        private static MemberInfo Member(Type t, string name)
        {
            string key = t.FullName + "|" + name;
            MemberInfo m;
            if (Cache.TryGetValue(key, out m)) return m;
            m = null;
            for (Type x = t; x != null && m == null; x = x.BaseType)
            {
                PropertyInfo p = x.GetProperty(name, F | BindingFlags.DeclaredOnly);
                if (p != null && p.GetIndexParameters().Length == 0) { m = p; break; }
                FieldInfo f = x.GetField(name, F | BindingFlags.DeclaredOnly);
                if (f != null) m = f;
            }
            Cache[key] = m;
            return m;
        }

        internal static bool Has(object o, string name) { return o != null && Member(o.GetType(), name) != null; }

        internal static object Get(object o, string name)
        {
            if (o == null) return null;
            MemberInfo m = Member(o.GetType(), name);
            var p = m as PropertyInfo;
            if (p != null) return p.GetValue(p.GetGetMethod(true).IsStatic ? null : o);
            var f = m as FieldInfo;
            if (f != null) return f.GetValue(f.IsStatic ? null : o);
            return null;
        }

        internal static bool Set(object o, string name, object value)
        {
            if (o == null) return false;
            MemberInfo m = Member(o.GetType(), name);
            var p = m as PropertyInfo;
            if (p != null && p.CanWrite) { p.SetValue(o, value); return true; }
            var f = m as FieldInfo;
            if (f != null) { f.SetValue(o, value); return true; }
            return false;
        }

        internal static object Static(Type t, string name)
        {
            if (t == null) return null;
            MemberInfo m = Member(t, name);
            var p = m as PropertyInfo;
            if (p != null) return p.GetValue(null);
            var f = m as FieldInfo;
            if (f != null) return f.GetValue(null);
            return null;
        }

        internal static string Str(object o, string name)
        {
            try { return Get(o, name) as string; } catch { return null; }
        }

        internal static object Call(object o, string name, params object[] args)
        {
            if (o == null) return null;
            foreach (MethodInfo mi in o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (mi.Name == name && mi.GetParameters().Length == args.Length) return mi.Invoke(o, args);
            throw new MissingMethodException(o.GetType().Name, name);
        }

        /// <summary>Il2Cpp objects come typed as a base class: this gives them as type t (null if they are not one).</summary>
        internal static object Cast(object o, Type t)
        {
            if (o == null || t == null) return null;
            if (t.IsInstanceOfType(o)) return o;
            var b = o as Il2CppObjectBase;
            if (b == null) return null;
            MethodInfo mi;
            if (!Casts.TryGetValue(t, out mi))
            {
                mi = null;
                MethodInfo g = typeof(Il2CppObjectBase).GetMethod("TryCast", BindingFlags.Public | BindingFlags.Instance);
                if (g != null) mi = g.MakeGenericMethod(t);
                Casts[t] = mi;
            }
            return mi != null ? mi.Invoke(b, null) : null;
        }

        /// <summary>The items of an Il2Cpp list / collection (Count + indexer, or GetEnumerator).</summary>
        internal static List<object> Items(object list)
        {
            var r = new List<object>();
            if (list == null) return r;
            Type t = list.GetType();
            PropertyInfo count = t.GetProperty("Count");
            MethodInfo getItem = t.GetMethod("get_Item", new[] { typeof(int) });
            if (count != null && getItem != null)
            {
                int n = Convert.ToInt32(count.GetValue(list));
                for (int i = 0; i < n; i++) r.Add(getItem.Invoke(list, new object[] { i }));
                return r;
            }
            MethodInfo ge = t.GetMethod("GetEnumerator", Type.EmptyTypes);
            if (ge == null) return r;
            object en = ge.Invoke(list, null);
            if (en == null) return r;
            MethodInfo move = en.GetType().GetMethod("MoveNext", Type.EmptyTypes);
            PropertyInfo cur = en.GetType().GetProperty("Current");
            if (move == null || cur == null) return r;
            int guard = 0;
            while ((bool)move.Invoke(en, null) && guard++ < 100000) r.Add(cur.GetValue(en));
            return r;
        }

        internal static int Count(object list)
        {
            if (list == null) return 0;
            PropertyInfo count = list.GetType().GetProperty("Count");
            return count != null ? Convert.ToInt32(count.GetValue(list)) : 0;
        }

        /// <summary>A type by its full name (any loaded assembly), or by its short name in assemblies with that hint.</summary>
        internal static Type FindType(string fullName, string shortName = null, string asmHint = null, Assembly first = null)
        {
            string key = fullName + "|" + shortName + "|" + asmHint;
            Type cached;
            if (Types.TryGetValue(key, out cached)) return cached;
            Type found = Search(fullName, shortName, asmHint, first);
            Types[key] = found;
            return found;
        }

        private static Type Search(string fullName, string shortName, string asmHint, Assembly first)
        {
            if (first != null)
            {
                try
                {
                    Type t = first.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { }
            }
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = a.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { }
            }
            if (shortName == null) return null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = a.GetName().Name ?? "";
                if (asmHint != null && an.IndexOf(asmHint, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    foreach (Type t in a.GetTypes()) if (t.Name == shortName) return t;
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (Type t in e.Types) if (t != null && t.Name == shortName) return t;
                }
                catch { }
            }
            return null;
        }
    }

    /// <summary>
    /// Everything about BONELAB's own Levels page and its maps: finding the page type, reading its map list (the same
    /// list the game shows), the mod.io id of each map's pack, the mod.io login, and starting a map.
    /// Main thread only.
    /// </summary>
    internal static class Levels
    {
        private static Type _panelType, _modIoTarget;
        private static bool _panelTypeSearched, _listingLogged, _fusionSearched;
        private static PropertyInfo _hasServer, _isHost;
        private static MethodInfo _requestLevel, _isInMarrowGame;
        private static bool _marrowGameSearched;

        /// <summary>The game's Levels page class (LevelsPanelView), found by name.</summary>
        internal static Type PanelType
        {
            get
            {
                if (_panelTypeSearched) return _panelType;
                _panelTypeSearched = true;
                Assembly game = typeof(Il2CppSLZ.Bonelab.UIRig).Assembly;   // Assembly-CSharp (loaded for sure)
                _panelType = R.FindType("Il2CppSLZ.Bonelab.LevelsPanelView", null, null, game)
                             ?? R.FindType("Il2CppSLZ.UI.LevelsPanelView", "LevelsPanelView", "Assembly-CSharp", game);
                Log.Info("Levels page class: " + (_panelType != null ? _panelType.FullName : "NOT FOUND"));
                return _panelType;
            }
        }

        // ---- reading the maps -----------------------------------------------------------------------------------

        /// <summary>How many maps the page lists right now (cheap: tells when the list changed).</summary>
        internal static int GameCount(object panel)
        {
            try { return R.Count(R.Get(panel, "_levelCrates")); }
            catch { return -1; }
        }

        /// <summary>
        /// The maps of the game's Levels page, plus the packs (for mod.io). If the page's list cannot be read, every
        /// level of every installed pack is used instead.
        /// </summary>
        internal static List<MapEntry> Read(object panel, out List<PackKey> packs, out string how)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var list = new List<MapEntry>();
            packs = new List<PackKey>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var crates = new List<Crate>();
            bool fromGame = false;
            try
            {
                foreach (object o in R.Items(R.Get(panel, "_levelCrates")))
                {
                    var c = R.Cast(o, typeof(Crate)) as Crate;
                    crates.Add(c);
                }
                fromGame = crates.Count > 0;
            }
            catch (Exception e) { Log.Warn("Reading the Levels page's list failed: " + e.Message); }
            if (!fromGame)
            {
                crates.Clear();
                crates.AddRange(WarehouseLevels());
            }

            var packOf = new Dictionary<string, PackKey>(StringComparer.OrdinalIgnoreCase);
            int index = -1;
            foreach (Crate c in crates)
            {
                index++;
                if (c == null) continue;
                try
                {
                    string id = c.Barcode != null ? c.Barcode.ID : null;
                    if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                    var e = new MapEntry
                    {
                        Barcode = id,
                        Title = Text.StripTags(c.Title) ?? id,
                        GameIndex = fromGame ? index : -1,
                        Crate = c,
                    };
                    if (string.IsNullOrWhiteSpace(e.Title)) e.Title = id;
                    Pallet p = null;
                    try { p = c.Pallet; } catch { }
                    if (p != null)
                    {
                        try { e.Pack = Text.StripTags(p.Title); } catch { }
                        try { e.PackBarcode = p.Barcode != null ? p.Barcode.ID : null; } catch { }
                        e.Author = Text.StripTags(R.Str(p, "Author"));
                        e.Slz = IsSlz(p, e.PackBarcode, e.Author);
                    }
                    if (string.IsNullOrEmpty(e.Pack)) e.Pack = e.Slz ? "BONELAB" : "";
                    list.Add(e);

                    if (!e.Slz && p != null && !string.IsNullOrEmpty(e.PackBarcode))
                    {
                        PackKey k;
                        if (!packOf.TryGetValue(e.PackBarcode, out k))
                        {
                            k = new PackKey { Barcode = e.PackBarcode, Title = e.Pack, Author = e.Author };
                            ReadListing(p, k);
                            packOf[e.PackBarcode] = k;
                            packs.Add(k);
                        }
                        k.LevelTitles.Add(e.Title);
                        e.ListingThumb = k.ListingThumb;
                    }
                }
                catch (Exception ex) { Log.Warn("Reading a map failed: " + ex.Message); }
            }
            sw.Stop();
            int withId = 0;
            foreach (PackKey k in packs) if (k.ListingModId > 0) withId++;
            how = (fromGame ? "the Levels page" : "all installed packs (the page's list was empty)") + ": " + list.Count + " maps, " + packs.Count + " mod packs ("
                  + withId + " with a mod.io id from the game), " + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms";
            return list;
        }

        /// <summary>Every level of every pack (only when the page's own list cannot be read).</summary>
        private static List<Crate> WarehouseLevels()
        {
            var r = new List<Crate>();
            try
            {
                Type levelType = typeof(Crate).Assembly.GetType("Il2CppSLZ.Marrow.Warehouse.LevelCrate");
                AssetWarehouse wh = AssetWarehouse.Instance;
                if (wh == null || levelType == null) return r;
                foreach (Pallet p in wh.GetPallets())
                {
                    if (p == null) continue;
                    try
                    {
                        if (p.Crates == null) continue;
                        foreach (Crate c in p.Crates)
                        {
                            if (c == null || R.Cast(c, levelType) == null) continue;
                            object red = null;
                            try { red = R.Get(c, "Redacted"); } catch { }
                            if (red is bool && (bool)red) continue;
                            string t = c.Title ?? "";
                            if (t.IndexOf("loading", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                            r.Add(c);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.Warn("Reading the installed levels failed: " + e.Message); }
            return r;
        }

        /// <summary>BONELAB's own content (by the game's own check when it has one, else SLZ's barcode / name).</summary>
        private static bool IsSlz(Pallet p, string barcode, string author)
        {
            if (!_marrowGameSearched)
            {
                _marrowGameSearched = true;
                try { _isInMarrowGame = p.GetType().GetMethod("IsInMarrowGame", Type.EmptyTypes); } catch { }
            }
            if (_isInMarrowGame != null)
            {
                try { return (bool)_isInMarrowGame.Invoke(p, null); } catch { }
            }
            if (!string.IsNullOrEmpty(barcode) && barcode.StartsWith("SLZ.", StringComparison.OrdinalIgnoreCase)) return true;
            string a = Text.Norm(author);
            return a == "slz" || a == "stresslevelzero";
        }

        /// <summary>
        /// The game's own mod listing of a pack (packs installed through BONELAB's mod browser): its mod.io id and maybe
        /// a picture. Same path Fusion uses: AssetWarehouse.palletManifests[pallet.Barcode].ModListing.Targets.
        /// </summary>
        private static void ReadListing(Pallet p, PackKey k)
        {
            try
            {
                object manifests = R.Get(AssetWarehouse.Instance, "palletManifests");
                if (manifests == null) return;
                object barcode = p.Barcode;
                Type mt = manifests.GetType();
                MethodInfo has = mt.GetMethod("ContainsKey");
                MethodInfo get = mt.GetMethod("get_Item");
                if (has == null || get == null || !(bool)has.Invoke(manifests, new[] { barcode })) return;
                object manifest = get.Invoke(manifests, new[] { barcode });
                object listing = R.Get(manifest, "ModListing");
                if (listing == null) return;

                if (!_listingLogged)
                {
                    _listingLogged = true;
                    var names = new List<string>();
                    foreach (PropertyInfo pi in listing.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) names.Add(pi.Name);
                    Log.Info("Mod listing members: " + string.Join(", ", names));
                }
                foreach (PropertyInfo pi in listing.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (pi.PropertyType != typeof(string) || pi.Name.IndexOf("thumb", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string u = null;
                    try { u = pi.GetValue(listing) as string; } catch { }
                    if (u != null && u.StartsWith("http", StringComparison.OrdinalIgnoreCase)) { k.ListingThumb = u; break; }
                }

                object targets = R.Get(listing, "Targets");
                if (targets == null) return;
                if (_modIoTarget == null)
                    _modIoTarget = listing.GetType().Assembly.GetType("Il2CppSLZ.Marrow.Forklift.Model.ModIOModTarget")
                                   ?? R.FindType("Il2CppSLZ.Marrow.Forklift.Model.ModIOModTarget", "ModIOModTarget", "SLZ");
                if (_modIoTarget == null) return;
                foreach (object t in R.Items(R.Get(targets, "Values")))
                {
                    object mio = R.Cast(t, _modIoTarget);
                    if (mio == null) continue;
                    object id = R.Get(mio, "ModId");
                    if (id != null) { long v = Convert.ToInt64(id); if (v > 0) { k.ListingModId = v; return; } }
                }
            }
            catch (Exception e)
            {
                if (!_listingLogged) { _listingLogged = true; Log.Warn("Reading a pack's mod listing failed: " + e.Message); }
            }
        }

        // ---- the mod.io login -----------------------------------------------------------------------------------

        /// <summary>
        /// The mod.io login of BONELAB's mod browser: the "mod.io.access_token" in the game's mod settings file
        /// (Il2CppSLZ.Marrow.Forklift.ModDownloader.ModSettingsPath - the same file Fusion reads). null = not logged in.
        /// </summary>
        internal static string ReadToken(out string where)
        {
            where = "";
            var files = new List<string>();
            try
            {
                Type dl = R.FindType("Il2CppSLZ.Marrow.Forklift.ModDownloader", "ModDownloader", "SLZ", typeof(Crate).Assembly);
                string path = R.Static(dl, "ModSettingsPath") as string;
                if (!string.IsNullOrEmpty(path)) files.Add(path);
            }
            catch (Exception e) { Log.Warn("Finding the mod settings file failed: " + e.Message); }
            try
            {
                string pd = Application.persistentDataPath;
                if (!string.IsNullOrEmpty(pd) && Directory.Exists(pd))
                    foreach (string f in Directory.GetFiles(pd, "*.json", SearchOption.TopDirectoryOnly)) if (!files.Contains(f)) files.Add(f);
            }
            catch { }
            foreach (string f in files)
            {
                try
                {
                    if (!File.Exists(f) || new FileInfo(f).Length > 512 * 1024) continue;
                    string text = File.ReadAllText(f);
                    if (text.IndexOf("mod.io.access_token", StringComparison.Ordinal) < 0) continue;
                    JNode root;
                    if (!Json.TryParse(text, out root)) continue;
                    string token = root["mod.io.access_token"].Str;
                    where = Path.GetFileName(f);
                    if (!string.IsNullOrWhiteSpace(token)) return token;
                }
                catch { }
            }
            return null;
        }

        /// <summary>The game's mod folder (its *.manifest files say which mod.io mod each pack is).</summary>
        internal static string ModsFolder()
        {
            try
            {
                string d = Path.Combine(Application.persistentDataPath, "Mods");
                return Directory.Exists(d) ? d : null;
            }
            catch { return null; }
        }

        // ---- Fusion ---------------------------------------------------------------------------------------------

        private static void FindFusion()
        {
            if (_fusionSearched) return;
            _fusionSearched = true;
            try
            {
                Type ni = R.FindType("LabFusion.Network.NetworkInfo");
                if (ni != null)
                {
                    _hasServer = ni.GetProperty("HasServer", BindingFlags.Public | BindingFlags.Static);
                    _isHost = ni.GetProperty("IsHost", BindingFlags.Public | BindingFlags.Static);
                }
                Type ls = R.FindType("LabFusion.Senders.LoadSender");
                if (ls != null)
                    foreach (MethodInfo m in ls.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        if (m.Name == "SendLevelRequest" && m.GetParameters().Length == 1) { _requestLevel = m; break; }
                Log.Info("Fusion: " + (ni != null ? "found (lobby check " + (_hasServer != null && _isHost != null) + ", level request " + (_requestLevel != null) + ")" : "not installed"));
            }
            catch (Exception e) { Log.Warn("Looking for Fusion failed: " + e.Message); }
        }

        /// <summary>In a Fusion lobby that someone else hosts: maps can only be requested from the host.</summary>
        internal static bool FusionClient
        {
            get
            {
                FindFusion();
                try
                {
                    if (_hasServer == null || _isHost == null) return false;
                    return (bool)_hasServer.GetValue(null) && !(bool)_isHost.GetValue(null);
                }
                catch { return false; }
            }
        }

        // ---- starting a map ---------------------------------------------------------------------------------------

        /// <summary>
        /// Starts a map the way the game's own Levels page does when you click it: its SelectItem (so everything the
        /// game does, and Fusion's "request level" for lobby guests, happens as usual). If that is not possible: Fusion's
        /// level request (guest) or the game's level loader (SceneStreamer.Load).
        /// </summary>
        internal static bool Play(object panel, MapEntry e, out string how)
        {
            how = "";
            if (e == null) return false;
            if (panel != null && e.Crate != null)
            {
                try
                {
                    var crates = R.Items(R.Get(panel, "_levelCrates"));
                    int idx = -1;
                    for (int i = 0; i < crates.Count; i++)
                    {
                        var c = R.Cast(crates[i], typeof(Crate)) as Crate;
                        if (c != null && c.Barcode != null && string.Equals(c.Barcode.ID, e.Barcode, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
                    }
                    int per = R.Count(R.Get(panel, "items"));
                    if (idx >= 0 && per > 0)
                    {
                        // the page shows the list in pages of "per" maps; SelectItem(i) = map i of the current page.
                        // The page number is NOT put back afterwards: the game (or Fusion) may read it a moment later.
                        R.Set(panel, "_currentPage", idx / per);
                        R.Call(panel, "SelectItem", idx % per);
                        how = "the game's Levels page (item " + (idx % per) + " of page " + (idx / per + 1) + ")";
                        return true;
                    }
                    how = "not in the page's list (" + idx + ", " + per + " per page)";
                }
                catch (Exception ex)
                {
                    how = "page failed: " + ex.GetBaseException().Message;
                    Log.Warn("Starting \"" + e.Title + "\" through the Levels page failed: " + ex);
                }
            }
            if (FusionClient && _requestLevel != null && e.Crate != null)
            {
                try
                {
                    Type pt = _requestLevel.GetParameters()[0].ParameterType;
                    _requestLevel.Invoke(null, new[] { R.Cast(e.Crate, pt) });
                    how += "; Fusion level request";
                    return true;
                }
                catch (Exception ex) { how += "; Fusion request failed: " + ex.GetBaseException().Message; }
            }
            try
            {
                Type ss = R.FindType("Il2CppSLZ.Marrow.SceneStreaming.SceneStreamer", "SceneStreamer", "SLZ", typeof(Crate).Assembly);
                if (ss == null) { how += "; no SceneStreamer"; return false; }
                foreach (MethodInfo m in ss.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "Load") continue;
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length != 2 || ps[0].ParameterType != typeof(Barcode)) continue;
                    var bc = (Barcode)Activator.CreateInstance(typeof(Barcode), e.Barcode);
                    m.Invoke(null, new object[] { bc, null });
                    how += "; SceneStreamer.Load";
                    return true;
                }
                how += "; no Load(Barcode, Barcode)";
            }
            catch (Exception ex) { how += "; SceneStreamer failed: " + ex.GetBaseException().Message; }
            return false;
        }
    }
}
