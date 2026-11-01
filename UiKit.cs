using System;
using Il2CppSLZ.Bonelab;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapMenuPlus
{
    /// <summary>Look of the new parts: the font of the game's own menu, the UI layer.</summary>
    internal sealed class UiStyle
    {
        internal TMP_FontAsset Font;
        internal int Layer = 5;   // UI layer: the game's laser pointer hits colliders on it
    }

    /// <summary>Texts and the invisible laser catcher (buttons are made by Paint).</summary>
    internal static class UiKit
    {
        /// <summary>A text that cannot be pressed.</summary>
        internal static TMP_Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize,
                                       UiStyle style, Color color)
        {
            var go = new GameObject("MMP_" + name);
            go.layer = style.Layer;
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (style.Font != null) tmp.font = style.Font;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return tmp;
        }

        /// <summary>
        /// An invisible trigger collider covering an area: the laser stops there, so it cannot reach the game's
        /// (hidden) buttons behind the HUD. Same idea as the game's own "backround_event_catcher_IMPORTANT".
        /// </summary>
        internal static void Catcher(Transform parent, string name, Vector2 position, Vector2 size, float z, UiStyle style)
        {
            var go = new GameObject("MMP_" + name);
            go.layer = style.Layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, z);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 10f);
            col.isTrigger = true;
        }
    }

    /// <summary>A QWERTY keyboard under the map menu (digits, letters, Backspace, Space, Clear), rounded keys.</summary>
    internal sealed class Keyboard
    {
        private static readonly string[] Rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
        private const float Key = 64f;
        private const float Gap = 8f;

        private readonly GameObject _root;

        internal Keyboard(Transform parent, Vector2 topCenter, UiStyle style,
                          Action<char> onChar, Action onBackspace, Action onSpace, Action onClear)
        {
            _root = new GameObject("MMP_Keyboard");
            _root.layer = style.Layer;
            _root.transform.SetParent(parent, false);
            var rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = topCenter;

            // a dark plate behind the keys (and a catcher, so the laser cannot click through the gaps)
            float plateW = 10 * Key + 9 * Gap + 28f, plateH = 5 * Key + 4 * Gap + 28f;
            var plate = new HudRect(0f, -(5 * Key + 4 * Gap) / 2f + Key / 2f, plateW, plateH);
            Paint.Rounded(_root.transform, "KeyboardShadow", new HudRect(plate.X + 6f, plate.Y - 8f, plate.W + 10f, plate.H + 10f), 20f, new Color(0f, 0f, 0f, 0.35f));
            Paint.Rounded(_root.transform, "KeyboardPlate", plate, 16f, Theme.TitleBar);
            UiKit.Catcher(_root.transform, "KeyboardCatcher", new Vector2(plate.X, plate.Y), new Vector2(plate.W, plate.H), 6f, style);

            TMP_Text ignored;
            float y = 0f;
            foreach (string row in Rows)
            {
                float width = row.Length * Key + (row.Length - 1) * Gap;
                float x0 = -width / 2f + Key / 2f;
                for (int j = 0; j < row.Length; j++)
                {
                    char c = row[j];
                    Paint.TextBtn(_root.transform, "Key_" + c, new HudRect(x0 + j * (Key + Gap), y, Key, Key), 10f, c.ToString(), 28f,
                                  Theme.Button, Theme.ButtonHover, Theme.Accent, Theme.Text, () => onChar(c), out ignored);
                }
                y -= Key + Gap;
            }
            Paint.TextBtn(_root.transform, "Key_Backspace", new HudRect(-250f, y, 200f, Key), 10f, "BACKSPACE", 20f, Theme.Button, Theme.ButtonHover, Theme.Accent, Theme.Text, onBackspace, out ignored);
            Paint.TextBtn(_root.transform, "Key_Space", new HudRect(0f, y, 280f, Key), 10f, "SPACE", 20f, Theme.Button, Theme.ButtonHover, Theme.Accent, Theme.Text, onSpace, out ignored);
            Paint.TextBtn(_root.transform, "Key_Clear", new HudRect(250f, y, 200f, Key), 10f, "CLEAR", 20f, Theme.Danger, Theme.DangerHover, Theme.DangerHover, Theme.Text, onClear, out ignored);
            _root.SetActive(false);
        }

        internal bool Visible { get { return _root != null && _root.activeSelf; } }

        internal GameObject Root { get { return _root; } }

        internal void Show()
        {
            if (_root == null) return;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();   // drawn on top of the menu's other parts
        }

        internal void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        internal void Destroy()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
    }
}
