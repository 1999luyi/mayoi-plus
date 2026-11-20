using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace Pwaat.Accessibility.Ui
{
    internal sealed class DyingMessageHandler : IDisposable
    {
        private static readonly FieldInfo NativeState = UiResources.Field(typeof(DyingMessageMiniGame), "SwDiemesProcState_");
        private static readonly FieldInfo Points = UiResources.Field(typeof(DyingMessageUtil), "converted_point_");
        private static readonly FieldInfo Sprites = UiResources.Field(typeof(DyingMessageMiniGame), "sprite_list_");
        private static readonly FieldInfo UtilityState = UiResources.Field(typeof(DyingMessageUtil), "stete_");
        private static readonly FieldInfo StartIndex = UiResources.Field(typeof(DyingMessageUtil), "linepoint_start_index_");
        private static readonly FieldInfo Lines = UiResources.Field(typeof(DyingMessageUtil), "line_list_");
        private static readonly FieldInfo Limits = UiResources.Field(typeof(MiniGameCursor), "cursor_limit_");
        private readonly UiRuntime _runtime;
        private readonly DyingMessageView _view = new DyingMessageView();
        private readonly InvestigationFeedback _feedback = new InvestigationFeedback();
        private readonly List<string> _pendingText = new List<string>();
        private DyingMessageMiniGame _owner;
        private DyingMessageUtil _utility;
        private GSPoint4[] _points;
        private Sprite[] _sprites;
        private DyingMessageState _editor;
        private Language _language;
        private int _inputFrame = -1;
        private int _generation, _area = 1, _column, _row, _action, _gridColumn, _gridRow;
        private bool _mode, _main, _intro, _repeat, _disposed, _submission, _accepted, _failed;
        private string _nativeIdentity, _edgeText;
        private Vector3 _lastCursor;
        internal DyingMessageHandler(UiRuntime runtime) { _runtime = runtime; }
        internal string Page { get { return CanRead() ? "dying-message:" + (_mode ? "braille" : "native") : null; } }
        internal bool IsAdapted { get { return !_failed && _owner != null && _mode && _main; } }
        internal bool OwnsUtility(DyingMessageUtil utility) { return _main && utility == _utility; }
        internal bool SuppressCursor(MiniGameCursor cursor) { return IsAdapted && _utility != null && cursor == _utility.cursor; }
        internal void Begin(DyingMessageMiniGame owner) { Close(); _owner = owner; _failed = false; }
        internal bool CanRead()
        {
            if (_disposed || _failed || !_main || _owner == null || _owner != DyingMessageMiniGame.instance || !_owner.body_active || !Application.isFocused) return false;
            keyGuideCtrl g = keyGuideCtrl.instance;
            return advCtrl.instance != null && advCtrl.instance.active && NativeFields.Visible(_owner)
                && (_runtime.Assistance == null || !_runtime.Assistance.BlocksInput)
                && (optionCtrl.instance == null || !optionCtrl.instance.is_open)
                && (SaveLoadUICtrl.instance == null || !SaveLoadUICtrl.instance.is_open)
                && (recordListCtrl.instance == null || !recordListCtrl.instance.is_open)
                && (_runtime.StoryUi == null || !_runtime.StoryUi.BoxBlocks())
                && (autoPlayCtrl.instance == null || !autoPlayCtrl.instance.isStoryMode())
                && fadeCtrl.instance != null && fadeCtrl.instance.status == fadeCtrl.Status.NO_FADE
                && g != null && g.active && g.current_guide == keyGuideBase.Type.DYING_MESSAGE && !g.CheckOpen() && !g.CheckClose();
        }
        internal void StateCommitted(DyingMessageMiniGame owner)
        {
            if (owner != _owner || _failed) return;
            int state = Convert.ToInt32(NativeState.GetValue(owner));
            if (state == 2)
            {
                if (_utility == null)
                {
                    _utility = DyingMessageUtil.instance; if (_utility == null) throw new InvalidOperationException("Dying message utility missing.");
                    _points = (GSPoint4[])Points.GetValue(_utility); if (_points == null || _points.Length == 0) throw new InvalidOperationException("Dying message points not committed.");
                    var sprites = (List<KeyValuePair<GameObject, AssetBundleSprite>>)Sprites.GetValue(owner);
                    if (sprites.Count != _points.Length) throw new InvalidOperationException("Dying message sprite/point count mismatch.");
                    _sprites = new Sprite[sprites.Count]; for (int i = 0; i < sprites.Count; i++) { _sprites[i] = sprites[i].Value.sprite_renderer_.sprite; if (_sprites[i] == null) throw new InvalidOperationException("Dying message point sprite missing."); }
                    _language = GSStatic.global_work_.language; _intro = true;
                }
                if (_language != GSStatic.global_work_.language) throw new InvalidOperationException("Dying message language changed without native resource rebuild.");
                _main = true; bool mode = _runtime.Speech.Preferences.PuzzleAdaptation;
                if (autoPlayCtrl.instance != null && autoPlayCtrl.instance.isStoryMode()) mode = false;
                if (mode != _mode) { _mode = mode; _intro = true; _nativeIdentity = null; _feedback.Clear(); Hide(); }
                if (_mode && _editor == null) _editor = new DyingMessageState(_points.Length);
                Request();
            }
            else
            {
                if (state == 3) { _submission = _mode && _main; _accepted = _submission && _editor != null && _editor.Held < 0 && DyingMessageAnswers.BaseAccept(_language, _editor.Masks()); }
                _main = false; Hide();
                if (state == 0) Close();
            }
        }
        internal bool TryResult(DyingMessageMiniGame owner, out bool result)
        { result = _accepted; return !_failed && owner == _owner && _submission; }
        internal void Hide() { _view.Hide(); _pendingText.Clear(); _feedback.Edge(0, _runtime.Speech.Clock); if (_owner != null && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner); }
        internal void Resume() { if (_main && _owner != null) StateCommitted(_owner); }
        private void Request()
        {
            if (_owner == null || _runtime.Presentation == null) return;
            DyingMessageMiniGame owner = _owner; int generation = _generation;
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (owner != _owner || generation != _generation || _failed) return;
                try { Read(); } catch (Exception error) { Fail(error); }
            });
        }
        private void Say(string text) { if (!string.IsNullOrEmpty(text)) { _pendingText.Add(text); Request(); } }
        private string T(string key) { return _runtime.Messages.Get("dying." + key); }
        private string F(string key, params object[] args) { return string.Format(T(key), args); }
        private string FocusText()
        {
            if (_area == 1) { int token = _column * 3 + _row; return F("source", token + 1) + " " + T(_editor.Used(token) ? "used" : "available"); }
            if (_area == 2) return T(_action == 0 ? "add" : "remove-cell");
            int cell = _column / 2, dot = _column % 2 * 3 + _row; return F("slot", cell + 1, dot + 1) + " " + T(_editor.At(cell, dot) >= 0 ? "filled" : "empty");
        }
        internal string Help
        {
            get
            {
                return F("navigation", Key(KeyType.Left), Key(KeyType.Right), Key(KeyType.Up), Key(KeyType.Down)) + "\n" + F("controls", Key(KeyType.A));
            }
        }
        private static string Key(KeyType key) { return ImageText.KeyName(padCtrl.instance.GetKeyCode(key)); }
        private Font Font()
        {
            if (messageBoardCtrl.instance != null) foreach (Text line in messageBoardCtrl.instance.line_list) if (line != null && line.font != null) return line.font;
            throw new InvalidOperationException("Dying message native font unavailable.");
        }
        private void Read()
        {
            if (!CanRead()) { Hide(); return; }
            List<string> text = new List<string>();
            if (_intro) { text.Add(T(_mode ? "scene-grid" : "scene-native")); text.Add(Help); _intro = false; }
            if (_mode)
            {
                string current = FocusText(); if (_editor.Held >= 0) current = NativeFields.Join(current, F("holding", _editor.Held + 1));
                if (_repeat) text.Add(Summary());
                text.Add(current); _view.Render(Font(), _runtime.Messages, _editor, _sprites, _area, _column, _row, _action, current, Help);
            }
            else
            {
                int hit = Hit(); int state = Convert.ToInt32(UtilityState.GetValue(_utility)); int count = ((IList)Lines.GetValue(_utility)).Count;
                string current = hit < 0 ? T("outside") : F("source", hit + 1);
                string identity = hit + ":" + state + ":" + count;
                if (_repeat || identity != _nativeIdentity)
                {
                    text.Add(current);
                    if (state == 2) text.Add(F("start", Convert.ToInt32(StartIndex.GetValue(_utility)) + 1));
                    if (_nativeIdentity != null || _repeat) text.Add(F("lines", count));
                    _nativeIdentity = identity;
                }
            }
            if (_edgeText != null) { text.Add(_edgeText); _edgeText = null; }
            text.InsertRange(0, _pendingText); _pendingText.Clear();
            _repeat = false; string output = NativeFields.Join(text.ToArray()); if (!string.IsNullOrEmpty(output)) _runtime.Presentation.Ui(_owner, output);
        }
        private int Hit()
        {
            Vector3 p = _utility.cursor.cursor_position;
            return MiniGameGSPoint4Hit.CheckHit(new GSRect((short)(p.x - 8), (short)(p.y - 8), 16, 16), _points);
        }
        internal void CursorCommitted(MiniGameCursor cursor)
        {
            if (_failed || !_main || _mode || _utility == null || cursor != _utility.cursor) return;
            Vector3 p = cursor.cursor_position; bool moved = p != _lastCursor; _lastCursor = p;
            // This is the native cursor commit, never an idle Update scan.
            int directions = 0;
            if (padCtrl.instance.GetKey(KeyType.Left)) directions |= 1; if (padCtrl.instance.GetKey(KeyType.Right)) directions |= 2;
            if (padCtrl.instance.GetKey(KeyType.Up)) directions |= 4; if (padCtrl.instance.GetKey(KeyType.Down)) directions |= 8;
            if (!moved && directions == 0) { _feedback.Edge(0, _runtime.Speech.Clock); return; }
            if (!CanRead()) { _feedback.Edge(0, _runtime.Speech.Clock); return; }
            Rect limit = (Rect)Limits.GetValue(cursor); int edge = 0;
            if ((directions & 1) != 0 && p.x <= limit.x) edge |= 1; if ((directions & 2) != 0 && p.x >= limit.width) edge |= 2;
            if ((directions & 4) != 0 && p.y <= limit.y) edge |= 4; if ((directions & 8) != 0 && p.y >= limit.height) edge |= 8;
            bool announce = _feedback.Edge(edge, _runtime.Speech.Clock); if (announce) { CursorFeedbackAudio.Edge(); _edgeText = _runtime.Messages.Get("investigation.edge"); }
            if (moved || announce) Request();
        }
        internal void NativeActionCommitted() { if (_main && !_mode) Request(); }
        internal bool BeforeBody()
        {
            if (!IsAdapted) return true;
            if (autoPlayCtrl.instance != null && autoPlayCtrl.instance.isStoryMode()) { _mode = false; _intro = true; Hide(); return true; }
            padCtrl p = padCtrl.instance; if (p == null) return false;
            bool left = p.GetKeyDown(KeyType.Left), right = p.GetKeyDown(KeyType.Right), up = p.GetKeyDown(KeyType.Up), down = p.GetKeyDown(KeyType.Down), confirm = p.GetKeyDown(KeyType.A), back = p.GetKeyDown(KeyType.B);
            if (!left && !right && !up && !down && !confirm && !back) return false;
            if (!CanRead()) { Hide(); return false; }
            if (_inputFrame == Time.frameCount) return false; _inputFrame = Time.frameCount;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return false;
            if (back) { bool held = _editor.Held >= 0; Say(T(_editor.Back() ? (held ? "returned" : "undone") : "nothing")); }
            else if (confirm) Activate();
            else Move((right ? 1 : 0) - (left ? 1 : 0), (down ? 1 : 0) - (up ? 1 : 0));
            Request(); return false;
        }
        private void Move(int dx, int dy)
        {
            int area = _area, col = _column, row = _row, action = _action;
            if (_area == 2)
            { if (dy < 0) { _area = 0; _column = Math.Min(_column, _editor.CellCount * 2 - 1); _row = 2; } else if (dx != 0) _action = Math.Max(0, Math.Min(1, _action + dx)); }
            else if (_area == 0)
            {
                if (dy > 0 && _row == 2) { _area = 2; _action = 0; }
                else { _row = Math.Max(0, Math.Min(2, _row + dy)); _column += dx; if (_column < 0) { _column = 0; } else if (_column >= _editor.CellCount * 2) { _area = 1; _column = 0; } }
            }
            else
            {
                _row = Math.Max(0, Math.Min(2, _row + dy)); _column += dx;
                if (_column < 0) { _area = 0; _column = _editor.CellCount * 2 - 1; }
                else if (_column > (_editor.SourceCount - 1) / 3) { _column = (_editor.SourceCount - 1) / 3; }
                if (_area == 1 && _column * 3 + _row >= _editor.SourceCount) _row = (_editor.SourceCount - 1) % 3;
            }
            if (area == _area && col == _column && row == _row && action == _action) { CursorFeedbackAudio.Edge(); Say(_runtime.Messages.Get("investigation.edge")); return; }
            if (_area == 0) { _gridColumn = _column; _gridRow = _row; }
            if (soundCtrl.instance != null) soundCtrl.instance.PlaySE(42);
        }
        private void Activate()
        {
            if (_area == 1)
            {
                int token = _column * 3 + _row;
                if (_editor.Pick(token)) { Say(F("picked", token + 1)); _area = 0; _column = Math.Min(_gridColumn, _editor.CellCount * 2 - 1); _row = _gridRow; } else Say(T("unavailable"));
            }
            else if (_area == 0)
            {
                int cell = _column / 2, dot = _column % 2 * 3 + _row;
                if (_editor.Held >= 0) { Say(T(_editor.Place(cell, dot) ? "placed" : "unavailable")); }
                else Say(T(_editor.Remove(cell, dot) ? "removed" : "nothing"));
            }
            else
            {
                bool changed = _action == 0 ? _editor.AddCell() : _editor.RemoveLastCell();
                Say(T(changed ? (_action == 0 ? "added" : "cell-removed") : "unavailable"));
                if (changed) { _area = 0; _column = (_editor.CellCount - 1) * 2; _row = 0; }
            }
            if (soundCtrl.instance != null) soundCtrl.instance.PlaySE(52);
        }
        private string Summary()
        {
            int[] masks = _editor.Masks(); List<string> result = new List<string>();
            for (int c = 0; c < masks.Length; c++) { List<string> dots = new List<string>(); for (int d = 0; d < 6; d++) if ((masks[c] & (1 << d)) != 0) dots.Add((d + 1).ToString()); result.Add(F("cell", c + 1, dots.Count == 0 ? T("empty") : string.Join(" ", dots.ToArray()))); }
            return NativeFields.Join(result.ToArray());
        }
        internal void Repeat() { if (CanRead()) { _repeat = true; Request(); } }
        internal void Fail(Exception error) { if (_failed) return; _failed = true; Hide(); _runtime.ReadFailure("dying-message", error); }
        internal void Close()
        {
            if (_owner != null && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner);
            _inputFrame = -1; _generation++; _owner = null; _utility = null; _points = null; _sprites = null; _editor = null; _main = _mode = _intro = _repeat = _submission = _accepted = false; _nativeIdentity = null; _edgeText = null; _area = 1; _column = _row = _action = _gridColumn = _gridRow = 0; _feedback.Clear(); _pendingText.Clear(); _view.Hide();
        }
        public void Dispose() { Close(); _disposed = true; _view.Dispose(); }
    }
}
