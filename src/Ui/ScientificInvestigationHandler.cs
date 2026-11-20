using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class ScientificInvestigationHandler : IDisposable
    {
        private sealed class Surface { internal MeshCollider Collider; internal List<ScientificSurfaceSamples.Sample> Samples; }
        private sealed class Point { internal int Index; internal readonly List<Surface> Surfaces = new List<Surface>(); }
        private readonly UiRuntime _runtime;
        private readonly CursorFeedbackAudio _audio = new CursorFeedbackAudio();
        private readonly ScientificPointLabels _labels;
        private readonly Dictionary<Mesh, List<ScientificSurfaceSamples.Sample>> _geometry = new Dictionary<Mesh, List<ScientificSurfaceSamples.Sample>>();
        private readonly List<Point> _points = new List<Point>();
        private readonly InvestigationFeedback _feedback = new InvestigationFeedback();
        private static readonly FieldInfo Models = UiResources.Field(typeof(evidenceObjectManager), "evidences_");
        private static readonly FieldInfo Cursor = UiResources.Field(typeof(scienceInvestigationCtrl), "cursor_");
        private static readonly FieldInfo View = UiResources.Field(typeof(scienceInvestigationCtrl), "camera_");
        private static readonly FieldInfo Area = UiResources.Field(typeof(scienceInvestigationCtrl), "movable_area_");
        private static readonly FieldInfo Taboo = UiResources.Field(typeof(scienceInvestigationCtrl), "taboo_area_");
        private static readonly FieldInfo GuideLimit = UiResources.Field(typeof(scienceInvestigationCtrl), "guide_limit_");
        private static readonly FieldInfo RayAdjust = UiResources.Field(typeof(scienceInvestigationCtrl), "ray_adjust_x_");
        private static readonly FieldInfo Drag = UiResources.Field(typeof(scienceInvestigationCtrl), "is_drag_");
        private static readonly FieldInfo CursorDrag = UiResources.Field(typeof(scienceInvestigationCtrl), "is_cursor_drag_");
        private static readonly FieldInfo CursorDown = UiResources.Field(typeof(scienceInvestigationCtrl), "is_cursor_down_");
        private scienceInvestigationCtrl _owner;
        private int _object = -1, _nativeHit = int.MinValue;
        private bool _installed, _entryPending;
        internal void Installed() { _installed = true; }
        private bool _nativeRead, _nativeActive, _dirty = true, _navigating, _disposed;
        private AssetBundleSprite _cursor;
        private string _bundle, _model, _hitModel;
        internal ScientificInvestigationHandler(UiRuntime runtime)
        {
            _runtime = runtime;
            _labels = new ScientificPointLabels(UiResources.EmbeddedXml("Pwaat.Accessibility.SciencePointLabels.xml"));
        }
        internal bool CanInspect()
        {
            scienceInvestigationCtrl o = _owner;
            return _installed && !_disposed && Application.isFocused && o != null && o == scienceInvestigationCtrl.instance
                && o.is_play && o.is_play_ready && o.active && o.body != null && o.body.activeInHierarchy && o.active_cursor && !o.active_message
                && !o.called_check && !o.called_back && !o.called_present && !o.is_ending_failed
                && o.evidence_manager != null && !o.evidence_manager.busy_request
                && (_runtime.Assistance == null || !_runtime.Assistance.BlocksInput)
                && (autoPlayCtrl.instance == null || !autoPlayCtrl.instance.isStoryMode())
                && (optionCtrl.instance == null || !optionCtrl.instance.is_open)
                && (SaveLoadUICtrl.instance == null || !SaveLoadUICtrl.instance.is_open)
                && (_runtime.StoryUi == null || !_runtime.StoryUi.BoxBlocks())
                && fadeCtrl.instance != null && fadeCtrl.instance.status == fadeCtrl.Status.NO_FADE
                && keyGuideCtrl.instance != null && keyGuideCtrl.instance.active && !keyGuideCtrl.instance.CheckOpen() && !keyGuideCtrl.instance.CheckClose()
                && (keyGuideCtrl.instance.current_guide == keyGuideBase.Type.KAGAKU_SYOUSAI
                    || keyGuideCtrl.instance.current_guide == keyGuideBase.Type.FORCE_KAGAKU_SYOUSAI
                    || keyGuideCtrl.instance.current_guide == keyGuideBase.Type.PRESENT_KAGAKU_SYOUSAI
                    || keyGuideCtrl.instance.current_guide == keyGuideBase.Type.ENDING_KAGAKU_SYOUSAI)
                && !(bool)Drag.GetValue(o) && !(bool)CursorDrag.GetValue(o) && !(bool)CursorDown.GetValue(o);
        }
        internal string Page { get { return CanInspect() ? "science:" + _object : null; } }
        internal void Ready(scienceInvestigationCtrl owner)
        {
            if (_disposed || owner == null) return;
            if (_owner != owner) { Close(); _entryPending = true; }
            _owner = owner; _dirty = true; _nativeHit = int.MinValue; _nativeActive = false; _cursor = (AssetBundleSprite)Cursor.GetValue(owner); _nativeRead = false; _feedback.Clear();
            Request();
        }
        internal void Committed(scienceInvestigationCtrl owner)
        {
            if (_disposed || _owner != owner || owner == null || _navigating) return;
            bool active = owner.active_cursor && !owner.active_message && !owner.called_check && !owner.called_back && !owner.called_present;
            int hit = owner.hit_point_index;
            bool read = hit >= 0 && _cursor != null && _cursor.sprite_no_ == 3;
            bool changed = hit != _nativeHit || read != _nativeRead || active != _nativeActive;
            if (active && CanInspect()) _audio.Commit(owner, _cursor.transform.localPosition, hit < 0 ? null : hit.ToString()); else _audio.Reset();
            _nativeHit = hit; _nativeRead = read; _nativeActive = active;
            if (changed) Request();
        }
        internal void ModelChanged(evidenceObjectManager manager)
        {
            if (_owner == null || _owner.evidence_manager != manager) return;
            _dirty = true; _feedback.Clear(); _audio.Reset(); _points.Clear(); _geometry.Clear(); _nativeHit = int.MinValue; Request();
        }
        private void Request()
        {
            scienceInvestigationCtrl o = _owner;
            if (o == null || _runtime.Presentation == null) return;
            _runtime.Presentation.ReadUi(o, delegate { if (_owner == o && CanInspect()) Read(); });
        }
        private void EnsurePoints()
        {
            if (!_dirty) return;
            _points.Clear();
            _object = -1; _bundle = _model = _hitModel = null;
            List<StModelSet> models = (List<StModelSet>)Models.GetValue(_owner.evidence_manager);
            StModelSet? active = null;
            foreach (StModelSet model in models) { if (model.model_ != null && model.active) { active = model; break; } }
            if (!active.HasValue) return;
            StModelSet current = active.Value; _object = current.obj_id;
            _bundle = polyDataCtrl.instance.obj_table[_object].common_name;
            _model = current.model_.name; _hitModel = current.hit_ == null ? string.Empty : current.hit_.name;
            Dictionary<int, Point> points = new Dictionary<int, Point>();
            AddSurfaces(current.model_, points); AddSurfaces(current.hit_, points);
            _points.AddRange(points.Values);
            // Stable top-to-bottom ordering in the evidence's current native model coordinates, independent of the player's camera rotation.
            Transform operate = _owner.evidence_manager.get_operate_trans;
            _points.Sort(delegate (Point a, Point b) { Vector3 x = Center(a, operate), y = Center(b, operate); int order = y.y.CompareTo(x.y); if (order == 0) order = x.x.CompareTo(y.x); return order == 0 ? a.Index.CompareTo(b.Index) : order; });
            _dirty = false;
            _runtime.Trace("science_points", "object=" + _object + " points=" + _points.Count);
        }
        private void AddSurfaces(GameObject root, Dictionary<int, Point> points)
        {
            if (root == null || !root.activeInHierarchy) return;
            foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>())
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                int index = ScientificPointState.ColliderIndex(collider.transform.name);
                Mesh mesh = collider.sharedMesh;
                if (index < 0 || mesh == null) continue;
                Point point; if (!points.TryGetValue(index, out point)) { point = new Point { Index = index }; points.Add(index, point); }
                List<ScientificSurfaceSamples.Sample> samples;
                if (!_geometry.TryGetValue(mesh, out samples))
                {
                    samples = ScientificSurfaceSamples.Read(mesh);
                    _geometry.Add(mesh, samples);
                }
                point.Surfaces.Add(new Surface { Collider = collider, Samples = samples });
            }
        }
        private static Vector3 Center(Point point, Transform operate)
        {
            Vector3 total = Vector3.zero; int count = 0;
            foreach (Surface s in point.Surfaces) if (s.Collider != null) { total += operate.InverseTransformPoint(s.Collider.transform.TransformPoint(s.Collider.sharedMesh.bounds.center)); count++; }
            return count == 0 ? Vector3.zero : total / count;
        }
        private string PointText(int hit, bool read)
        {
            if (hit < 0) return _runtime.Messages.Get("science.outside");
            Vector3 position = ((AssetBundleSprite)Cursor.GetValue(_owner)).transform.localPosition;
            string[] keys = { "tl", "tc", "tr", "ml", "mc", "mr", "bl", "bc", "br" };
            int column = Mathf.Min(2, (int)(Mathf.Clamp01((position.x + 960f) / 1920f) * 3f));
            int row = Mathf.Min(2, (int)(Mathf.Clamp01((540f - position.y) / 1080f) * 3f));
            string text = _points.Exists(p => p.Index == hit) ? _labels.Get(_object, _bundle, _model, _hitModel, hit, UiResources.Culture()) : null;
            if (string.IsNullOrEmpty(text)) text = string.Format(_runtime.Messages.Get("science.point"), _runtime.Messages.Get("investigation.position." + keys[row * 3 + column]));
            return read ? string.Format(_runtime.Messages.Get("science.examined"), text) : text;
        }
        private void Read()
        {
            EnsurePoints();
            _audio.Commit(_owner, _cursor.transform.localPosition, _owner.hit_point_index < 0 ? null : _owner.hit_point_index.ToString());
            string text = PointText(_owner.hit_point_index, _nativeRead);
            bool changed = _feedback.Focus(_object + ":" + _owner.hit_point_index, _nativeRead ? 1 : 0);
            if (_entryPending) { text = _runtime.Messages.Get("science.title") + "\n" + text; _entryPending = false; changed = true; }
            if (changed)
            { _runtime.Trace("science_focus", "object=" + _object + " hit=" + _owner.hit_point_index + " read=" + _nativeRead); _runtime.Presentation.Ui(_owner, text); }
        }
        internal void Repeat()
        { if (CanInspect()) { _feedback.Clear(); Request(); } }
        internal void Navigate(int direction)
        {
            if (!CanInspect()) return;
            EnsurePoints();
            if (_points.Count == 0) { _runtime.Speech.Say(_runtime.Messages.Get("science.no-points"), false); return; }
            int actualHit = ScientificInvestigationHooks.SelectingIndex(_owner);
            int found = _points.FindIndex(p => p.Index == actualHit);
            Point target = _points[ScientificPointState.Next(found, direction, _points.Count)];
            evidenceObjectManager manager = _owner.evidence_manager;
            AssetBundleSprite cursor = (AssetBundleSprite)Cursor.GetValue(_owner);
            Transform operate = manager.get_operate_trans;
            Vector3 originalPosition = operate.position, originalAngles = operate.eulerAngles, originalCursor = cursor.transform.localPosition;
            float originalScale = manager.scale_ratio;
            Camera view = (Camera)View.GetValue(_owner);
            int queries = 0; bool success = false;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            _navigating = true;
            try
            {
                // Native exclusions/occlusion may leave a region unreachable. Do not trap repeated next/previous on it.
                for (int offset = 0; offset < _points.Count && !success; offset++)
                {
                    target = _points[ScientificPointState.Next(found, direction, _points.Count)];
                    success = TryTarget(target, view, cursor, manager, originalPosition, originalAngles, originalScale, ref queries);
                    found = _points.IndexOf(target);
                }
            }
            finally
            {
                if (!success) { SetPosture(manager, originalPosition, originalAngles, originalScale); cursor.transform.localPosition = originalCursor; Physics.SyncTransforms(); }
                _navigating = false;
                _runtime.Trace("science_navigation", "object=" + _object + " target=" + target.Index + " accepted=" + success + " queries=" + queries + " milliseconds=" + ((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.###", CultureInfo.InvariantCulture));
            }
            if (!success) _runtime.Speech.Say(_runtime.Messages.Get("science.point-unavailable"), false);
            else { _feedback.Clear(); _nativeHit = int.MinValue; }
            // The next native ChangeGuideIconSprite commit supplies final hit/read state. No callback, confirmation, or timed retry is synthesized.
        }
        private bool TryTarget(Point target, Camera view, AssetBundleSprite cursor, evidenceObjectManager manager, Vector3 position, Vector3 angles, float scale, ref int queries)
        {
            SetPosture(manager, position, angles, scale);
            // Try every sampled interior in the player's current view before changing its rotation.
            foreach (Surface surface in target.Surfaces)
            {
                if (!Valid(surface)) continue;
                if (TryCursor(view, cursor, surface.Collider.transform.TransformPoint(surface.Collider.sharedMesh.bounds.center), target.Index, ref queries)) return true;
                for (int i = 0; i < Math.Min(48, surface.Samples.Count); i++)
                    if (TryCursor(view, cursor, surface.Collider.transform.TransformPoint(surface.Samples[i].Position), target.Index, ref queries)) return true;
            }
            foreach (Surface surface in target.Surfaces)
            {
                if (!Valid(surface)) continue;
                for (int i = 0; i < Math.Min(48, surface.Samples.Count); i++)
                {
                    SetPosture(manager, position, angles, scale);
                    Vector3 normal = manager.get_operate_trans.InverseTransformDirection(surface.Collider.transform.TransformDirection(surface.Samples[i].Normal)).normalized;
                    // Unity Euler applies X before Y when Z is zero. Solve that order against the actual source normal.
                    float pitch = Mathf.Atan2(-normal.y, -normal.z) * Mathf.Rad2Deg;
                    float yaw = Mathf.Atan2(normal.x, Mathf.Sqrt(normal.y * normal.y + normal.z * normal.z)) * Mathf.Rad2Deg;
                    Quaternion rotation = view.transform.rotation * Quaternion.Euler(pitch, yaw, 0f);
                    SetPosture(manager, position, rotation.eulerAngles, scale);
                    if (TryCursor(view, cursor, surface.Collider.transform.TransformPoint(surface.Samples[i].Position), target.Index, ref queries)) return true;
                }
            }
            return false;
        }
        private static bool Valid(Surface surface)
        { return surface.Collider != null && surface.Collider.enabled && surface.Collider.gameObject.activeInHierarchy; }
        private static void SetPosture(evidenceObjectManager manager, Vector3 position, Vector3 angles, float scale)
        {
            System.Collections.IEnumerator request = manager.RequestTo(position, angles, scale, 0f);
            try { if (!request.MoveNext()) throw new InvalidOperationException("Native posture commit missing."); }
            finally { IDisposable disposable = request as IDisposable; if (disposable != null) disposable.Dispose(); }
        }
        private bool TryCursor(Camera view, AssetBundleSprite cursor, Vector3 target, int index, ref int queries)
        {
            Vector3 screen = view.WorldToScreenPoint(target);
            if (screen.z <= 0f) return false;
            screen.x -= (float)RayAdjust.GetValue(_owner);
            Ray ray = view.ScreenPointToRay(screen);
            Plane plane = new Plane(cursor.transform.parent.TransformDirection(Vector3.forward), cursor.transform.position);
            float distance; if (!plane.Raycast(ray, out distance)) return false;
            Vector3 local = cursor.transform.parent.InverseTransformPoint(ray.GetPoint(distance));
            Rect area = ((RectTransform)Area.GetValue(_owner)).rect;
            Rect taboo = (Rect)Taboo.GetValue(_owner);
            if (local.x < area.x || local.x > area.width / 2f || local.y < area.y || local.y > area.height / 2f
                || local.y < taboo.y && local.y > taboo.y - taboo.height && local.x > (float)GuideLimit.GetValue(_owner) && local.x < taboo.x + taboo.width) return false;
            local.z = cursor.transform.localPosition.z; cursor.transform.localPosition = local;
            Physics.SyncTransforms(); queries++;
            return ScientificInvestigationHooks.SelectingIndex(_owner) == index;
        }
        internal void Close()
        {
            _audio.Reset();
            if (_owner != null && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner);
            _owner = null; _points.Clear(); _geometry.Clear(); _object = -1; _bundle = _model = _hitModel = null; _dirty = true; _cursor = null;
            _nativeHit = int.MinValue; _nativeActive = false; _entryPending = false; _feedback.Clear();
        }
        public void Dispose() { Close(); _disposed = true; }
    }
}
