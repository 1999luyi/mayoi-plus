using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class AccessibilityHelpHandler : IDisposable
    {
        private readonly UiRuntime _runtime;
        private readonly Dictionary<keyGuideBase, string> _knownGuides = new Dictionary<keyGuideBase, string>();
        internal readonly GuideAnnouncementPolicy Guides = new GuideAnnouncementPolicy();
        private readonly List<string> _rows = new List<string>();
        private GameObject _canvas;
        private Text _body;
        private Text _heading;
        private int _index;
        private bool _wasGameActive;
        private bool _wasAnimationPaused;
        private bool _isOpen;
        private bool _closing;
        private readonly HashSet<KeyCode> _release = new HashSet<KeyCode>();
        internal bool BypassInput;
        internal int Scene = 2;
        internal bool IsOpen { get { return _isOpen; } }
        internal bool BlocksInput { get { return _isOpen || _closing; } }
        internal AccessibilityHelpHandler(UiRuntime runtime) { _runtime = runtime; }
        internal string CurrentPage()
        {
            if (_isOpen) { return "help"; }
            optionCtrl option = optionCtrl.instance;
            if (option != null && option.is_open) { return "options:" + NativeFields.Number(option, "current_cat") + ":" + NativeFields.Number(option, "menuState_"); }
            if (_runtime.DyingMessage != null && _runtime.DyingMessage.CanRead()) { return _runtime.DyingMessage.Page; }
            if (_runtime.VaseShow != null && _runtime.VaseShow.CanRead()) { return _runtime.VaseShow.Page; }
            if (_runtime.SafePassword != null && _runtime.SafePassword.CanRead()) { return _runtime.SafePassword.Page; }
            if (_runtime.VideoTape != null && _runtime.VideoTape.CanRead()) { return _runtime.VideoTape.Page; }
            if (_runtime.Fingerprints != null && _runtime.Fingerprints.CanRead()) { return _runtime.Fingerprints.Page; }
            if (_runtime.VasePuzzle != null && _runtime.VasePuzzle.CanRead()) { return _runtime.VasePuzzle.Page; }
            if (_runtime.Luminol != null && _runtime.Luminol.CanRead()) { return _runtime.Luminol.Page; }
            if (_runtime.ScientificInvestigation != null && _runtime.ScientificInvestigation.CanInspect()) { return _runtime.ScientificInvestigation.Page; }
            if (recordListCtrl.instance != null && recordListCtrl.instance.is_open)
            { return "record:" + NativeFields.Number(recordListCtrl.instance, "record_type_") + ":" + recordListCtrl.instance.detail_open; }
            if (selectPlateCtrl.instance != null && selectPlateCtrl.instance.body_active) { return "selection:" + selectPlateCtrl.instance.is_select; }
            string investigation = _runtime.Investigation == null ? null : _runtime.Investigation.Page();
            if (investigation != null) { return investigation; }
            if (_runtime.PicturePointing != null && _runtime.PicturePointing.IsReady) { return _runtime.PicturePointing.Page; }
            if (advCtrl.instance != null && advCtrl.instance.active && GSStatic.global_work_ != null)
            { return "story:" + GSStatic.global_work_.r.no_0; }
            return "menu:" + Scene;
        }
        internal string GuideText(keyGuideBase guide)
        {
            if (guide == null || !NativeFields.Visible(guide)) { return null; }
            System.Reflection.FieldInfo bodyField = HarmonyLib.AccessTools.Field(guide.GetType(), "body_");
            GameObject body = bodyField == null ? null : bodyField.GetValue(guide) as GameObject;
            if (body != null && (!body.activeInHierarchy || body.transform.localScale.y <= 0f)) { return null; }
            guideCtrl normal = guide as guideCtrl;
            if (normal != null && (!normal.getEnables() || normal.current_guide == guideCtrl.GuideType.NO_GUIDE || normal.is_open_close_guid())) { return null; }
            keyGuideBoard board = guide as keyGuideBoard;
            if (board != null && (!board.active || board.current_guide == keyGuideBase.Type.NO_GUIDE || board.CheckOpen() || board.CheckClose())) { return null; }
            if (optionCtrl.instance != null && optionCtrl.instance.is_open && guide != optionCtrl.instance.key_guide) { return null; }
            if (recordListCtrl.instance != null && recordListCtrl.instance.is_open && guide != recordListCtrl.instance.record_guide && guide != keyGuideCtrl.instance) { return null; }
            if (guide is recordGuideCtrl && (recordListCtrl.instance == null || !recordListCtrl.instance.is_open || !recordListCtrl.instance.body_active)) { return null; }
            return _runtime.Batch.ReadGuideText(guide);
        }
        internal string AutomaticHint(string owner, string text)
        { return Guides.Announce(CurrentPage(), owner, text, _runtime.Speech.Preferences.Guides) ? text : null; }
        internal string AutomaticGuide(keyGuideBase guide, string text)
        {
            if (guide == null) { return null; }
            _knownGuides[guide] = text;
            string page = CurrentPage();
            string owner = guide.GetType().FullName;
            guideCtrl normal = guide as guideCtrl; if (normal != null) { owner += ":" + normal.current_guide; }
            keyGuideBoard board = guide as keyGuideBoard; if (board != null) { owner += ":" + board.current_guide; }
            return Guides.Announce(page, owner, text, _runtime.Speech.Preferences.Guides) ? text : null;
        }
        internal void EnterPage() { if (!_isOpen) { Guides.Enter(CurrentPage()); } }
        internal void LeavePage() { Guides.Leave(); }
        internal void GuideHidden(keyGuideBase guide) { _knownGuides.Remove(guide); }
        private List<string> Capture()
        {
            List<string> native = new List<string>();
            foreach (keyGuideBase guide in new List<keyGuideBase>(_knownGuides.Keys))
            {
                if (guide == null) { _knownGuides.Remove(guide); continue; }
                string text = GuideText(guide); if (!string.IsNullOrEmpty(text)) { foreach (string line in text.Split('\n')) { if (!native.Contains(line)) { native.Add(line); } } }
            }
            if (_runtime.RecordImages != null) { string detailHint = _runtime.RecordImages.DetailHint(recordListCtrl.instance); if (!string.IsNullOrEmpty(detailHint) && !native.Contains(detailHint)) { native.Add(detailHint); } }
            if (_runtime.VideoTape != null && _runtime.VideoTape.CanRead()) { string videoGuide = _runtime.VideoTape.NativeGuide; if (!string.IsNullOrEmpty(videoGuide)) foreach (string line in videoGuide.Split('\n')) if (!native.Contains(line)) native.Add(line); }
            AddDirections(native);
            List<string> rows = new List<string>();
            rows.Add(_runtime.Messages.Get("help.native"));
            if (native.Count == 0) { rows.Add(_runtime.Messages.Get("help.no-native")); } else { rows.AddRange(native); }
            rows.Add(_runtime.Messages.Get("help.mod")); rows.Add(_runtime.Messages.Get("help.f1"));
            if (_runtime.DyingMessage != null && _runtime.DyingMessage.CanRead()) { rows.Add(_runtime.Messages.Get("help.f2")); rows.Add(_runtime.DyingMessage.Help); }
            else if (_runtime.VaseShow != null && _runtime.VaseShow.CanRead()) { rows.Add(_runtime.Messages.Get("vase-show.help-repeat")); rows.Add(_runtime.Messages.Get("vase-show.help-f3")); rows.Add(_runtime.Messages.Get("vase-show.help")); }
            else if (_runtime.SafePassword != null && _runtime.SafePassword.CanRead()) { rows.Add(_runtime.Messages.Get("safe-password.help")); }
            else if (_runtime.VideoTape != null && _runtime.VideoTape.CanRead()) { rows.Add(_runtime.VideoTape.Help); }
            else if (_runtime.Fingerprints != null && _runtime.Fingerprints.CanRead()) { rows.Add(_runtime.Fingerprints.Help); }
            else if (_runtime.VasePuzzle != null && _runtime.VasePuzzle.CanRead()) { rows.Add(_runtime.Messages.Get("vase-puzzle.help-repeat")); rows.Add(_runtime.Messages.Get("vase-puzzle.help")); }
            else if (_runtime.Luminol != null && _runtime.Luminol.CanRead()) { rows.Add(_runtime.Messages.Get("luminol.help-repeat")); }
            else if (_runtime.ScientificInvestigation != null && _runtime.ScientificInvestigation.CanInspect()) { rows.Add(_runtime.Messages.Get("science.help-repeat")); }
            else if (_runtime.PicturePointing != null && _runtime.PicturePointing.IsReady) { rows.Add(_runtime.Messages.Get("picture-pointing.help-repeat")); }
            else if (_runtime.Story != null && _runtime.Story.CanRepeat()) { rows.Add(_runtime.Messages.Get("help.f2")); }
            if (_runtime.Presentation != null && _runtime.Presentation.CanAcceptHealthQuery()) { rows.Add(_runtime.Messages.Get(_runtime.PsycheLocks != null && _runtime.PsycheLocks.RemainingText() != null ? "psyche-lock.help-f4" : "help.f4")); }
            if (_runtime.Investigation != null && _runtime.Investigation.CanInspect()) { rows.Add(_runtime.Messages.Get("investigation.help-prev")); rows.Add(_runtime.Messages.Get("investigation.help-next")); }
            if (_runtime.Luminol != null && _runtime.Luminol.CanInspect()) { rows.Add(_runtime.Messages.Get("luminol.help")); }
            if (_runtime.ScientificInvestigation != null && _runtime.ScientificInvestigation.CanInspect()) { rows.Add(_runtime.Messages.Get("science.help")); }
            if (_runtime.PicturePointing != null && _runtime.PicturePointing.IsReady) { rows.Add(_runtime.Messages.Get("picture-pointing.help")); }
            if (_runtime.Settings.Debug && _runtime.Investigation != null && _runtime.Investigation.CanInspect()) { rows.Add(_runtime.Messages.Get("investigation.help-debug")); rows.Add(_runtime.Messages.Get("investigation.help-reload")); }
            rows.Add(_runtime.Messages.Get("help.history-prev")); rows.Add(_runtime.Messages.Get("help.history-next"));
            rows.Add(string.Format(_runtime.Messages.Get("help.return"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.B))));
            return rows;
        }
        private void AddDirections(List<string> rows)
        {
            if (_runtime.DyingMessage != null && _runtime.DyingMessage.CanRead()) return;
            if (_runtime.VaseShow != null && _runtime.VaseShow.CanRead()) return;
            if (_runtime.SafePassword != null && _runtime.SafePassword.CanRead())
            {
                rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Left)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Right))));
                rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Up)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Down))));
                rows.Add(string.Format(_runtime.Messages.Get("help.confirm"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.A)))); return;
            }
            if (_runtime.VideoTape != null && _runtime.VideoTape.CanRead())
            {
                if (_runtime.VideoTape.HasDirectionalCursor)
                { rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Left)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Right)))); rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Up)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Down)))); }
                return;
            }
            if (_runtime.Fingerprints != null && _runtime.Fingerprints.CanRead())
            {
                rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Left)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Right))));
                if (_runtime.Fingerprints.HasDirectionalCursor) rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Up)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Down))));
                return;
            }
            if (_runtime.VasePuzzle != null && _runtime.VasePuzzle.CanRead()) { rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Left)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Right)))); return; }
            if (_runtime.ScientificInvestigation != null && _runtime.ScientificInvestigation.CanInspect()) { return; }
            titleSelectPlate title = _runtime.Title == null ? null : _runtime.Title.VisiblePlate();
            if (title != null && (optionCtrl.instance == null || !optionCtrl.instance.is_open))
            {
                if ((bool)NativeFields.Get(title, "is_horizontal_")) { rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Left)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Right)))); }
                if ((bool)NativeFields.Get(title, "is_vertical_") || title.is_title_with_game_end) { rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Up)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Down)))); }
                rows.Add(string.Format(_runtime.Messages.Get("help.confirm"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.A))));
            }
            if (_runtime.Investigation != null && _runtime.Investigation.CanMenu())
            { rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Left)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Right)))); }
            bool list = _runtime.Investigation != null && _runtime.Investigation.CanMove() || optionCtrl.instance != null && optionCtrl.instance.is_open || selectPlateCtrl.instance != null && selectPlateCtrl.instance.body_active;
            if (list)
            {
                rows.Add(string.Format(_runtime.Messages.Get("help.browse"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Up)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Down))));
                rows.Add(string.Format(_runtime.Messages.Get("help.confirm"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.A))));
                if (optionCtrl.instance != null && optionCtrl.instance.is_open && NativeFields.Number(optionCtrl.instance, "menuState_") == 2)
                { rows.Add(string.Format(_runtime.Messages.Get("help.adjust"), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Left)), ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.Right)))); }
            }
        }
        private Font Font()
        {
            if (optionCtrl.instance != null)
            {
                IList categories = (IList)NativeFields.Get(optionCtrl.instance, "categoryItem");
                foreach (object category in categories) { Text text = NativeFields.Get(category, "m_Text") as Text; if (text != null && text.font != null) { return text.font; } }
            }
            if (messageBoardCtrl.instance != null && messageBoardCtrl.instance.line_list.Count > 0) { return messageBoardCtrl.instance.line_list[0].font; }
            throw new InvalidOperationException("No current native font available for help.");
        }
        internal void Open()
        {
            if (_isOpen || _closing || padCtrl.instance == null) { return; }
            if (StoryInputHandler.IsBound(KeyCode.F1)) { _runtime.Speech.Say(string.Format(_runtime.Messages.Get("story.key-conflict"), "F1"), false); return; }
            List<string> rows = Capture();
            if (_runtime.DyingMessage != null) _runtime.DyingMessage.Hide();
            Build(Font()); _rows.Clear(); _rows.AddRange(rows); _index = 0;
            if (_runtime.VideoTape != null) _runtime.VideoTape.PauseForHelp();
            _wasGameActive = GSMain.is_active_; _wasAnimationPaused = AnimationSystem.Instance != null && AnimationSystem.Instance.pause;
            GSMain.is_active_ = false; if (AnimationSystem.Instance != null) { AnimationSystem.Instance.pause = true; }
            _release.Clear(); AddHeldKeys(); _isOpen = true; _canvas.SetActive(true); Refresh(true);
            if (soundCtrl.instance != null) { soundCtrl.instance.PlaySE(49); }
        }
        private void Build(Font font)
        {
            if (_canvas != null) { _body.font = font; _heading.font = font; return; }
            _canvas = new GameObject("PwaatAccessibilityHelp", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = _canvas.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f);
            GameObject panel = new GameObject("HelpPanel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(_canvas.transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.06f, .06f); rect.anchorMax = new Vector2(.94f, .94f); rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(.025f, .035f, .07f, .99f);
            _heading = MakeText(panel.transform, "Heading", font, new Vector2(.04f, .84f), new Vector2(.96f, .96f), 44);
            _body = MakeText(panel.transform, "Rows", font, new Vector2(.04f, .05f), new Vector2(.96f, .82f), 34);
            _canvas.SetActive(false);
        }
        private static Text MakeText(Transform parent, string name, Font font, Vector2 min, Vector2 max, int size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Text text = obj.GetComponent<Text>(); text.font = font; text.fontSize = size; text.color = Color.white; text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false; return text;
        }
        private void Refresh(bool initial)
        {
            _heading.text = _runtime.Messages.Get("help.title");
            int start = Math.Max(0, _index - 3), end = Math.Min(_rows.Count, start + 9); List<string> visible = new List<string>();
            for (int i = start; i < end; i++) { visible.Add((i == _index ? "▶ " : "   ") + _rows[i]); }
            _body.text = string.Join("\n\n", visible.ToArray());
            _runtime.Speech.OverlaySay(NativeFields.Join(initial ? _runtime.Messages.Get("help.title") : null, _rows[_index]));
        }
        internal void InputFrame()
        {
            if (!_isOpen && !_closing) { return; }
            if (_closing)
            {
                foreach (KeyCode key in new List<KeyCode>(_release)) { if (!UnityEngine.Input.GetKey(key) && !UnityEngine.Input.GetKeyUp(key)) { _release.Remove(key); } }
                bool backHeld = false;
                BypassInput = true; try { if (padCtrl.instance != null) { backHeld = padCtrl.instance.GetKey(KeyType.B) || padCtrl.instance.GetKeyDown(KeyType.B) || padCtrl.instance.GetKeyUp(KeyType.B); } } finally { BypassInput = false; }
                if (_release.Count == 0 && !backHeld) { _closing = false; if (_runtime.DyingMessage != null) _runtime.DyingMessage.Resume(); if (_runtime.VideoTape != null) _runtime.VideoTape.HelpReturned(); }
                return;
            }
            if (!Application.isFocused) { return; }
            bool up = UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow);
            bool down = UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) || UnityEngine.Input.GetKeyDown(KeyCode.RightArrow);
            bool back;
            BypassInput = true; try { back = padCtrl.instance.GetKeyDown(KeyType.B); } finally { BypassInput = false; }
            if (back)
            {
                Close();
                if (soundCtrl.instance != null) { soundCtrl.instance.PlaySE(44); }
                return;
            }
            if (up || down)
            {
                int previous = _index;
                _index = Math.Max(0, Math.Min(_rows.Count - 1, _index + (up ? -1 : 1)));
                if (_index != previous && soundCtrl.instance != null) { soundCtrl.instance.PlaySE(42); }
                Refresh(false);
            }
        }
        private void AddHeldKeys()
        {
            foreach (KeyCode key in new[] { KeyCode.F1, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.Mouse0, KeyCode.Mouse1 }) { if (UnityEngine.Input.GetKey(key) || UnityEngine.Input.GetKeyDown(key) || UnityEngine.Input.GetKeyUp(key)) { _release.Add(key); } }
            if (padCtrl.instance != null) { long[][] bindings = padCtrl.instance.key_type_; foreach (long[] slots in bindings) { if (slots == null) continue; foreach (long key in slots) { if (key > 0 && key < 65536 && UnityEngine.Input.GetKey((KeyCode)key)) { _release.Add((KeyCode)key); } } } }
        }
        internal void Close()
        {
            if (!_isOpen) return;
            AddHeldKeys(); _isOpen = false; _closing = true; if (_canvas != null) _canvas.SetActive(false);
            if (_runtime.VideoTape != null) _runtime.VideoTape.RestoreAfterHelp();
            GSMain.is_active_ = _wasGameActive; if (AnimationSystem.Instance != null) AnimationSystem.Instance.pause = _wasAnimationPaused;
            _runtime.Speech.CancelOverlayPending();
        }
        /// <summary>Restores the owned pause state and destroys the mod help view.</summary>
        public void Dispose() { if (_isOpen) Close(); _closing = false; _knownGuides.Clear(); if (_canvas != null) UnityEngine.Object.Destroy(_canvas); }
    }
}
