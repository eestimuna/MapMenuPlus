using System.Collections.Generic;

namespace MapMenuPlus
{
    /// <summary>A rectangle in the page's own units (centre x/y, width/height; y points up).</summary>
    internal struct HudRect
    {
        internal float X, Y, W, H;
        internal HudRect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        internal float Left { get { return X - W / 2f; } }
        internal float Right { get { return X + W / 2f; } }
        internal float Top { get { return Y + H / 2f; } }
        internal float Bottom { get { return Y - H / 2f; } }
    }

    /// <summary>
    /// Every position and size of the map menu (same size as the Spawn Menu Plus menu: about 1.3 mm per unit in VR).
    /// Checked by a test: nothing overlaps, every gap is at least 8 units, everything inside the panel.
    ///
    ///   +-----------------------------------------------------------------------------+
    ///   | [map] MAP MENU            ( * mod.io: 54 of 60 maps found )   [CLASSIC][X]  |
    ///   +-------------------+---------------------------------------------------------+
    ///   | LIBRARY           | [ Search maps...  ] [ SORT: A - Z ]       [<] 1/5 [>]   |
    ///   | All Maps       60 | +--------+ +--------+ +--------+                        |
    ///   | Favorites       3 | |picture | |picture | |picture |   3 x 3 map cards      |
    ///   | Recently Played 8 | +--------+ +--------+ +--------+   (mod.io pictures)    |
    ///   | Not Played Yet 52 | name       name       name                              |
    ///   | Mod Maps       48 | by author  by author  by author                         |
    ///   | BONELAB Maps   12 | ...                                                     |
    ///   | [ SURPRISE ME ]   |                                                         |
    ///   | NOW PLAYING [pic] |                                                         |
    ///   +-------------------+---------------------------------------------------------+
    ///
    /// Clicking a card opens the details view in the right part: a big picture (all of the mod's screenshots),
    /// name, author, downloads, rating, the description and PLAY / FAVORITE.
    /// </summary>
    internal static class MapLayout
    {
        internal static readonly HudRect Panel = new HudRect(0f, 0f, 980f, 720f);

        // title bar
        internal static readonly HudRect TitleBar = new HudRect(0f, 333f, 980f, 54f);
        internal static readonly HudRect TitleIcon = new HudRect(-464f, 333f, 28f, 28f);
        internal static readonly HudRect Title = new HudRect(-328f, 333f, 230f, 44f);           // group: title + subtitle
        internal static readonly HudRect TitleText = new HudRect(-328f, 341f, 230f, 26f);
        internal static readonly HudRect SubTitle = new HudRect(-328f, 318f, 230f, 14f);
        internal static readonly HudRect Status = new HudRect(80f, 333f, 340f, 34f);
        internal static readonly HudRect Classic = new HudRect(350f, 333f, 140f, 40f);
        internal static readonly HudRect Close = new HudRect(458f, 333f, 44f, 40f);

        // sidebar
        internal static readonly HudRect Sidebar = new HudRect(-375f, -27f, 230f, 666f);
        internal static readonly HudRect SideTitle = new HudRect(-375f, 285f, 206f, 26f);
        internal const float CatTop = 266f, CatH = 40f, CatGap = 8f, CatW = 214f, CatX = -375f;
        internal static float CatY(int i) { return CatTop - CatH / 2f - i * (CatH + CatGap); }
        internal static readonly HudRect CatIcon = new HudRect(-CatW / 2f + 22f, 0f, 20f, 20f);     // inside a row
        internal static readonly HudRect CatCount = new HudRect(CatW / 2f - 26f, 0f, 40f, 22f);     // inside a row
        internal static readonly HudRect CatBar = new HudRect(-CatW / 2f + 2f, 0f, 4f, 24f);        // inside a row
        internal const float CatLabelLeft = 42f;
        internal static readonly HudRect Separator = new HudRect(-375f, -26f, 198f, 2f);
        internal static readonly HudRect Random = new HudRect(-375f, -58f, 214f, 42f);
        internal static readonly HudRect NowLabel = new HudRect(-375f, -102f, 206f, 18f);
        internal static readonly HudRect NowPic = new HudRect(-375f, -172f, 206f, 116f);
        internal static readonly HudRect NowTitle = new HudRect(-375f, -244f, 206f, 22f);
        internal static readonly HudRect NowSub = new HudRect(-375f, -265f, 206f, 16f);
        internal static readonly HudRect Tip = new HudRect(-375f, -318f, 206f, 56f);

        // main area: top bar
        internal static readonly HudRect Main = new HudRect(120f, -27f, 740f, 666f);
        internal static readonly HudRect Search = new HudRect(-125f, 272f, 230f, 46f);
        internal static readonly HudRect Sort = new HudRect(110f, 272f, 220f, 46f);
        internal static readonly HudRect Prev = new HudRect(322f, 272f, 46f, 46f);
        internal static readonly HudRect PageText = new HudRect(387f, 272f, 72f, 46f);
        internal static readonly HudRect Next = new HudRect(452f, 272f, 46f, 46f);

        // the grid of map cards
        internal const float GridLeft = -240f, GridRight = 480f, GridTop = 238f, GridBottom = -350f, Gap = 12f;
        internal static float CardW { get { return (GridRight - GridLeft - (MapModel.Cols - 1) * Gap) / MapModel.Cols; } }
        internal static float CardH { get { return (GridTop - GridBottom - (MapModel.Rows - 1) * Gap) / MapModel.Rows; } }
        internal static float CardX(int col) { return GridLeft + CardW / 2f + col * (CardW + Gap); }
        internal static float CardY(int row) { return GridTop - CardH / 2f - row * (CardH + Gap); }
        internal static float GridX { get { return (GridLeft + GridRight) / 2f; } }
        internal static float GridY { get { return (GridTop + GridBottom) / 2f; } }
        internal static float GridW { get { return GridRight - GridLeft; } }

        // inside a card (relative to its centre): 16:9 picture, name, "by author"
        internal const float CardPad = 4f;
        internal static HudRect CardPhoto { get { return new HudRect(0f, CardH / 2f - CardPad - 63f, CardW - 2f * CardPad, 126f); } }
        internal static HudRect CardTitle { get { return new HudRect(0f, CardPhoto.Bottom - 15f, CardW - 18f, 22f); } }
        internal static HudRect CardSub { get { return new HudRect(0f, CardPhoto.Bottom - 38f, CardW - 18f, 16f); } }
        internal static HudRect CardShade { get { HudRect p = CardPhoto; return new HudRect(0f, p.Bottom + 20f, p.W, 40f); } }
        internal static HudRect CardDlIcon { get { HudRect p = CardPhoto; return new HudRect(p.Left + 14f, p.Bottom + 12f, 14f, 14f); } }
        internal static HudRect CardDlText { get { HudRect p = CardPhoto; return new HudRect(p.Left + 62f, p.Bottom + 12f, 80f, 18f); } }
        internal static HudRect CardRateText { get { HudRect p = CardPhoto; return new HudRect(p.Right - 28f, p.Bottom + 12f, 44f, 18f); } }
        internal static HudRect CardRateIcon { get { HudRect p = CardPhoto; return new HudRect(p.Right - 58f, p.Bottom + 12f, 14f, 14f); } }
        internal static HudRect CardBadge { get { HudRect p = CardPhoto; return new HudRect(p.Left + 48f, p.Top - 15f, 84f, 20f); } }
        internal static HudRect CardFav { get { HudRect p = CardPhoto; return new HudRect(p.Right - 17f, p.Top - 17f, 24f, 24f); } }

        // the details view (in the main area)
        internal static readonly HudRect Back = new HudRect(-178f, 272f, 124f, 46f);
        internal static readonly HudRect Crumb = new HudRect(93f, 272f, 380f, 30f);
        internal static readonly HudRect BigPic = new HudRect(120f, 76f, 576f, 324f);
        internal static readonly HudRect GalPrev = new HudRect(-209f, 76f, 44f, 88f);
        internal static readonly HudRect GalNext = new HudRect(449f, 76f, 44f, 88f);
        internal static readonly HudRect Dots = new HudRect(120f, -100f, 300f, 10f);
        internal static readonly HudRect DTitle = new HudRect(120f, -132f, 700f, 36f);
        internal static readonly HudRect DInfo = new HudRect(120f, -164f, 700f, 22f);
        internal static readonly HudRect DStats = new HudRect(120f, -192f, 700f, 24f);
        internal static readonly HudRect DSummary = new HudRect(120f, -240f, 700f, 62f);
        internal static readonly HudRect Play = new HudRect(-80f, -316f, 300f, 58f);
        internal static readonly HudRect Fav = new HudRect(170f, -316f, 176f, 58f);
        internal static readonly HudRect Played = new HudRect(372f, -316f, 196f, 40f);

        // decoration
        internal const float ShadowOffset = 12f, ShadowGrow = 24f;

        // keyboard under the panel (rows of 64-unit keys, 8 apart)
        internal const float KeyboardTopY = -410f;
        internal static float KeyboardBottom { get { return KeyboardTopY - 4f * 72f - 32f; } }

        /// <summary>The fixed parts of the grid view (for the overlap test).</summary>
        internal static List<KeyValuePair<string, HudRect>> GridParts()
        {
            var list = Common();
            list.Add(P("Search", Search));
            list.Add(P("Sort", Sort));
            list.Add(P("Prev", Prev));
            list.Add(P("PageText", PageText));
            list.Add(P("Next", Next));
            for (int r = 0; r < MapModel.Rows; r++)
                for (int c = 0; c < MapModel.Cols; c++)
                    list.Add(P("Card" + r + c, new HudRect(CardX(c), CardY(r), CardW, CardH)));
            return list;
        }

        /// <summary>The fixed parts of the details view (for the overlap test).</summary>
        internal static List<KeyValuePair<string, HudRect>> DetailParts()
        {
            var list = Common();
            list.Add(P("Back", Back));
            list.Add(P("Crumb", Crumb));
            list.Add(P("Prev", Prev));
            list.Add(P("PageText", PageText));
            list.Add(P("Next", Next));
            list.Add(P("BigPic", BigPic));
            list.Add(P("GalPrev", GalPrev));
            list.Add(P("GalNext", GalNext));
            list.Add(P("Dots", Dots));
            list.Add(P("DTitle", DTitle));
            list.Add(P("DInfo", DInfo));
            list.Add(P("DStats", DStats));
            list.Add(P("DSummary", DSummary));
            list.Add(P("Play", Play));
            list.Add(P("Fav", Fav));
            list.Add(P("Played", Played));
            return list;
        }

        /// <summary>The parts inside one card, relative to the card (for the overlap test).</summary>
        internal static List<KeyValuePair<string, HudRect>> CardParts()
        {
            return new List<KeyValuePair<string, HudRect>>
            {
                P("Photo", CardPhoto), P("Title", CardTitle), P("Sub", CardSub),
            };
        }

        internal static List<KeyValuePair<string, HudRect>> PhotoParts()
        {
            return new List<KeyValuePair<string, HudRect>>
            {
                P("DlIcon", CardDlIcon), P("DlText", CardDlText), P("RateIcon", CardRateIcon), P("RateText", CardRateText),
                P("Badge", CardBadge), P("Fav", CardFav),
            };
        }

        private static List<KeyValuePair<string, HudRect>> Common()
        {
            var list = new List<KeyValuePair<string, HudRect>>
            {
                P("TitleIcon", TitleIcon), P("Title", Title), P("Status", Status), P("Classic", Classic), P("Close", Close),
                P("SideTitle", SideTitle), P("Separator", Separator), P("Random", Random), P("NowLabel", NowLabel), P("NowPic", NowPic),
                P("NowTitle", NowTitle), P("NowSub", NowSub), P("Tip", Tip),
            };
            for (int i = 0; i < MapModel.Cats.Length; i++) list.Add(P("Cat" + i, new HudRect(CatX, CatY(i), CatW, CatH)));
            return list;
        }

        private static KeyValuePair<string, HudRect> P(string n, HudRect r) { return new KeyValuePair<string, HudRect>(n, r); }
    }
}
