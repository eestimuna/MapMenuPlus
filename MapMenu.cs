using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapMenuPlus
{
    /// <summary>
    /// The new map menu on BONELAB's own Levels page (MapLayout shows a sketch): a sidebar (All, Favorites, Recently
    /// Played, Not Played Yet, Mod Maps, BONELAB Maps, SURPRISE ME, the map you are in), a search bar and 3 x 3 big
    /// picture cards with the map's mod.io picture, name, author, downloads and rating. Click a card: the details view
    /// with all the mod's screenshots, its description and PLAY.
    ///
    /// Built like Spawn Menu Plus (works in the game): our own buttons on the game's page, the game's own parts shrunk
    /// away while the new menu is on (CLASSIC brings them back exactly), an invisible catcher behind the menu so the
    /// laser cannot reach the game's hidden buttons. PLAY goes through the page's own code (so Fusion still turns it
    /// into a "request level" for lobby guests).
    /// </summary>
    internal sealed class MapMenu
    {
        private const float RootZ = -20f;     // in front of the game's page (negative = towards you)
        private const float CatcherZ = 6f;    // just behind our buttons, still in front of the game's
        private const float Tiny = 0.0001f;
        private const float UnitMetres = 0.0013f;   // one layout unit in the other menus (looks right in VR)
        private const float HoverScale = 1.045f;
        private const float OpenTime = 0.18f;
        private const float CapR = 8f;

        private static readonly Vector2 CursorCentre = new Vector2(0f, (MapLayout.Panel.Top + 30f + MapLayout.KeyboardBottom - 30f) / 2f);
        private static readonly Vector2 CursorSize = new Vector2(MapLayout.Panel.W + 60f, (MapLayout.Panel.Top + 30f) - (MapLayout.KeyboardBottom - 30f));

        private sealed class CardSlot
        {
            internal int Index;
            internal GameObject Go;
            internal Transform Visual;
            internal CanvasGroup Group;
            internal Button Btn;
            internal RawImage Bg, Outline, Art, Photo, Shimmer, Shade, DlIcon, RateIcon, BadgeBg, FavIcon, FavBack;
            internal TMP_Text ArtText, DlText, RateText, BadgeText, Title, Sub;
            internal List<RawImage> Caps;
            internal MapEntry Entry;
            internal string Url;
            internal bool PhotoShown;
            internal float PhotoAlpha, Scale = 1f, Hover, Appear = 1f, AppearStart, Zoom = 1f;
            internal bool Hovered;
        }

        private sealed class CatSlot
        {
            internal GameObject Go;
            internal Button Btn;
            internal RawImage Icon, Bar, CountBg;
            internal TMP_Text Label, Count;
            internal MapCat Cat;
            internal int State = -1;
        }

        private sealed class Hidden
        {
            internal Transform T;
            internal Vector3 Scale;
        }

        private readonly MapModel _model = new MapModel();
        private readonly Pictures _pics = new Pictures();
        private readonly IdStore _favorites;
        private readonly PlayLog _played;
        private readonly System.Random _rng = new System.Random();

        private Component _panel;
        private Transform _page;
        private bool _built, _broken, _wasOpen, _scaleChecked;
        private float _openedAt, _openT = 1f, _baseScale = 1f;
        private UiStyle _style;

        private GameObject _root, _newMenu, _gridView, _detailView;
        private CanvasGroup _group;
        private readonly List<CardSlot> _cards = new List<CardSlot>();
        private readonly List<CatSlot> _cats = new List<CatSlot>();
        private readonly List<Hidden> _hidden = new List<Hidden>();
        private readonly CursorArea _cursorArea = new CursorArea();
        private Vector2 _newMenuPos;
        private Keyboard _keyboard;

        private TMP_Text _subTitle, _statusText, _searchText, _sortText, _pageText, _emptyText, _emptyHint, _tip;
        private RawImage _statusDot, _searchFrame;
        private Button _prev, _next;
        private RawImage _nowArt, _nowPic;
        private TMP_Text _nowTitle, _nowSub, _nowArtText;

        // details view
        private TMP_Text _crumb, _mapPos, _dTitle, _dInfo, _dlText, _rateText, _tagText, _dSummary, _playText, _favText, _playedText, _bigArtText, _bigHidden;
        private RawImage _bigArt, _bigA, _bigB, _bigShimmer, _dlIcon, _rateIcon, _favIconD;
        private Button _mapPrev, _mapNext, _galPrev, _galNext, _play, _favBtn;
        private readonly List<RawImage> _dots = new List<RawImage>();
        private MapEntry _detail;
        private List<string> _gal = new List<string>();
        private int _galIndex;
        private Texture _bigShown;
        private int _bigShownIndex = -1;
        private float _bigFade = 1f, _nextGalSwap, _playLockUntil;
        private string _playLockText;

        // data
        private int _dataCount = -2, _modVersion = -1, _imgVersion = -1, _dataLogs;
        private float _nextInfoApply, _nextTokenCheck, _nextCountCheck;
        private string _current;               // barcode of the map you are in
        private string _flash;
        private float _flashUntil;
        private bool _caretOn;
        private float _nextCaret;
        private string _lastPageKey;
        private float _shimmerT, _nextNow;

        private readonly System.Diagnostics.Stopwatch _work = new System.Diagnostics.Stopwatch();
        private int _spikeLogs;

        internal MapMenu(string dataDir)
        {
            _favorites = new IdStore(dataDir != null ? Path.Combine(dataDir, "favorites.txt") : null, 5000);
            _played = new PlayLog(dataDir != null ? Path.Combine(dataDir, "played.txt") : null);
            Log.Info("Map menu: " + (Settings.NewMenu ? "new menu on" : "off (CLASSIC)") + ", " + _favorites.Ids.Count + " favorites, " + _played.Count + " maps played before.");
        }

        internal bool IsOpen { get { return _wasOpen; } }

        // ---- from the mod ---------------------------------------------------------------------------------------

        /// <summary>A level finished loading: it is the map you are in now, and it goes to the top of Recently Played.</summary>
        internal void LevelLoaded(string barcode, string title)
        {
            _current = barcode;
            if (!string.IsNullOrEmpty(barcode))
            {
                _played.Played(barcode, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                _model.SetRecent(_played.Recent(), false);
            }
        }

        /// <summary>BoneMenu changed a setting.</summary>
        internal void SettingsChanged()
        {
            if (!_built) return;
            _modVersion = -1;
            ApplyMode(false);
        }

        internal void ClearPictures()
        {
            _pics.Clear();
            foreach (CardSlot c in _cards) { c.PhotoShown = false; c.Url = null; }
            _bigShown = null;
            _bigShownIndex = -1;
            _modVersion = -1;
        }

        internal string StatusLine()
        {
            int mods = 0, found = 0, info = 0;
            foreach (MapEntry e in _model.All)
            {
                if (e.Slz) continue;
                mods++;
                if (e.ModId > 0) found++;
                if (e.Info != null) info++;
            }
            return StatusWords(true) + "\n" + _model.AllCount + " maps (" + mods + " from mods: " + found + " found on mod.io, " + info + " with info). "
                   + _pics.Decoded + " pictures shown, " + ModIo.ImagesDownloaded + " downloaded this session.";
        }

        // ---- lifecycle ----------------------------------------------------------------------------------------

        internal void Reset()
        {
            try { ShowGameParts(); } catch { }
            try { if (_root != null) UnityEngine.Object.Destroy(_root); } catch { }
            try { if (_newMenu != null) UnityEngine.Object.Destroy(_newMenu); } catch { }
            _root = null; _newMenu = null; _keyboard = null; _group = null; _gridView = null; _detailView = null;
            _cards.Clear(); _cats.Clear(); _dots.Clear();
            _cursorArea.Reset();
            _built = false; _broken = false; _wasOpen = false; _scaleChecked = false;
            _baseScale = 1f;
            _page = null; _panel = null;
            _hidden.Clear();
            _detail = null;
            _bigShown = null; _bigShownIndex = -1;
            _dataCount = -2;
            _modVersion = -1;
            _lastPageKey = null;
        }

        internal void Update(float now, Component panel)
        {
            if (panel == null) return;
            float dt = Time.unscaledDeltaTime;
            bool wasOpen = _wasOpen;
            _work.Restart();
            try { Step(now, dt, panel); }
            finally
            {
                _work.Stop();
                double ms = _work.Elapsed.TotalMilliseconds;
                if (ms > 15.0 && _spikeLogs < 40)
                {
                    _spikeLogs++;
                    Log.Info("Slow menu frame: this mod took " + ms.ToString("0.0") + " ms" + (wasOpen ? "" : " (menu closed)") + ".");
                }
            }
        }

        private void Step(float now, float dt, Component panel)
        {
            Transform page = null;
            try { page = panel.transform; } catch { }
            if (page == null) return;
            if (page != _page)
            {
                if (_page != null || _built) Reset();
                _page = page;
                _panel = panel;
            }

            bool open = page.gameObject.activeInHierarchy;
            if (!open)
            {
                if (_wasOpen)
                {
                    _wasOpen = false;
                    if (_keyboard != null) _keyboard.Hide();
                    ModIo.Want(null, null);
                    Log.Info("Map menu closed.");
                }
                _cursorArea.Hide();
                // build the menu while it is still closed, so opening it does not freeze the game
                if (!_built && !_broken) Build();
                if (_built && now >= _nextCountCheck)
                {
                    _nextCountCheck = now + 2f;
                    int n = Levels.GameCount(_panel);
                    if (n > 0 && n != _dataCount) ReadData();
                }
                return;
            }

            if (!_built && !_broken) Build();
            if (!_built) return;

            if (!_wasOpen)
            {
                _wasOpen = true;
                _openedAt = now;
                _openT = 0f;
                _lastPageKey = null;
                int n = Levels.GameCount(_panel);
                if (n != _dataCount || _model.AllCount == 0) ReadData();
                if (now >= _nextTokenCheck && ModIo.Status != ModIo.State.Ready && ModIo.Status != ModIo.State.Working)
                {
                    _nextTokenCheck = now + 10f;
                    string where;
                    ModIo.SetToken(Levels.ReadToken(out where));   // you may have logged in to mod.io in the meantime
                }
                Log.Info("Map menu opened (" + (Settings.NewMenu ? "new menu" : "classic") + ", " + _model.AllCount + " maps).");
                ApplyMode(false);
            }
            if (!_scaleChecked && now - _openedAt > 0.6f) CheckScale();

            if (Settings.NewMenu && _root != null) _cursorArea.Show(now, _root.transform, CursorCentre, CursorSize, 60f);
            else if (_newMenu != null) _cursorArea.Show(now, _page, _newMenuPos, new Vector2(320f, 90f), 40f);
            if (!Settings.NewMenu) return;

            HideGameParts();          // every frame: the game may reset sizes when it redraws
            if (ModIo.Version != _modVersion && (now >= _nextInfoApply || _modVersion < 0))
            {
                _nextInfoApply = now + 1.2f;
                if (ApplyInfo()) Render();
                else RenderStatus();
            }
            _pics.Tick();
            ShowNewPhotos(now);
            HoverCheck(now);
            Animate(now, dt);
        }

        // ---- data ---------------------------------------------------------------------------------------------------

        private void ReadData()
        {
            try
            {
                List<PackKey> packs;
                string how;
                List<MapEntry> list = Levels.Read(_panel, out packs, out how);
                _dataCount = Levels.GameCount(_panel);
                _model.SetData(list, _favorites.Ids, _played.Recent());
                ModIo.Register(packs);
                _modVersion = -1;
                ApplyInfo();
                if (_dataLogs++ < 6) Log.Info("Maps read from " + how + ".");
                if (_detail != null) _detail = _model.Find(_detail.Barcode);
                if (_built && _wasOpen && Settings.NewMenu) Render();
            }
            catch (Exception e)
            {
                Log.Error("Reading the maps failed", e);
                _dataCount = Levels.GameCount(_panel);
            }
        }

        /// <summary>Puts the newest mod.io ids + info on the maps. True when something changed.</summary>
        private bool ApplyInfo()
        {
            _modVersion = ModIo.Version;
            bool changed = false;
            bool use = Settings.UseModIo;
            var later = new List<string>();
            foreach (MapEntry e in _model.All)
            {
                if (e.Slz) continue;
                long id = use ? ModIo.ModIdOf(e.PackBarcode) : 0;
                ModInfo info = use && id > 0 ? ModIo.InfoOf(id) : null;
                if (id != e.ModId || info != e.Info) { e.ModId = id; e.Info = info; changed = true; }
                string u = CardUrl(e);
                if (u != null) later.Add(u);
            }
            if (changed) _model.InfoChanged();
            if (use && later.Count > 0) ModIo.WantLater(later);
            return changed;
        }

        /// <summary>The picture of a map's card (null = none: drawn art instead).</summary>
        private static string CardUrl(MapEntry e)
        {
            if (e == null || e.Slz || !Settings.UseModIo) return null;
            if (e.Info != null)
            {
                if (e.Info.Explicit && !Settings.ShowMature) return null;
                return e.Info.CardPicture ?? e.ListingThumb;
            }
            return e.ListingThumb;
        }

        private static bool HiddenMature(MapEntry e) { return e != null && e.Info != null && e.Info.Explicit && !Settings.ShowMature && Settings.UseModIo; }

        // ---- building ----------------------------------------------------------------------------------------------

        private void Build()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _style = new UiStyle();
                ReadStyle(_style);
                Paint.Style = _style;
                Paint.Layer = _style.Layer;
                UiStyle s = _style;

                _root = new GameObject("MMP_Menu");
                _root.layer = s.Layer;
                _root.transform.SetParent(_page, false);
                var rt = _root.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                _root.transform.localPosition = new Vector3(0f, 0f, RootZ);
                _group = _root.AddComponent<CanvasGroup>();
                Transform r = _root.transform;
                TMP_Text ignored;

                // soft drop shadow (3 layers), panel, title bar, sidebar, catcher
                HudRect p = MapLayout.Panel;
                for (int i = 3; i >= 1; i--)
                {
                    float grow = MapLayout.ShadowGrow * i / 3f;
                    Paint.Pic(r, "Shadow" + i, new Vector2(p.X + MapLayout.ShadowOffset * 0.6f, p.Y - MapLayout.ShadowOffset), new Vector2(p.W + grow, p.H + grow),
                              Bank.Get("panel"), new Color(0f, 0f, 0f, 0.10f + 0.07f * (3 - i)));
                }
                Paint.Pic(r, "Panel", p, "panel", Theme.Panel);
                Paint.Pic(r, "TitleBar", MapLayout.TitleBar, "titlebar", Theme.TitleBar);
                Paint.Pic(r, "Sidebar", MapLayout.Sidebar, "sidebar", Theme.Sidebar);
                Image accent = Paint.Solid(r, "AccentLine", new Vector2(0f, MapLayout.TitleBar.Bottom), new Vector2(p.W - 2f, 2f), Theme.A(Theme.Accent, 0.9f));
                accent.raycastTarget = false;
                UiKit.Catcher(r, "Catcher", Paint.V(p), Paint.S(p), CatcherZ, s);

                // title bar
                Paint.Icon(r, "TitleIcon", MapLayout.TitleIcon, "map", Theme.Accent);
                TMP_Text title = Paint.Text(r, "Title", "MAP MENU", MapLayout.TitleText, 26f, Theme.Text, TextAlignmentOptions.Left, true);
                title.characterSpacing = 2f;
                _subTitle = Paint.Text(r, "SubTitle", "", MapLayout.SubTitle, 12.5f, Theme.Text2);
                Paint.Rounded(r, "StatusBg", MapLayout.Status, 17f, Theme.Input);
                HudRect st = MapLayout.Status;
                _statusDot = Paint.Pic(r, "StatusDot", new HudRect(st.Left + 20f, st.Y, 10f, 10f), "dot", Theme.Muted);
                _statusText = Paint.Text(r, "StatusText", "", new HudRect(st.X + 12f, st.Y, st.W - 52f, st.H), 13.5f, Theme.Text2);
                Paint.TextBtn(r, "Classic", MapLayout.Classic, 10f, "CLASSIC", 16f, Theme.Button, Theme.ButtonHover, Theme.ButtonPress, Theme.Text2, OnClassic, out ignored);
                RawImage closeBg;
                Button close = Paint.Btn(r, "Close", MapLayout.Close, 10f, Theme.Danger, Theme.DangerHover, Theme.DangerHover, OnClose, out closeBg);
                Paint.Icon(close.transform, "X", new HudRect(0f, 0f, 20f, 20f), "close", Theme.Text);

                BuildSidebar(r);

                _gridView = Paint.Node(r, "GridView", Vector2.zero, Vector2.zero).gameObject;
                BuildGrid(_gridView.transform);
                _detailView = Paint.Node(r, "DetailView", Vector2.zero, Vector2.zero).gameObject;
                BuildDetails(_detailView.transform);
                _detailView.SetActive(false);

                _keyboard = new Keyboard(r, new Vector2(0f, MapLayout.KeyboardTopY), s, OnChar, OnBackspace, OnSpace, OnClear);
                _root.transform.SetAsLastSibling();

                // NEW MAP MENU button on the game's own page (only shown in classic mode)
                float y = 430f;
                try
                {
                    RectTransform prt = _page.GetComponent<RectTransform>();
                    if (prt != null && prt.rect.height > 100f) y = prt.rect.height * 0.5f + 32f;   // just above the page
                }
                catch { }
                _newMenuPos = new Vector2(0f, y);
                Button nm = Paint.TextBtn(_page, "NewMenu", new HudRect(0f, y, 280f, 50f), 25f, "NEW MAP MENU", 18f, Theme.AccentDeep, Theme.Accent, Theme.ButtonPress, Theme.Text,
                                          OnNewMenu, out ignored, "map");
                _newMenu = nm.gameObject;

                _built = true;
                float unit = Unit();
                sw.Stop();
                Log.Info("Map menu built in " + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms" + (_page.gameObject.activeInHierarchy ? "" : " (ahead of time, menu closed)")
                         + " (layer " + s.Layer + ", font " + (s.Font != null) + ", 1 page unit = " + (unit * 1000f).ToString("0.###") + " mm, NEW MAP MENU button at y=" + y.ToString("0")
                         + ", page \"" + _page.gameObject.name + "\" with " + _page.childCount + " parts).");
            }
            catch (Exception e)
            {
                _broken = true;
                Log.Error("Building the map menu failed: the game's menu stays", e);
                try { if (_root != null) UnityEngine.Object.Destroy(_root); } catch { }
                try { if (_newMenu != null) UnityEngine.Object.Destroy(_newMenu); } catch { }
                _root = null; _newMenu = null;
                ShowGameParts();
            }
        }

        private void BuildSidebar(Transform r)
        {
            TMP_Text side = Paint.Text(r, "SideTitle", "LIBRARY", MapLayout.SideTitle, 13f, Theme.Muted, TextAlignmentOptions.Left, true);
            side.characterSpacing = 9f;
            for (int i = 0; i < MapModel.Cats.Length; i++) _cats.Add(MakeCat(r, i, MapModel.Cats[i]));
            Paint.Solid(r, "Separator", Paint.V(MapLayout.Separator), Paint.S(MapLayout.Separator), Theme.Outline);
            TMP_Text ignored;
            Paint.TextBtn(r, "Random", MapLayout.Random, 10f, "SURPRISE ME", 15f, Theme.Button, Theme.ButtonHover, Theme.ButtonPress, Theme.Text, OnRandom, out ignored, "dice");

            TMP_Text now = Paint.Text(r, "NowLabel", "YOU ARE IN", MapLayout.NowLabel, 11.5f, Theme.Muted, TextAlignmentOptions.Left, true);
            now.characterSpacing = 6f;
            HudRect np = MapLayout.NowPic;
            RawImage nowFrame;
            Button nowBtn = Paint.Btn(r, "NowPic", np, 8f, Theme.Clear, Theme.Accent, Theme.AccentHover, OnNow, out nowFrame);
            Transform nt = nowBtn.transform;
            var inner = new HudRect(0f, 0f, np.W, np.H);
            Paint.Solid(nt, "NowBg", Vector2.zero, Paint.S(inner), Theme.PhotoBg);
            _nowArt = Paint.Pic(nt, "NowArt", inner, "mapart", Theme.Muted);
            _nowArtText = Paint.Text(nt, "NowArtText", "", new HudRect(0f, 0f, inner.W - 16f, inner.H - 10f), 15f, new Color(1f, 1f, 1f, 0.92f), TextAlignmentOptions.Center, true, true);
            _nowPic = Paint.Pic(nt, "NowPhoto", inner, null, Color.white);
            _nowPic.enabled = false;
            Paint.Caps(nt, "Now", inner, 8f, Theme.Sidebar);
            // the button's own graphic is an outline drawn on top: it lights up blue when you point at it
            nowFrame.texture = OutlineTex(np.W, np.H);
            nowFrame.transform.SetAsLastSibling();
            _nowTitle = Paint.Text(r, "NowTitle", "", MapLayout.NowTitle, 15f, Theme.Text, TextAlignmentOptions.Left, true);
            _nowSub = Paint.Text(r, "NowSub", "", MapLayout.NowSub, 12f, Theme.Text2);
            _tip = Paint.Text(r, "Tip", "", MapLayout.Tip, 12f, Theme.Muted, TextAlignmentOptions.TopLeft, false, true);
        }

        private CatSlot MakeCat(Transform r, int i, MapCat cat)
        {
            var c = new CatSlot { Cat = cat };
            var rect = new HudRect(MapLayout.CatX, MapLayout.CatY(i), MapLayout.CatW, MapLayout.CatH);
            RawImage bg;
            c.Btn = Paint.Btn(r, "Cat" + i, rect, 9f, Theme.Clear, Theme.RowHover, Theme.AccentSoft, () => OnCat(c), out bg);
            c.Go = c.Btn.gameObject;
            Transform t = c.Go.transform;
            c.Bar = Paint.Rounded(t, "Bar", MapLayout.CatBar, 2f, Theme.Accent);
            c.Icon = Paint.Icon(t, "Icon", MapLayout.CatIcon, MapModel.CatIcon(cat), Theme.Text2);
            float left = -MapLayout.CatW / 2f + MapLayout.CatLabelLeft, right = MapLayout.CatCount.Left - 6f;
            c.Label = Paint.Text(t, "Label", MapModel.CatLabel(cat), new HudRect((left + right) / 2f, 0f, right - left, MapLayout.CatH), 15.5f, Theme.Text2);
            c.CountBg = Paint.Rounded(t, "CountBg", MapLayout.CatCount, 11f, Theme.Pill);
            c.Count = Paint.Text(t, "Count", "", MapLayout.CatCount, 11.5f, Theme.Text2, TextAlignmentOptions.Center, true);
            return c;
        }

        private void BuildGrid(Transform g)
        {
            RawImage searchBg;
            Button search = Paint.Btn(g, "Search", MapLayout.Search, 12f, Theme.Input, Theme.InputHover, Theme.Input, OnSearchBox, out searchBg);
            HudRect sr = MapLayout.Search;
            _searchFrame = Paint.Pic(search.transform, "SearchFrame", new HudRect(0f, 0f, sr.W, sr.H), null, Theme.Outline);
            _searchFrame.texture = OutlineTex(sr.W, sr.H);
            Paint.Icon(search.transform, "SearchIcon", new HudRect(-sr.W / 2f + 22f, 0f, 18f, 18f), "search", Theme.Muted);
            _searchText = Paint.Text(search.transform, "SearchText", "", new HudRect(14f, 0f, sr.W - 60f, sr.H), 15f, Theme.Muted);
            Paint.TextBtn(g, "Sort", MapLayout.Sort, 12f, "SORT: A - Z", 14f, Theme.Button, Theme.ButtonHover, Theme.ButtonPress, Theme.Text2, OnSort, out _sortText, "list");
            _prev = IconBtn(g, "Prev", MapLayout.Prev, "chevron_left", OnPrev);
            _pageText = Paint.Text(g, "Page", "1 / 1", MapLayout.PageText, 17f, Theme.Text, TextAlignmentOptions.Center, true);
            _next = IconBtn(g, "Next", MapLayout.Next, "chevron_right", OnNext);

            for (int i = 0; i < MapModel.PerPage; i++) _cards.Add(MakeCard(g, i));
            _emptyText = Paint.Text(g, "Empty", "", new HudRect(MapLayout.GridX, 40f, MapLayout.GridW - 40f, 40f), 24f, Theme.Text2, TextAlignmentOptions.Center, true);
            _emptyHint = Paint.Text(g, "EmptyHint", "", new HudRect(MapLayout.GridX, 4f, MapLayout.GridW - 40f, 30f), 15f, Theme.Muted, TextAlignmentOptions.Center);
        }

        private static readonly Dictionary<string, Texture2D> Outlines = new Dictionary<string, Texture2D>();

        private static Texture2D OutlineTex(float w, float h)
        {
            string k = w + "x" + h;
            Texture2D t;
            if (Outlines.TryGetValue(k, out t) && t != null) return t;
            t = Bank.ToTexture(Shapes.RoundRectOutline((int)(w * 2f), (int)(h * 2f), 24f, 3f, Rgba.White));
            Outlines[k] = t;
            return t;
        }

        private Button IconBtn(Transform parent, string name, HudRect r, string icon, Action onClick)
        {
            RawImage bg;
            Button b = Paint.Btn(parent, name, r, 10f, Theme.Button, Theme.ButtonHover, Theme.ButtonPress, onClick, out bg);
            Paint.Icon(b.transform, "Icon", new HudRect(0f, 0f, r.H * 0.42f, r.H * 0.42f), icon, Theme.Text);
            return b;
        }

        /// <summary>
        /// One map card. The button and its collider never move; everything you SEE is in "Visual", which grows a little
        /// when you point at it (so the laser target stays still and there is no flicker between cards).
        /// </summary>
        private CardSlot MakeCard(Transform g, int i)
        {
            var c = new CardSlot { Index = i };
            int col = i % MapModel.Cols, row = i / MapModel.Cols;
            float w = MapLayout.CardW, h = MapLayout.CardH;
            UiStyle s = _style;

            c.Go = new GameObject("MMP_Card" + i);
            c.Go.layer = s.Layer;
            c.Go.transform.SetParent(g, false);
            var rect = c.Go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(MapLayout.CardX(col), MapLayout.CardY(row));
            c.Go.AddComponent<CanvasRenderer>();
            c.Group = c.Go.AddComponent<CanvasGroup>();

            var colGo = new GameObject("Collider");
            colGo.layer = s.Layer;
            colGo.transform.SetParent(c.Go.transform, false);
            var box = colGo.AddComponent<BoxCollider>();
            box.size = new Vector3(w + 6f, h + 6f, 1f);
            box.isTrigger = true;

            c.Visual = Paint.Node(c.Go.transform, "Visual", Vector2.zero, new Vector2(w, h));
            Transform v = c.Visual;
            c.Bg = Paint.Pic(v, "Card", new HudRect(0f, 0f, w, h), "card", Theme.Card);
            c.Bg.raycastTarget = true;
            HudRect ph = MapLayout.CardPhoto;
            Paint.Solid(v, "PhotoBg", Paint.V(ph), Paint.S(ph), Theme.PhotoBg);
            c.Art = Paint.Pic(v, "Art", ph, "mapart", Theme.Muted);
            c.ArtText = Paint.Text(v, "ArtText", "", new HudRect(ph.X, ph.Y + 4f, ph.W - 28f, ph.H - 30f), 19f, new Color(1f, 1f, 1f, 0.93f), TextAlignmentOptions.Center, true, true);
            c.ArtText.characterSpacing = 3f;
            c.Photo = Paint.Pic(v, "Photo", ph, null, Color.white);
            c.Photo.enabled = false;
            c.Shimmer = Paint.Pic(v, "Shimmer", ph, "shimmer", Color.white);
            c.Shimmer.gameObject.SetActive(false);
            c.Shade = Paint.Pic(v, "Shade", MapLayout.CardShade, "shade", Color.white);
            c.Caps = Paint.Caps(v, "Photo", ph, CapR, Theme.Card);
            c.DlIcon = Paint.Icon(v, "DlIcon", MapLayout.CardDlIcon, "download", Theme.Text);
            c.DlText = Paint.Text(v, "DlText", "", MapLayout.CardDlText, 12.5f, Theme.Text, TextAlignmentOptions.Left, true);
            c.RateIcon = Paint.Icon(v, "RateIcon", MapLayout.CardRateIcon, "star", Theme.Gold);
            c.RateText = Paint.Text(v, "RateText", "", MapLayout.CardRateText, 12.5f, Theme.Text, TextAlignmentOptions.Right, true);
            c.BadgeBg = Paint.Rounded(v, "BadgeBg", MapLayout.CardBadge, 10f, Theme.Slz);
            c.BadgeText = Paint.Text(v, "BadgeText", "", MapLayout.CardBadge, 10.5f, Theme.TitleBar, TextAlignmentOptions.Center, true);
            c.BadgeText.characterSpacing = 3f;
            HudRect fr = MapLayout.CardFav;
            c.FavBack = Paint.Pic(v, "FavBack", new HudRect(fr.X, fr.Y, fr.W + 8f, fr.H + 8f), "dot", new Color(0f, 0f, 0f, 0.55f));
            c.FavIcon = Paint.Icon(v, "FavIcon", fr, "star", Theme.Gold);
            c.Title = Paint.Text(v, "Title", "", MapLayout.CardTitle, 16.5f, Theme.Text, TextAlignmentOptions.Left, true);
            c.Sub = Paint.Text(v, "Sub", "", MapLayout.CardSub, 12.5f, Theme.Text2);
            c.Outline = Paint.Pic(v, "Outline", new HudRect(0f, 0f, w, h), "card_outline", Theme.Clear);

            c.Btn = c.Go.AddComponent<Button>();
            c.Btn.onClick.AddListener((Action)(() => OnCard(c)));
            c.Btn.transition = Selectable.Transition.None;   // the card's look is animated by the menu itself
            c.Btn.targetGraphic = c.Bg;
            c.Go.AddComponent<Il2CppSLZ.Bonelab.ButtonHoverClick>();
            c.Go.SetActive(false);
            return c;
        }

        private void BuildDetails(Transform d)
        {
            TMP_Text ignored;
            Paint.TextBtn(d, "Back", MapLayout.Back, 12f, "BACK", 15f, Theme.Button, Theme.ButtonHover, Theme.ButtonPress, Theme.Text, OnBack, out ignored, "chevron_left");
            _crumb = Paint.Text(d, "Crumb", "", MapLayout.Crumb, 13f, Theme.Muted, TextAlignmentOptions.Left, true);
            _crumb.characterSpacing = 3f;
            _mapPrev = IconBtn(d, "MapPrev", MapLayout.Prev, "chevron_left", OnMapPrev);
            _mapPos = Paint.Text(d, "MapPos", "", MapLayout.PageText, 15f, Theme.Text, TextAlignmentOptions.Center, true);
            _mapNext = IconBtn(d, "MapNext", MapLayout.Next, "chevron_right", OnMapNext);

            HudRect bp = MapLayout.BigPic;
            Paint.Pic(d, "BigFrame", new HudRect(bp.X, bp.Y, bp.W + 8f, bp.H + 8f), "bigframe", Theme.Input);
            _bigArt = Paint.Pic(d, "BigArt", bp, "mapart", Theme.Muted);
            _bigArtText = Paint.Text(d, "BigArtText", "", new HudRect(bp.X, bp.Y, bp.W - 80f, bp.H - 80f), 40f, new Color(1f, 1f, 1f, 0.93f), TextAlignmentOptions.Center, true, true);
            _bigArtText.characterSpacing = 6f;
            _bigA = Paint.Pic(d, "BigA", bp, null, Color.white);
            _bigA.enabled = false;
            _bigB = Paint.Pic(d, "BigB", bp, null, Color.white);
            _bigB.enabled = false;
            _bigShimmer = Paint.Pic(d, "BigShimmer", bp, "shimmer", Color.white);
            _bigShimmer.gameObject.SetActive(false);
            _bigHidden = Paint.Text(d, "BigHidden", "", new HudRect(bp.X, bp.Y - 60f, bp.W - 60f, 30f), 15f, Theme.Text2, TextAlignmentOptions.Center);
            Paint.Caps(d, "Big", bp, 10f, Theme.Input);
            _galPrev = IconBtn(d, "GalPrev", MapLayout.GalPrev, "chevron_left", OnGalPrev);
            _galNext = IconBtn(d, "GalNext", MapLayout.GalNext, "chevron_right", OnGalNext);
            for (int i = 0; i < 12; i++)
            {
                RawImage dot = Paint.Pic(d, "Dot" + i, new HudRect(0f, MapLayout.Dots.Y, 8f, 8f), "dot", Theme.Muted);
                dot.gameObject.SetActive(false);
                _dots.Add(dot);
            }

            _dTitle = Paint.Text(d, "DTitle", "", MapLayout.DTitle, 30f, Theme.Text, TextAlignmentOptions.Left, true);
            _dInfo = Paint.Text(d, "DInfo", "", MapLayout.DInfo, 15f, Theme.Text2);
            HudRect ds = MapLayout.DStats;
            float L = ds.Left;
            _dlIcon = Paint.Icon(d, "DDlIcon", new HudRect(L + 9f, ds.Y, 16f, 16f), "download", Theme.Accent);
            _dlText = Paint.Text(d, "DDlText", "", new HudRect(L + 139f, ds.Y, 230f, ds.H), 14.5f, Theme.Text);
            _rateIcon = Paint.Icon(d, "DRateIcon", new HudRect(L + 268f, ds.Y, 16f, 16f), "star", Theme.Gold);
            _rateText = Paint.Text(d, "DRateText", "", new HudRect(L + 407f, ds.Y, 250f, ds.H), 14.5f, Theme.Text);
            _tagText = Paint.Text(d, "DTags", "", new HudRect(L + 620f, ds.Y, 160f, ds.H), 12.5f, Theme.Muted, TextAlignmentOptions.Right);
            _dSummary = Paint.Text(d, "DSummary", "", MapLayout.DSummary, 14.5f, Theme.Text2, TextAlignmentOptions.TopLeft, false, true);

            _play = Paint.TextBtn(d, "Play", MapLayout.Play, 14f, "PLAY", 24f, Theme.Play, Theme.PlayHover, Theme.PlayPress, Theme.TitleBar, OnPlay, out _playText, "play");
            _favBtn = Paint.TextBtn(d, "Fav", MapLayout.Fav, 14f, "FAVORITE", 15f, Theme.Button, Theme.ButtonHover, Theme.ButtonPress, Theme.Text2, OnFav, out _favText, "star");
            Transform fi = _favBtn.transform.Find("MMP_Icon");
            _favIconD = fi != null ? fi.GetComponent<RawImage>() : null;
            _playedText = Paint.Text(d, "Played", "", MapLayout.Played, 12.5f, Theme.Muted, TextAlignmentOptions.Right, false, true);
        }

        /// <summary>The font of the game's own menu page (any text on it) and the UI layer.</summary>
        private void ReadStyle(UiStyle s)
        {
            s.Layer = 5;   // UI layer: the laser hits it
            try
            {
                foreach (TMP_Text t in _page.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t == null || t.font == null) continue;
                    s.Font = t.font;
                    break;
                }
            }
            catch (Exception e) { Log.Warn("Reading the menu's font failed (default font used): " + e.Message); }
        }

        private float Unit()
        {
            try { return (_page.TransformPoint(new Vector3(1f, 0f, 0f)) - _page.TransformPoint(Vector3.zero)).magnitude; }
            catch { return 0f; }
        }

        /// <summary>If this page is scaled very differently from the other menus, scale the menu so it has the same size in VR.</summary>
        private void CheckScale()
        {
            _scaleChecked = true;
            float unit = Unit();
            if (unit < 1e-7f || _root == null) return;
            float ratio = UnitMetres / unit;
            if (ratio > 0.6f && ratio < 1.6f) return;
            _baseScale = Mathf.Clamp(ratio, 0.25f, 4f);
            _root.transform.localScale = new Vector3(_baseScale, _baseScale, _baseScale);
            Log.Info("Levels page has another size (1 unit = " + (unit * 1000f).ToString("0.###") + " mm): menu scaled x" + _baseScale.ToString("0.##") + ".");
        }

        /// <summary>Shows the new menu or the game's classic one, as chosen.</summary>
        private void ApplyMode(bool fromButton)
        {
            if (!_built) return;
            if (Settings.NewMenu)
            {
                HideGameParts();
                _root.SetActive(true);
                if (_newMenu != null) _newMenu.SetActive(false);
                ApplyInfo();
                Render();
            }
            else
            {
                if (_keyboard != null) _keyboard.Hide();
                _root.SetActive(false);
                ShowGameParts();
                if (_newMenu != null) _newMenu.SetActive(true);
                ModIo.Want(null, null);
            }
            if (fromButton) Log.Info("Map menu " + (Settings.NewMenu ? "ON" : "OFF (classic menu)") + ".");
        }

        // ---- the game's own page: shrunk while the new menu is on, restored exactly afterwards ------------------

        private void HideGameParts()
        {
            if (_page == null) return;
            for (int i = 0; i < _page.childCount; i++)
            {
                Transform t = _page.GetChild(i);
                if (t == null) continue;
                if (_root != null && t == _root.transform) continue;
                if (_newMenu != null && t == _newMenu.transform) continue;
                Hidden h = null;
                foreach (Hidden x in _hidden) if (x.T == t) { h = x; break; }
                if (h == null)
                {
                    h = new Hidden { T = t, Scale = t.localScale };
                    _hidden.Add(h);
                }
                Vector3 sc = t.localScale;
                if (sc.x != Tiny || sc.y != Tiny || sc.z != Tiny) t.localScale = new Vector3(Tiny, Tiny, Tiny);
            }
        }

        private void ShowGameParts()
        {
            foreach (Hidden h in _hidden)
            {
                try { if (h.T != null) h.T.localScale = h.Scale; } catch { }
            }
            _hidden.Clear();
        }

        // ---- drawing -------------------------------------------------------------------------------------------------

        private static string N(long n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        private static string Esc(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace("<", "("); }

        private void Render()
        {
            if (!_built || !Settings.NewMenu) return;
            int mods = 0;
            foreach (MapEntry e in _model.All) if (!e.Slz) mods++;
            Paint.SetText(_subTitle, N(_model.AllCount) + " maps  ·  " + N(mods) + " from mods");
            RenderStatus();
            RenderSidebar();
            bool details = _detail != null;
            Paint.Show(_gridView, !details);
            Paint.Show(_detailView, details);
            if (details) RenderDetails();
            else RenderGrid();
            RenderNow();
            Wants();
        }

        private string StatusWords(bool plain)
        {
            int mods = 0, found = 0, pending = 0;
            foreach (MapEntry e in _model.All)
            {
                if (e.Slz) continue;
                mods++;
                if (e.ModId > 0) found++;
                else if (e.ModId == 0) pending++;
            }
            switch (ModIo.Status)
            {
                case ModIo.State.Disabled: return "mod.io pictures are off (BoneMenu)";
                case ModIo.State.NoLogin: return plain ? "Not logged in to mod.io: log in to mod.io in BONELAB's mod menu for map pictures + info." : "Log in to mod.io in BONELAB for map pictures";
                case ModIo.State.LoginExpired: return plain ? "The mod.io login has expired: log in to mod.io again in BONELAB." : "mod.io login expired - log in again in BONELAB";
                case ModIo.State.Offline: return "No connection to mod.io (saved pictures only)";
                case ModIo.State.Ready:
                    return mods == 0 ? "mod.io: no mod maps installed" : "mod.io: " + found + " of " + mods + " mod maps found";
                default:
                    return "mod.io: getting map info" + (mods > 0 ? " (" + found + " of " + mods + ")" : "") + "...";
            }
        }

        private void RenderStatus()
        {
            float now = Time.realtimeSinceStartup;
            if (_flash != null && now < _flashUntil)
            {
                Paint.SetText(_statusText, "<color=" + Theme.GoldHex + ">" + Esc(_flash) + "</color>");
                Paint.SetColor(_statusDot, Theme.Gold);
                return;
            }
            _flash = null;
            Paint.SetText(_statusText, StatusWords(false));
            Color dot;
            switch (ModIo.Status)
            {
                case ModIo.State.Ready: dot = Theme.Green; break;
                case ModIo.State.NoLogin: dot = Theme.Gold; break;
                case ModIo.State.LoginExpired:
                case ModIo.State.Offline: dot = Theme.DangerHover; break;
                case ModIo.State.Disabled: dot = Theme.Muted; break;
                default: dot = Theme.Accent; break;
            }
            Paint.SetColor(_statusDot, dot);
        }

        private void RenderSidebar()
        {
            foreach (CatSlot c in _cats)
            {
                bool sel = _model.Cat == c.Cat && !_model.Searching;
                int state = sel ? 1 : 0;
                Paint.SetText(c.Count, N(_model.Count(c.Cat)));
                if (state == c.State) continue;
                c.State = state;
                Paint.Colors(c.Btn, sel ? Theme.AccentSoft : Theme.Clear, sel ? Theme.AccentSoft : Theme.RowHover, Theme.AccentSoft);
                Paint.Show(c.Bar, sel);
                Paint.SetColor(c.Label, sel ? Theme.Text : Theme.Text2);
                c.Label.fontStyle = sel ? FontStyles.Bold : FontStyles.Normal;
                Paint.SetColor(c.Icon, sel ? Theme.Accent : (c.Cat == MapCat.Favorites ? Theme.Gold : Theme.Text2));
                Paint.SetColor(c.CountBg, sel ? Theme.A(Theme.Accent, 0.3f) : Theme.Pill);
                Paint.SetColor(c.Count, sel ? Theme.Text : Theme.Text2);
            }
            Paint.SetText(_tip, _detail != null
                ? "Point at the picture's arrows to see more screenshots. PLAY starts the map."
                : "Click a map to see its pictures, info and PLAY.\nThe pictures come from mod.io.");
        }

        private void RenderGrid()
        {
            List<MapEntry> items = _model.PageItems();
            string key = _model.Cat + "|" + _model.Sort + "|" + _model.Query + "|" + _model.Page;
            bool newPage = key != _lastPageKey;
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < _cards.Count; i++)
            {
                CardSlot c = _cards[i];
                if (i < items.Count)
                {
                    FillCard(c, items[i]);
                    if (newPage)
                    {
                        c.Appear = 0f;
                        c.AppearStart = now + 0.022f * i;
                        c.Group.alpha = 0f;
                    }
                }
                else
                {
                    c.Entry = null;
                    Paint.Show(c.Go, false);
                }
            }
            _lastPageKey = key;
            Paint.SetText(_pageText, (_model.Page + 1) + " / " + _model.Pages);
            SetEnabled(_prev, _model.Page > 0);
            SetEnabled(_next, _model.Page < _model.Pages - 1);
            Paint.SetBtnText(_sortText, "SORT: " + MapModel.SortLabel(_model.Sort));
            RenderSearch();

            bool empty = _model.ShownCount == 0;
            string text = "", hint = "";
            if (empty)
            {
                if (_model.AllCount == 0) { text = "Reading the maps..."; hint = "(open the menu again if this stays)"; }
                else if (_model.Searching) { text = "No map matches \"" + Esc(_model.Query.Trim()) + "\""; hint = "Searches names, packs and authors - try fewer letters"; }
                else if (_model.Cat == MapCat.Favorites) { text = "No favorites yet"; hint = "Open a map and press FAVORITE to keep it here"; }
                else if (_model.Cat == MapCat.Recent) { text = "No maps played yet"; hint = "Maps you play show up here, last played first"; }
                else { text = "No maps here"; hint = ""; }
            }
            Paint.SetText(_emptyText, text);
            Paint.SetText(_emptyHint, hint);
        }

        private void RenderSearch()
        {
            bool typing = _keyboard != null && _keyboard.Visible;
            string q = _model.Query;
            if (q.Length == 0 && !typing)
            {
                Paint.SetText(_searchText, "Search maps...");
                Paint.SetColor(_searchText, Theme.Muted);
            }
            else
            {
                Paint.SetText(_searchText, q + (typing && _caretOn ? "|" : (typing ? " " : "")));
                Paint.SetColor(_searchText, Theme.Text);
            }
            Paint.SetColor(_searchFrame, typing ? Theme.Accent : (q.Length > 0 ? Theme.A(Theme.Accent, 0.45f) : Theme.Outline));
        }

        private static void SetEnabled(Button b, bool on)
        {
            if (b == null) return;
            if (b.interactable != on) b.interactable = on;
            Transform ic = b.transform.Find("MMP_Icon");
            RawImage img = ic != null ? ic.GetComponent<RawImage>() : null;
            Paint.SetColor(img, on ? Theme.Text : Theme.A(Theme.Muted, 0.5f));
        }

        private void FillCard(CardSlot c, MapEntry e)
        {
            bool changed = c.Entry != e;
            c.Entry = e;
            Paint.Show(c.Go, true);
            Paint.SetText(c.Title, e.Title);
            string author = e.ShownAuthor;
            string sub;
            if (e.Slz) sub = "<color=#FF8C2E>BONELAB</color>";
            else if (!string.IsNullOrEmpty(author)) sub = "by " + Esc(author) + (e.PackMaps > 1 && !string.IsNullOrEmpty(e.Pack) ? "  <color=" + Theme.MutedHex + ">·  " + Esc(e.Pack) + "</color>" : "");
            else sub = Esc(e.Pack);
            Paint.SetText(c.Sub, sub);

            // badge: PLAYING > 18+ > BONELAB
            bool playing = _current != null && string.Equals(_current, e.Barcode, StringComparison.OrdinalIgnoreCase);
            bool hidden = HiddenMature(e);
            string badge = playing ? "PLAYING" : (hidden ? "18+" : (e.Slz ? "BONELAB" : null));
            Paint.Show(c.BadgeBg, badge != null);
            Paint.Show(c.BadgeText, badge != null);
            if (badge != null)
            {
                Paint.SetText(c.BadgeText, badge);
                Paint.SetColor(c.BadgeBg, playing ? Theme.Green : (hidden ? Theme.DangerHover : Theme.Slz));
            }
            bool fav = _model.IsFavorite(e);
            Paint.Show(c.FavIcon, fav);
            Paint.Show(c.FavBack, fav);

            // numbers from mod.io
            ModInfo info = e.Info;
            bool stats = info != null;
            Paint.Show(c.Shade, stats);
            Paint.Show(c.DlIcon, stats);
            Paint.Show(c.DlText, stats);
            bool rated = stats && info.RatingPct >= 0;
            Paint.Show(c.RateIcon, rated);
            Paint.Show(c.RateText, rated);
            if (stats) Paint.SetText(c.DlText, Text.Short(info.Downloads));
            if (rated) Paint.SetText(c.RateText, info.RatingPct + "%");

            // picture
            string url = CardUrl(e);
            if (changed || url != c.Url)
            {
                c.Url = url;
                c.PhotoShown = false;
                c.Photo.enabled = false;
                c.Zoom = 1f;
                Paint.SetColor(c.Art, Theme.MapColor(e.Barcode));
            }
            Paint.SetText(c.ArtText, hidden ? "18+\npicture hidden" : e.Title.ToUpperInvariant());
            if (!c.PhotoShown) SetCardPhoto(c, _pics.Get(url));
        }

        /// <summary>Shows the picture (it fades in), or the drawn map art while there is none.</summary>
        private void SetCardPhoto(CardSlot c, Texture2D tex)
        {
            if (tex != null)
            {
                c.Photo.texture = tex;
                Crop(c.Photo, tex, MapLayout.CardPhoto, c.Zoom);
                c.Photo.enabled = true;
                c.PhotoAlpha = 0f;
                c.Photo.color = new Color(1f, 1f, 1f, 0f);
                c.PhotoShown = true;
                Paint.Show(c.Shimmer, false);
                return;
            }
            bool loading = c.Url != null && !_pics.Failed(c.Url) && Settings.UseModIo && ModIo.Status != ModIo.State.Offline;
            Paint.Show(c.Shimmer, loading);
        }

        /// <summary>The middle part of a picture that fits the box (16:9 pictures fill it exactly); zoom &gt; 1 = closer.</summary>
        private static void Crop(RawImage img, Texture tex, HudRect box, float zoom)
        {
            float ta = tex != null && tex.height > 0 ? tex.width / (float)tex.height : 16f / 9f;
            float ba = box.W / box.H;
            float w = 1f, h = 1f;
            if (ta > ba) w = ba / ta; else h = ta / ba;
            w /= zoom;
            h /= zoom;
            var r = new Rect((1f - w) / 2f, (1f - h) / 2f, w, h);
            img.uvRect = r;
        }

        private void RenderNow()
        {
            MapEntry e = _model.Find(_current);
            if (e == null)
            {
                Paint.SetText(_nowTitle, _current != null ? "(not in the list)" : "-");
                Paint.SetText(_nowSub, "");
                Paint.SetText(_nowArtText, "");
                _nowPic.enabled = false;
                return;
            }
            Paint.SetText(_nowTitle, e.Title);
            Paint.SetText(_nowSub, e.Slz ? "BONELAB" : (!string.IsNullOrEmpty(e.ShownAuthor) ? "by " + e.ShownAuthor : e.Pack));
            Paint.SetColor(_nowArt, Theme.MapColor(e.Barcode));
            Paint.SetText(_nowArtText, e.Title.ToUpperInvariant());
            Texture2D tex = _pics.Get(CardUrl(e));
            if (tex != null)
            {
                if (_nowPic.texture != tex) { _nowPic.texture = tex; Crop(_nowPic, tex, MapLayout.NowPic, 1f); }
                _nowPic.enabled = true;
            }
            else _nowPic.enabled = false;
        }

        // ---- details -------------------------------------------------------------------------------------------------

        private void OpenDetails(MapEntry e)
        {
            if (e == null) return;
            if (_keyboard != null) _keyboard.Hide();
            bool same = _detail == e;
            _detail = e;
            if (!same)
            {
                _galIndex = 0;
                _bigShown = null;
                _bigShownIndex = -1;
                _bigA.enabled = false;
                _bigB.enabled = false;
                _bigFade = 1f;
                _nextGalSwap = Time.realtimeSinceStartup + 6f;
            }
            Log.Info("Details: \"" + e.Title + "\" (" + e.Barcode + (e.ModId > 0 ? ", mod.io " + e.ModId : "") + ").");
            Render();
        }

        private List<string> BigUrls(MapEntry e)
        {
            var l = new List<string>();
            if (e == null || HiddenMature(e) || !Settings.UseModIo || e.Slz) return l;
            if (e.Info != null) l.AddRange(e.Info.BigPictures());
            else if (e.ListingThumb != null) l.Add(e.ListingThumb);
            return l;
        }

        private void RenderDetails()
        {
            MapEntry e = _detail;
            if (e == null) return;
            ModInfo info = e.Info;
            _gal = BigUrls(e);
            if (_galIndex >= _gal.Count) _galIndex = 0;

            int pos = _model.IndexOf(e);
            Paint.SetText(_crumb, (_model.Searching ? "SEARCH" : MapModel.CatLabel(_model.Cat).ToUpperInvariant()) + "   /   " + e.Title.ToUpperInvariant());
            Paint.SetText(_mapPos, pos >= 0 ? (pos + 1) + " / " + _model.ShownCount : "-");
            SetEnabled(_mapPrev, pos > 0);
            SetEnabled(_mapNext, pos >= 0 && pos < _model.ShownCount - 1);

            bool multi = _gal.Count > 1;
            Paint.Show(_galPrev, multi);
            Paint.Show(_galNext, multi);
            int dots = Math.Min(_gal.Count, _dots.Count);
            float x0 = MapLayout.Dots.X - (dots - 1) * 8f;
            for (int i = 0; i < _dots.Count; i++)
            {
                RawImage d = _dots[i];
                bool on = multi && i < dots;
                Paint.Show(d, on);
                if (!on) continue;
                d.GetComponent<RectTransform>().anchoredPosition = new Vector2(x0 + i * 16f, MapLayout.Dots.Y);
                Paint.SetColor(d, i == _galIndex ? Theme.Accent : Theme.A(Theme.Muted, 0.6f));
            }

            bool hidden = HiddenMature(e);
            Paint.SetColor(_bigArt, Theme.MapColor(e.Barcode));
            Paint.SetText(_bigArtText, hidden ? "18+" : e.Title.ToUpperInvariant());
            Paint.SetText(_bigHidden, hidden ? "This mod is marked explicit on mod.io - its pictures are hidden (BoneMenu: Show 18+ pictures)." : "");
            ShowBig();

            Paint.SetText(_dTitle, e.Title);
            string author = e.ShownAuthor;
            string line;
            if (e.Slz) line = "<color=#FF8C2E>BONELAB</color>  ·  one of the game's own maps";
            else
            {
                line = !string.IsNullOrEmpty(author) ? "by <color=#F0F2F7>" + Esc(author) + "</color>" : "";
                if (!string.IsNullOrEmpty(e.Pack)) line += (line.Length > 0 ? "  ·  " : "") + "pack: " + Esc(e.Pack) + (e.PackMaps > 1 ? " (" + e.PackMaps + " maps)" : "");
                if (info != null && info.DateUpdated > 0) line += "  ·  updated " + Text.Date(info.DateUpdated);
            }
            Paint.SetText(_dInfo, line);

            bool stats = info != null;
            Paint.Show(_dlIcon, stats);
            Paint.Show(_rateIcon, stats && info.RatingPct >= 0);
            if (stats)
            {
                Paint.SetText(_dlText, N(info.Downloads) + " downloads");
                Paint.SetText(_rateText, info.RatingPct >= 0
                    ? info.RatingPct + "% liked  <color=" + Theme.MutedHex + ">·  " + N(info.RatingsTotal) + (info.RatingsTotal == 1 ? " rating" : " ratings") + (string.IsNullOrEmpty(info.RatingText) ? "" : "  ·  " + Esc(info.RatingText)) + "</color>"
                    : "<color=" + Theme.MutedHex + ">not rated yet</color>");
                Paint.SetText(_tagText, string.Join(", ", info.Tags.ToArray()));
            }
            else
            {
                string why;
                if (e.Slz) why = "";
                else if (!Settings.UseModIo) why = "mod.io info is off (BoneMenu)";
                else if (ModIo.Status == ModIo.State.NoLogin) why = "Log in to mod.io in BONELAB to see downloads, ratings and pictures";
                else if (ModIo.Status == ModIo.State.LoginExpired) why = "The mod.io login expired - log in again in BONELAB";
                else if (e.ModId < 0) why = "This pack was not found on mod.io";
                else why = "Getting the info from mod.io...";
                Paint.SetText(_dlText, why);
                Paint.SetText(_rateText, "");
                Paint.SetText(_tagText, "");
            }
            // the text goes over the icon's place when there are no numbers
            RectTransform dr = _dlText.GetComponent<RectTransform>();
            dr.anchoredPosition = new Vector2(stats ? MapLayout.DStats.Left + 139f : MapLayout.DStats.X, MapLayout.DStats.Y);
            dr.sizeDelta = new Vector2(stats ? 230f : MapLayout.DStats.W, MapLayout.DStats.H);
            Paint.SetColor(_dlText, stats ? Theme.Text : Theme.Muted);

            string summary = info != null && !string.IsNullOrEmpty(info.Summary) ? info.Summary
                : (e.Slz ? "One of BONELAB's own maps, made by Stress Level Zero." : "");
            Paint.SetText(_dSummary, Esc(summary));

            bool playing = _current != null && string.Equals(_current, e.Barcode, StringComparison.OrdinalIgnoreCase);
            float now = Time.realtimeSinceStartup;
            string playLabel = now < _playLockUntil && _playLockText != null ? _playLockText
                : (Levels.FusionClient ? "REQUEST MAP" : (playing ? "PLAY AGAIN" : "PLAY"));
            Paint.SetBtnText(_playText, playLabel);
            bool fav = _model.IsFavorite(e);
            Paint.SetBtnText(_favText, fav ? "FAVORITED" : "FAVORITE");
            Paint.SetColor(_favText, fav ? Theme.Gold : Theme.Text2);
            Paint.SetColor(_favIconD, fav ? Theme.Gold : Theme.Muted);
            Paint.Colors(_favBtn, fav ? Theme.A(Theme.Gold, 0.18f) : Theme.Button, fav ? Theme.A(Theme.Gold, 0.3f) : Theme.ButtonHover, Theme.ButtonPress);

            long last = _played.LastPlayed(e.Barcode);
            int times = _played.Times(e.Barcode);
            Paint.SetText(_playedText, playing ? "You are in this map now"
                : (last > 0 ? "Played " + (times == 1 ? "once" : times + " times") + "\nlast " + Text.Ago(last, DateTimeOffset.UtcNow.ToUnixTimeSeconds()) : "Not played yet"));
        }

        /// <summary>
        /// The big picture of the details view: the wanted gallery picture (1280x720) when it is ready; until then the
        /// card's picture (instant), else the drawn art. A new picture cross-fades over the old one.
        /// </summary>
        private void ShowBig()
        {
            MapEntry e = _detail;
            if (e == null) return;
            string url = _galIndex < _gal.Count ? _gal[_galIndex] : null;
            Texture2D tex = url != null ? _pics.Get(url, true) : null;
            bool fallback = false;
            if (tex == null && _galIndex == 0)
            {
                tex = _pics.Get(CardUrl(e));   // the same picture, smaller: already loaded for the card
                fallback = tex != null;
            }
            bool loading = url != null && tex == null && !_pics.Failed(url) && ModIo.Status != ModIo.State.Offline;
            Paint.Show(_bigShimmer, loading || fallback);
            if (tex == null)
            {
                // another picture was chosen and is not here yet: the drawn art (with the loading light) shows meanwhile
                if (url == null || _pics.Failed(url) || _bigShownIndex != _galIndex) { _bigA.enabled = false; _bigB.enabled = false; _bigShown = null; }
                return;
            }
            if (tex == _bigShown) return;
            // the old picture stays in A, the new one fades in on B
            if (_bigShown != null && _bigA.enabled)
            {
                if (_bigB.enabled && _bigFade < 1f) { _bigA.texture = _bigB.texture; _bigA.uvRect = _bigB.uvRect; }
                _bigB.texture = tex;
                Crop(_bigB, tex, MapLayout.BigPic, 1f);
                _bigB.color = new Color(1f, 1f, 1f, 0f);
                _bigB.enabled = true;
                _bigFade = 0f;
            }
            else
            {
                _bigA.texture = tex;
                Crop(_bigA, tex, MapLayout.BigPic, 1f);
                _bigA.color = new Color(1f, 1f, 1f, 0f);
                _bigA.enabled = true;
                _bigB.enabled = false;
                _bigFade = -1f;   // A fades in
            }
            _bigShown = tex;
            _bigShownIndex = _galIndex;
        }

        // ---- what to download first ------------------------------------------------------------------------------

        private void Wants()
        {
            var visible = new List<string>();
            var soon = new List<string>();
            if (_detail != null)
            {
                for (int i = 0; i < _gal.Count; i++)
                {
                    int k = (_galIndex + i) % _gal.Count;
                    (i < 2 ? visible : soon).Add(_gal[k]);
                }
            }
            foreach (MapEntry e in _model.PageItems()) Add(_detail == null ? visible : soon, CardUrl(e));
            foreach (MapEntry e in _model.PageItems(_model.Page + 1)) Add(soon, CardUrl(e));
            foreach (MapEntry e in _model.PageItems(_model.Page - 1)) Add(soon, CardUrl(e));
            Add(soon, CardUrl(_model.Find(_current)));
            ModIo.Want(visible, soon);
        }

        private static void Add(List<string> l, string u) { if (u != null && !l.Contains(u)) l.Add(u); }

        /// <summary>Every frame: pictures that just became ready go into their cards / the big picture.</summary>
        private void ShowNewPhotos(float now)
        {
            if (_detail == null)
            {
                foreach (CardSlot c in _cards)
                {
                    if (c.Entry == null || !c.Go.activeSelf) continue;
                    Texture2D tex = _pics.Get(c.Url);   // also marks the picture as in use (it is not thrown away)
                    if (!c.PhotoShown) SetCardPhoto(c, tex);
                }
            }
            else ShowBig();
            int iv = ModIo.ImageVersion;
            if (iv != _imgVersion || now >= _nextNow)
            {
                _imgVersion = iv;
                _nextNow = now + 0.5f;
                RenderNow();
            }
        }

        // ---- pointer + animation ------------------------------------------------------------------------------------

        private void HoverCheck(float now)
        {
            if (_detail == null)
                foreach (CardSlot c in _cards)
                {
                    bool over = c.Entry != null && c.Go.activeSelf && Pointer.IsOver(c.Btn);
                    if (over != c.Hovered) c.Hovered = over;
                }
            if (_keyboard != null && _keyboard.Visible && now >= _nextCaret)
            {
                _nextCaret = now + 0.5f;
                _caretOn = !_caretOn;
                RenderSearch();
            }
            else if (now >= _nextCaret) _nextCaret = now + 0.5f;
            if (_flash != null && now >= _flashUntil) { _flash = null; RenderStatus(); }
            if (_playLockText != null && now >= _playLockUntil) { _playLockText = null; if (_detail != null) RenderDetails(); }
            // the gallery moves on by itself every few seconds (only to a picture that is ready)
            if (_detail != null && _gal.Count > 1 && now >= _nextGalSwap && _bigFade >= 1f)
            {
                int next = (_galIndex + 1) % _gal.Count;
                if (_pics.Get(_gal[next], true) != null) { _galIndex = next; _nextGalSwap = now + 6f; RenderDetails(); }
                else _nextGalSwap = now + 1f;   // not ready yet (it is being loaded now): look again soon
            }
        }

        /// <summary>Menu fade-in, cards popping in / lifting when pointed at, pictures fading in, loading shimmer.</summary>
        private void Animate(float now, float dt)
        {
            float k = 1f - (float)Math.Exp(-Math.Max(0.001f, Math.Min(dt, 0.1f)) * 14f);
            if (_openT < 1f && _group != null)
            {
                _openT = Mathf.Min(1f, _openT + dt / OpenTime);
                _group.alpha = 1f - (1f - _openT) * (1f - _openT);
            }
            _shimmerT += dt;
            float sh = (_shimmerT % 1.6f) / 1.6f;              // 0..1 every 1.6 s
            var shimmerUv = new Rect(0.75f - sh * 1.5f, 0f, 1f, 1f);

            if (_detail == null)
                foreach (CardSlot c in _cards)
                {
                    if (c.Entry == null || !c.Go.activeSelf) continue;
                    float pop = 1f;
                    if (c.Appear < 1f)
                    {
                        float p = Mathf.Clamp01((now - c.AppearStart) / 0.26f);
                        c.Appear = p;
                        float e = 1f - (1f - p) * (1f - p) * (1f - p);
                        pop = 0.9f + 0.1f * e;
                        c.Group.alpha = Mathf.Clamp01(p * 1.4f);
                        if (p >= 1f) { pop = 1f; c.Group.alpha = 1f; }
                    }
                    float target = c.Hovered ? 1f : 0f;
                    if (Mathf.Abs(c.Hover - target) > 0.002f) c.Hover += (target - c.Hover) * k;
                    else c.Hover = target;
                    float sc = (1f + (HoverScale - 1f) * c.Hover) * pop;
                    if (c.Visual.localScale.x != sc) c.Visual.localScale = new Vector3(sc, sc, 1f);
                    Color bg = Theme.Lerp(Theme.Card, Theme.CardHover, c.Hover);
                    if (c.Bg.color != bg)
                    {
                        c.Bg.color = bg;
                        foreach (RawImage cap in c.Caps) cap.color = bg;
                    }
                    Paint.SetColor(c.Outline, Theme.A(Theme.Accent, 0.85f * c.Hover));
                    if (c.PhotoShown)
                    {
                        if (c.PhotoAlpha < 1f)
                        {
                            c.PhotoAlpha = Mathf.Min(1f, c.PhotoAlpha + dt / 0.25f);
                            c.Photo.color = new Color(1f, 1f, 1f, c.PhotoAlpha);
                        }
                        float z = 1f + 0.06f * c.Hover;          // the picture moves a little closer when pointed at
                        if (Mathf.Abs(z - c.Zoom) > 0.0005f) { c.Zoom = z; Crop(c.Photo, c.Photo.texture, MapLayout.CardPhoto, z); }
                    }
                    else if (c.Shimmer.gameObject.activeSelf) c.Shimmer.uvRect = shimmerUv;
                }
            else
            {
                if (_bigShimmer.gameObject.activeSelf) _bigShimmer.uvRect = shimmerUv;
                if (_bigFade < 0f)
                {
                    float a = Mathf.Min(1f, _bigA.color.a + dt / 0.3f);
                    _bigA.color = new Color(1f, 1f, 1f, a);
                    if (a >= 1f) _bigFade = 1f;
                }
                else if (_bigFade < 1f)
                {
                    _bigFade = Mathf.Min(1f, _bigFade + dt / 0.45f);
                    float a = _bigFade * _bigFade * (3f - 2f * _bigFade);
                    _bigB.color = new Color(1f, 1f, 1f, a);
                    if (_bigFade >= 1f)
                    {
                        _bigA.texture = _bigB.texture;
                        _bigA.uvRect = _bigB.uvRect;
                        _bigA.color = Color.white;
                        _bigB.enabled = false;
                    }
                }
            }
            // the status dot breathes while mod.io is working
            if (_statusDot != null && (ModIo.Status == ModIo.State.Working || ModIo.Status == ModIo.State.Starting) && _flash == null)
                _statusDot.color = Theme.A(Theme.Accent, 0.45f + 0.55f * (0.5f + 0.5f * (float)Math.Sin(now * 5f)));
        }

        private void Flash(string msg)
        {
            _flash = msg;
            _flashUntil = Time.realtimeSinceStartup + 3.5f;
            RenderStatus();
        }

        // ---- buttons ---------------------------------------------------------------------------------------------

        private void OnCard(CardSlot c)
        {
            if (c == null || c.Entry == null) return;
            c.Hover = 0.4f;   // press feedback
            OpenDetails(c.Entry);
        }

        private void OnBack()
        {
            _detail = null;
            _lastPageKey = null;   // the cards pop in again
            Render();
        }

        private void OnMapPrev() { StepMap(-1); }
        private void OnMapNext() { StepMap(1); }

        private void StepMap(int d)
        {
            int pos = _model.IndexOf(_detail);
            int to = pos + d;
            if (pos < 0 || to < 0 || to >= _model.ShownCount) return;
            MapEntry e = _model.Shown[to];
            _model.GoTo(to / MapModel.PerPage);   // BACK returns to the page of this map
            OpenDetails(e);
        }

        private void OnGalPrev() { Gallery(-1); }
        private void OnGalNext() { Gallery(1); }

        private void Gallery(int d)
        {
            if (_gal.Count < 2) return;
            _galIndex = (_galIndex + d + _gal.Count) % _gal.Count;
            _nextGalSwap = Time.realtimeSinceStartup + 9f;   // you are looking: no automatic change for a while
            RenderDetails();
            Wants();
        }

        private void OnPlay()
        {
            MapEntry e = _detail;
            if (e == null) return;
            float now = Time.realtimeSinceStartup;
            if (now < _playLockUntil) return;
            bool guest = Levels.FusionClient;
            string how;
            bool ok = Levels.Play(_panel, e, out how);
            Log.Info("PLAY \"" + e.Title + "\" (" + e.Barcode + "): " + (ok ? "started" : "FAILED") + " via " + how + (guest ? " (Fusion lobby guest: the host is asked)" : "") + ".");
            _playLockUntil = now + 3f;
            if (!ok)
            {
                _playLockText = "COULD NOT START";
                Flash("Could not start \"" + e.Title + "\" - see the log (GET_LOG.bat)");
            }
            else if (guest)
            {
                _playLockText = "REQUESTED";
                Flash("Asked the lobby host to load \"" + e.Title + "\"");
            }
            else _playLockText = "LOADING...";
            RenderDetails();
        }

        private void OnFav()
        {
            MapEntry e = _detail;
            if (e == null) return;
            bool on = _model.ToggleFavorite(e);
            _favorites.SetAll(_model.FavoriteIds);
            Log.Info("FAVORITE " + (on ? "on" : "off") + ": " + e.Title);
            Render();
            Flash(on ? "Added to Favorites: " + e.Title : "Removed from Favorites: " + e.Title);
        }

        private void OnCat(CatSlot c)
        {
            if (c == null) return;
            if (_keyboard != null) _keyboard.Hide();
            if (_model.Searching) _model.SetQuery("");
            if (c.Cat == MapCat.Recent) _model.SetRecent(_played.Recent(), false);
            _model.SetCat(c.Cat);
            _detail = null;
            Render();
        }

        private void OnRandom()
        {
            MapEntry e = _model.Random(_rng, _detail);
            if (e == null) { Flash("No maps in this list"); return; }
            Log.Info("SURPRISE ME: " + e.Title);
            _model.GoTo(_model.IndexOf(e) / MapModel.PerPage);
            OpenDetails(e);
        }

        private void OnNow()
        {
            MapEntry e = _model.Find(_current);
            if (e != null) OpenDetails(e);
        }

        private void OnSearchBox()
        {
            if (_keyboard == null) return;
            if (_keyboard.Visible) _keyboard.Hide();
            else { _keyboard.Show(); _caretOn = true; _nextCaret = Time.realtimeSinceStartup + 0.5f; }
            Render();
        }

        private void OnSort()
        {
            _model.SetSort(MapModel.NextSort(_model.Sort));
            if ((_model.Sort == MapSort.Popular || _model.Sort == MapSort.TopRated || _model.Sort == MapSort.Newest) && ModIo.Status != ModIo.State.Ready)
                Flash("Sorting by mod.io numbers needs the mod.io info (" + StatusWords(false) + ")");
            Render();
        }

        private void OnChar(char c) { _model.SetQuery(_model.Query + c); Render(); }
        private void OnSpace() { if (_model.Query.Length > 0 && !_model.Query.EndsWith(" ")) { _model.SetQuery(_model.Query + " "); Render(); } }
        private void OnBackspace() { if (_model.Query.Length > 0) { _model.SetQuery(_model.Query.Substring(0, _model.Query.Length - 1)); Render(); } }
        private void OnClear() { _model.SetQuery(""); Render(); }

        private void OnPrev() { if (_model.Prev()) Render(); }
        private void OnNext() { if (_model.Next()) Render(); }

        private void OnClassic()
        {
            Settings.SetNewMenu(false);
            ApplyMode(true);
        }

        private void OnNewMenu()
        {
            Settings.SetNewMenu(true);
            _openT = 0f;
            _lastPageKey = null;
            ApplyMode(true);
        }

        private void OnClose()
        {
            if (_keyboard != null) _keyboard.Hide();
            bool ok = false;
            try
            {
                var ui = Il2CppSLZ.Bonelab.UIRig.Instance;
                if (ui != null && ui.popUpMenu != null) { ui.popUpMenu.Deactivate(); ok = true; }
            }
            catch (Exception e) { Log.Warn("Closing the menu failed: " + e.Message); }
            Log.Info("X pressed: " + (ok ? "menu closed." : "could not close the menu (use the menu button)."));
        }
    }
}
