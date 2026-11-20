using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    /// <summary>Three real rows appended to the native option model.</summary>
    public sealed class AccessibilityOptionItem : optionItem
    {
        internal int Index;
        private Text _value;
        internal Text DescriptionControl { get; private set; }
        internal string Label { get { return UiRuntime.Current.Messages.Get(Index == 0 ? "a11y.guides" : Index == 1 ? "a11y.interruptions" : "a11y.puzzle-adaptation"); } }
        internal string Description { get { return UiRuntime.Current.Messages.Get(Index == 0 ? "a11y.guides-description" : Index == 1 ? "a11y.interruptions-description" : "a11y.puzzle-adaptation-description"); } }
        internal string Value
        {
            get
            {
                AccessibilityPreferences p = UiRuntime.Current.Speech.Preferences;
                return UiRuntime.Current.Messages.Get(Index == 0 ? "a11y.guides-" + (int)p.Guides : Index == 1 ? "a11y.interruptions-" + (int)p.Interruptions : "a11y.puzzle-adaptation-" + (p.PuzzleAdaptation ? "1" : "0"));
            }
        }
        internal void Configure(int index, Text value, Text description) { Index = index; _value = value; DescriptionControl = description; }
        /// <summary>Initializes the native background and current translated values.</summary>
        public override void Init() { base.Init(); if (DescriptionControl != null) mainCtrl.instance.addText(DescriptionControl); Refresh(); }
        private void Refresh() { SetText(Label); if (_value != null) { _value.text = "‹  " + Value + "  ›"; } if (DescriptionControl != null) DescriptionControl.text = Description; }
        /// <summary>Releases the appended description from the native font registry.</summary>
        public override void Close() { if (DescriptionControl != null) mainCtrl.instance.removeText(DescriptionControl); base.Close(); }
        /// <summary>Changes the persisted mod preference through the native left/right action.</summary>
        public override void ChangeValue(int amount)
        {
            UiRuntime r = UiRuntime.Current; AccessibilityPreferences p = r.Speech.Preferences;
            bool oldAdaptation = p.PuzzleAdaptation;
            GuideAnnouncement oldGuide = p.Guides; InterruptionHandling oldInterrupt = p.Interruptions;
            if (Index == 0) { p.Guides = (GuideAnnouncement)(((int)p.Guides + amount + 3) % 3); }
            else if (Index == 1) { p.Interruptions = (InterruptionHandling)(((int)p.Interruptions + amount + 2) % 2); }
            if (Index == 2 && (amount % 2) != 0) p.PuzzleAdaptation = !p.PuzzleAdaptation;
            try { p.Save(); } catch (Exception error) { p.Guides = oldGuide; p.Interruptions = oldInterrupt; p.PuzzleAdaptation = oldAdaptation; r.ReadFailure("accessibility-save", error); return; }
            Refresh(); if (r.Assistance != null) { r.Assistance.Guides.ChangedPreference(); }
            r.Batch.OptionValueChanged(this);
            if (soundCtrl.instance != null) { soundCtrl.instance.PlaySE(42); }
        }
        /// <summary>Native confirmation advances the current setting.</summary>
        public override bool SelectDecision() { ChangeValue(1); return false; }
        /// <summary>Mouse activation uses the same setting action.</summary>
        public override void OnTouch(TouchParameter parameter) { ChangeValue(1); }
        /// <summary>Updates the value text without writing game settings.</summary>
        public override void InitValueSet() { Refresh(); }
        /// <summary>Game default-reset does not overwrite independent accessibility preferences.</summary>
        public override void DefaultValueSet() { Refresh(); }
        /// <summary>Uses the native focus color for both labels.</summary>
        public override void SelectEntry() { base.SelectEntry(); if (_value != null) { _value.color = Color.black; } }
        /// <summary>Restores the native unfocused color.</summary>
        public override void SelectExit() { base.SelectExit(); if (_value != null) { _value.color = Color.white; } }
    }
    internal sealed class NativeAccessibilitySettings : IDisposable
    {
        internal const int CategoryId = 9;
        internal const int FirstItem = 38;
        private readonly UiRuntime _runtime;
        private optionCtrl _owner;
        private optionCategoryItem _category;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private optionCtrl.Category[][] _originalCats;
        private optionCtrl.OptionItem[,] _originalRanges;
        private Vector3 _categoryScale;
        internal NativeAccessibilitySettings(UiRuntime runtime) { _runtime = runtime; }
        internal bool IsCategory(optionCtrl.Category category) { return (int)category == CategoryId; }
        internal void Ensure(optionCtrl owner)
        {
            if (_owner == owner && _category != null) { return; }
            if (_owner != null) { throw new InvalidOperationException("Native settings instance replaced without cleanup."); }
            List<optionCategoryItem> categories = (List<optionCategoryItem>)NativeFields.Get(owner, "categoryItem");
            List<optionItem> items = (List<optionItem>)NativeFields.Get(owner, "option_item_");
            if (items.Count != FirstItem || categories.Count < 9) { throw new InvalidOperationException("Unexpected native settings shape."); }
            _owner = owner;
            optionCategoryItem template = categories.Find(c => c.category == optionCtrl.Category.PRIVACY);
            if (template == null) { throw new InvalidOperationException("Native category template missing."); }
            GameObject categoryObject = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent, false); _objects.Add(categoryObject);
            categoryObject.name = "PwaatAccessibilityCategory"; categoryObject.transform.SetAsLastSibling();
            _category = categoryObject.GetComponent<optionCategoryItem>(); Set(_category, "m_Category", (optionCtrl.Category)CategoryId);
            categories.Add(_category);
            _categoryScale = template.transform.parent.localScale;
            optionCtrl.OptionItem[,] ranges = (optionCtrl.OptionItem[,])NativeFields.Get(owner, "cat2optionItemRange");
            _originalRanges = ranges;
            optionCtrl.OptionItem[,] expanded = new optionCtrl.OptionItem[10, 2]; Array.Copy(ranges, expanded, ranges.Length);
            expanded[CategoryId, 0] = (optionCtrl.OptionItem)FirstItem; expanded[CategoryId, 1] = (optionCtrl.OptionItem)(FirstItem + 2);
            Set(owner, "cat2optionItemRange", expanded);
            _originalCats = (optionCtrl.Category[][])NativeFields.Get(typeof(optionCtrl), "useCats");
            optionCtrl.Category[][] cats = new optionCtrl.Category[_originalCats.Length][];
            for (int i = 0; i < cats.Length; i++) { cats[i] = new optionCtrl.Category[_originalCats[i].Length + 1]; Array.Copy(_originalCats[i], cats[i], _originalCats[i].Length); cats[i][cats[i].Length - 1] = (optionCtrl.Category)CategoryId; }
            Set(typeof(optionCtrl), "useCats", cats);
            for (int index = 0; index < 3; index++)
            {
                // Clone a native two-choice row to preserve its backdrop, title font, and hit targets.
                optionItem source = items[(int)optionCtrl.OptionItem.V_SYNC];
                GameObject obj = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false); _objects.Add(obj);
                obj.name = "PwaatAccessibilityOption" + index;
                optionItem original = obj.GetComponent<optionItem>();
                Text title = (Text)NativeFields.Get(original, "option_title_");
                GameObject body = (GameObject)NativeFields.Get(original, "body_");
                foreach (Transform child in body.transform) { if (child != title.transform && !title.transform.IsChildOf(child) && child.GetComponentInChildren<Text>() != null) { child.gameObject.SetActive(false); } }
                AccessibilityOptionItem added = obj.AddComponent<AccessibilityOptionItem>();
                foreach (FieldInfo field in typeof(optionItem).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) { field.SetValue(added, field.GetValue(original)); }
                UnityEngine.Object.DestroyImmediate(original);
                added.touch_list.Clear(); if (added.select_touch != null) { added.touch_list.Add(added.select_touch); }
                Text value = UnityEngine.Object.Instantiate(title, title.transform.parent, false); value.name = "AccessibilityValue";
                value.rectTransform.anchoredPosition = title.rectTransform.anchoredPosition + new Vector2(630f, 0f);
                value.rectTransform.sizeDelta = new Vector2(620f, title.rectTransform.sizeDelta.y); value.alignment = TextAnchor.MiddleCenter;
                value.fontSize = Math.Min(32, title.fontSize); value.gameObject.SetActive(true);
                if (index == 2) { title.resizeTextForBestFit = true; title.resizeTextMinSize = 18; title.resizeTextMaxSize = title.fontSize; }
                Text description = null;
                if (index == 2)
                {
                    optionStory story = items[(int)optionCtrl.OptionItem.STORY] as optionStory;
                    if (story == null) throw new InvalidOperationException("Native story description template missing.");
                    description = CreatePuzzleDescription(story, body.transform);
                }
                added.Configure(index, value, description); items.Add(added);
            }
            _runtime.Trace("native_accessibility_append", "category=9 items=38,39,40");
        }
        internal static Text CreatePuzzleDescription(optionStory story, Transform parent)
        {
            IList warnings = (IList)NativeFields.Get(story, "warning_text");
            Text first = (Text)warnings[0], second = (Text)warnings[1];
            Text description = UnityEngine.Object.Instantiate(first, parent, false);
            description.name = "AccessibilityPuzzleDescription";
            // Use the same full-width region below the row as optionStory,
            // with three native line intervals available for wrapped text.
            RectTransform rect = description.rectTransform;
            float lineInterval = Math.Abs(first.rectTransform.anchoredPosition.y - second.rectTransform.anchoredPosition.y);
            rect.anchoredPosition += new Vector2(0f, rect.sizeDelta.y * (1f - rect.pivot.y));
            rect.pivot = new Vector2(rect.pivot.x, 1f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, lineInterval * 3f);
            description.alignment = TextAnchor.UpperLeft;
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Truncate;
            description.resizeTextForBestFit = true;
            description.resizeTextMinSize = 18;
            description.resizeTextMaxSize = first.fontSize;
            description.raycastTarget = false;
            description.gameObject.SetActive(true);
            return description;
        }
        internal void Layout(optionCtrl owner, optionCtrl.OptionType type)
        {
            if (_category == null || _owner != owner) { return; }
            Transform parent = _category.transform.parent;
            float scale = type == optionCtrl.OptionType.TITLE ? 8f / 9f : 1f;
            parent.localScale = new Vector3(_categoryScale.x, _categoryScale.y * scale, _categoryScale.z);
            Canvas.ForceUpdateCanvases();
        }
        internal void InitCategory(optionCategoryItem owner)
        {
            AssetBundleSprite background = new AssetBundleSprite { sprite_renderer_ = (SpriteRenderer)NativeFields.Get(owner, "m_Bg") };
            background.load("/menu/optionLA/", "option_list_menu_bg"); Set(owner, "m_abBg", background);
            ((Text)NativeFields.Get(owner, "m_Text")).text = _runtime.Messages.Get("a11y.title");
        }
        internal void Description(optionCtrl owner, bool category, int id)
        {
            textKeyIconCtrl icon = (textKeyIconCtrl)NativeFields.Get(owner, "key_icon_"); icon.keyIconActiveSet(false);
            IList lines = (IList)NativeFields.Get(owner, "title_back_text_");
            string text = category ? _runtime.Messages.Get("a11y.description") : ((AccessibilityOptionItem)((IList)NativeFields.Get(owner, "option_item_"))[id]).Description;
            bool inRow = !category && ((AccessibilityOptionItem)((IList)NativeFields.Get(owner, "option_item_"))[id]).DescriptionControl != null;
            for (int i = 0; i < lines.Count; i++) { ((Text)lines[i]).text = i == 0 && !inRow ? text : string.Empty; }
        }
        internal static void Set(object owner, string name, object value)
        {
            Type type = owner as Type ?? owner.GetType();
            for (Type t = type; t != null; t = t.BaseType)
            { FieldInfo field = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly); if (field != null) { field.SetValue(owner is Type ? null : owner, value); return; } }
            throw new MissingFieldException(type.FullName, name);
        }
        /// <summary>Restores original native model ownership before unloading the appended objects.</summary>
        public void Dispose()
        {
            if (_originalCats != null) { Set(typeof(optionCtrl), "useCats", _originalCats); }
            if (_owner != null)
            {
                if (_originalRanges != null) { Set(_owner, "cat2optionItemRange", _originalRanges); }
                List<optionCategoryItem> categories = (List<optionCategoryItem>)NativeFields.Get(_owner, "categoryItem"); categories.Remove(_category);
                List<optionItem> items = (List<optionItem>)NativeFields.Get(_owner, "option_item_");
                if (items.Count > FirstItem && items.Count <= FirstItem + 3) { items.RemoveRange(FirstItem, items.Count - FirstItem); }
                if (_category != null) { _category.transform.parent.localScale = _categoryScale; }
            }
            foreach (GameObject obj in _objects) { if (obj != null) { UnityEngine.Object.Destroy(obj); } }
            _objects.Clear(); _owner = null;
        }
    }
}
