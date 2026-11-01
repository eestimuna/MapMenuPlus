using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSLZ.Bonelab;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapMenuPlus
{
    /// <summary>The colours of the map menu (dark theme like Spawn Menu Plus, blue accent, gold favorites, green "playing").</summary>
    internal static class Theme
    {
        internal static readonly Color Panel = Rgb(24, 26, 31);
        internal static readonly Color TitleBar = Rgb(14, 15, 18);
        internal static readonly Color Sidebar = Rgb(17, 18, 22);
        internal static readonly Color Card = Rgb(36, 39, 47);
        internal static readonly Color CardHover = Rgb(52, 57, 70);
        internal static readonly Color PhotoBg = Rgb(28, 30, 37);
        internal static readonly Color Input = Rgb(11, 12, 15);
        internal static readonly Color InputHover = Rgb(20, 22, 27);
        internal static readonly Color Button = Rgb(38, 41, 50);
        internal static readonly Color ButtonHover = Rgb(58, 63, 77);
        internal static readonly Color ButtonPress = Rgb(77, 115, 179);
        internal static readonly Color Accent = Rgb(66, 153, 255);
        internal static readonly Color AccentHover = Rgb(110, 178, 255);
        internal static readonly Color AccentDeep = Rgb(31, 69, 117);
        internal static readonly Color AccentSoft = new Color(0.26f, 0.60f, 1f, 0.20f);
        internal static readonly Color Play = Rgb(46, 196, 109);
        internal static readonly Color PlayHover = Rgb(76, 224, 139);
        internal static readonly Color PlayPress = Rgb(30, 150, 80);
        internal static readonly Color Danger = Rgb(140, 41, 48);
        internal static readonly Color DangerHover = Rgb(199, 61, 69);
        internal static readonly Color Text = Rgb(240, 242, 247);
        internal static readonly Color Text2 = Rgb(153, 163, 184);
        internal static readonly Color Muted = Rgb(107, 115, 133);
        internal static readonly Color Gold = Rgb(255, 199, 61);
        internal static readonly Color Green = Rgb(102, 242, 140);
        internal static readonly Color Slz = Rgb(255, 140, 46);         // BONELAB's own maps
        internal static readonly Color Outline = new Color(1f, 1f, 1f, 0.08f);
        internal static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
        internal static readonly Color RowHover = new Color(1f, 1f, 1f, 0.07f);
        internal static readonly Color Pill = new Color(1f, 1f, 1f, 0.08f);
        internal static readonly Color PillDark = new Color(0f, 0f, 0f, 0.62f);
        internal const string Text2Hex = "#99A3B8";
        internal const string MutedHex = "#6B7385";
        internal const string GoldHex = "#FFC73D";
        internal const string GreenHex = "#66F28C";
        internal const string AccentHex = "#4299FF";

        internal static Color Rgb(int r, int g, int b) { return new Color(r / 255f, g / 255f, b / 255f, 1f); }
        internal static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

        internal static Color Lerp(Color a, Color b, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }

        /// <summary>A stable, rich colour per map (the picture of maps without one).</summary>
        internal static Color MapColor(string key)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in key ?? "") h = h * 31 + char.ToLowerInvariant(c);
                float hue = (h & 0xFFFF) / 65535f;
                return Color.HSVToRGB(hue, 0.55f, 0.75f);
            }
        }
    }

    /// <summary>
    /// The pictures the menu is drawn with (rounded shapes, icons, gradients), made by Shapes. The big ones are drawn in
    /// the background when the game starts; small ones (pills of a certain size) on first use. Units: 2 pixels per
    /// menu unit (1 for the big panels), so corners stay crisp in VR.
    /// </summary>
    internal static class Bank
    {
        private static Task<Dictionary<string, Pic>> _making;
        private static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        private static Rgba W { get { return Rgba.White; } }

        internal static void Prepare()
        {
            if (_making == null) _making = Task.Run(() => MakeAll());
        }

        private static Dictionary<string, Pic> MakeAll()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var d = new Dictionary<string, Pic>();
            HudRect p = MapLayout.Panel, tb = MapLayout.TitleBar, sb = MapLayout.Sidebar;
            d["panel"] = Shapes.RoundRect((int)p.W, (int)p.H, 18f, W);
            d["titlebar"] = Shapes.RoundRect((int)(tb.W * 2f), (int)(tb.H * 2f), 36f, 36f, 0f, 0f, W, W);
            d["sidebar"] = Shapes.RoundRect((int)sb.W, (int)sb.H, 0f, 0f, 0f, 18f, W, W);
            d["card"] = Shapes.RoundRect((int)(MapLayout.CardW * 2f), (int)(MapLayout.CardH * 2f), 22f, W);
            d["card_outline"] = Shapes.RoundRectOutline((int)(MapLayout.CardW * 2f), (int)(MapLayout.CardH * 2f), 22f, 4f, W);
            d["bigframe"] = Shapes.RoundRect((int)(MapLayout.BigPic.W + 8f), (int)(MapLayout.BigPic.H + 8f), 14f, W);
            d["shade"] = Shapes.VGradient(4, 64, new Rgba(0f, 0f, 0f, 0f), new Rgba(0f, 0f, 0f, 0.78f));
            d["shade_top"] = Shapes.VGradient(4, 64, new Rgba(0f, 0f, 0f, 0.55f), new Rgba(0f, 0f, 0f, 0f));
            d["mapart"] = Shapes.MapArt(384, 216);
            d["dot"] = Shapes.Circle(24, W);
            d["shimmer"] = Shimmer(256);
            d["cap"] = Cap(24);
            foreach (string n in Shapes.IconNames) d["icon_" + n] = Shapes.NamedIcon(n, 64);
            sw.Stop();
            Log.Info("Menu pictures drawn in the background: " + d.Count + " in " + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms.");
            return d;
        }

        /// <summary>A soft light band in the middle (slid over a picture that is still loading).</summary>
        private static Pic Shimmer(int w)
        {
            var pic = new Pic(w, 4);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = (x + 0.5f) / w;
                    float a = (float)Math.Exp(-Math.Pow((t - 0.5f) / 0.12f, 2)) * 0.10f;
                    int k = (y * w + x) * 4;
                    pic.Px[k] = pic.Px[k + 1] = pic.Px[k + 2] = 255;
                    pic.Px[k + 3] = (byte)(a * 255f + 0.5f);
                }
            return pic;
        }

        /// <summary>
        /// A rounded-corner cap: solid OUTSIDE a quarter circle (bottom-left corner piece; rotated for the others).
        /// Laid over a picture's corners in the colour behind it, so square pictures get round corners.
        /// </summary>
        private static Pic Cap(int size)
        {
            var pic = new Pic(size, size);
            float R = size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = R - (x + 0.5f), dy = R - (y + 0.5f);       // centre of the circle = top right of the piece
                    float d = (float)Math.Sqrt(dx * dx + dy * dy) - R;
                    float cov = Shapes.Clamp01(0.5f + d);                 // outside the circle = solid
                    int k = (y * size + x) * 4;
                    pic.Px[k] = pic.Px[k + 1] = pic.Px[k + 2] = 255;
                    pic.Px[k + 3] = (byte)(cov * 255f + 0.5f);
                }
            return pic;
        }

        /// <summary>The texture of a picture (made on first use; main thread).</summary>
        internal static Texture2D Get(string name)
        {
            Texture2D t;
            if (_tex.TryGetValue(name, out t) && t != null) return t;
            Prepare();
            Dictionary<string, Pic> all = _making.Result;   // normally long done (drawn when the game started)
            Pic p;
            if (!all.TryGetValue(name, out p)) { Log.Warn("No menu picture called " + name); return null; }
            t = ToTexture(p);
            _tex[name] = t;
            return t;
        }

        /// <summary>A rounded rectangle of this size (menu units) and corner radius, made on first use.</summary>
        internal static Texture2D Round(float w, float h, float radius)
        {
            int pw = (int)Math.Round(w * 2f), ph = (int)Math.Round(h * 2f);
            string key = "rr_" + pw + "_" + ph + "_" + (int)(radius * 2f);
            Texture2D t;
            if (_tex.TryGetValue(key, out t) && t != null) return t;
            float r = Math.Min(radius * 2f, Math.Min(pw, ph) / 2f);
            t = ToTexture(Shapes.RoundRect(pw, ph, r, Rgba.White));
            _tex[key] = t;
            return t;
        }

        /// <summary>A Unity texture from a picture (RGBA, row 0 = bottom).</summary>
        internal static Texture2D ToTexture(Pic p)
        {
            var tex = new Texture2D(p.W, p.H, TextureFormat.RGBA32, false);
            var px = new Color32[p.W * p.H];
            for (int i = 0; i < px.Length; i++)
            {
                int k = i * 4;
                Color32 c = default(Color32);
                c.r = p.Px[k]; c.g = p.Px[k + 1]; c.b = p.Px[k + 2]; c.a = p.Px[k + 3];
                px[i] = c;
            }
            tex.SetPixels32(new Il2CppStructArray<Color32>(px), 0);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return tex;
        }
    }

    /// <summary>
    /// Building blocks: pictures (RawImage with a Bank texture, tinted), plain rectangles, texts, and pressable buttons
    /// (the proven recipe: graphic + trigger BoxCollider "Collider" + Button + the game's ButtonHoverClick, so the
    /// game's laser points and clicks them like its own buttons).
    /// </summary>
    internal static class Paint
    {
        internal static int Layer = 5;
        internal static UiStyle Style;

        internal static RectTransform Node(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("MMP_" + name);
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        internal static RawImage Pic(Transform parent, string name, Vector2 pos, Vector2 size, Texture tex, Color tint)
        {
            RectTransform rect = Node(parent, name, pos, size);
            RawImage img = rect.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            if (tex != null) img.texture = tex;
            img.color = tint;
            return img;
        }

        internal static RawImage Pic(Transform parent, string name, HudRect r, string bank, Color tint)
        {
            return Pic(parent, name, V(r), S(r), bank != null ? Bank.Get(bank) : null, tint);
        }

        /// <summary>A rounded rectangle of exactly this size.</summary>
        internal static RawImage Rounded(Transform parent, string name, HudRect r, float radius, Color tint)
        {
            return Pic(parent, name, V(r), S(r), Bank.Round(r.W, r.H, radius), tint);
        }

        internal static RawImage Icon(Transform parent, string name, HudRect r, string icon, Color tint)
        {
            return Pic(parent, name, V(r), S(r), Bank.Get("icon_" + icon), tint);
        }

        internal static Image Solid(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            RectTransform rect = Node(parent, name, pos, size);
            Image img = rect.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = color;
            return img;
        }

        internal static TMP_Text Text(Transform parent, string name, string text, HudRect r, float fontSize, Color color,
                                      TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false, bool wrap = false)
        {
            TMP_Text t = UiKit.Label(parent, name, text, V(r), S(r), fontSize, Style, color);
            t.alignment = align;
            if (bold) t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = false;
            t.enableWordWrapping = wrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        /// <summary>
        /// A pressable area. The graphic (a rounded rectangle of this size) is the button's colour target (normal /
        /// pointed at / pressed); the label (optional) sits on it.
        /// </summary>
        internal static Button Btn(Transform parent, string name, HudRect r, float radius, Color normal, Color hover, Color pressed,
                                   Action onClick, out RawImage graphic)
        {
            RectTransform rect = Node(parent, name, V(r), S(r));
            GameObject go = rect.gameObject;
            go.AddComponent<CanvasRenderer>();

            RectTransform g = Node(go.transform, "bg", Vector2.zero, S(r));
            graphic = g.gameObject.AddComponent<RawImage>();
            graphic.texture = radius > 0f ? Bank.Round(r.W, r.H, radius) : null;
            graphic.color = Color.white;
            graphic.raycastTarget = true;

            var colGo = new GameObject("Collider");
            colGo.layer = Layer;
            colGo.transform.SetParent(go.transform, false);
            var col = colGo.AddComponent<BoxCollider>();
            col.size = new Vector3(r.W + 4f, r.H + 4f, 1f);
            col.isTrigger = true;

            Button b = go.AddComponent<Button>();
            b.onClick.AddListener(onClick);
            b.transition = Selectable.Transition.ColorTint;
            b.targetGraphic = graphic;
            Colors(b, normal, hover, pressed);
            go.AddComponent<ButtonHoverClick>();
            return b;
        }

        /// <summary>A button with a text (and optionally an icon left of the text).</summary>
        internal static Button TextBtn(Transform parent, string name, HudRect r, float radius, string label, float fontSize, Color normal, Color hover,
                                       Color pressed, Color textColor, Action onClick, out TMP_Text text, string icon = null)
        {
            RawImage ignored;
            Button b = Btn(parent, name, r, radius, normal, hover, pressed, onClick, out ignored);
            float iconW = icon != null ? fontSize * 1.05f : 0f;
            text = Text(b.transform, "Text", label, new HudRect(icon != null ? iconW * 0.5f + 3f : 0f, 0f, r.W - 16f - (icon != null ? iconW + 6f : 0f), r.H),
                        fontSize, textColor, TextAlignmentOptions.Center, true);
            if (icon != null)
            {
                Icon(b.transform, "Icon", new HudRect(-r.W / 2f + 14f + iconW / 2f, 0f, iconW, iconW), icon, textColor);
                Center(text);
            }
            return b;
        }

        /// <summary>A button's label changes: the icon + text stay centred together.</summary>
        internal static void SetBtnText(TMP_Text t, string s)
        {
            if (t == null || t.text == s) return;
            t.text = s;
            Center(t);
        }

        /// <summary>Puts a button's icon and text side by side in its middle (measured text width).</summary>
        internal static void Center(TMP_Text t)
        {
            try
            {
                Transform b = t.transform.parent;
                Transform ic = b != null ? b.Find("MMP_Icon") : null;
                if (ic == null) return;
                RectTransform br = b.GetComponent<RectTransform>();   // (a C# cast does not work on game objects)
                RectTransform ir = ic.GetComponent<RectTransform>();
                RectTransform tr = t.GetComponent<RectTransform>();
                float maxW = br.sizeDelta.x - 24f - ir.sizeDelta.x - 7f;
                object pw = R.Get(t, "preferredWidth");          // TextMeshPro's measured width of the text
                if (!(pw is float)) return;
                float tw = Math.Min((float)pw, maxW);
                if (tw <= 0f || float.IsNaN(tw)) return;
                float total = ir.sizeDelta.x + 7f + tw, left = -total / 2f;
                ir.anchoredPosition = new Vector2(left + ir.sizeDelta.x / 2f, 0f);
                t.alignment = TextAlignmentOptions.Left;
                tr.sizeDelta = new Vector2(tw + 6f, tr.sizeDelta.y);
                tr.anchoredPosition = new Vector2(left + ir.sizeDelta.x + 7f + tw / 2f + 3f, 0f);
            }
            catch { }
        }

        internal static void Colors(Button b, Color normal, Color hover, Color pressed)
        {
            if (b == null) return;
            var c = b.colors;
            if (c.normalColor == normal && c.highlightedColor == hover && c.pressedColor == pressed) return;
            c.normalColor = normal;
            c.highlightedColor = hover;
            c.pressedColor = pressed;
            c.selectedColor = normal;      // otherwise it stays lit after a press
            c.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.5f);
            b.colors = c;
        }

        /// <summary>
        /// Round corners for a square picture: four caps in the colour behind it. Returns them (to recolour on hover).
        /// </summary>
        internal static List<RawImage> Caps(Transform parent, string name, HudRect r, float radius, Color behind)
        {
            var list = new List<RawImage>(4);
            Texture2D cap = Bank.Get("cap");
            for (int i = 0; i < 4; i++)
            {
                float sx = (i == 1 || i == 2) ? 1f : -1f;   // 0 bottom-left, 1 bottom-right, 2 top-right, 3 top-left
                float sy = i >= 2 ? 1f : -1f;
                var pos = new Vector2(r.X + sx * (r.W / 2f - radius / 2f), r.Y + sy * (r.H / 2f - radius / 2f));
                RawImage c = Pic(parent, name + "Cap" + i, pos, new Vector2(radius, radius), cap, behind);
                c.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * i);   // bottom-left piece turned counter-clockwise
                list.Add(c);
            }
            return list;
        }

        internal static Vector2 V(HudRect r) { return new Vector2(r.X, r.Y); }
        internal static Vector2 S(HudRect r) { return new Vector2(r.W, r.H); }

        internal static void SetText(TMP_Text t, string s)
        {
            if (t != null && t.text != s) t.text = s;
        }

        internal static void SetColor(Graphic g, Color c)
        {
            if (g != null && g.color != c) g.color = c;
        }

        internal static void Show(Component c, bool on)
        {
            if (c == null) return;
            GameObject go = c.gameObject;
            if (go.activeSelf != on) go.SetActive(on);
        }

        internal static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
