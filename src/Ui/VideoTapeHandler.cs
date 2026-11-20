using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class VideoTapeHandler : IDisposable
    {
        private static readonly FieldInfo Screen = UiResources.Field(typeof(MovieController), "screen");
        private static readonly FieldInfo Pausing = UiResources.Field(typeof(MovieController), "isPausing");
        private static readonly FieldInfo Speed = UiResources.Field(typeof(MovieController), "speed_rate");
        private static readonly FieldInfo Touch = UiResources.Field(typeof(FingerCursor), "touch_rect");
        // Reviewed image identities, not answer times. Actual native frame commits select the current scene.
        internal static readonly int[] SceneStarts = { 1, 41, 116, 216, 281, 321, 401, 451, 465, 485, 505, 521, 641, 681, 801, 841, 883, 899, 928, 941, 1161, 1201, 1321, 1361 };
        private static readonly int[] SceneKeys = { 0, 1, 18, 19, 2, 3, 4, 5, 20, 6, 21, 7, 8, 9, 10, 11, 12, 22, 23, 13, 14, 15, 16, 17 };
        private readonly UiRuntime _runtime;
        private MovieController _controller;
        private ConfrontWithMovie _owner;
        private int _generation, _lastScene = -1, _lastMode = -1, _lastMask, _lastHit = -2;
        private int _signalScene = -1, _signalMask = -1, _signalMode = -1;
        private bool _ready, _intro, _force, _disposed, _navigating, _hasPosition, _forceFocus, _replayTargets;
        private Vector3 _position;
        private MovieController _helpController;
        private int _helpGeneration;
        private bool _helpWasPaused;
        internal int Generation { get { return _generation; } }
        internal bool Owns(int generation) { return !_disposed && _controller != null && generation == _generation; }
        internal VideoTapeHandler(UiRuntime runtime) { _runtime = runtime; }
        internal string Page { get { return "video:" + _generation + ":" + (_owner != null && _owner.IsDetailing ? "detail" : "confront"); } }
        internal static int SceneAt(int frame)
        { if (frame < 1 || frame > 1420) return -1; int result = 0; for (int i = 1; i < SceneStarts.Length && frame >= SceneStarts[i]; i++) result = i; return result; }
        private bool Foreground()
        {
            return !_disposed && _controller != null && _owner != null && _owner == ConfrontWithMovie.instance
                && _controller == _owner.controller && _controller.enabled && _controller.is_play && NativeFields.Visible(_controller)
                && Application.isFocused && GSMain.is_active_ && advCtrl.instance != null && advCtrl.instance.active
                && (_runtime.Assistance == null || !_runtime.Assistance.BlocksInput)
                && (optionCtrl.instance == null || !optionCtrl.instance.is_open)
                && (SaveLoadUICtrl.instance == null || !SaveLoadUICtrl.instance.is_open)
                && (_runtime.StoryUi == null || !_runtime.StoryUi.BoxBlocks())
                && (autoPlayCtrl.instance == null || !autoPlayCtrl.instance.isStoryMode())
                && (fadeCtrl.instance == null || fadeCtrl.instance.status == fadeCtrl.Status.NO_FADE)
                && (recordListCtrl.instance == null || !recordListCtrl.instance.is_open || _owner.IsDetailing)
                && (bgCtrl.instance == null || !bgCtrl.instance.is_slider);
        }
        private bool HasFrame()
        {
            if (_controller == null || SceneAt(_controller.Frame) < 0) return false;
            RawImage screen = (RawImage)Screen.GetValue(_controller);
            return screen != null && NativeFields.Visible(screen) && screen.enabled && screen.texture != null
                && screen.texture.name == _controller.Frame.ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
        }
        private bool Interactive()
        {
            if (_owner == null || _controller == null || _owner.IsDetailing || _owner.auto_play || !_ready
                || advCtrl.instance == null || advCtrl.instance.sub_window_ == null) return false;
            SubWindow sub = advCtrl.instance.sub_window_; Routine current = sub.GetCurrentRoutine();
            if (current == null || current.r.no_0 != 29 || sub.req_ != SubWindow.Req.NONE || sub.bar_req_ != SubWindow.BarReq.NONE || sub.busy_ != 0) return false;
            if (current.r.no_1 != 1 && current.r.no_1 != 3 && current.r.no_1 != 4) return false;
            keyGuideCtrl guide = _owner.movie_guide;
            return guide != null && guide.active && NativeFields.Visible(guide) && !guide.CheckOpen() && !guide.CheckClose()
                && (guide.current_guide == keyGuideBase.Type.CONFRONT_WITH_MOVIE || guide.current_guide == keyGuideBase.Type.CONFRONT_WITH_MOVIE_PAUSE)
                && _owner.cursor != null && _owner.cursor.enabled && NativeFields.Visible(_owner.cursor)
                && _owner.collision_player != null && _owner.collision_player.enabled;
        }
        internal bool CanRead() { return Foreground() && HasFrame() && (_owner.IsDetailing || _owner.auto_play || Interactive()); }
        internal bool CanNavigate() { return CanRead() && Interactive() && Rects().Count == 4; }
        internal bool HasDirectionalCursor { get { return CanNavigate(); } }
        internal string Help { get { return _runtime.Messages.Get(CanNavigate() ? "video.help" : "video.help-view"); } }
        internal string NativeGuide
        {
            get
            {
                keyGuideCtrl guide = _owner == null ? null : _owner.movie_guide;
                if (guide == null || !guide.active || !NativeFields.Visible(guide) || guide.CheckOpen() || guide.CheckClose()) return null;
                if (guide.current_guide != keyGuideBase.Type.CONFRONT_WITH_MOVIE && guide.current_guide != keyGuideBase.Type.CONFRONT_WITH_MOVIE_PAUSE && guide.current_guide != keyGuideBase.Type.VIDEO_DETAIL) return null;
                return _runtime.Batch.ReadGuideText(guide);
            }
        }
        internal void Begin(MovieController controller)
        {
            Close(); if (_disposed || ConfrontWithMovie.instance == null || controller != ConfrontWithMovie.instance.controller) return;
            _controller = controller; _owner = ConfrontWithMovie.instance; _intro = true;
        }
        internal void InputReady(ConfrontWithMovie owner, KeyType key, bool pressed)
        {
            if (_owner == null || _owner != owner) return;
            if (pressed) { _ready = false; return; }
            if (!_ready) { _ready = true; Request(); }
        }
        internal void GuideReady(keyGuideBoard guide)
        { if (_owner != null && guide == _owner.movie_guide) Request(); }
        internal void ModeCommitted(MovieController controller)
        { if (controller == _controller) { if (!controller.is_play) { Close(); return; } _signalMode = -1; Request(); } }
        private int Mode()
        { return (bool)Pausing.GetValue(_controller) ? 0 : _controller.State == MovieController.PlayState.Rewinding ? 2 : (float)Speed.GetValue(_controller) > 1f ? 3 : 1; }
        private List<RectTransform> Rects()
        {
            List<RectTransform> result = new List<RectTransform>();
            if (_owner == null || _owner.collision_player == null || _controller == null || _owner.collision_player.previousFrame != _controller.Frame) return result;
            foreach (RectTransform rect in _owner.collision_player.Rects) result.Add(rect != null && rect.gameObject.activeInHierarchy ? rect : null);
            if (result.Count == 0) return result; // CollisionUpdate also runs before native Play supplies its dataset.
            if (result.Count != 4) throw new InvalidOperationException("Native video collision dataset count changed.");
            return result;
        }
        private static int Mask(List<RectTransform> rects)
        { int mask = 0; for (int i = 0; i < rects.Count; i++) if (rects[i] != null) mask |= 1 << i; return mask; }
        private string Target(int index) { return _runtime.Messages.Get(index == 3 && _controller.Frame < 899 ? "video.target-coat" : "video.target-" + index); }
        private string Timed(string text)
        { return string.Format(_runtime.Messages.Get("video.timed"), VideoTapeClock.At(_controller.Frame), text); }
        internal void PhaseCommitted(SubWindow sub, int previous)
        {
            if (_owner == null || advCtrl.instance == null || sub != advCtrl.instance.sub_window_) return;
            Routine current = sub.GetCurrentRoutine();
            if (current != null && current.r.no_1 != previous && current.r.no_1 == 1)
            { _ready = true; if (previous == 3 || previous == 4) { _force = true; _replayTargets = true; } Request(); }
        }
        private int Hit(List<RectTransform> rects)
        { int hit = _owner.cursor.GetCollidedNo(); return hit >= 0 && hit < rects.Count && rects[hit] != null ? hit : -1; }
        internal void ImageCommitted(MovieController controller)
        {
            if (controller != _controller || controller == null) return;
            int scene = SceneAt(controller.Frame), mode = Mode();
            if (scene != _signalScene || mode != _signalMode)
            { _signalScene = scene; _signalMode = mode; Request(); }
        }
        internal void FrameCommitted(MovieCollisionPlayer player)
        {
            if (_owner == null || player != _owner.collision_player || _controller == null) return;
            int scene = SceneAt(_controller.Frame), mode = Mode();
            int mask = _owner.IsDetailing || _owner.auto_play ? 0 : Mask(Rects());
            if (scene != _signalScene || mask != _signalMask || mode != _signalMode)
            { _signalScene = scene; _signalMask = mask; _signalMode = mode; Request(); }
        }
        internal void CursorCommitted(FingerCursor cursor)
        {
            if (_owner == null || cursor != _owner.cursor || _navigating) return;
            Vector3 position = cursor.transform.localPosition;
            bool moved = !_hasPosition || position.x != _position.x || position.y != _position.y;
            _hasPosition = true; _position = position;
            if (moved) Request();
        }
        private void Request()
        {
            MovieController controller = _controller; int generation = _generation;
            if (controller == null || _runtime.Presentation == null) return;
            _runtime.Presentation.ReadUi(controller, delegate
            { if (_disposed || _controller != controller || generation != _generation) return; try { Read(); } catch (Exception error) { _runtime.ReadFailure("video-read", error); } });
        }
        private void Read()
        {
            if (!CanRead()) return;
            int scene = SceneAt(_controller.Frame), mode = Mode();
            List<string> output = new List<string>();
            if (_intro) { output.Add(Timed(_runtime.Messages.Get("video.scene"))); _intro = false; }
            if (mode != _lastMode || _force) { output.Add(_runtime.Messages.Get("video.mode-" + mode)); _lastMode = mode; }
            if (scene != _lastScene || _force) { output.Add(Timed(_runtime.Messages.Get("video.description-" + SceneKeys[scene]))); _lastScene = scene; }
            if (CanNavigate())
            {
                List<RectTransform> rects = Rects(); int mask = Mask(rects), appeared = mask & ~_lastMask, disappeared = _lastMask & ~mask; int hit = Hit(rects);
                if (disappeared != 0) soundCtrl.instance.PlaySE(51);
                if (appeared != 0) soundCtrl.instance.PlaySE(363);
                List<string> names = new List<string>();
                int announced = appeared | (_replayTargets ? mask : 0);
                for (int i = 0; i < 4; i++) if ((announced & (1 << i)) != 0) names.Add(Target(i));
                if (names.Count != 0) output.Add(Timed(string.Format(_runtime.Messages.Get("video.available"), string.Join("; ", names.ToArray()))));
                if ((hit != _lastHit && hit >= 0 && (appeared & (1 << hit)) == 0) || _force || _forceFocus)
                    output.Add(hit < 0 ? _runtime.Messages.Get(mask == 0 ? "video.no-targets" : "video.outside") : Target(hit));
                _lastMask = mask; _lastHit = hit;
            }
            string nativeGuide = NativeGuide;
            if (!string.IsNullOrEmpty(nativeGuide) && _runtime.Assistance != null)
            { string hint = _runtime.Assistance.AutomaticGuide(_owner.movie_guide, nativeGuide); if (!string.IsNullOrEmpty(hint)) output.Add(hint); }
            _force = false; _forceFocus = false; _replayTargets = false;
            if (output.Count > 0) _runtime.Presentation.Ui(_controller, string.Join("\n", output.ToArray()));
        }
        internal void Repeat() { if (CanRead()) { _force = true; Request(); } }
        internal void Navigate(int direction)
        {
            if (!CanNavigate() || direction == 0) return;
            List<RectTransform> rects = Rects(); int mask = Mask(rects);
            if (mask == 0) { _forceFocus = true; Request(); return; }
            int current = Hit(rects), target = -1;
            for (int step = 1; step <= 4; step++)
            { int next = current < 0 ? (direction < 0 ? 4 - step : step - 1) : (current + (direction < 0 ? -step : step) + 8) % 4; if (rects[next] != null) { target = next; break; } }
            FingerCursor cursor = _owner.cursor; Vector3 original = cursor.transform.localPosition;
            Vector3[] corners = new Vector3[4], touch = new Vector3[4]; rects[target].GetWorldCorners(corners);
            RectTransform touchRect = (RectTransform)Touch.GetValue(cursor); touchRect.GetWorldCorners(touch);
            Vector3 root = cursor.transform.position;
            Vector3[] offsets = { (touch[0] + touch[2]) * .5f - root, touch[0] - root, touch[1] - root, touch[2] - root, touch[3] - root };
            bool located = false; _navigating = true;
            try
            {
                // A bounded geometry search preserves native hit priority when regions overlap.
                float[] samples = { .5f, .25f, .75f };
                foreach (Vector3 offset in offsets)
                {
                    foreach (float x in samples)
                    {
                        foreach (float y in samples)
                        {
                            Vector3 point = corners[0] + (corners[3] - corners[0]) * x + (corners[1] - corners[0]) * y - offset;
                            Vector3 local = cursor.transform.parent.InverseTransformPoint(point); Rect limit = cursor.MovableRange;
                            local.x = Mathf.Clamp(local.x, limit.x, limit.x + limit.width); local.y = Mathf.Clamp(local.y, limit.y, limit.y + limit.height);
                            if (local.x > cursor.movie_keyguide_scroll_limit && local.y < -400f) local.y = -400f;
                            local.z = original.z; cursor.transform.localPosition = local;
                            if (cursor.GetCollidedNo() == target) { located = true; break; }
                        }
                        if (located) break;
                    }
                    if (located) break;
                }
                if (!located) cursor.transform.localPosition = original;
            }
            catch { cursor.transform.localPosition = original; throw; }
            finally { _navigating = false; }
            if (!located) { _runtime.Presentation.Ui(_controller, _runtime.Messages.Get("video.unreachable")); return; }
            _position = cursor.transform.localPosition; _hasPosition = true; _forceFocus = true;
            Request();
        }
        internal void PauseForHelp()
        {
            if (!CanRead() || _helpController != null) return;
            _helpController = _controller; _helpGeneration = _generation; _helpWasPaused = (bool)Pausing.GetValue(_controller);
            Pausing.SetValue(_controller, true);
        }
        internal void RestoreAfterHelp()
        {
            if (_helpController != null && _helpController == _controller && _helpGeneration == _generation) Pausing.SetValue(_controller, _helpWasPaused);
            _helpController = null;
        }
        internal void HelpReturned() { if (_controller != null) { _signalMode = -1; Request(); } }
        internal void Close()
        {
            RestoreAfterHelp();
            if (_controller != null && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_controller);
            _generation++; _controller = null; _owner = null; _ready = false; _intro = false; _force = false; _forceFocus = false; _hasPosition = false;
            _replayTargets = false;
            _lastScene = _lastMode = _lastHit = -1; _lastMask = 0; _signalScene = _signalMask = _signalMode = -1;
        }
        internal void Stop(MovieController controller) { if (controller == _controller) Close(); }
        /// <summary>Releases only this video's pending accessibility messages.</summary>
        public void Dispose() { Close(); _disposed = true; }
    }
}
