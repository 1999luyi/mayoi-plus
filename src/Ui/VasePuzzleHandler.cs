using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class VasePuzzleHandler : IDisposable
    {
        private readonly UiRuntime _runtime;
        private static readonly FieldInfo Pieces = UiResources.Field(typeof(VasePuzzleMiniGame), "pieces_status_");
        private static readonly FieldInfo Cursor = UiResources.Field(typeof(VasePuzzleMiniGame), "icon_cursor_");
        private static readonly FieldInfo Step = UiResources.Field(typeof(VasePuzzleMiniGame), "puzzle_step_");
        private static readonly FieldInfo Restart = UiResources.Field(typeof(VasePuzzleMiniGame), "restert_step_");
        private static readonly FieldInfo Proc = UiResources.Field(typeof(VasePuzzleMiniGame), "proc_id_");
        private static readonly FieldInfo LocalStep = UiResources.Field(typeof(VasePuzzleMiniGame), "local_proc_step_");
        private static readonly FieldInfo Answers = UiResources.Field(typeof(VasePuzzleUtil), "puzzle_correct_data");
        private static readonly FieldInfo AnswerPiece = UiResources.Field(Answers.FieldType.GetElementType(), "pieces_index");
        private static readonly FieldInfo AnswerAngle = UiResources.Field(Answers.FieldType.GetElementType(), "angle_id");
        private VasePuzzleMiniGame _owner;
        private int _generation, _observedIndex = -1, _observedAngle = -1, _eventFrame = -1;
        private bool _intro, _selection, _rotation, _forceSelection, _disposed;
        private string _result;
        internal VasePuzzleHandler(UiRuntime runtime) { _runtime = runtime; }
        private int Phase { get { return Convert.ToInt32(Proc.GetValue(_owner), CultureInfo.InvariantCulture); } }
        private bool Foreground()
        {
            return !_disposed && _owner != null && _owner == VasePuzzleMiniGame.instance && _owner.enabled
                && NativeFields.Visible(_owner) && Application.isFocused
                && advCtrl.instance != null && advCtrl.instance.active
                && (_runtime.Assistance == null || !_runtime.Assistance.BlocksInput)
                && (optionCtrl.instance == null || !optionCtrl.instance.is_open)
                && (SaveLoadUICtrl.instance == null || !SaveLoadUICtrl.instance.is_open)
                && (recordListCtrl.instance == null || !recordListCtrl.instance.is_open)
                && (_runtime.StoryUi == null || !_runtime.StoryUi.BoxBlocks())
                && (autoPlayCtrl.instance == null || !autoPlayCtrl.instance.isStoryMode())
                && fadeCtrl.instance != null && fadeCtrl.instance.status == fadeCtrl.Status.NO_FADE;
        }
        internal bool CanRead()
        {
            return Foreground() && Phase == 3 && (int)LocalStep.GetValue(_owner) == 1
                && keyGuideCtrl.instance != null && keyGuideCtrl.instance.active
                && keyGuideCtrl.instance.current_guide == keyGuideBase.Type.POT_PAZZLE
                && !keyGuideCtrl.instance.CheckOpen() && !keyGuideCtrl.instance.CheckClose();
        }
        internal string Page { get { return CanRead() ? "vase-puzzle:" + Restart.GetValue(_owner) : null; } }
        internal void Start(VasePuzzleMiniGame owner)
        {
            Close(); if (_disposed || owner == null) return;
            _owner = owner; _intro = true;
            VasePuzzleHooks.PrepareSounds();
            _runtime.Trace("vase_puzzle_start", "restart=" + Restart.GetValue(owner));
        }
        internal void StateChanged(VasePuzzleMiniGame owner)
        {
            if (_owner != owner || owner == null) return;
            int phase = Phase;
            if (phase == 0 || phase == 8) { Close(); return; }
            if (phase != 3)
            {
                _selection = _rotation = _forceSelection = false; _result = null;
                _runtime.Presentation.ClearUi(owner);
            }
            if (phase == 7)
            {
                PiecesStatus[] rows = (PiecesStatus[])Pieces.GetValue(owner);
                bool complete = rows != null && rows.Length > 0 && (int)Step.GetValue(owner) >= rows.Length;
                if (complete) foreach (PiecesStatus row in rows) if (!row.used) { complete = false; break; }
                if (complete) { _result = "vase-puzzle.complete"; Request(); }
            }
        }
        internal void ModelCommitted(PiecesStatus[] rows, int index)
        {
            if (_owner == null || _disposed || Phase != 3 || index < 0 || rows == null
                || !ReferenceEquals(rows, Pieces.GetValue(_owner)) || index >= rows.Length || rows[index].used) return;
            bool selected = _observedIndex != index;
            bool rotated = !selected && _observedAngle != rows[index].angle_id;
            _observedIndex = index; _observedAngle = rows[index].angle_id;
            if (!selected && !rotated) return;
            BeginEvent(); _selection |= selected; _rotation |= rotated; Request();
        }
        internal void ReadyShown(bool enabled)
        {
            if (!enabled || _owner == null || _disposed || Phase != 3) return;
            BeginEvent(); _forceSelection = true; Request();
        }
        internal void GuideReady(keyGuideBase guide)
        {
            if (_owner != null && guide == keyGuideCtrl.instance && keyGuideCtrl.instance.current_guide == keyGuideBase.Type.POT_PAZZLE) { BeginEvent(); Request(); }
        }
        internal void Attempted(VasePuzzleMiniGame owner)
        {
            if (_owner != owner || !Foreground()) return;
            if (Phase == 4) _result = "vase-puzzle.success";
            else if (Phase == 5) _result = "vase-puzzle.failure";
            else return;
            Request();
        }
        private void BeginEvent()
        {
            if (_eventFrame != Time.frameCount) { _selection = _rotation = _forceSelection = false; _eventFrame = Time.frameCount; }
        }
        private void Request()
        {
            VasePuzzleMiniGame owner = _owner; int generation = _generation;
            if (owner == null || _runtime.Presentation == null) return;
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (_owner != owner || generation != _generation) return;
                try { Read(); } catch (Exception error) { _runtime.ReadFailure("vase-puzzle-read", error); }
            });
        }
        private string AngleText(int angle)
        {
            if (angle < 0 || angle > 3) throw new InvalidOperationException("Invalid native vase orientation.");
            return string.Format(_runtime.Messages.Get("vase-puzzle.angle"), angle * 90);
        }
        private bool Matches(int index, int angle, bool includeAngle)
        {
            if (VasePuzzleUtil.instance == null) throw new InvalidOperationException("Native vase answers unavailable.");
            Array answers = (Array)Answers.GetValue(VasePuzzleUtil.instance);
            int step = (int)Restart.GetValue(_owner) + (int)Step.GetValue(_owner);
            if (answers == null || step < 0 || step >= answers.Length) throw new InvalidOperationException("Native vase step unavailable.");
            object answer = answers.GetValue(step);
            return (int)AnswerPiece.GetValue(answer) == index && (!includeAngle || (int)AnswerAngle.GetValue(answer) == angle);
        }
        private void Read()
        {
            string result = _result; _result = null;
            bool selection = _selection, rotation = _rotation, force = _forceSelection;
            _selection = _rotation = _forceSelection = false;
            if (!Foreground()) return;
            List<string> text = new List<string>();
            if (result != null) text.Add(_runtime.Messages.Get(result));
            if (CanRead())
            {
                PiecesStatus[] rows = (PiecesStatus[])Pieces.GetValue(_owner);
                int index = (int)Cursor.GetValue(_owner);
                if (rows == null || index < 0 || index >= rows.Length || rows[index].used) throw new InvalidOperationException("Native vase selection unavailable.");
                int angle = rows[index].angle_id;
                if (_intro)
                {
                    text.Add(_runtime.Messages.Get(rows.Length == 1 ? "vase-puzzle.scene-single" : "vase-puzzle.scene"));
                    _intro = false; force = true;
                }
                if (selection || force) text.Add(string.Format(_runtime.Messages.Get("vase-puzzle.piece"), index + 1, AngleText(angle)));
                else if (rotation) text.Add(AngleText(angle));
                // These cues are explicitly selected by the player; native selection/rotation audio stays intact.
                if (soundCtrl.instance != null)
                {
                    if (selection && Matches(index, angle, false)) soundCtrl.instance.PlaySE(365);
                    if (rotation && Matches(index, angle, true)) soundCtrl.instance.PlaySE(419);
                }
                if (selection || rotation || force) _runtime.Trace("vase_puzzle_focus", "piece=" + index + " angle=" + angle + " step=" + Step.GetValue(_owner) + " selection=" + selection + " rotation=" + rotation);
            }
            if (text.Count > 0) _runtime.Presentation.Ui(_owner, NativeFields.Join(text.ToArray()));
        }
        internal void Repeat()
        {
            if (!CanRead()) return;
            _forceSelection = true; Request();
        }
        internal void Close()
        {
            if (!ReferenceEquals(_owner, null) && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner);
            _generation++; _owner = null; _observedIndex = _observedAngle = _eventFrame = -1;
            _intro = _selection = _rotation = _forceSelection = false; _result = null;
        }
        /// <summary>Releases only the pending feedback owned by this puzzle.</summary>
        public void Dispose() { Close(); _disposed = true; }
    }
}
