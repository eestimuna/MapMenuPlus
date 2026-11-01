using System;
using System.Collections.Generic;

namespace MapMenuPlus
{
    /// <summary>One map of the menu (plain data; Crate is the game's LevelCrate, only touched on the main thread).</summary>
    internal sealed class MapEntry
    {
        internal string Barcode;        // the level's barcode
        internal string Title;          // tags removed
        internal string Pack;           // pallet title
        internal string PackBarcode;
        internal string Author;         // pallet author (mod.io's uploader is used when known)
        internal bool Slz;              // one of BONELAB's own maps
        internal int GameIndex = -1;    // position in the game's own level list (-1 = not in it)
        internal object Crate;
        internal int PackMaps = 1;      // how many maps the pack has
        internal string Search;         // everything you can search for, normalized
        internal string Key;            // the title, normalized (sorting)

        internal string ListingThumb;   // a picture from the game's own mod listing (when there is no mod.io info)
        internal long ModId;            // filled in from mod.io (0 = not known)
        internal ModInfo Info;

        internal string ShownAuthor { get { return Info != null && !string.IsNullOrEmpty(Info.Author) ? Info.Author : Author; } }
    }

    internal enum MapCat { All, Favorites, Recent, NotPlayed, Mods, Bonelab }
    internal enum MapSort { AZ, Recent, Popular, TopRated, Newest }

    /// <summary>
    /// What the map menu shows: the list for the chosen category, search and sort, cut into pages of 3 x 3.
    /// No game code in here (tested on its own).
    /// </summary>
    internal sealed class MapModel
    {
        internal const int Cols = 3, Rows = 3, PerPage = Cols * Rows;
        internal static readonly MapCat[] Cats = { MapCat.All, MapCat.Favorites, MapCat.Recent, MapCat.NotPlayed, MapCat.Mods, MapCat.Bonelab };

        private List<MapEntry> _all = new List<MapEntry>();
        private readonly HashSet<string> _fav = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _recent = new List<string>();
        private readonly Dictionary<string, int> _recentRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapEntry> _byBarcode = new Dictionary<string, MapEntry>(StringComparer.OrdinalIgnoreCase);
        private List<MapEntry> _shown = new List<MapEntry>();
        private readonly int[] _counts = new int[Cats.Length];

        internal MapCat Cat { get; private set; }
        internal MapSort Sort { get; private set; }
        internal string Query { get; private set; } = "";
        internal int Page { get; private set; }

        internal int AllCount { get { return _all.Count; } }
        internal int ShownCount { get { return _shown.Count; } }
        internal IReadOnlyList<MapEntry> Shown { get { return _shown; } }
        internal IReadOnlyList<MapEntry> All { get { return _all; } }
        internal int Pages { get { return Math.Max(1, (_shown.Count + PerPage - 1) / PerPage); } }
        internal bool Searching { get { return Query.Trim().Length > 0; } }
        internal int Count(MapCat c) { return _counts[Array.IndexOf(Cats, c)]; }
        internal List<string> FavoriteIds { get { return new List<string>(_fav); } }

        internal void SetData(List<MapEntry> all, IEnumerable<string> favorites, IEnumerable<string> recent)
        {
            _all = all ?? new List<MapEntry>();
            _byBarcode.Clear();
            var packs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (MapEntry e in _all)
            {
                if (e.Barcode != null) _byBarcode[e.Barcode] = e;
                string pk = e.PackBarcode ?? "";
                int n;
                packs[pk] = packs.TryGetValue(pk, out n) ? n + 1 : 1;
            }
            foreach (MapEntry e in _all)
            {
                e.PackMaps = packs[e.PackBarcode ?? ""];
                e.Key = Text.Norm(e.Title);
                e.Search = " " + Text.Norm(e.Title) + " " + Text.Norm(e.Pack) + " " + Text.Norm(e.Author) + " ";
            }
            _fav.Clear();
            if (favorites != null) foreach (string f in favorites) if (!string.IsNullOrEmpty(f)) _fav.Add(f);
            SetRecent(recent, false);
            Apply(false);
        }

        internal void SetRecent(IEnumerable<string> recent, bool apply = true)
        {
            _recent.Clear();
            _recentRank.Clear();
            if (recent != null)
                foreach (string r in recent)
                    if (!string.IsNullOrEmpty(r) && !_recentRank.ContainsKey(r)) { _recentRank[r] = _recent.Count; _recent.Add(r); }
            if (apply) Apply(true);
        }

        /// <summary>mod.io info arrived: the search words get the uploader too; sorting by numbers may change.</summary>
        internal void InfoChanged()
        {
            foreach (MapEntry e in _all)
                e.Search = " " + Text.Norm(e.Title) + " " + Text.Norm(e.Pack) + " " + Text.Norm(e.ShownAuthor) + " " + Text.Norm(e.Author) + " "
                           + (e.Info != null ? Text.Norm(e.Info.Name) + " " : "");
            Apply(true);
        }

        internal MapEntry Find(string barcode)
        {
            MapEntry e;
            return barcode != null && _byBarcode.TryGetValue(barcode, out e) ? e : null;
        }

        internal bool IsFavorite(MapEntry e) { return e != null && _fav.Contains(e.Barcode); }
        internal bool Played(MapEntry e) { return e != null && _recentRank.ContainsKey(e.Barcode); }

        internal bool ToggleFavorite(MapEntry e)
        {
            if (e == null) return false;
            bool on = !_fav.Remove(e.Barcode);
            if (on) _fav.Add(e.Barcode);
            Apply(true);
            return on;
        }

        internal void SetCat(MapCat c) { Cat = c; Page = 0; Apply(false); }
        internal void SetSort(MapSort s) { Sort = s; Page = 0; Apply(false); }

        internal void SetQuery(string q)
        {
            Query = q ?? "";
            Page = 0;
            Apply(false);
        }

        internal static MapSort NextSort(MapSort s)
        {
            switch (s)
            {
                case MapSort.AZ: return MapSort.Popular;
                case MapSort.Popular: return MapSort.TopRated;
                case MapSort.TopRated: return MapSort.Newest;
                case MapSort.Newest: return MapSort.Recent;
                default: return MapSort.AZ;
            }
        }

        internal static string SortLabel(MapSort s)
        {
            switch (s)
            {
                case MapSort.Popular: return "POPULAR";
                case MapSort.TopRated: return "TOP RATED";
                case MapSort.Newest: return "NEWEST";
                case MapSort.Recent: return "LAST PLAYED";
                default: return "A - Z";
            }
        }

        internal static string CatLabel(MapCat c)
        {
            switch (c)
            {
                case MapCat.Favorites: return "Favorites";
                case MapCat.Recent: return "Recently Played";
                case MapCat.NotPlayed: return "Not Played Yet";
                case MapCat.Mods: return "Mod Maps";
                case MapCat.Bonelab: return "BONELAB Maps";
                default: return "All Maps";
            }
        }

        internal static string CatIcon(MapCat c)
        {
            switch (c)
            {
                case MapCat.Favorites: return "star";
                case MapCat.Recent: return "clock";
                case MapCat.NotPlayed: return "spark";
                case MapCat.Mods: return "cube";
                case MapCat.Bonelab: return "diamond";
                default: return "grid";
            }
        }

        internal bool Prev() { if (Page <= 0) return false; Page--; return true; }
        internal bool Next() { if (Page >= Pages - 1) return false; Page++; return true; }

        internal void GoTo(int page) { Page = Math.Max(0, Math.Min(Pages - 1, page)); }

        /// <summary>The maps of the page (up to 9).</summary>
        internal List<MapEntry> PageItems()
        {
            var l = new List<MapEntry>(PerPage);
            for (int i = Page * PerPage; i < _shown.Count && l.Count < PerPage; i++) l.Add(_shown[i]);
            return l;
        }

        /// <summary>The maps of another page (for downloading their pictures early).</summary>
        internal List<MapEntry> PageItems(int page)
        {
            var l = new List<MapEntry>(PerPage);
            if (page < 0) return l;
            for (int i = page * PerPage; i < _shown.Count && l.Count < PerPage; i++) l.Add(_shown[i]);
            return l;
        }

        internal int IndexOf(MapEntry e) { return e == null ? -1 : _shown.IndexOf(e); }

        /// <summary>A random map of the current list (for "SURPRISE ME"); never the same as "not" if there is another.</summary>
        internal MapEntry Random(Random rng, MapEntry not)
        {
            if (_shown.Count == 0) return null;
            if (_shown.Count == 1) return _shown[0];
            for (int i = 0; i < 8; i++)
            {
                MapEntry e = _shown[rng.Next(_shown.Count)];
                if (e != not) return e;
            }
            return _shown[0] != not ? _shown[0] : _shown[1];
        }

        // ---- filter + sort ------------------------------------------------------------------------------------

        private bool InCat(MapEntry e, MapCat c)
        {
            switch (c)
            {
                case MapCat.Favorites: return _fav.Contains(e.Barcode);
                case MapCat.Recent: return _recentRank.ContainsKey(e.Barcode);
                case MapCat.NotPlayed: return !_recentRank.ContainsKey(e.Barcode);
                case MapCat.Mods: return !e.Slz;
                case MapCat.Bonelab: return e.Slz;
                default: return true;
            }
        }

        /// <summary>
        /// Builds the shown list. keepPage = stay on the same page if possible (info arrived, favorites changed) instead
        /// of jumping back to page 1.
        /// </summary>
        private void Apply(bool keepPage)
        {
            for (int i = 0; i < Cats.Length; i++)
            {
                int n = 0;
                foreach (MapEntry e in _all) if (InCat(e, Cats[i])) n++;
                _counts[i] = n;
            }

            string[] words = Words(Query);
            var list = new List<MapEntry>();
            foreach (MapEntry e in _all)
            {
                // a search looks through ALL maps, like a search box should
                if (words.Length == 0 && !InCat(e, Cat)) continue;
                if (words.Length > 0 && !Matches(e, words)) continue;
                list.Add(e);
            }
            // Recently Played always shows the last played first
            MapSort sort = Cat == MapCat.Recent && words.Length == 0 && Sort == MapSort.AZ ? MapSort.Recent : Sort;
            list.Sort((a, b) => Compare(a, b, sort, words));
            _shown = list;
            if (!keepPage) Page = 0;
            if (Page > Pages - 1) Page = Pages - 1;
        }

        internal static string[] Words(string q)
        {
            var w = new List<string>();
            if (!string.IsNullOrEmpty(q))
                foreach (string part in q.Split(' '))
                {
                    string n = Text.Norm(part);
                    if (n.Length > 0) w.Add(n);
                }
            return w.ToArray();
        }

        private static bool Matches(MapEntry e, string[] words)
        {
            foreach (string w in words) if (e.Search == null || e.Search.IndexOf(w, StringComparison.Ordinal) < 0) return false;
            return true;
        }

        private int Compare(MapEntry a, MapEntry b, MapSort sort, string[] words)
        {
            int c;
            if (words.Length > 0)
            {
                // names that START with what you typed come first
                c = StartsRank(b, words).CompareTo(StartsRank(a, words));
                if (c != 0) return c;
            }
            switch (sort)
            {
                case MapSort.Recent:
                    c = Rank(a).CompareTo(Rank(b));
                    if (c != 0) return c;
                    break;
                case MapSort.Popular:
                    c = (b.Info != null ? b.Info.Downloads : -1).CompareTo(a.Info != null ? a.Info.Downloads : -1);
                    if (c != 0) return c;
                    break;
                case MapSort.TopRated:
                    c = Score(b).CompareTo(Score(a));
                    if (c != 0) return c;
                    break;
                case MapSort.Newest:
                    c = (b.Info != null ? b.Info.Newest : -1).CompareTo(a.Info != null ? a.Info.Newest : -1);
                    if (c != 0) return c;
                    break;
            }
            c = string.Compare(a.Key ?? "", b.Key ?? "", StringComparison.Ordinal);
            if (c != 0) return c;
            return string.Compare(a.Barcode, b.Barcode, StringComparison.OrdinalIgnoreCase);
        }

        private int Rank(MapEntry e)
        {
            int r;
            return _recentRank.TryGetValue(e.Barcode, out r) ? r : int.MaxValue;
        }

        /// <summary>Top rated: mod.io's weighted rating (few votes count less), the plain % when that is missing.</summary>
        private static double Score(MapEntry e)
        {
            if (e.Info == null || e.Info.RatingsTotal <= 0) return -1;
            if (e.Info.Weighted > 0) return e.Info.Weighted;
            return e.Info.RatingPct / 100.0 * Math.Min(1.0, e.Info.RatingsTotal / 20.0);
        }

        private static int StartsRank(MapEntry e, string[] words)
        {
            string t = e.Key ?? "";
            return t.StartsWith(words[0], StringComparison.Ordinal) ? 2 : (t.Contains(words[0]) ? 1 : 0);
        }
    }
}
