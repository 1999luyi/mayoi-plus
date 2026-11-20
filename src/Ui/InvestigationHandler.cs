using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class InvestigationHandler : IDisposable
    {
        private sealed class Focus { internal string Identity; internal string Text; }
        private sealed class Point
        {
            internal INSPECT_DATA Data;
            internal int Index;
            internal float X, Y;
            internal string Name;
            internal bool Named;
        }
        private readonly UiRuntime _runtime;
        private readonly CursorFeedbackAudio _audio = new CursorFeedbackAudio();
        private readonly TopicHistory _history = new TopicHistory();
        private readonly Dictionary<int, bool> _readMarks = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _lockMarks = new Dictionary<int, bool>();
        private readonly Dictionary<int, TopicSnapshot> _topics = new Dictionary<int, TopicSnapshot>();
        private readonly Dictionary<Component, Focus> _focus = new Dictionary<Component, Focus>();
        private readonly List<Point> _points = new List<Point>();
        private readonly InvestigationFeedback _feedback = new InvestigationFeedback();
        private readonly Func<SubWindow_Inspect.RECT, SubWindow_Inspect.POINT4, bool> _hitRegion;
        private readonly Func<short, byte>[] _hitMessages;
        private tanteiMenu _menu;
        private moveCtrl _move;
        private selectPlateCtrl _talk;
        private inspectCtrl _inspect;
        private inspectCtrl _nativeCursor;
        private int _nativeState;
        private uint _nativeMessage;
        private INSPECT_DATA _nativeRegion;
        private bool _newTopics, _moveFirst, _dirty, _navigating;
        private bool _inspectionInstalled;
        private int _queryCount;
        private string _labelContext, _labelCulture;
        private int _side = -1, _background = -1;
        private INSPECT_DATA[] _data;
        private float _offset, _minX, _maxX, _minY, _maxY, _guideX;
        private int _moveEntryIndex = -1, _newTopicIndex = -1;
        private bool _readingCursor;
        internal InvestigationHandler(UiRuntime runtime)
        {
            _runtime = runtime;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            _hitRegion = (Func<SubWindow_Inspect.RECT, SubWindow_Inspect.POINT4, bool>)Delegate.CreateDelegate(typeof(Func<SubWindow_Inspect.RECT, SubWindow_Inspect.POINT4, bool>), typeof(SubWindow_Inspect).GetMethod("ObjHitCheck2", flags));
            _hitMessages = new Func<short, byte>[3];
            for (int i = 0; i < 3; i++) _hitMessages[i] = (Func<short, byte>)Delegate.CreateDelegate(typeof(Func<short, byte>), typeof(inspectCtrl).GetMethod("GS" + (i + 1) + "_sw_inspect_main_CheckHitMessage", flags));
        }
        internal void InspectionInstalled() { _inspectionInstalled = true; }
        private bool Foreground()
        {
            return Application.isFocused && _runtime.Presentation != null && _runtime.Presentation.Foreground()
                && (recordListCtrl.instance == null || !recordListCtrl.instance.is_open)
                && GSStatic.global_work_ != null && GSStatic.global_work_.r.no_0 != 8 && GSStatic.global_work_.r.no_0 != 17
                && fadeCtrl.instance != null && fadeCtrl.instance.status == fadeCtrl.Status.NO_FADE;
        }
        internal bool CanMenu()
        { return _menu != null && _menu.is_play && _menu.active && !_menu.select_animation_playing && Foreground(); }
        internal bool CanMove()
        { return _move != null && _move.is_play && _move.active && !_move.select_animation_playing && Foreground(); }
        internal bool CanInspect()
        {
            if (!_inspectionInstalled || _inspect == null || !_inspect.is_play || !_inspect.active || _inspect.select_animation_playing || !Foreground() || bgCtrl.instance == null || bgCtrl.instance.is_slider) return false;
            Routine routine = advCtrl.instance.sub_window_.GetCurrentRoutine();
            return routine.r.no_0 == 6 && routine.r.no_1 == 2 && !advCtrl.instance.sub_window_.IsBusy();
        }
        private void Speak(Component owner, string identity, string text)
        {
            Focus old;
            if (_focus.TryGetValue(owner, out old) && old.Identity == identity && old.Text == text) return;
            _focus[owner] = new Focus { Identity = identity, Text = text };
            _runtime.Trace("investigation_focus", "owner=" + owner.GetType().Name + " identity=" + identity + " characters=" + text.Length);
            _runtime.Presentation.Ui(owner, text);
        }
        private void Clear(Component owner)
        {
            if (ReferenceEquals(owner, null)) return;
            _focus.Remove(owner); _runtime.Presentation.ClearUi(owner);
        }
        internal void MenuReady(tanteiMenu owner) { _menu = owner; Clear(owner); MenuChanged(owner); }
        internal void MenuChanged(tanteiMenu owner)
        {
            if (_menu != owner) return;
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (_menu != owner || !CanMenu()) return;
                IList rows = (IList)NativeFields.Get(owner, "select_list_");
                int index = owner.cursor_no;
                if (index < 0 || index >= NativeFields.Number(owner, "cursor_num_") || index >= rows.Count) return;
                tanteiMenu.SelectPlate row = (tanteiMenu.SelectPlate)rows[index];
                if (row.active) Speak(owner, index.ToString(), NativeFields.TextValue(row.text_));
            });
        }
        internal void MenuClosed(tanteiMenu owner) { Clear(owner); if (_menu == owner) _menu = null; }
        internal void MoveReady(moveCtrl owner) { _move = owner; _moveFirst = true; _moveEntryIndex = -1; Clear(owner); MoveChanged(owner); }
        internal void MoveChanged(moveCtrl owner)
        {
            if (_move != owner) return;
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (_move != owner || !CanMove()) return;
                int index = NativeFields.Number(owner, "cursor_no_");
                IList rows = (IList)NativeFields.Get(owner, "select_list_");
                if (index < 0 || index >= NativeFields.Number(owner, "cursor_num_") || index >= rows.Count) return;
                string text = NativeFields.Text(rows[index], "text_");
                if (_moveEntryIndex == -1) _moveEntryIndex = index;
                if (_moveFirst && _moveEntryIndex != index) _moveFirst = false;
                if (_moveFirst) text = NativeFields.Join(_runtime.Messages.Get("investigation.move"), text);
                Speak(owner, index.ToString(), text);
            });
        }
        internal void MoveClosed(moveCtrl owner) { Clear(owner); if (_move == owner) _move = null; }
        internal void ReadMark(selectPlateCtrl owner, int index)
        {
            IList rows = (IList)NativeFields.Get(owner, "select_list_");
            if (index < 0 || index >= rows.Count) return;
            selectPlateCtrl.SelectPlate row = (selectPlateCtrl.SelectPlate)rows[index];
            // Read the game's final sprite after its scenario-specific corrections.
            _readMarks[index] = row.select_.sprite_renderer_.sprite == row.select_.sprite_data_[3];
            _lockMarks[index] = row.psylook_.active;
        }
        internal void TopicsReady(selectPlateCtrl owner, selectPlateCtrl.FromEntryRequest request)
        {
            _topics.Clear(); _newTopics = false; _newTopicIndex = -1; _talk = null;
            if (request != selectPlateCtrl.FromEntryRequest.TALK) { _readMarks.Clear(); _lockMarks.Clear(); return; }
            TALK_DATA active = null;
            foreach (TALK_DATA row in GSStatic.talk_work_.talk_data_)
            {
                if (row.room == 255) break;
                if (row.room == GSStatic.global_work_.Room && row.pl_id == AnimationSystem.Instance.IdlingCharacterMasked && row.sw == 1) { active = row; break; }
            }
            if (active == null) { _readMarks.Clear(); _lockMarks.Clear(); return; }
            List<TopicSnapshot> snapshot = new List<TopicSnapshot>();
            int count = NativeFields.Number(owner, "cursor_num_");
            for (int i = 0; i < count && i < active.tag.Length; i++)
            {
                bool read;
                if (active.tag[i] == 255 || !_readMarks.TryGetValue(i, out read)) continue;
                bool locked; _lockMarks.TryGetValue(i, out locked);
                TopicSnapshot topic = new TopicSnapshot { Identity = active.tag[i].ToString(), Read = read, Locked = locked };
                _topics.Add(i, topic); snapshot.Add(topic);
            }
            string context = GSStatic.global_work_.title + ":" + GSStatic.global_work_.scenario + ":" + GSStatic.global_work_.Room + ":" + active.pl_id;
            _newTopics = _history.Commit(context, snapshot); _talk = owner; _readMarks.Clear(); _lockMarks.Clear();
            _runtime.Trace("investigation_topics", "context=" + context + " added=" + _newTopics + " rows=" + snapshot.Count);
        }
        internal string TopicText(selectPlateCtrl owner, int index, string text)
        {
            if (_talk != owner || !owner.is_talk) return text;
            TopicSnapshot topic;
            if (_topics.TryGetValue(index, out topic))
            {
                if (topic.Locked) text = string.Format(_runtime.Messages.Get("psyche-lock.topic"), text);
                else if (topic.Read) text = string.Format(_runtime.Messages.Get("investigation.talked"), text);
                else if (topic.Updated) text = string.Format(_runtime.Messages.Get("investigation.updated"), text);
            }
            if (_newTopics)
            {
                if (_newTopicIndex == -1) _newTopicIndex = index;
                if (_newTopicIndex != index) _newTopics = false;
                else text = NativeFields.Join(_runtime.Messages.Get("investigation.new-topic"), text);
            }
            return text;
        }
        internal void NativeRegion(INSPECT_DATA data) { if (_readingCursor) _nativeRegion = data; }
        internal void NativeHit(uint message) { if (_readingCursor) _nativeMessage = message; }
        internal void CursorStarting(inspectCtrl owner) { _nativeCursor = owner; _nativeRegion = null; _readingCursor = true; }
        internal void CursorCommitted(inspectCtrl owner)
        {
            _readingCursor = false;
            AssetBundleSprite cursor = (AssetBundleSprite)NativeFields.Get(owner, "cursor_");
            _nativeState = cursor.sprite_renderer_.sprite == cursor.sprite_data_[3] ? 3 : cursor.sprite_renderer_.sprite == cursor.sprite_data_[0] ? 0 : 1;
            if (_inspect != owner || _navigating) return;
            if (_side >= 0 && CanInspect()) _audio.BlockMovement(EdgeDirections());
            RequestInspect();
        }
        internal void InspectReady(inspectCtrl owner)
        {
            _audio.Reset(); _inspect = owner; _side = -1; _background = -1; _data = null; _dirty = true; _feedback.Clear(); Clear(owner); RequestInspect();
        }
        internal void InspectClosed(inspectCtrl owner)
        {
            Clear(owner);
            if (_inspect == owner) { _audio.Reset(); _inspect = null; _points.Clear(); _data = null; _side = -1; _feedback.Clear(); }
        }
        internal void SideReady() { if (_inspect != null && _inspect.is_play) { _dirty = true; RequestInspect(); } }
        internal void StateChanged() { _dirty = true; }
        internal void VisualChanged() { _dirty = true; if (_inspect != null) RequestInspect(); }
        internal void InputPressed() { _feedback.Edge(0, _runtime.Speech.Clock); }
        private void RequestInspect()
        {
            inspectCtrl owner = _inspect;
            if (owner == null) return;
            _runtime.Presentation.ReadUi(owner, delegate { if (_inspect == owner) ReadInspect(); });
        }
        private void Bounds()
        {
            AssetBundleSprite cursor = (AssetBundleSprite)NativeFields.Get(_inspect, "cursor_");
            Vector2 size = cursor.sprite_renderer_.size;
            _minX = -systemCtrl.instance.ScreenWidth * 0.5f + size.x * 0.5f;
            _maxX = -_minX; _minY = -systemCtrl.instance.ScreenHeight * 0.5f + size.y * 0.5f; _maxY = -_minY;
            _guideX = systemCtrl.instance.ScreenWidth - keyGuideCtrl.instance.GetGuideWidth() - keyGuideCtrl.instance.guide_space - 960f;
        }
        private bool Within(float x, float y)
        { return x >= _minX && x <= _maxX && y >= _minY && y <= _maxY && !(x > _guideX && y < -400f); }
        private bool IsHitMessage(uint message)
        {
            int title = (int)GSStatic.global_work_.title;
            return title >= 0 && title < _hitMessages.Length && _hitMessages[title]((short)message) == 1;
        }
        private static SubWindow_Inspect.POINT4 Polygon(INSPECT_DATA d)
        { return new SubWindow_Inspect.POINT4((int)d.x0, (int)d.y0, (int)d.x1, (int)d.y1, (int)d.x2, (int)d.y2, (int)d.x3, (int)d.y3); }
        private bool RegionContains(INSPECT_DATA d, float x, float y)
        {
            int px = (int)(x + _offset + 960f), py = (int)(540f - y);
            bool small = GSStatic.global_work_.title == TitleId.GS1 && GSStatic.global_work_.scenario >= 17;
            return _hitRegion(small ? new SubWindow_Inspect.RECT(px - 3, py - 3, 6, 6) : new SubWindow_Inspect.RECT(px, py, 4, 16), Polygon(d));
        }
        private bool TryTarget(INSPECT_DATA data, out float x, out float y)
        {
            float left = Mathf.Max(_minX, Mathf.Min(Mathf.Min(data.x0, data.x1), Mathf.Min(data.x2, data.x3)) - _offset - 960f);
            float right = Mathf.Min(_maxX, Mathf.Max(Mathf.Max(data.x0, data.x1), Mathf.Max(data.x2, data.x3)) - _offset - 960f);
            float bottom = Mathf.Max(_minY, 540f - Mathf.Max(Mathf.Max(data.y0, data.y1), Mathf.Max(data.y2, data.y3)));
            float top = Mathf.Min(_maxY, 540f - Mathf.Min(Mathf.Min(data.y0, data.y1), Mathf.Min(data.y2, data.y3)));
            x = y = 0f;
            if (left > right || bottom > top) return false;
            // Bounded sample points locate a native-accepted interior, including clipped/overlapping polygons.
            for (int attempt = 0; attempt < 26; attempt++)
            {
                if (attempt == 0)
                {
                    x = Mathf.Clamp(((float)data.x0 + data.x1 + data.x2 + data.x3) * 0.25f - _offset - 960f, left, right);
                    y = Mathf.Clamp(540f - ((float)data.y0 + data.y1 + data.y2 + data.y3) * 0.25f, bottom, top);
                }
                else { int cell = attempt - 1; x = Mathf.Lerp(left, right, ((cell % 5) + 0.5f) / 5f); y = Mathf.Lerp(bottom, top, ((cell / 5) + 0.5f) / 5f); }
                if (!Within(x, y) || !RegionContains(data, x, y)) continue;
                _queryCount++; INSPECT_DATA actual; uint native = InvestigationHooks.QueryAt(x, y, out actual);
                if (ReferenceEquals(actual, data) && native == data.message && IsHitMessage(native)) return true;
            }
            return false;
        }
        private void BuildPoints()
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp(); _queryCount = 0;
            _labelCulture = UiResources.Culture();
            _labelContext = _runtime.InvestigationNames == null ? null : _runtime.InvestigationNames.Context();
            Bounds(); _points.Clear(); _data = GSStatic.inspect_data_; _offset = bgCtrl.instance.bg_pos_x; _background = bgCtrl.instance.bg_no;
            if (_data == null) return;
            for (int i = 0; i < _data.Length; i++)
            {
                INSPECT_DATA data = _data[i];
                if (data.place == uint.MaxValue) break;
                if (data.place == 254) continue;
                float x, y;
                if (!TryTarget(data, out x, out y)) continue;
                Point point = new Point { Data = data, Index = i, X = x, Y = y };
                SetName(point);
                _points.Add(point);
            }
            Dictionary<string, int> names = new Dictionary<string, int>();
            foreach (Point point in _points) { if (point.Named) continue; if (!names.ContainsKey(point.Name)) names.Add(point.Name, 0); names[point.Name]++; }
            foreach (Point point in _points) if (!point.Named && names[point.Name] > 1)
                point.Name = string.Format(_runtime.Messages.Get("investigation.point-detail"), point.Name, Mathf.RoundToInt((point.X + 960f) / 19.2f), Mathf.RoundToInt((540f - point.Y) / 10.8f));
            _dirty = false;
            _runtime.Trace("investigation_labels", "context=" + (_labelContext ?? "unavailable") + " named=" + _points.FindAll(p => p.Named).Count + " total=" + _points.Count);
            _runtime.Trace("investigation_points", "background=" + _background + " side=" + _side + " rows=" + _points.Count + " queries=" + _queryCount + " elapsedMs=" + ((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }
        private void EnsurePoints()
        {
            if (_dirty || _data != GSStatic.inspect_data_ || _background != bgCtrl.instance.bg_no || _offset != bgCtrl.instance.bg_pos_x || _labelCulture != UiResources.Culture() || _labelContext != (_runtime.InvestigationNames == null ? null : _runtime.InvestigationNames.Context())) BuildPoints();
        }
        private void SetName(Point point)
        {
            string name = _runtime.InvestigationNames == null ? null : _runtime.InvestigationNames.Get(_labelContext, point.Data);
            point.Named = !string.IsNullOrEmpty(name);
            point.Name = point.Named ? name : PositionName(point.X, point.Y);
        }
        private string PositionName(float x, float y)
        {
            float horizontal = Mathf.Clamp01((x + 960f) / 1920f), vertical = Mathf.Clamp01((540f - y) / 1080f);
            string[] keys = { "tl", "tc", "tr", "ml", "mc", "mr", "bl", "bc", "br" };
            int column = Mathf.Min(2, (int)(horizontal * 3f)), row = Mathf.Min(2, (int)(vertical * 3f));
            return string.Format(_runtime.Messages.Get("investigation.point"), _runtime.Messages.Get("investigation.position." + keys[row * 3 + column]));
        }
        private Point CurrentPoint()
        {
            if (_nativeCursor != _inspect || _nativeState == 0) return null;
            foreach (Point point in _points) if (ReferenceEquals(point.Data, _nativeRegion) && point.Data.message == _nativeMessage) return point;
            if (_nativeRegion == null || _data == null || !Within(_inspect.pos_x, _inspect.pos_y)) return null;
            int index = Array.IndexOf(_data, _nativeRegion);
            if (index < 0) return null;
            // A genuine native hover remains a point even if bounded interior sampling missed a thin sliver.
            Point hovered = new Point { Data = _nativeRegion, Index = index, X = _inspect.pos_x, Y = _inspect.pos_y, Name = null };
            SetName(hovered);
            _points.Add(hovered);
            _runtime.Trace("investigation_native_hover_added", "index=" + index + " background=" + _background + " side=" + _side);
            return hovered;
        }
        private string PointText(Point point)
        {
            string text = point == null ? _runtime.Messages.Get("investigation.outside") : point.Name;
            return point != null && _nativeState == 3 ? string.Format(_runtime.Messages.Get("investigation.examined"), text) : text;
        }
        private int EdgeDirections()
        {
            float x = _inspect.pos_x, y = _inspect.pos_y;
            padCtrl pad = padCtrl.instance;
            int blocked = 0;
            if ((pad.GetKey(KeyType.Left) || pad.axis_pos_L.x < 0f) && x <= _minX + 0.01f) blocked |= 1;
            if ((pad.GetKey(KeyType.Right) || pad.axis_pos_L.x > 0f) && (x >= _maxX - 0.01f || y < -400f && x >= _guideX - 0.01f)) blocked |= 2;
            if ((pad.GetKey(KeyType.Up) || pad.axis_pos_L.y > 0f) && y >= _maxY - 0.01f) blocked |= 4;
            if ((pad.GetKey(KeyType.Down) || pad.axis_pos_L.y < 0f) && (y <= _minY + 0.01f || x > _guideX && y <= -399.99f)) blocked |= 8;
            return blocked;
        }
        private void ReadInspect()
        {
            if (!CanInspect()) { _audio.Reset(); _feedback.Edge(0, _runtime.Speech.Clock); return; }
            bool dual = GSMain_TanteiPart.IsBGSlide(bgCtrl.instance.bg_no);
            int side = dual ? (bgCtrl.instance.bg_pos_x > 0f ? 1 : 0) : 2;
            List<string> output = new List<string>();
            if (_side != side)
            {
                _side = side; _dirty = true; _feedback.Clear(); _audio.Reset();
                if (dual) output.Add(_runtime.Messages.Get(side == 0 ? "investigation.left-scene" : "investigation.right-scene"));
            }
            EnsurePoints();
            Point point = CurrentPoint();
            int blocked = EdgeDirections();
            _audio.Commit(_inspect, new Vector3(_inspect.pos_x, _inspect.pos_y, 0f), point == null ? null : _background + ":" + _side + ":" + point.Index, blocked);
            string identity = point == null ? "outside" : _background + ":" + _side + ":" + point.Index;
            if (_feedback.Focus(identity + ":" + PointText(point), point == null ? 0 : _nativeState)) output.Add(PointText(point));
            if (_feedback.Edge(blocked, _runtime.Speech.Clock)) { CursorFeedbackAudio.Edge(); output.Add(_runtime.Messages.Get("investigation.edge")); }
            if (output.Count > 0) _runtime.Presentation.Ui(_inspect, string.Join("\n", output.ToArray()));
        }
        internal void Navigate(int direction)
        {
            if (!CanInspect()) return;
            EnsurePoints();
            Point current = CurrentPoint();
            if (_points.Count == 0) { _runtime.Speech.Say(_runtime.Messages.Get("investigation.no-points"), false); return; }
            int index = current == null ? (direction > 0 ? -1 : 0) : _points.IndexOf(current);
            Point next = _points[(index + direction + _points.Count) % _points.Count];
            // Revalidate native conditions immediately before moving. Never activate or submit an investigation.
            INSPECT_DATA selected; uint native = InvestigationHooks.QueryAt(next.X, next.Y, out selected);
            if (!Within(next.X, next.Y) || !ReferenceEquals(selected, next.Data) || native != next.Data.message || !IsHitMessage(native))
            { _dirty = true; _runtime.Speech.Say(_runtime.Messages.Get("investigation.point-unavailable"), false); return; }
            _navigating = true;
            try { _inspect.SetCursorPos(next.X, next.Y); InvestigationHooks.RefreshCursor(_inspect); }
            finally { _navigating = false; }
            Point actual = CurrentPoint();
            if (actual == null || actual.Data != next.Data)
            { _dirty = true; _runtime.Speech.Say(_runtime.Messages.Get("investigation.point-unavailable"), false); return; }
            _feedback.Clear(); RequestInspect();
        }
        internal void DebugPoint(bool reload)
        {
            if (!_runtime.Settings.Debug || !CanInspect() || _runtime.InvestigationNames == null) return;
            if (reload)
            {
                bool loaded = _runtime.InvestigationNames.Reload();
                _dirty = true; _feedback.Clear();
                _runtime.Speech.Say(_runtime.Messages.Get(loaded ? "investigation.file-reloaded" : "investigation.file-missing"), false);
                RequestInspect(); return;
            }
            EnsurePoints();
            Point current = CurrentPoint();
            if (current == null) { _runtime.Speech.Say(_runtime.Messages.Get("investigation.debug-no-point"), false); return; }
            string context = _runtime.InvestigationNames.Context();
            string number = _runtime.InvestigationNames.Number(context, current.Data) ?? _runtime.Messages.Get("investigation.debug-unlisted");
            string location = _runtime.InvestigationNames.Location(context, current.Data) ?? context;
            string text = string.Format(_runtime.Messages.Get("investigation.debug-point"), number, current.Name, location, current.Data.message, InvestigationLabels.Region(current.Data));
            _runtime.Trace("investigation_debug_point", "id=" + number + " context=" + context + " region=" + InvestigationLabels.Region(current.Data) + " row=" + current.Index);
            _runtime.Speech.Say(text, false);
        }
        internal string Page()
        {
            if (CanInspect()) return "inspect:" + GSStatic.global_work_.title + ":" + GSStatic.global_work_.Room + ":" + bgCtrl.instance.bg_no + ":" + bgCtrl.instance.bg_pos_x;
            if (CanMove()) return "move:" + GSStatic.global_work_.Room;
            if (CanMenu()) return "investigation:" + GSStatic.global_work_.Room + ":" + _menu.setting;
            return null;
        }
        internal void Reset()
        {
            _audio.Reset();
            Clear(_menu); Clear(_move); Clear(_inspect); _menu = null; _move = null; _talk = null; _inspect = null; _nativeCursor = null;
            _topics.Clear(); _readMarks.Clear(); _lockMarks.Clear(); _history.Clear(); _points.Clear(); _data = null; _focus.Clear(); _feedback.Clear(); _side = -1;
        }
        /// <summary>Releases this module's pending presentation and scene identities.</summary>
        public void Dispose() { Reset(); }
    }
}
