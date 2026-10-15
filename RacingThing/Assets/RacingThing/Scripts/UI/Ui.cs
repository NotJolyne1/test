using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RacingThing
{
    // Small helpers for building uGUI from code, so every screen looks the same.
    public static class Ui
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.16f, 0.82f);
        public static readonly Color PanelLight = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Accent = new Color(1f, 0.72f, 0.16f);
        public static readonly Color AccentDark = new Color(0.95f, 0.45f, 0.12f);
        public static readonly Color TextColor = new Color(0.97f, 0.97f, 1f);
        public static readonly Color TextDim = new Color(0.75f, 0.8f, 0.9f, 0.85f);
        public static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.1f);
        public static readonly Color Good = new Color(0.35f, 0.95f, 0.5f);
        public static readonly Color Bad = new Color(1f, 0.38f, 0.35f);

        static Font font;
        static Sprite rounded, roundedSmall, circle, gradient;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Helvetica" }, 32);
                }
                return font;
            }
        }

        public static Sprite Rounded { get { return rounded ?? (rounded = MakeRounded(64, 22)); } }
        public static Sprite RoundedSmall { get { return roundedSmall ?? (roundedSmall = MakeRounded(32, 9)); } }
        public static Sprite Circle { get { return circle ?? (circle = MakeRounded(64, 32)); } }

        public static Sprite Gradient
        {
            get
            {
                if (gradient == null)
                {
                    var t = new Texture2D(4, 64, TextureFormat.RGBA32, false);
                    t.wrapMode = TextureWrapMode.Clamp;
                    for (int y = 0; y < 64; y++)
                        for (int x = 0; x < 4; x++)
                            t.SetPixel(x, y, new Color(1, 1, 1, y / 63f));
                    t.Apply();
                    gradient = Sprite.Create(t, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);
                }
                return gradient;
            }
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            float b = Mathf.Min(radius, size / 2 - 1);
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        // ------------------------------------------------------------------ layout

        public static RectTransform Rect(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        // stretch to fill the parent with optional padding.
        public static RectTransform Fill(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        public static GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            return go;
        }

        public static Image Panel(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = Node(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite ?? Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        public static Image Plain(Transform parent, string name, Color color)
        {
            var go = Node(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true)
        {
            var go = Node("Label", parent);
            var t = go.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Shadow AddShadow(Graphic g, float distance = 3f, float alpha = 0.45f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha);
            s.effectDistance = new Vector2(distance, -distance);
            return s;
        }

        public static Outline AddOutline(Graphic g, Color c, float distance = 2f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(distance, -distance);
            return o;
        }

        // ------------------------------------------------------------------ buttons

        public enum ButtonStyle { Primary, Normal, Ghost }

        public static Button Button(Transform parent, string text, int fontSize, ButtonStyle style, UnityAction onClick)
        {
            var img = Panel(parent, "Button_" + text, Color.white);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            Color normal;
            Color txt = TextColor;
            switch (style)
            {
                case ButtonStyle.Primary:
                    normal = Accent;
                    txt = new Color(0.12f, 0.08f, 0.02f);
                    break;
                case ButtonStyle.Ghost:
                    normal = new Color(1f, 1f, 1f, 0.0f);
                    break;
                default:
                    normal = ButtonColor;
                    break;
            }
            img.color = Color.white;
            colors.normalColor = normal;
            colors.highlightedColor = style == ButtonStyle.Primary ? new Color(1f, 0.82f, 0.35f) : new Color(1f, 1f, 1f, 0.22f);
            colors.pressedColor = style == ButtonStyle.Primary ? AccentDark : new Color(1f, 1f, 1f, 0.32f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.04f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            var label = Label(img.transform, text, fontSize, txt);
            Fill(label.rectTransform);
            btn.onClick.AddListener(() => Sfx.Play(Sound.Click));
            if (onClick != null) btn.onClick.AddListener(onClick);
            img.gameObject.AddComponent<ButtonFx>();
            return btn;
        }

        public static Text ButtonLabel(Button b)
        {
            return b.GetComponentInChildren<Text>();
        }

        // ------------------------------------------------------------------ text boxes

        public static InputField Input(Transform parent, string placeholder, string value, int fontSize, int limit)
        {
            var bg = Panel(parent, "Input", Color.white, RoundedSmall);
            var field = bg.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            var colors = field.colors;
            colors.normalColor = new Color(0f, 0f, 0f, 0.35f);
            colors.highlightedColor = new Color(0f, 0f, 0f, 0.45f);
            colors.selectedColor = new Color(0f, 0f, 0f, 0.55f);
            colors.pressedColor = colors.selectedColor;
            colors.fadeDuration = 0.08f;
            field.colors = colors;
            var nav = field.navigation;
            nav.mode = Navigation.Mode.None;
            field.navigation = nav;

            var text = Label(bg.transform, "", fontSize, TextColor, TextAnchor.MiddleLeft, false);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Fill(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16f, 4f);
            text.rectTransform.offsetMax = new Vector2(-16f, -4f);

            var hint = Label(bg.transform, placeholder, fontSize, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft, false);
            hint.fontStyle = FontStyle.Italic;
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            Fill(hint.rectTransform);
            hint.rectTransform.offsetMin = new Vector2(16f, 4f);
            hint.rectTransform.offsetMax = new Vector2(-16f, -4f);

            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = limit;
            field.lineType = InputField.LineType.SingleLine;
            field.customCaretColor = true;
            field.caretColor = Accent;
            field.caretWidth = 2;
            field.selectionColor = new Color(1f, 0.72f, 0.16f, 0.35f);
            field.text = value ?? "";
            return field;
        }

        public static bool Typing
        {
            get
            {
                var es = EventSystem.current;
                var go = es != null ? es.currentSelectedGameObject : null;
                var field = go != null ? go.GetComponent<InputField>() : null;
                return field != null && field.isFocused;
            }
        }
    }

    // grows a little on hover
    public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float HoverScale = 1.04f;
        bool hover;
        float s = 1f;

        public void OnPointerEnter(PointerEventData e) { hover = true; Sfx.Play(Sound.Hover); }
        public void OnPointerExit(PointerEventData e) { hover = false; }

        void OnDisable()
        {
            hover = false;
            s = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float target = hover ? HoverScale : 1f;
            s = Mathf.Lerp(s, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
