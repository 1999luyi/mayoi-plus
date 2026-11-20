using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class LuminolHandler : IDisposable
    {
        private readonly UiRuntime _runtime;
        private readonly CursorFeedbackAudio _audio = new CursorFeedbackAudio();
        private readonly ControlGlyphs _glyphs;
        private luminolBloodstain _reactionBlood, _revealBlood, _announcedRevealBlood;
        private bool _viewPending;
        private readonly InvestigationFeedback _feedback = new InvestigationFeedback();
        private static readonly FieldInfo State = UiResources.Field(typeof(luminolMiniGame), "state");
        private static readonly FieldInfo Cursor = UiResources.Field(typeof(luminolMiniGame), "cursor_position_");
        private static readonly FieldInfo Size = UiResources.Field(typeof(luminolMiniGame), "body_size");
        private static readonly FieldInfo Icon = UiResources.Field(typeof(luminolMiniGame), "icon_");
        private static readonly FieldInfo Body = UiResources.Field(typeof(luminolMiniGame), "body_");
        private static readonly FieldInfo Rows = UiResources.Field(typeof(luminolMiniGame), "expl_ck_data_");
        private static readonly FieldInfo Blood = UiResources.Field(typeof(luminolMiniGame), "active_blood_");
        private static readonly FieldInfo Acquired = UiResources.Field(typeof(luminolMiniGame), "acquired_index_");
        private luminolMiniGame _owner;
        private Vector3 _lastPosition;
        private Vector2 _size;
        private float _guideX;
        private int _lastEdge, _side = -1;
        private bool _hasPosition, _navigating, _disposed, _guideBlockedDown;
        private int _generation;
        private double _edgeRequestTime;
        internal LuminolHandler(UiRuntime runtime) { _runtime = runtime; _glyphs = new ControlGlyphs(runtime.Messages); }
        private luminolState CurrentState { get { return (luminolState)State.GetValue(_owner); } }
        private bool OwnedForeground()
        {
            return !_disposed && _owner != null && _owner == luminolMiniGame.instance && !_owner.is_end
                && NativeFields.Visible(_owner) && ((Transform)Body.GetValue(_owner)).gameObject.activeInHierarchy
                && Application.isFocused && advCtrl.instance != null && advCtrl.instance.active
                && (_runtime.Assistance == null || !_runtime.Assistance.BlocksInput)
                && (autoPlayCtrl.instance == null || !autoPlayCtrl.instance.isStoryMode())
                && (optionCtrl.instance == null || !optionCtrl.instance.is_open)
                && (SaveLoadUICtrl.instance == null || !SaveLoadUICtrl.instance.is_open)
                && (recordListCtrl.instance == null || !recordListCtrl.instance.is_open)
                && (_runtime.StoryUi == null || !_runtime.StoryUi.BoxBlocks())
                && bgCtrl.instance != null && !bgCtrl.instance.is_slider;
        }
        private bool Foreground()
        {
            return OwnedForeground() && fadeCtrl.instance != null && fadeCtrl.instance.status == fadeCtrl.Status.NO_FADE
                && keyGuideCtrl.instance != null && keyGuideCtrl.instance.active
                && !keyGuideCtrl.instance.CheckOpen() && !keyGuideCtrl.instance.CheckClose();
        }
        internal bool CanRead()
        {
            if (!Foreground()) return false;
            Vector2 size = (Vector2)Size.GetValue(_owner);
            if (size.x <= 0f || size.y <= 0f) return false;
            keyGuideBase.Type guide = keyGuideCtrl.instance.current_guide;
            if (CurrentState == luminolState.Acquired)
                return guide == keyGuideBase.Type.LUMINOL_INSPECT && HitBlood((int)Acquired.GetValue(_owner)) != null
                    && HitBlood((int)Acquired.GetValue(_owner)).state_ == BloodstainState.Discovered;
            return CurrentState == luminolState.Search && ((RectTransform)Icon.GetValue(_owner)).gameObject.activeInHierarchy
                && (guide == keyGuideBase.Type.LUMINOL || guide == keyGuideBase.Type.LUMINOL_SLIDE || guide == keyGuideBase.Type.LUMINOL_TUTORIAL);
        }
        internal bool CanInspect() { return CanRead() && CurrentState == luminolState.Search; }
        internal string Page { get { return CanRead() ? "luminol:" + bgCtrl.instance.bg_no + ":" + bgCtrl.instance.bg_pos_x : null; } }
        internal void Ready(luminolMiniGame owner)
        {
            Close(); if (_disposed || owner == null) return;
            _owner = owner; _runtime.Trace("luminol_ready", "background=" + bgCtrl.instance.bg_no); Request();
        }
        internal void InputPressed() { _feedback.Edge(0, _runtime.Speech.Clock); _lastEdge = 0; }
        internal void Committed(luminolMiniGame owner)
        {
            if (_owner != owner || owner == null || _navigating || _disposed) return;
            Vector3 position = (Vector3)Cursor.GetValue(owner);
            // This is the original cursor commit, not a frame scan. Only changed scalar positions or a held boundary request a snapshot.
            padCtrl pad = padCtrl.instance;
            _guideBlockedDown = _hasPosition && position.x > _guideX && position.y == _lastPosition.y
                && pad != null && (pad.GetKey(KeyType.Down) || pad.axis_pos_L.y < 0f);
            int edge = CurrentState == luminolState.Search ? EdgeDirections(position) : 0;
            _audio.BlockMovement(edge);
            bool moved = !_hasPosition || position.x != _lastPosition.x || position.y != _lastPosition.y;
            _lastPosition = position; _hasPosition = true;
            double now = _runtime.Speech.Clock;
            if (moved || _lastEdge != edge || edge != 0 && (now < _edgeRequestTime || now - _edgeRequestTime >= 600.0))
            { if (edge != 0) _edgeRequestTime = now; Request(); }
            _lastEdge = edge;
        }
        internal void Changed(luminolMiniGame owner)
        {
            if (_owner != owner || owner == null) return;
            _hasPosition = false; Request();
        }
        internal void BloodChanged(luminolBloodstain blood, bool sprayed)
        {
            if (_owner == null || blood == null || !sprayed) return;
            if (!((List<luminolBloodstain>)Blood.GetValue(_owner)).Contains(blood)) return;
            _reactionBlood = blood.state_ == BloodstainState.Undiscovered ? blood : null;
            Request();
        }
        internal void Revealed(luminolMiniGame owner)
        {
            if (_owner != owner || owner == null) return;
            luminolBloodstain blood = HitBlood((int)Acquired.GetValue(owner));
            if (blood == null || blood == _announcedRevealBlood || blood.state_ != BloodstainState.Discovered) return;
            _reactionBlood = null; _revealBlood = blood; Request();
        }
        internal void Viewing(luminolMiniGame owner)
        {
            if (_owner != owner || owner == null) return;
            _audio.Reset(); _reactionBlood = _revealBlood = null; _viewPending = true;
            _runtime.Trace("luminol_view_requested", "phase=" + CurrentState); Request();
        }
        internal void GuideReady(keyGuideBase guide)
        { if (_owner != null && guide == keyGuideCtrl.instance) Request(); }
        private void Request()
        {
            luminolMiniGame owner = _owner; int generation = _generation;
            if (owner == null || _runtime.Presentation == null) return;
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (_owner != owner || generation != _generation) return;
                if (_viewPending)
                {
                    _viewPending = false;
                    // The native animation closes the guide and fades; its input-triggered entry owns this status.
                    if (OwnedForeground() && CurrentState == luminolState.AcquiredAnim)
                    { _runtime.Trace("luminol_viewing", "phase=" + CurrentState); _runtime.Presentation.Ui(owner, _runtime.Messages.Get("luminol.viewing")); }
                    return;
                }
                if (CanRead()) Read(); else { _audio.Reset(); _reactionBlood = null; }
            });
        }
        private luminolBloodstain HitBlood(int hit)
        {
            List<luminolBloodstain> blood = (List<luminolBloodstain>)Blood.GetValue(_owner);
            List<luminolTable> rows = (List<luminolTable>)Rows.GetValue(_owner);
            return hit >= 0 && hit < blood.Count && hit < rows.Count && hit < _owner.converted_point_.Count ? blood[hit] : null;
        }
        private int Hit()
        { return CurrentState == luminolState.Acquired ? (int)Acquired.GetValue(_owner) : LuminolHooks.Hit(_owner); }
        private string PointText(int hit)
        {
            luminolBloodstain blood = HitBlood(hit);
            if (blood == null) return _runtime.Messages.Get("luminol.outside");
            GSPoint4 p = _owner.converted_point_[hit];
            Vector2 size = (Vector2)Size.GetValue(_owner);
            float x = (p.x0 + p.x1 + p.x2 + p.x3) / 4f, y = (p.y0 + p.y1 + p.y2 + p.y3) / 4f;
            string[] positions = { "tl", "tc", "tr", "ml", "mc", "mr", "bl", "bc", "br" };
            int column = Mathf.Min(2, (int)(Mathf.Clamp01(x / size.x) * 3f));
            int row = Mathf.Min(2, (int)(Mathf.Clamp01(y / size.y) * 3f));
            string text = string.Format(_runtime.Messages.Get("luminol.point"), _runtime.Messages.Get("investigation.position." + positions[row * 3 + column]));
            string state = blood.state_ == BloodstainState.Undiscovered ? "luminol.unrevealed"
                : blood.state_ == BloodstainState.Acquired ? "luminol.investigated" : "luminol.revealed";
            return NativeFields.Join(text, _runtime.Messages.Get(state));
        }
        private string RevealText()
        {
            keyGuideCtrl guide = keyGuideCtrl.instance;
            if (guide != null && guide.current_guide == keyGuideBase.Type.LUMINOL_INSPECT)
            {
                IList icons = (IList)NativeFields.Get(guide, "guide_list_");
                if (icons.Count > 0)
                {
                    keyGuideBase.GuideIcon icon = (keyGuideBase.GuideIcon)icons[0];
                    if (icon.active && icon.sprite_ != null && icon.sprite_.active && icon.sprite_.enabled && icon.sprite_.color.a > 0f)
                    {
                        string identity;
                        return string.Format(_runtime.Messages.Get("luminol.reveal-ready-key"), _glyphs.Read(icon.sprite_, out identity));
                    }
                }
            }
            return _runtime.Messages.Get("luminol.reveal-ready");
        }
        private int EdgeDirections(Vector3 position)
        {
            Vector2 size = _size; padCtrl pad = padCtrl.instance; if (pad == null || size.x <= 0f || size.y <= 0f) return 0;
            float guideX = _guideX;
            int edge = 0;
            if ((pad.GetKey(KeyType.Left) || pad.axis_pos_L.x < 0f) && position.x <= .01f) edge |= 1;
            if ((pad.GetKey(KeyType.Right) || pad.axis_pos_L.x > 0f) && (position.x >= size.x - .01f || position.y > 900f && position.x >= guideX - .01f)) edge |= 2;
            if ((pad.GetKey(KeyType.Up) || pad.axis_pos_L.y > 0f) && position.y <= .01f) edge |= 4;
            if ((pad.GetKey(KeyType.Down) || pad.axis_pos_L.y < 0f) && (position.y >= size.y - .01f || position.x > guideX && (position.y >= 899.99f || _guideBlockedDown))) edge |= 8;
            return edge;
        }
        private void Read()
        {
            _size = (Vector2)Size.GetValue(_owner);
            _guideX = 1920f - keyGuideCtrl.instance.GetGuideWidth() - keyGuideCtrl.instance.guide_space;
            bool dual = GSMain_TanteiPart.IsBGSlide(bgCtrl.instance.bg_no) && keyGuideCtrl.instance.current_guide != keyGuideBase.Type.LUMINOL_TUTORIAL;
            int side = dual ? (bgCtrl.instance.bg_pos_x > 0f ? 1 : 0) : 2;
            List<string> output = new List<string>();
            if (_side != side)
            {
                _side = side; _feedback.Clear(); _audio.Reset();
                if (dual) output.Add(_runtime.Messages.Get(side == 0 ? "luminol.left-scene" : "luminol.right-scene"));
            }
            int hit = Hit(); luminolBloodstain blood = HitBlood(hit);
            int blocked = CanInspect() ? EdgeDirections((Vector3)Cursor.GetValue(_owner)) : 0;
            if (CanInspect()) _audio.Commit(_owner, (Vector3)Cursor.GetValue(_owner), blood == null ? null : side + ":" + hit, blocked); else _audio.Reset();
            int state = blood == null ? -1 : (int)blood.state_;
            bool changed = _feedback.Focus(side + ":" + hit, state);
            if (_revealBlood != null && _revealBlood == blood && CurrentState == luminolState.Acquired && blood.state_ == BloodstainState.Discovered)
            {
                _revealBlood = null; _announcedRevealBlood = blood; output.Add(RevealText());
                _runtime.Trace("luminol_revealed", "hit=" + hit + " state=" + state);
            }
            else if (changed) output.Add(PointText(hit));
            if (_reactionBlood != null)
            {
                if (_reactionBlood == blood && blood.state_ == BloodstainState.Undiscovered)
                { output.Add(_runtime.Messages.Get("luminol.reaction")); _runtime.Trace("luminol_reaction", "hit=" + hit); }
                _reactionBlood = null;
            }
            if (CanInspect() && _feedback.Edge(blocked, _runtime.Speech.Clock)) { CursorFeedbackAudio.Edge(); output.Add(_runtime.Messages.Get("investigation.edge")); }
            if (output.Count > 0)
            {
                _runtime.Trace("luminol_focus", "side=" + side + " hit=" + hit + " state=" + state + " phase=" + CurrentState);
                _runtime.Presentation.Ui(_owner, NativeFields.Join(output.ToArray()));
            }
        }
        internal void Repeat() { if (CanRead()) { _feedback.Clear(); Request(); } }
        internal void Navigate(int direction)
        {
            if (!CanInspect()) return;
            int count = Math.Min(_owner.converted_point_.Count, Math.Min(((List<luminolTable>)Rows.GetValue(_owner)).Count, ((List<luminolBloodstain>)Blood.GetValue(_owner)).Count));
            if (count == 0) { _runtime.Speech.Say(_runtime.Messages.Get("luminol.no-points"), false); return; }
            int hit = Hit(), next = hit < 0 ? (direction < 0 ? count - 1 : 0) : (hit + direction + count) % count;
            Vector3 original = (Vector3)Cursor.GetValue(_owner);
            GSPoint4 p = _owner.converted_point_[next];
            Vector3 target = new Vector3((p.x0 + p.x1 + p.x2 + p.x3) / 4f, (p.y0 + p.y1 + p.y2 + p.y3) / 4f, original.z);
            Vector2 size = (Vector2)Size.GetValue(_owner);
            float guideX = 1920f - keyGuideCtrl.instance.GetGuideWidth() - keyGuideCtrl.instance.guide_space;
            if (target.x < 0 || target.y < 0 || target.x > size.x || target.y > size.y || target.x > guideX && target.y > 900f)
            { _runtime.Speech.Say(_runtime.Messages.Get("luminol.point-unavailable"), false); return; }
            _navigating = true;
            try
            {
                Cursor.SetValue(_owner, target); LuminolHooks.UpdateCursor(_owner);
                if (LuminolHooks.Hit(_owner) != next)
                { Cursor.SetValue(_owner, original); LuminolHooks.UpdateCursor(_owner); _runtime.Speech.Say(_runtime.Messages.Get("luminol.point-unavailable"), false); return; }
            }
            finally { _navigating = false; }
            _runtime.Trace("luminol_navigation", "hit=" + next); _feedback.Clear(); Request();
        }
        internal void Close()
        {
            _audio.Reset();
            if (_owner != null && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner);
            _reactionBlood = _revealBlood = _announcedRevealBlood = null; _viewPending = false;
            _generation++; _owner = null; _side = -1; _hasPosition = false; _lastEdge = 0; _guideBlockedDown = false; _size = new Vector2(); _feedback.Clear();
        }
        /// <summary>Clears only this module's pending feedback and owner.</summary>
        public void Dispose() { Close(); _disposed = true; }
    }
}
