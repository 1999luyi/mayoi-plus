using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    internal enum FingerprintPhase { None, Select, Dust, Compare, Scan }
    internal sealed class FingerprintStamp
    {
        internal int Generation, X, Y, Width, Height;
    }
    internal sealed class FingerprintHandler : IDisposable
    {
        private static readonly FieldInfo Infos = UiResources.StaticField(typeof(FingerMiniGame), "finger_info");
        private static readonly Type InfoType = Infos.FieldType.GetElementType();
        private static readonly FieldInfo Rows = UiResources.Field(InfoType, "ptbl");
        private static readonly Type RowType = Rows.FieldType.GetElementType();
        private static readonly FieldInfo Inspect = UiResources.Field(RowType, "finger_inspect");
        private static readonly FieldInfo HitFlag = UiResources.Field(RowType, "hit");
        private static readonly FieldInfo MaskId = UiResources.Field(RowType, "init_finger");
        private static readonly FieldInfo Offsets = UiResources.Field(typeof(FingerMiniGame), "select_finger_offset_");
        private static readonly FieldInfo Scales = UiResources.Field(typeof(FingerMiniGame), "select_finger_scale_");
        private static readonly FieldInfo Index = UiResources.Field(typeof(FingerMiniGame), "finger_index_");
        private static readonly FieldInfo Mask = UiResources.Field(typeof(FingerMiniGame), "mask_data_");
        private static readonly FieldInfo MaskWidth = UiResources.Field(typeof(FingerMiniGame), "mask_width_");
        private static readonly FieldInfo MaskHeight = UiResources.Field(typeof(FingerMiniGame), "mask_height_");
        private static readonly FieldInfo Render = UiResources.Field(typeof(FingerMiniGame), "render_texture_");
        private static readonly FieldInfo Material = UiResources.Field(typeof(FingerMiniGame), "render_material_");
        private static readonly FieldInfo Brushes = UiResources.Field(typeof(FingerMiniGame), "powder_sprites_");
        private static readonly FieldInfo Scores = UiResources.StaticField(typeof(FingerMiniGame), "chk_inf_finger");
        private static readonly FieldInfo Score = UiResources.Field(typeof(FingerMiniGame), "score_");
        private static readonly FieldInfo Match = UiResources.Field(typeof(FingerMiniGame), "comp_hit_");
        private static readonly FieldInfo PersonIndex = UiResources.Field(typeof(FingerMiniGame), "comp_cursor_");
        private static readonly FieldInfo Texts = UiResources.Field(typeof(FingerMiniGame), "info_texts_");
        private static readonly FieldInfo MainRoot = UiResources.Field(typeof(FingerMiniGame), "main_root_");
        private static readonly FieldInfo CompareRoot = UiResources.Field(typeof(FingerMiniGame), "comp_main_root_");
        private static readonly FieldInfo CursorLimit = UiResources.Field(typeof(MiniGameCursor), "cursor_limit_");
        private static readonly MethodInfo Clear = typeof(FingerMiniGame).GetMethod("CheckClear", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly UiRuntime _runtime;
        private readonly CursorFeedbackAudio _audio = new CursorFeedbackAudio();
        private readonly InvestigationFeedback _feedback = new InvestigationFeedback();
        private FingerMiniGame _owner;
        private FingerprintPhase _phase;
        private FingerprintCoverage _coverage;
        private int _generation, _tutorial, _lastEdge, _lastPerson = -1;
        private bool _ready, _blowing, _sceneRead, _force, _disposed, _hasPosition, _navigating, _captureFailed;
        private Vector3 _lastPosition;
        private double _edgeRequest;
        private string _notice;
        private Array _rows;
        private GSPoint4[] _points;
        private Texture2D _readback;
        private AudioSource _dustAudio;
        private GameObject _audioObject;
        private int _brushWidth, _brushHeight;
        internal FingerprintHandler(UiRuntime runtime) { _runtime = runtime; }
        internal int Generation { get { return _generation; } }
        internal bool Owns(FingerMiniGame owner, int generation) { return !_disposed && _owner == owner && owner != null && generation == _generation; }
        private bool ActorForeground()
        {
            return !_disposed && _owner != null && _owner == FingerMiniGame.instance && _owner.is_running && _owner.enabled
                && NativeFields.Visible(_owner) && Application.isFocused && advCtrl.instance != null && advCtrl.instance.active
                && (_runtime.Assistance == null || !_runtime.Assistance.BlocksInput)
                && (optionCtrl.instance == null || !optionCtrl.instance.is_open)
                && (SaveLoadUICtrl.instance == null || !SaveLoadUICtrl.instance.is_open)
                && (recordListCtrl.instance == null || !recordListCtrl.instance.is_open)
                && (_runtime.StoryUi == null || !_runtime.StoryUi.BoxBlocks())
                && bgCtrl.instance != null && !bgCtrl.instance.is_slider
                && (autoPlayCtrl.instance == null || !autoPlayCtrl.instance.isStoryMode());
        }
        internal bool CanRead()
        {
            if (!ActorForeground() || !_ready || _tutorial != 0 || _blowing || MiniGameCursor.instance == null
                || fadeCtrl.instance == null || fadeCtrl.instance.status != fadeCtrl.Status.NO_FADE
                || keyGuideCtrl.instance == null || !keyGuideCtrl.instance.active
                || keyGuideCtrl.instance.CheckOpen() || keyGuideCtrl.instance.CheckClose()) return false;
            keyGuideBase.Type guide = keyGuideCtrl.instance.current_guide;
            if (_phase == FingerprintPhase.Select) return guide == keyGuideBase.Type.FINGER_SELECT;
            if (_phase == FingerprintPhase.Dust) return guide == keyGuideBase.Type.FINGER_MAIN && ((GameObject)MainRoot.GetValue(_owner)).activeInHierarchy;
            return _phase == FingerprintPhase.Compare && ((GameObject)CompareRoot.GetValue(_owner)).activeInHierarchy
                && (guide == keyGuideBase.Type.FINGER_COMP || guide == keyGuideBase.Type.FINGER_COMP_TUTORIAL);
        }
        internal bool CanHoldPowderNavigation { get { return _phase == FingerprintPhase.Dust; } }
        internal bool CanNavigate() { return CanRead() && (_phase == FingerprintPhase.Select || _phase == FingerprintPhase.Dust); }
        internal string Help { get { return !CanRead() ? null : _runtime.Messages.Get(_phase == FingerprintPhase.Select ? "fingerprint.help-select" : _phase == FingerprintPhase.Dust ? "fingerprint.help-dust" : "fingerprint.help-repeat"); } }
        internal bool HasDirectionalCursor { get { return CanNavigate(); } }
        internal string Page { get { return CanRead() ? "fingerprint:" + _owner.game_id + ":" + _phase : null; } }
        internal void Start(FingerMiniGame owner, ushort configuration)
        { Close(); if (_disposed || owner == null) return; _owner = owner; _runtime.Trace("fingerprint_start", "configuration=" + configuration); }
        internal void Enter(FingerprintPhase phase)
        {
            _audio.Reset(); StopDust(); _phase = phase; _ready = false; _hasPosition = false; _lastEdge = 0; _feedback.Clear(); _force = true;
            if (phase == FingerprintPhase.Select) BuildPoints();
            if (phase == FingerprintPhase.Compare) _lastPerson = -1;
        }
        internal void Exit(FingerprintPhase phase)
        { if (_phase == phase) { _audio.Reset(); StopDust(); _phase = FingerprintPhase.None; _ready = false; } }
        internal void BeginTutorial() { _audio.Reset(); _tutorial++; _ready = false; StopDust(); }
        internal void EndTutorial() { if (_tutorial > 0) _tutorial--; _force = true; }
        internal void InputReady()
        {
            if (_owner == null || _tutorial != 0 || _phase == FingerprintPhase.Scan) return;
            if (!_ready) { _ready = true; _force = true; Request(); }
        }
        internal void GuideReady(keyGuideBase guide) { if (_owner != null && guide == keyGuideCtrl.instance) Request(); }
        private void BuildPoints()
        {
            Array infos = (Array)Infos.GetValue(null);
            if (_owner.game_id < 0 || _owner.game_id >= infos.Length) throw new InvalidOperationException("Invalid fingerprint configuration.");
            _rows = (Array)Rows.GetValue(infos.GetValue(_owner.game_id));
            Vector2 offset = ((Vector2[])Offsets.GetValue(_owner))[_owner.game_id];
            Vector2 scale = ((Vector2[])Scales.GetValue(_owner))[_owner.game_id];
            List<GSPoint4> points = new List<GSPoint4>();
            foreach (object row in _rows)
            {
                INSPECT_DATA data = (INSPECT_DATA)Inspect.GetValue(row);
                GSPoint4 original = new GSPoint4(data.x0, data.y0, data.x1, data.y1, data.x2, data.y2, data.x3, data.y3);
                points.AddRange(MiniGameGSPoint4Hit.ConvertPoint(new GSPoint4[] { original }, offset, scale));
            }
            _points = points.ToArray();
        }
        private bool Eligible(int index)
        {
            if (_owner.game_id != 0) return true;
            int current = GSFlag.Check(0u, scenario.SCE4_FLAG_ST_G_FINGER_MES2) ? 1 : 0;
            return (byte)HitFlag.GetValue(_rows.GetValue(index)) == current;
        }
        private int Hit()
        {
            Vector3 position = MiniGameCursor.instance.cursor_position;
            GSRect rect = new GSRect((short)position.x, (short)position.y, 4, 4);
            for (int i = 0; i < _points.Length; i++) if (GSUtility.ObjHitCheck2(rect, _points[i])) return i;
            return -1;
        }
        private int Edge(Vector3 position)
        {
            if (MiniGameCursor.instance == null || padCtrl.instance == null) return 0;
            Rect limit = (Rect)CursorLimit.GetValue(MiniGameCursor.instance);
            Vector2 exception = MiniGameCursor.instance.cursor_exception_limit;
            padCtrl pad = padCtrl.instance; int blocked = 0;
            if ((pad.GetKey(KeyType.Left) || pad.axis_pos_L.x < 0f) && position.x <= limit.x + 0.01f) blocked |= 1;
            if ((pad.GetKey(KeyType.Right) || pad.axis_pos_L.x > 0f)
                && (position.x >= limit.width - 0.01f || exception != Vector2.zero && position.y > exception.y && position.x >= exception.x - 0.01f)) blocked |= 2;
            if ((pad.GetKey(KeyType.Up) || pad.axis_pos_L.y > 0f) && position.y <= limit.y + 0.01f) blocked |= 4;
            if ((pad.GetKey(KeyType.Down) || pad.axis_pos_L.y < 0f)
                && (position.y >= limit.height - 0.01f || exception != Vector2.zero && position.x > exception.x && position.y >= exception.y - 0.01f)) blocked |= 8;
            return blocked;
        }
        internal void HeldCursorCommitted(MiniGameCursor cursor)
        {
            if (cursor != MiniGameCursor.instance || _navigating || !CanHoldPowderNavigation) return;
            bool previous = Input.GetKey(KeyCode.LeftBracket), next = Input.GetKey(KeyCode.RightBracket);
            if (previous == next || !CanNavigate() || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return;
            KeyCode key = previous ? KeyCode.LeftBracket : KeyCode.RightBracket;
            if (StoryInputHandler.IsBound(key)) return;
            int x, y;
            if (PowderTarget(previous ? -1 : 1, out x, out y)) SetCursor(x, y);
            else if (Input.GetKeyDown(key)) _runtime.Presentation.Ui(_owner, _runtime.Messages.Get("fingerprint.covered"));
        }
        private bool PowderTarget(int direction, out int x, out int y)
        {
            if (_coverage == null || _captureFailed) throw new InvalidOperationException("Native powder readback unavailable.");
            MiniGameCursor cursor = MiniGameCursor.instance;
            Rect limit = (Rect)CursorLimit.GetValue(cursor); Vector2 exception = cursor.cursor_exception_limit;
            return _coverage.NextStamp(cursor.cursor_position.x, cursor.cursor_position.y, limit.x, limit.y, limit.width, limit.height,
                exception == Vector2.zero ? float.MaxValue : exception.x, exception == Vector2.zero ? float.MaxValue : exception.y, direction, out x, out y);
        }
        internal void CursorCommitted(MiniGameCursor cursor)
        {
            if (_owner == null || cursor != MiniGameCursor.instance || _navigating
                || (_phase != FingerprintPhase.Select && _phase != FingerprintPhase.Dust)) return;
            Vector3 position = cursor.cursor_position;
            int edge = _ready && _tutorial == 0 && !_blowing ? Edge(position) : 0;
            _audio.BlockMovement(edge);
            bool moved = !_hasPosition || position.x != _lastPosition.x || position.y != _lastPosition.y;
            _lastPosition = position; _hasPosition = true;
            double now = _runtime.Speech.Clock;
            if (moved || edge != _lastEdge || edge != 0 && (now < _edgeRequest || now - _edgeRequest >= 600.0))
            { if (edge != 0) _edgeRequest = now; Request(); }
            _lastEdge = edge;
        }
        internal void PersonCommitted() { if (_owner != null && _phase == FingerprintPhase.Compare) Request(); }
        private void Request()
        {
            FingerMiniGame owner = _owner; int generation = _generation;
            if (owner == null || _runtime.Presentation == null) return;
            _runtime.Presentation.ReadUi(owner, delegate
            { if (!Owns(owner, generation)) return; try { Read(); } catch (Exception error) { _runtime.ReadFailure("fingerprint-read", error); } });
        }
        private void Read()
        {
            if (!ActorForeground()) { _audio.Reset(); _notice = null; StopDust(); return; }
            List<string> text = new List<string>();
            if (_notice != null) { text.Add(_notice); _notice = null; }
            if (CanRead())
            {
                int blocked = (_phase == FingerprintPhase.Select || _phase == FingerprintPhase.Dust) ? Edge(MiniGameCursor.instance.cursor_position) : 0;
                if (_phase == FingerprintPhase.Select)
                {
                    if (!_sceneRead) { text.Add(_runtime.Messages.Get("fingerprint.scene-" + _owner.game_id)); _sceneRead = true; }
                    int hit = Hit();
                    _audio.Commit(_owner, MiniGameCursor.instance.cursor_position, hit < 0 ? null : hit.ToString(), blocked);
                    string focus = hit < 0 ? _runtime.Messages.Get("fingerprint.outside") : string.Format(_runtime.Messages.Get("fingerprint.finger"), hit + 1);
                    if (_feedback.Focus("finger:" + hit, 0) || _force) text.Add(focus);
                }
                else if (_phase == FingerprintPhase.Compare)
                {
                    int current = (int)PersonIndex.GetValue(_owner);
                    if (current != _lastPerson || _force)
                    {
                        Text[] fields = (Text[])Texts.GetValue(_owner);
                        List<string> original = new List<string>();
                        foreach (Text field in fields) if (field != null && !string.IsNullOrEmpty(field.text)) original.Add(field.text);
                        if (original.Count == 0) throw new InvalidOperationException("Native fingerprint person text unavailable.");
                        text.Add(string.Join("\n", original.ToArray())); _lastPerson = current;
                    }
                }
                else if (_phase == FingerprintPhase.Dust)
                {
                    // Powder is applied at every permitted cursor position; there is no native target-range hit in this phase.
                    _audio.Commit(_owner, MiniGameCursor.instance.cursor_position, null, blocked);
                    if (_force) text.Add(_runtime.Messages.Get("fingerprint.dusting"));
                }
                if ((_phase == FingerprintPhase.Select || _phase == FingerprintPhase.Dust)
                    && _feedback.Edge(blocked, _runtime.Speech.Clock)) { CursorFeedbackAudio.Edge(); text.Add(_runtime.Messages.Get("investigation.edge")); }
                _force = false;
            }
            if (text.Count != 0) _runtime.Presentation.Ui(_owner, string.Join("\n", text.ToArray()));
        }
        internal void Repeat() { if (CanRead()) { _force = true; Request(); } }
        internal void Navigate(int direction)
        {
            if (!CanNavigate()) return;
            MiniGameCursor cursor = MiniGameCursor.instance;
            if (_phase == FingerprintPhase.Select)
            {
                int current = Hit(), index = current < 0 ? (direction > 0 ? -1 : 0) : current;
                for (int count = 0; count < _points.Length; count++)
                {
                    index = (index + direction + _points.Length) % _points.Length;
                    if (!Eligible(index)) continue;
                    GSPoint4 p = _points[index];
                    float x = ((int)p.x0 + p.x1 + p.x2 + p.x3) * 0.25f, y = ((int)p.y0 + p.y1 + p.y2 + p.y3) * 0.25f;
                    SetCursor(x, y);
                    if (Hit() != index) throw new InvalidOperationException("Native fingerprint navigation did not reach its selected polygon.");
                    _feedback.Clear(); _force = true; Request(); return;
                }
                _runtime.Presentation.Ui(_owner, _runtime.Messages.Get("fingerprint.no-fingers")); return;
            }
            int targetX, targetY;
            if (!PowderTarget(direction, out targetX, out targetY))
            { _runtime.Presentation.Ui(_owner, _runtime.Messages.Get("fingerprint.covered")); return; }
            SetCursor(targetX, targetY);
            _runtime.Trace("fingerprint_powder_navigation", "x=" + targetX + " y=" + targetY);
        }
        private void SetCursor(float x, float y)
        {
            _navigating = true;
            try { MiniGameCursor.instance.cursor_position = new Vector3(x, y, 0f); }
            finally { _navigating = false; }
            _lastPosition = MiniGameCursor.instance.cursor_position; _hasPosition = true;
        }
        internal void Loaded()
        {
            _coverage = null; _captureFailed = false;
            RenderTexture target = (RenderTexture)Render.GetValue(_owner);
            byte[] mask = (byte[])Mask.GetValue(_owner);
            if (target == null || mask == null) throw new InvalidOperationException("Native fingerprint render or mask unavailable.");
            bool flipX, flipY; Calibrate(target, (Material)Material.GetValue(_owner), out flipX, out flipY);
            _coverage = new FingerprintCoverage(mask, (int)MaskWidth.GetValue(_owner), (int)MaskHeight.GetValue(_owner),
                target.width, target.height, systemCtrl.instance.ScreenWidth, systemCtrl.instance.ScreenHeight, flipX, flipY);
            _brushWidth = _brushHeight = 0;
            foreach (Sprite sprite in (Sprite[])Brushes.GetValue(_owner))
            { _brushWidth = Math.Max(_brushWidth, Mathf.CeilToInt(sprite.rect.width)); _brushHeight = Math.Max(_brushHeight, Mathf.CeilToInt(sprite.rect.height)); }
            _coverage.ConfigureBrush(_brushWidth, _brushHeight);
            _runtime.Trace("fingerprint_loaded", "flipX=" + flipX + " flipY=" + flipY + " target=" + target.width + "x" + target.height
                + " mask=" + MaskWidth.GetValue(_owner) + "x" + MaskHeight.GetValue(_owner) + " calibration=native-order sweep=" + Math.Max(1, _brushWidth / 4) + "x" + Math.Max(1, _brushHeight / 4));
        }
        // Match DropPowder: load the pixel matrix on the backbuffer, then bind the draw target.
        // Matrix setup while a render texture is already bound can select a different GPU transform.
        private void Calibrate(RenderTexture nativeTarget, Material material, out bool flipX, out bool flipY)
        {
            RenderTexture previous = RenderTexture.active;
            int width = nativeTarget.width, height = nativeTarget.height;
            RenderTexture probe = new RenderTexture(width, height, 0);
            Texture2D sample = new Texture2D(4, 4);
            try
            {
                Graphics.SetRenderTarget(probe); GL.Clear(false, true, Color.clear);
                Graphics.SetRenderTarget(null);
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0f, width, height, 0f);
                    Graphics.SetRenderTarget(probe);
                    Graphics.DrawTexture(new Rect(8f, 12f, 4f, 4f), Texture2D.whiteTexture,
                        new Rect(0f, 0f, 1f, 1f), 0, 0, 0, 0, Color.white, material);
                    Graphics.SetRenderTarget(null);
                }
                finally { GL.PopMatrix(); }
                Graphics.SetRenderTarget(probe);
                int matched = -1;
                // Four bounded reads identify the same native draw/readback axes without scanning the target.
                for (int candidate = 0; candidate < 4; candidate++)
                {
                    int x = (candidate & 1) == 0 ? 8 : width - 12;
                    int y = (candidate & 2) == 0 ? 12 : height - 16;
                    sample.ReadPixels(new Rect(x, y, 4f, 4f), 0, 0, false);
                    Color32[] pixels = sample.GetPixels32(); int count = 0;
                    foreach (Color32 pixel in pixels) if (pixel.a != 0) count++;
                    if (count == 0) continue;
                    if (count != 16 || matched >= 0) throw new InvalidOperationException("Unsupported native fingerprint draw/readback transform.");
                    matched = candidate;
                }
                if (matched < 0) throw new InvalidOperationException("Fingerprint draw/readback calibration produced no pixels.");
                flipX = (matched & 1) != 0; flipY = (matched & 2) != 0;
            }
            finally
            {
                Graphics.SetRenderTarget(previous); UnityEngine.Object.Destroy(sample);
                probe.Release(); UnityEngine.Object.Destroy(probe);
            }
        }
        internal FingerprintStamp BeginDust(int x, int y)
        {
            if (!ActorForeground() || _coverage == null || _captureFailed) return null;
            int left = Math.Max(0, x - _brushWidth / 2), top = Math.Max(0, y - _brushHeight / 2);
            int right = Math.Min(_coverage.Width, x + _brushWidth / 2), bottom = Math.Min(_coverage.Height, y + _brushHeight / 2);
            if (right <= left || bottom <= top) return null;
            int rx = _coverage.FlipX ? _coverage.Width - right : left, ry = _coverage.FlipY ? _coverage.Height - bottom : top;
            if (!_coverage.HasIncomplete(rx, ry, right - left, bottom - top)) { StopDust(); return null; }
            return new FingerprintStamp { Generation = _generation, X = rx, Y = ry, Width = right - left, Height = bottom - top };
        }
        private Color32[] ReadPixels(RenderTexture target, int x, int y, int width, int height)
        {
            if (_readback == null || _readback.width != width || _readback.height != height)
            { if (_readback != null) UnityEngine.Object.Destroy(_readback); _readback = new Texture2D(width, height); }
            RenderTexture previous = RenderTexture.active;
            try { Graphics.SetRenderTarget(target); _readback.ReadPixels(new Rect(x, y, width, height), 0, 0, false); return _readback.GetPixels32(); }
            finally { Graphics.SetRenderTarget(previous); }
        }
        internal void EndDust(FingerprintStamp stamp)
        {
            if (stamp == null || _owner == null || stamp.Generation != _generation || !ActorForeground()) return;
            bool added = false;
            try
            {
                Color32[] pixels = ReadPixels((RenderTexture)Render.GetValue(_owner), stamp.X, stamp.Y, stamp.Width, stamp.Height);
                for (int y = 0; y < stamp.Height; y++) for (int x = 0; x < stamp.Width; x++)
                    added |= _coverage.Commit(stamp.X + x, stamp.Y + y, pixels[y * stamp.Width + x].a);
            }
            catch { _captureFailed = true; StopDust(); throw; }
            if (added) PlayDust(); else StopDust();
        }
        private void PlayDust()
        {
            if (soundCtrl.instance == null) return;
            if (_dustAudio == null)
            {
                _audioObject = new GameObject("PwaatFingerprintPowderCue"); _audioObject.transform.SetParent(_owner.transform, false);
                _dustAudio = _audioObject.AddComponent<AudioSource>(); _dustAudio.playOnAwake = false; _dustAudio.loop = false; _dustAudio.spatialBlend = 0f;
                _dustAudio.clip = FingerprintHooks.Clip(420);
            }
            _dustAudio.volume = soundCtrl.instance.option_set_se_rate * SoundDefine.GetVolume(_dustAudio.clip);
            if (!_dustAudio.isPlaying) _dustAudio.Play();
        }
        internal void StopDust() { if (_dustAudio != null && _dustAudio.isPlaying) _dustAudio.Stop(); }
        internal void BeginBlow() { _blowing = true; StopDust(); }
        internal void BlowEvaluated(bool demonstration)
        { if (ActorForeground() && (demonstration || (bool)Clear.Invoke(_owner, null)) && soundCtrl.instance != null) soundCtrl.instance.PlaySE(139); }
        internal void EndBlow(bool demonstration, bool completed)
        {
            _blowing = false; if (!completed || _owner == null) return;
            if (_coverage != null)
            {
                RenderTexture target = (RenderTexture)Render.GetValue(_owner);
                Color32[] pixels = ReadPixels(target, 0, 0, target.width, target.height);
                for (int y = 0; y < target.height; y++) for (int x = 0; x < target.width; x++) _coverage.Commit(x, y, pixels[y * target.width + x].a);
            }
            if (demonstration || !ActorForeground()) return;
            bool clear = (bool)Clear.Invoke(_owner, null);
            if (clear) _notice = _runtime.Messages.Get("fingerprint.revealed");
            else
            {
                int index = (int)Index.GetValue(_owner); int mask = (byte)MaskId.GetValue(_rows.GetValue(index));
                MG_CHECK_INFO[] infos = (MG_CHECK_INFO[])Scores.GetValue(null);
                int threshold = (int)((float)infos[mask].ptbl[0].min_cnt * 15f);
                int percentage = FingerprintCoverage.Percentage((int)Score.GetValue(_owner), threshold, false);
                _notice = string.Format(_runtime.Messages.Get("fingerprint.progress"), percentage);
            }
            Request();
        }
        internal void ResultShown()
        { if (_owner != null) { _notice = _runtime.Messages.Get((bool)Match.GetValue(_owner) ? "fingerprint.match" : "fingerprint.no-match"); Request(); } }
        internal void Close()
        {
            _audio.Reset();
            if (!ReferenceEquals(_owner, null) && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner);
            StopDust(); if (_audioObject != null) UnityEngine.Object.Destroy(_audioObject); if (_readback != null) UnityEngine.Object.Destroy(_readback);
            _audioObject = null; _dustAudio = null; _readback = null; _coverage = null; _owner = null; _rows = null; _points = null;
            _generation++; _phase = FingerprintPhase.None; _ready = _blowing = _sceneRead = _force = _hasPosition = _captureFailed = false;
            _tutorial = _lastEdge = 0; _lastPerson = -1; _notice = null; _feedback.Clear();
        }
        /// <summary>Releases this examination's requests, readbacks and owned cue only.</summary>
        public void Dispose() { Close(); _disposed = true; }
    }
}
