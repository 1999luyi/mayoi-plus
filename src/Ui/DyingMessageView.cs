using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Pwaat.Accessibility.Ui
{
    internal sealed class DyingMessageView : IDisposable
    {
        private GameObject _root, _panel;
        private Text _title, _status, _help, _left, _right;
        private Font _font;
        private readonly List<GameObject> _items = new List<GameObject>();
        internal bool Visible { get { return _root != null && _root.activeSelf; } }
        internal void Hide() { if (_root != null) _root.SetActive(false); }
        private static Text TextAt(Transform parent, Font font, string name, float x, float y, float w, float h, int size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            Text text = go.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = Color.white; text.raycastTarget = false; text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        private GameObject Tile(float x, float y, bool focused, bool filled, Sprite sprite, string label)
        {
            GameObject go = new GameObject("Dot", typeof(RectTransform), typeof(Image)); go.transform.SetParent(_panel.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(92, 92);
            Image bg = go.GetComponent<Image>(); bg.color = focused ? new Color(.08f, .48f, .75f, 1) : filled ? new Color(.25f, .3f, .4f, 1) : new Color(.09f, .12f, .18f, 1); bg.raycastTarget = false;
            Text number = TextAt(go.transform, _font, "Number", 4, 2, 84, 26, 22); number.text = label;
            if (sprite != null && filled) { GameObject dot = new GameObject("NativePoint", typeof(RectTransform), typeof(Image)); dot.transform.SetParent(go.transform, false); RectTransform dr = dot.GetComponent<RectTransform>(); dr.anchorMin = new Vector2(.18f, .08f); dr.anchorMax = new Vector2(.82f, .7f); dr.offsetMin = dr.offsetMax = Vector2.zero; Image image = dot.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; }
            _items.Add(go); return go;
        }
        internal void Render(Font font, UiMessages m, DyingMessageState state, Sprite[] sources, int area, int column, int row, int action, string status, string help)
        {
            if (_root == null)
            {
                _font = font; _root = new GameObject("PwaatDyingMessageBraille", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                Canvas canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32750;
                CanvasScaler scale = _root.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scale.referenceResolution = new Vector2(1920, 1080);
                _panel = new GameObject("Puzzle", typeof(RectTransform), typeof(Image)); _panel.transform.SetParent(_root.transform, false); RectTransform panel = _panel.GetComponent<RectTransform>(); panel.anchorMin = new Vector2(.03f, .16f); panel.anchorMax = new Vector2(.97f, .94f); panel.offsetMin = panel.offsetMax = Vector2.zero; _panel.GetComponent<Image>().color = new Color(.015f, .025f, .045f, 1); _panel.GetComponent<Image>().raycastTarget = false;
                _title = TextAt(_panel.transform, font, "Title", 35, 20, 1700, 65, 36); _left = TextAt(_panel.transform, font, "Left", 35, 100, 920, 55, 30); _right = TextAt(_panel.transform, font, "Right", 1070, 100, 680, 55, 30);
                _status = TextAt(_panel.transform, font, "Status", 35, 565, 1700, 80, 30); _help = TextAt(_panel.transform, font, "Help", 35, 660, 1700, 170, 23);
            }
            _root.SetActive(true);
            foreach (GameObject item in _items) UnityEngine.Object.Destroy(item); _items.Clear();
            _title.text = m.Get("dying.title"); _left.text = m.Get("dying.grid"); _right.text = m.Get("dying.pool"); _status.text = status; _help.text = help;
            int first = area == 0 ? column / 2 / 4 * 4 : Math.Max(0, state.CellCount - 4);
            int last = Math.Min(state.CellCount, first + 4);
            for (int c = first; c < last; c++) for (int d = 0; d < 6; d++)
            {
                int token = state.At(c, d); Tile(35 + (c - first) * 235 + (d / 3) * 102, 180 + (d % 3) * 104, area == 0 && column == c * 2 + d / 3 && row == d % 3, token >= 0, token < 0 ? null : sources[token], (c + 1) + ":" + (d + 1));
            }
            for (int i = 0; i < state.SourceCount; i++) Tile(1070 + (i / 3) * 125, 180 + (i % 3) * 104, area == 1 && column == i / 3 && row == i % 3, !state.Used(i), sources[i], (i + 1).ToString());
            Text add = TextAt(_panel.transform, font, "AddCell", 35, 500, 410, 45, 27); add.text = (area == 2 && action == 0 ? "> " : "") + m.Get("dying.add"); _items.Add(add.gameObject);
            Text remove = TextAt(_panel.transform, font, "RemoveCell", 470, 500, 470, 45, 27); remove.text = (area == 2 && action == 1 ? "> " : "") + m.Get("dying.remove-cell"); _items.Add(remove.gameObject);
        }
        public void Dispose() { if (_root != null) UnityEngine.Object.Destroy(_root); _root = null; _items.Clear(); }
    }
}
