using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class VaseShowHandler : IDisposable
    {
        private readonly UiRuntime _runtime;
        private static readonly FieldInfo Body = UiResources.Field(typeof(VaseShowMiniGame), "body_");
        private static readonly FieldInfo Camera = UiResources.Field(typeof(VaseShowMiniGame), "vase_camera_");
        private static readonly FieldInfo Model = UiResources.Field(typeof(VaseShowMiniGame), "vase_");
        private static readonly FieldInfo Manager = UiResources.Field(typeof(VaseShowMiniGame), "evidence_manager_");
        private static readonly FieldInfo First = UiResources.Field(typeof(VaseShowMiniGame), "answer_rotate_1_");
        private static readonly FieldInfo Second = UiResources.Field(typeof(VaseShowMiniGame), "answer_rotate_2_");
        private static readonly FieldInfo Tolerance = UiResources.Field(typeof(VaseShowMiniGame), "safe_range_");
        private VaseShowMiniGame _owner;
        private VaseShowState _last;
        private int _generation;
        private bool _ready, _intro, _repeat, _query, _moved, _disposed;
        private GameObject _audioObject;
        private AudioSource _rotationAudio;
        internal VaseShowHandler(UiRuntime runtime) { _runtime = runtime; }
        internal string Page { get { return CanRead() ? "vase-show" : null; } }
        internal bool IsOpen { get { return !_disposed && _owner != null && _ready; } }
        internal bool OwnsCamera(GameObject camera) { return _owner != null && camera == (Camera.GetValue(_owner) as GameObject); }
        internal void Begin(VaseShowMiniGame owner)
        {
            Close(); if (_disposed || owner == null) return;
            _owner = owner; VaseShowHooks.PrepareSounds();
            _runtime.Trace("vase_show_start", "owner=" + owner.GetInstanceID());
        }
        internal bool CanRead()
        {
            if (!IsOpen || _owner != VaseShowMiniGame.instance || !Application.isFocused || !NativeFields.Visible(_owner)) return false;
            GameObject body = Body.GetValue(_owner) as GameObject, camera = Camera.GetValue(_owner) as GameObject;
            keyGuideCtrl guide = keyGuideCtrl.instance;
            return body != null && body.activeInHierarchy && camera != null && camera.activeInHierarchy
                && advCtrl.instance != null && advCtrl.instance.active
                && (_runtime.Assistance == null || !_runtime.Assistance.BlocksInput)
                && (optionCtrl.instance == null || !optionCtrl.instance.is_open)
                && (SaveLoadUICtrl.instance == null || !SaveLoadUICtrl.instance.is_open)
                && (recordListCtrl.instance == null || !recordListCtrl.instance.is_open)
                && (_runtime.StoryUi == null || !_runtime.StoryUi.BoxBlocks())
                && (autoPlayCtrl.instance == null || !autoPlayCtrl.instance.isStoryMode())
                && fadeCtrl.instance != null && fadeCtrl.instance.status == fadeCtrl.Status.NO_FADE
                && guide != null && guide.active && guide.current_guide == keyGuideBase.Type.VASE_SHOW
                && !guide.CheckOpen() && !guide.CheckClose();
        }
        internal void CameraCommitted(GameObject camera, bool active)
        {
            if (!OwnsCamera(camera)) return;
            if (!active) { Close(); return; }
            if (!_ready) { _ready = true; _intro = true; Request(); }
        }
        internal void GuideReady(keyGuideBase guide)
        { if (_owner != null && guide == keyGuideCtrl.instance && keyGuideCtrl.instance.current_guide == keyGuideBase.Type.VASE_SHOW) Request(); }
        internal void BeginInput(VaseShowMiniGame owner) { if (owner == _owner) _moved = false; }
        internal void EndInput(VaseShowMiniGame owner) { if (owner == _owner && (!_moved || !CanRead())) StopRotation(); }
        internal void RotationCommitted(evidenceObjectManager manager, float horizontal, float vertical, float roll)
        {
            if (_owner == null || manager != (Manager.GetValue(_owner) as evidenceObjectManager) || (horizontal == 0f && vertical == 0f && roll == 0f)) return;
            _moved = true;
            if (!CanRead()) { StopRotation(); return; }
            PlayRotation(); Request();
        }
        internal void TouchReleased(VaseShowMiniGame owner) { if (owner == _owner) StopRotation(); }
        private void Request()
        {
            VaseShowMiniGame owner = _owner; int generation = _generation;
            if (owner == null || _runtime.Presentation == null) return;
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (_disposed || _owner != owner || generation != _generation) return;
                try { Read(); } catch (Exception error) { StopRotation(); _runtime.ReadFailure("vase-show-read", error); }
            });
        }
        private VaseShowState Snapshot()
        {
            GameObject model = Model.GetValue(_owner) as GameObject;
            if (model == null) throw new InvalidOperationException("Native vase model unavailable.");
            // Read the final transform after all native rotation calls in this presentation.
            return new VaseShowState(model.transform.localEulerAngles, (Vector3)First.GetValue(_owner), (Vector3)Second.GetValue(_owner), (Vector3)Tolerance.GetValue(_owner));
        }
        private string Axis(int index) { return _runtime.Messages.Get("vase-show.axis-" + index); }
        private string Status(int index, int state)
        { return string.Format(_runtime.Messages.Get(state == 2 ? "vase-show.aligned" : state == 1 ? "vase-show.near" : "vase-show.crooked"), Axis(index)); }
        private string StatusText(VaseShowState state)
        { return NativeFields.Join(Status(0, state.Axes[0]), Status(1, state.Axes[1]), Status(2, state.Axes[2]), state.AllAligned ? _runtime.Messages.Get("vase-show.ready") : null); }
        private void Read()
        {
            if (!CanRead()) { StopRotation(); _repeat = _query = false; return; }
            VaseShowState state = Snapshot(); List<string> text = new List<string>();
            bool initial = _intro;
            if (initial)
            {
                text.Add(_runtime.Messages.Get("vase-show.scene"));
                soundCtrl.instance.PlaySE(363); _intro = false;
            }
            else if (_repeat) text.Add(_runtime.Messages.Get("vase-show.scene"));
            bool changed = false;
            if (_last != null)
            {
                bool lost = false;
                for (int i = 0; i < 3; i++)
                {
                    int before = _last.Axes[i], after = state.Axes[i];
                    if (before == after) continue;
                    changed = true;
                    if (before > 0 && after < before) { lost = true; if (!_query) text.Add(Status(i, 0)); }
                    else if (after > 0) { if (!_query) text.Add(Status(i, after)); }
                    if (after == 2) soundCtrl.instance.PlaySE(i == 0 ? 428 : i == 1 ? 116 : 362);
                }
                if (lost) soundCtrl.instance.PlaySE(51);
            }
            if (state.AllAligned && (_last == null || !_last.AllAligned))
            {
                VaseShowHooks.PlayReadySound();
                if (!_query) text.Add(_runtime.Messages.Get("vase-show.ready"));
            }
            if (_query)
            {
                text.Add(string.Format(_runtime.Messages.Get("vase-show.angles"), Number(state.Rotation.x), Number(state.Rotation.y), Number(state.Rotation.z)));
                text.Add(StatusText(state));
            }
            else if (_repeat) text.Add(StatusText(state));
            _last = state; _query = _repeat = false;
            if (text.Count > 0) _runtime.Presentation.Ui(_owner, NativeFields.Join(text.ToArray()));
            if (initial || changed) _runtime.Trace("vase_show_state", "axes=" + state.Axes[0] + ":" + state.Axes[1] + ":" + state.Axes[2] + " ready=" + state.AllAligned);
        }
        private static string Number(float value) { return Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture); }
        internal void Query() { if (CanRead()) { _query = true; Request(); } }
        internal void Repeat() { if (CanRead()) { _repeat = true; Request(); } }
        private void PlayRotation()
        {
            if (soundCtrl.instance == null) return;
            if (_rotationAudio == null)
            {
                _audioObject = new GameObject("PwaatVaseRotationCue"); _audioObject.transform.SetParent(_owner.transform, false);
                _rotationAudio = _audioObject.AddComponent<AudioSource>(); _rotationAudio.playOnAwake = false;
                _rotationAudio.loop = true; _rotationAudio.spatialBlend = 0f; _rotationAudio.clip = VaseShowHooks.Clip(132);
            }
            _rotationAudio.volume = 0.2f * soundCtrl.instance.option_set_se_rate * SoundDefine.GetVolume(_rotationAudio.clip);
            if (!_rotationAudio.isPlaying) _rotationAudio.Play();
        }
        internal void StopRotation() { if (_rotationAudio != null && _rotationAudio.isPlaying) _rotationAudio.Stop(); }
        internal void Close()
        {
            bool sounded = _ready && !_intro;
            if (!ReferenceEquals(_owner, null) && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner);
            StopRotation(); if (_audioObject != null) UnityEngine.Object.Destroy(_audioObject);
            _generation++; _owner = null; _last = null; _audioObject = null; _rotationAudio = null;
            _ready = _intro = _repeat = _query = _moved = false;
            if (sounded && !_disposed && Application.isFocused && soundCtrl.instance != null) soundCtrl.instance.PlaySE(51);
        }
        /// <summary>Releases only this minigame's pending feedback and private rotation audio.</summary>
        public void Dispose() { _disposed = true; Close(); }
    }
}
