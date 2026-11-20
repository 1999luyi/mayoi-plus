using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class TanchikiHandler : IDisposable
    {
        private static readonly FieldInfo CursorField = UiResources.Field(typeof(TanchikiMiniGame), "cursor_");
        private static readonly FieldInfo Limits = UiResources.Field(typeof(MiniGameCursor), "cursor_limit_");
        private readonly UiRuntime _runtime;
        private readonly InvestigationFeedback _feedback = new InvestigationFeedback();
        private TanchikiMiniGame _owner;
        private MiniGameCursor _cursor;
        private SubWindow _scope;
        private Vector3 _beforePosition;
        private int _pointCount, _sample = -1, _lastHit = -1, _side = -1, _generation;
        private bool _sampled, _failed, _sidePending, _edgePending;
        internal TanchikiHandler(UiRuntime runtime) { _runtime = runtime; }
        internal void Begin(TanchikiMiniGame owner)
        {
            Close();
            GSPoint4[] points = TanchikiMiniGame.find_target;
            if (points == null || points.Length < 2) throw new InvalidOperationException("Detector point data missing.");
            _cursor = CursorField.GetValue(owner) as MiniGameCursor;
            if (_cursor == null) throw new InvalidOperationException("Detector cursor missing.");
            TanchikiHooks.PrepareSounds();
            _owner = owner; _pointCount = points.Length - 1; _failed = false;
        }
        internal void BeforeMain(SubWindow subWindow)
        {
            _scope = !_failed && _owner != null && _owner == TanchikiMiniGame.instance ? subWindow : null;
            _sampled = false;
            if (_scope != null) _beforePosition = _cursor.cursor_position;
        }
        internal void HitComputed(TanchikiMiniGame owner, int hit)
        {
            // Consume the native hit result; never search the hidden region table.
            if (_scope == null || owner != _owner || _failed) return;
            _sample = hit; _sampled = true;
        }
        internal void MainCommitted(SubWindow subWindow)
        {
            bool sampled = _scope == subWindow && _sampled;
            _scope = null; _sampled = false;
            if (!sampled || _failed || _owner == null || _owner != TanchikiMiniGame.instance) return;
            int hit = _sample >= 0 && _sample < _pointCount ? _sample : -1;
            int previous = _lastHit; _lastHit = hit;
            if (!CanFeedback(subWindow)) { _feedback.Edge(0, _runtime.Speech.Clock); return; }
            Vector3 position = _cursor.cursor_position;
            bool moved = position.x != _beforePosition.x || position.y != _beforePosition.y;
            if (soundCtrl.instance != null)
            {
                // Resolve the selected GS1 cue only within its native playback call.
                if (moved) TanchikiHooks.PlayMovement();
                if (previous != hit)
                {
                    if (previous >= 0) soundCtrl.instance.PlaySE(51);
                    if (hit >= 0) soundCtrl.instance.PlaySE(363);
                }
            }
            bool edge = _feedback.Edge(Edge(position), _runtime.Speech.Clock);
            if (edge || moved && _side < 0) Request(subWindow, moved && _side < 0, edge);
        }
        internal void StateCommitted(SubWindow subWindow)
        {
            if (_owner != null && !_failed && CanFeedback(subWindow))
            { _feedback.Edge(0, _runtime.Speech.Clock); Request(subWindow, true, false); }
        }
        private int Edge(Vector3 position)
        {
            padCtrl pad = padCtrl.instance;
            if (pad == null) return 0;
            Rect limit = (Rect)Limits.GetValue(_cursor);
            Vector2 exception = _cursor.cursor_exception_limit;
            int blocked = 0;
            if ((pad.GetKey(KeyType.Left) || pad.axis_pos_L.x < 0f) && position.x <= limit.x + 0.01f) blocked |= 1;
            if ((pad.GetKey(KeyType.Right) || pad.axis_pos_L.x > 0f)
                && (position.x >= limit.width - 0.01f || exception != Vector2.zero && position.y > exception.y && position.x >= exception.x - 0.01f)) blocked |= 2;
            if ((pad.GetKey(KeyType.Up) || pad.axis_pos_L.y > 0f) && position.y <= limit.y + 0.01f) blocked |= 4;
            if ((pad.GetKey(KeyType.Down) || pad.axis_pos_L.y < 0f)
                && (position.y >= limit.height - 0.01f || exception != Vector2.zero && position.x > exception.x && position.y >= exception.y - 0.01f)) blocked |= 8;
            return blocked;
        }
        private void Request(SubWindow subWindow, bool side, bool edge)
        {
            if (_owner == null || _runtime.Presentation == null) return;
            _sidePending |= side; _edgePending |= edge;
            TanchikiMiniGame owner = _owner; int generation = _generation;
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (owner != _owner || generation != _generation || _failed) return;
                bool readSide = _sidePending, readEdge = _edgePending;
                _sidePending = _edgePending = false;
                if (!CanFeedback(subWindow) || bgCtrl.instance == null || bgCtrl.instance.is_slider) return;
                try
                {
                    List<string> text = new List<string>();
                    if (readSide)
                    {
                        bool dual = GSMain_TanteiPart.IsBGSlide(bgCtrl.instance.bg_no);
                        int current = dual ? (bgCtrl.instance.bg_pos_x > 0f ? 1 : 0) : 2;
                        if (current != _side)
                        {
                            _side = current;
                            if (dual) text.Add(_runtime.Messages.Get(current == 0 ? "tanchiki.left-screen" : "tanchiki.right-screen"));
                        }
                    }
                    if (readEdge) { CursorFeedbackAudio.Edge(); text.Add(_runtime.Messages.Get("investigation.edge")); }
                    if (text.Count > 0) _runtime.Presentation.Ui(owner, string.Join("\n", text.ToArray()));
                }
                catch (Exception error) { Fail(error); }
            });
        }
        private bool CanFeedback(SubWindow subWindow)
        {
            if (_failed || _owner == null || _owner != TanchikiMiniGame.instance || _cursor == null
                || !Application.isFocused || _runtime.Presentation == null || !_runtime.Presentation.Foreground()
                || (_runtime.Assistance != null && _runtime.Assistance.BlocksInput)
                || (recordListCtrl.instance != null && recordListCtrl.instance.is_open)
                || (autoPlayCtrl.instance != null && autoPlayCtrl.instance.isStoryMode())
                || fadeCtrl.instance == null || fadeCtrl.instance.status != fadeCtrl.Status.NO_FADE
                || advCtrl.instance == null || advCtrl.instance.sub_window_ != subWindow || subWindow.IsBusy()) return false;
            Routine routine = subWindow.GetCurrentRoutine();
            return routine != null && routine.r.no_0 == (byte)SubWindow.State.TANCHIKI && routine.r.no_1 == 2;
        }
        internal void Fail(Exception error)
        {
            if (_failed) return;
            Close(); _failed = true; _runtime.ReadFailure("tanchiki", error);
        }
        internal void Close()
        {
            if (_owner != null && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner);
            _generation++; _owner = null; _cursor = null; _scope = null;
            _pointCount = 0; _sample = _lastHit = _side = -1; _sampled = _sidePending = _edgePending = false; _feedback.Clear();
        }
        public void Dispose() { Close(); _failed = true; }
    }
}
