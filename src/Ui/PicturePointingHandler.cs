using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class PicturePointingHandler : IDisposable
    {
        private sealed class Region
        {
            internal string Identity;
            internal readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal);
            internal Rect Bounds;
            internal Vector2 Anchor;
            internal bool Dynamic, Available;
            internal Region Source;
        }
        private sealed class Picture
        {
            internal string Key;
            internal readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly List<Region> Regions = new List<Region>();
        }
        private sealed class Layer
        {
            internal readonly List<Region> Regions = new List<Region>();
            internal readonly List<string> SuppressedRegions = new List<string>();
            internal readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
        }
        private readonly UiRuntime _runtime;
        private readonly CursorFeedbackAudio _audio = new CursorFeedbackAudio();
        private readonly Dictionary<string, Picture> _pictures = new Dictionary<string, Picture>(StringComparer.Ordinal);
        private readonly Dictionary<string, Region> _icons = new Dictionary<string, Region>(StringComparer.Ordinal);
        private readonly Dictionary<string, Layer> _seals = new Dictionary<string, Layer>(StringComparer.Ordinal);
        private readonly List<Region> _regions = new List<Region>();
        private readonly List<PicturePointingArea> _areas = new List<PicturePointingArea>();
        private PointMiniGame _owner;
        private MiniGameCursor _cursor;
        private SpriteRenderer _background;
        private Picture _picture;
        private Dictionary<string, string> _descriptionOverride;
        private string _focus, _focusText, _context;
        private bool _ready, _intro, _dirty, _disposed;
        private int _generation;
        private int _siteki;
        private Vector3 _lastPosition;

        internal PicturePointingHandler(UiRuntime runtime)
        {
            _runtime = runtime;
            XmlDocument document = UiResources.EmbeddedXml("Pwaat.Accessibility.PicturePointing.xml");
            foreach (XmlElement image in document.SelectNodes("/picturePointing/image"))
            {
                Picture picture = new Picture { Key = Key(image.GetAttribute("title"), image.GetAttribute("background"), image.GetAttribute("sprite")) };
                ReadLanguages(image, "description", picture.Descriptions);
                foreach (XmlElement row in image.SelectNodes("region"))
                {
                    XmlElement rect = (XmlElement)row.SelectSingleNode("rect");
                    Region region = new Region { Identity = row.GetAttribute("id"), Bounds = Rect.MinMaxRect(Number(rect, "x0"), Number(rect, "y0"), Number(rect, "x1"), Number(rect, "y1")), Anchor = new Vector2(Number(rect, "x"), Number(rect, "y")) };
                    ReadLanguages(row, "text", region.Names); picture.Regions.Add(region);
                }
                _pictures.Add(picture.Key, picture);
            }
            foreach (XmlElement icon in document.SelectNodes("/picturePointing/icon"))
            {
                Region region = ReadRegion(icon);
                _icons.Add(Key(icon.GetAttribute("title"), icon.GetAttribute("nativeId"), icon.GetAttribute("sprite")), region);
            }
            foreach (XmlElement seal in document.SelectNodes("/picturePointing/seal"))
            {
                Layer layer = new Layer();
                foreach (XmlElement row in seal.SelectNodes("region")) layer.Regions.Add(ReadRegion(row));
                foreach (XmlElement row in seal.SelectNodes("suppress")) layer.SuppressedRegions.Add(row.GetAttribute("region"));
                ReadLanguages(seal, "description", layer.Descriptions);
                _seals.Add(Key(seal.GetAttribute("title"), seal.GetAttribute("background"), seal.GetAttribute("nativeId")) + ":" + seal.GetAttribute("sprite"), layer);
            }
            if (_pictures.Count == 0) throw new InvalidDataException("Picture pointing resources are empty.");
        }

        // This lightweight state predicate is also used while the help window has paused advCtrl.
        internal bool IsReady { get { return _ready && !_disposed && _owner != null && _owner == PointMiniGame.instance && _owner.is_running && _cursor != null && _cursor == MiniGameCursor.instance; } }
        internal string Page { get { return IsReady ? "picture-pointing:" + _context + ":" + _siteki : null; } }
        private string _lastBlocked;
        private bool Reject(string reason)
        {
            if (_lastBlocked != reason)
            {
                _lastBlocked = reason;
                _runtime.Trace("picture_pointing", "stage=blocked reason=" + reason + " siteki=" + _siteki);
            }
            return false;
        }
        private bool CanPoint(bool navigating = false)
        {
            if (!IsReady) return false;
            if (!_owner.gameObject.activeInHierarchy || !Application.isFocused || _runtime.Presentation == null || !_runtime.Presentation.Foreground()) return Reject("foreground");
            if ((_runtime.Assistance != null && _runtime.Assistance.BlocksInput) || (recordListCtrl.instance != null && recordListCtrl.instance.is_open)) return Reject("overlay");
            SubWindow window = advCtrl.instance.sub_window_;
            string nativeBlock = NativePointBlockReason(window);
            if (nativeBlock != null) return Reject(nativeBlock);
            if (fadeCtrl.instance == null || fadeCtrl.instance.status != fadeCtrl.Status.NO_FADE || bgCtrl.instance == null || bgCtrl.instance.is_slider) return Reject("visual-transition");
            if (navigating && ((bool)NativeFields.Get(_owner, "is_cursor_lock_") || NativeFields.Number(_owner, "touch_state_") != 0)) return Reject("native-drag");
            _lastBlocked = null; return true;
        }
        // Proc_Point retains the POINT request while phase 1 owns the running minigame.
        // NONE is also valid; another request must never inherit this input permission.
        internal static string NativePointBlockReason(SubWindow window)
        {
            if (window == null) return "native-window-missing";
            Routine routine = window.GetCurrentRoutine();
            if (routine == null) return "native-routine-missing";
            string reason;
            if (routine.r.no_0 != (int)SubWindow.State.POINT || routine.r.no_1 != 1) reason = "native-point-routine";
            else if (window.busy_ != 0) reason = "native-point-busy";
            else if (window.req_ != SubWindow.Req.NONE && window.req_ != SubWindow.Req.POINT) reason = "native-point-request";
            else return null;
            return reason + " routine=" + routine.r.no_0 + ":" + routine.r.no_1 + " busy=" + window.busy_ + " req=" + window.req_;
        }
        internal void GuideReady(keyGuideBoard guide, keyGuideBase.Type type)
        {
            if (guide != keyGuideCtrl.instance || (type != keyGuideBase.Type.POINT && type != keyGuideBase.Type.POINT_SLIDE && type != keyGuideBase.Type.POINT_TANTEI)) return;
            if (!IsReady) return;
            // Re-read once at this real input/guide commit; never retry on a clock.
            _runtime.Trace("picture_pointing", "stage=guide-ready siteki=" + _siteki); Request();
        }
        internal void Ready()
        {
            Close(); _owner = PointMiniGame.instance; _cursor = MiniGameCursor.instance;
            if (_owner == null || !_owner.is_running || _cursor == null) return;
            _ready = true; _intro = true; _dirty = true; _lastPosition = _cursor.cursor_position;
            _siteki = GSStatic.tantei_work_.siteki_no;
            _runtime.Trace("picture_pointing", "stage=native-ready siteki=" + _siteki + " part=" + GSStatic.global_work_.r.no_0 + " mode=" + GSStatic.global_work_.r.no_1 + " bar=" + advCtrl.instance.sub_window_.bar_req_);
            Request();
        }
        internal void VisualChanged()
        {
            if (!IsReady) return;
            _dirty = true; Request();
        }
        internal void CursorCommitted(Vector3 previous)
        {
            if (!IsReady) return;
            Vector3 actual = _cursor.cursor_position;
            if (actual.x == previous.x && actual.y == previous.y && actual.x == _lastPosition.x && actual.y == _lastPosition.y) return;
            _lastPosition = actual; Request();
        }
        private void Request()
        {
            if (!IsReady || _runtime.Presentation == null) return;
            PointMiniGame owner = _owner; int generation = _generation;
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (_generation != generation || _owner != owner) return;
                if (CanPoint()) Read(); else _audio.Reset();
            });
        }
        private SpriteRenderer Background() { return bgCtrl.instance == null ? null : (SpriteRenderer)NativeFields.Get(bgCtrl.instance, "sprite_renderer_"); }
        private string CurrentContext()
        {
            SpriteRenderer image = Background();
            if (image == null || image.sprite == null || !image.enabled || !image.gameObject.activeInHierarchy || image.color.a <= 0f) return null;
            return Key(((int)GSStatic.global_work_.title).ToString(CultureInfo.InvariantCulture), bgCtrl.instance.bg_no.ToString(CultureInfo.InvariantCulture), image.sprite.name);
        }
        private void Refresh()
        {
            string context = CurrentContext();
            if (context != _context) { _audio.Reset(); _context = context; _intro = true; _dirty = true; _focus = null; }
            if (!_dirty) return;
            _dirty = false; _regions.Clear(); _areas.Clear(); _picture = null; _descriptionOverride = null; _background = Background();
            if (context == null || _background == null || !_pictures.TryGetValue(context, out _picture)) return;
            foreach (Region source in _picture.Regions)
            {
                Rect rect = source.Bounds;
                Vector2 p0 = Project(_background, rect.xMin, rect.yMin), p1 = Project(_background, rect.xMax, rect.yMax);
                Region region = new Region { Identity = source.Identity, Source = source, Bounds = Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y)), Anchor = Project(_background, source.Anchor.x, source.Anchor.y) };
                if (region.Bounds.xMax < 0f || region.Bounds.yMax < 0f || region.Bounds.xMin > _cursor.cursor_area_size.x || region.Bounds.yMin > _cursor.cursor_area_size.y) continue;
                _regions.Add(region);
            }
            AddIcons();
            AddSeals();
            _areas.Clear();
            foreach (Region region in _regions) _areas.Add(new PicturePointingArea { Left = region.Bounds.xMin, Top = region.Bounds.yMin, Right = region.Bounds.xMax, Bottom = region.Bounds.yMax, X = region.Anchor.x, Y = region.Anchor.y, Dynamic = region.Dynamic });
            for (int i = 0; i < _regions.Count; i++) FindAnchor(i);
            _runtime.Trace("picture_pointing", "stage=snapshot context=" + context + " siteki=" + _siteki + " regions=" + _regions.Count);
        }
        private Vector2 Project(SpriteRenderer image, float x, float y)
        {
            Sprite sprite = image.sprite;
            // Coordinates in the reviewed resource are normalized to the actual sprite texture, from top left.
            float pixelX = x * sprite.rect.width / 1000f, pixelY = (1000f - y) * sprite.rect.height / 1000f;
            Vector3 local = new Vector3((pixelX - sprite.pivot.x) / sprite.pixelsPerUnit, (pixelY - sprite.pivot.y) / sprite.pixelsPerUnit, 0f);
            if (image.flipX) local.x = -local.x;
            if (image.flipY) local.y = -local.y;
            Vector3 screen = systemCtrl.instance.system_camera.WorldToScreenPoint(image.transform.TransformPoint(local));
            RectTransform parent = (RectTransform)NativeFields.Get(_cursor, "cursor_parent_");
            Vector2 point;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, _cursor.canvas.worldCamera, out point))
                throw new InvalidDataException("Picture location cannot be projected onto the native cursor canvas.");
            // Match MiniGameCursor.UpdateTouchCursorPosition exactly, including its downward Y axis.
            return new Vector2(point.x, -point.y);
        }
        private void AddIcons()
        {
            if (GSMapIcon.instance == null || GSStatic.expl_char_work_ == null) return;
            SpriteRenderer[] icons = (SpriteRenderer[])NativeFields.Get(GSMapIcon.instance, "icons_");
            if (icons == null) return;
            ExplCharData[] data = GSStatic.expl_char_work_.expl_char_data_;
            for (int i = 0; i < icons.Length && i < data.Length; i++)
            {
                SpriteRenderer image = icons[i];
                if (image == null || image.sprite == null || !image.gameObject.activeInHierarchy || image.color.a <= 0f || data[i].id == 255) continue;
                if (!image.enabled && (data[i].blink == 0 || (data[i].status & 4) != 0)) continue;
                string key = Key(((int)GSStatic.global_work_.title).ToString(CultureInfo.InvariantCulture), data[i].id.ToString(CultureInfo.InvariantCulture), image.sprite.name);
                Region source;
                if (!_icons.TryGetValue(key, out source)) { _runtime.Trace("picture_pointing_icon", "unreviewed=" + key); continue; }
                AddVisualRegion(image, source, "icon:" + i + ":" + data[i].id);
            }
        }
        internal string RecordBackgroundLayer(int background, Sprite sprite, out string markersText, out string[] hiddenLabels)
        {
            markersText = null;
            hiddenLabels = new string[0];
            if (bgCtrl.instance == null || bgCtrl.instance.bg_no != background || sprite == null) { return null; }
            Picture picture;
            if (!_pictures.TryGetValue(Key(((int)GSStatic.global_work_.title).ToString(CultureInfo.InvariantCulture), background.ToString(CultureInfo.InvariantCulture), sprite.name), out picture)) { return null; }
            Dictionary<string, string> replacement = null;
            List<string> hidden = new List<string>();
            List<string> markers = new List<string>();
            System.Collections.IList layers = (System.Collections.IList)NativeFields.Get(bgCtrl.instance, "seal_list_");
            if (layers != null) foreach (bgCtrl.SealData native in layers)
            {
                SpriteRenderer image = native.sprite_;
                if (!native.active || image == null || !image.enabled || !image.gameObject.activeInHierarchy || image.color.a <= 0f || image.sprite == null) { continue; }
                string key = Key(((int)GSStatic.global_work_.title).ToString(CultureInfo.InvariantCulture), background.ToString(CultureInfo.InvariantCulture), native.id_.ToString(CultureInfo.InvariantCulture)) + ":" + image.sprite.name;
                Layer layer; if (!_seals.TryGetValue(key, out layer)) { continue; }
                foreach (string id in layer.SuppressedRegions)
                {
                    Region region = picture.Regions.Find(delegate (Region value) { return value.Identity == id; });
                    if (region != null) { string label = Language(region.Names); int suffix = label.IndexOf("（", StringComparison.Ordinal); if (suffix < 0) { suffix = label.IndexOf(" (", StringComparison.Ordinal); } hidden.Add(suffix < 0 ? label : label.Substring(0, suffix)); }
                }
                if (layer.Descriptions.Count > 0) { replacement = layer.Descriptions; }
                foreach (Region region in layer.Regions) { string text = Name(region); if (!markers.Contains(text)) { markers.Add(text); } }
            }
            hiddenLabels = hidden.ToArray();
            markersText = markers.Count == 0 ? null : string.Format(_runtime.Messages.Get("picture-pointing.markers"), string.Join("; ", markers.ToArray()));
            // The record owns the base description. Only an audited cover layer replaces it.
            return replacement == null ? null : Language(replacement);
        }
        private void AddSeals()
        {
            if (bgCtrl.instance == null) return;
            System.Collections.IList layers = (System.Collections.IList)NativeFields.Get(bgCtrl.instance, "seal_list_");
            if (layers == null) return;
            foreach (bgCtrl.SealData native in layers)
            {
                SpriteRenderer image = native.sprite_;
                if (!native.active || image == null || !image.enabled || !image.gameObject.activeInHierarchy || image.color.a <= 0f || image.sprite == null) continue;
                string key = Key(((int)GSStatic.global_work_.title).ToString(CultureInfo.InvariantCulture), bgCtrl.instance.bg_no.ToString(CultureInfo.InvariantCulture), native.id_.ToString(CultureInfo.InvariantCulture)) + ":" + image.sprite.name;
                Layer layer;
                if (!_seals.TryGetValue(key, out layer)) { _runtime.Trace("picture_pointing_seal", "unreviewed=" + key); continue; }
                // Opaque replacement patches can hide a base object without introducing a new marker.
                foreach (string hidden in layer.SuppressedRegions)
                    _regions.RemoveAll(region => !region.Dynamic && region.Source != null && region.Source.Identity == hidden);
                if (layer.Descriptions.Count > 0) _descriptionOverride = layer.Descriptions;
                foreach (Region source in layer.Regions) AddVisualRegion(image, source, "seal:" + source.Identity);
                _runtime.Trace("picture_pointing_seal", "matched=" + key + " regions=" + layer.Regions.Count);
            }
        }
        private void AddVisualRegion(SpriteRenderer image, Region source, string identity)
        {
            Rect rect = source.Bounds;
            Vector2 p0 = Project(image, rect.xMin, rect.yMin), p1 = Project(image, rect.xMax, rect.yMax);
            Rect bounds = Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y));
            if (bounds.xMax < 0f || bounds.yMax < 0f || bounds.xMin > _cursor.cursor_area_size.x || bounds.yMin > _cursor.cursor_area_size.y) return;
            Region region = new Region { Identity = identity, Source = source, Dynamic = true, Bounds = bounds, Anchor = Project(image, source.Anchor.x, source.Anchor.y) };
            // Several replacement tiles can draw the same visible symbol; expose one semantic location.
            _regions.RemoveAll(existing => existing.Dynamic && existing.Identity == identity);
            _regions.Add(region);
        }
        private string Name(Region region) { return Language(region.Source == null ? region.Names : region.Source.Names); }
        private void Read()
        {
            Refresh();
            Region audioRegion = Hit(_cursor.cursor_position);
            _audio.Commit(_owner, _cursor.cursor_position, audioRegion == null ? null : audioRegion.Identity);
            if (_intro)
            {
                List<string> text = new List<string>();
                text.Add(_runtime.Messages.Get("picture-pointing.title"));
                text.Add(_picture == null ? _runtime.Messages.Get("picture-pointing.unknown-picture") : Language(_descriptionOverride ?? _picture.Descriptions));
                List<string> markers = new List<string>();
                foreach (Region region in _regions) if (region.Dynamic) markers.Add(Name(region) + " (" + Location(region.Anchor) + ")");
                if (markers.Count > 0) text.Add(string.Format(_runtime.Messages.Get("picture-pointing.markers"), string.Join("; ", markers.ToArray())));
                Region focus = Hit(_cursor.cursor_position); _focus = focus == null ? "outside" : focus.Identity;
                _focusText = focus == null ? _runtime.Messages.Get("picture-pointing.outside") : Name(focus);
                text.Add(_focusText);
                _intro = false;
                _runtime.Trace("picture_pointing", "stage=intro context=" + _context + " siteki=" + _siteki + " regions=" + _regions.Count);
                _runtime.Presentation.Ui(_owner, NativeFields.Join(text.ToArray()));
                return;
            }
            Region next = Hit(_cursor.cursor_position); string identity = next == null ? "outside" : next.Identity;
            string spoken = next == null ? _runtime.Messages.Get("picture-pointing.outside") : Name(next);
            if (_focus == identity && _focusText == spoken) return;
            _focus = identity; _focusText = spoken; _runtime.Presentation.Ui(_owner, spoken);
            _runtime.Trace("picture_pointing_focus", "context=" + _context + " region=" + identity + " x=" + _cursor.cursor_position.x + " y=" + _cursor.cursor_position.y);
        }
        private string Location(Vector2 p)
        {
            Vector2 size = _cursor.cursor_area_size;
            int x = Mathf.Clamp((int)(p.x * 3f / size.x), 0, 2), y = Mathf.Clamp((int)(p.y * 3f / size.y), 0, 2);
            return _runtime.Messages.Get("investigation.position." + new[] { "tl", "tc", "tr", "ml", "mc", "mr", "bl", "bc", "br" }[y * 3 + x]);
        }
        private Region Hit(Vector2 p)
        {
            int index = PicturePointingGeometry.Hit(_areas, p.x, p.y);
            return index < 0 ? null : _regions[index];
        }
        private bool Within(Vector2 point)
        {
            Rect limit = (Rect)NativeFields.Get(_cursor, "cursor_limit_"); Vector2 excluded = _cursor.cursor_exception_limit;
            return PicturePointingGeometry.Within(point.x, point.y, limit.x, limit.y, limit.width, limit.height, excluded.x, excluded.y);
        }
        private void FindAnchor(int index)
        {
            float x, y; Region region = _regions[index];
            region.Available = PicturePointingGeometry.FindAnchor(_areas, index, (px, py) => Within(new Vector2(px, py)), out x, out y);
            if (region.Available) region.Anchor = new Vector2(x, y);
        }
        internal void Navigate(int direction)
        {
            if (!CanPoint(true) || direction == 0) return;
            Refresh();
            List<Region> available = new List<Region>();
            foreach (Region region in _regions) if (region.Available) available.Add(region);
            if (available.Count == 0) { _runtime.Speech.Say(_runtime.Messages.Get("picture-pointing.unavailable"), false); return; }
            Region current = Hit(_cursor.cursor_position); int index = current == null ? -1 : available.IndexOf(current);
            if (index < 0) index = direction > 0 ? -1 : 0;
            Region target = available[PicturePointingGeometry.Next(index, available.Count, direction)];
            // This property is the same native commit used by touch and the entry animation. It does not confirm.
            _cursor.cursor_position = new Vector3(target.Anchor.x, target.Anchor.y, 0f);
            _lastPosition = _cursor.cursor_position;
            if (!ReferenceEquals(Hit(_lastPosition), target)) { _runtime.Speech.Say(_runtime.Messages.Get("picture-pointing.unavailable"), false); return; }
            _focus = null; Request();
        }
        internal void Repeat()
        {
            if (!CanPoint()) return;
            _intro = true; Request();
        }
        internal void Close()
        {
            _audio.Reset();
            if (_owner != null && _runtime.Presentation != null) _runtime.Presentation.ClearUi(_owner);
            _lastBlocked = null; _generation++; _ready = false; _owner = null; _cursor = null; _picture = null; _descriptionOverride = null; _context = null; _focus = null; _focusText = null; _regions.Clear(); _areas.Clear();
        }
        /// <summary>Clears only this module’s pending presentation and visual identities.</summary>
        public void Dispose() { _disposed = true; Close(); _pictures.Clear(); _icons.Clear(); _seals.Clear(); }
        private static Region ReadRegion(XmlElement row)
        {
            XmlElement rect = (XmlElement)row.SelectSingleNode("rect");
            Region region = new Region { Identity = row.GetAttribute("id"), Bounds = Rect.MinMaxRect(Number(rect, "x0"), Number(rect, "y0"), Number(rect, "x1"), Number(rect, "y1")), Anchor = new Vector2(Number(rect, "x"), Number(rect, "y")) };
            ReadLanguages(row, "text", region.Names);
            return region;
        }
        private static string Key(string title, string background, string sprite) { return title + ":" + background + ":" + sprite; }
        private static float Number(XmlElement element, string name) { return float.Parse(element.GetAttribute(name), CultureInfo.InvariantCulture); }
        private static void ReadLanguages(XmlElement owner, string element, Dictionary<string, string> result)
        { foreach (XmlElement row in owner.SelectNodes(element)) { if (row.InnerText.Trim().Length == 0) throw new InvalidDataException("Empty picture pointing text."); result.Add(row.GetAttribute("culture"), row.InnerText); } }
        private static string Language(Dictionary<string, string> values)
        { string result; if (!values.TryGetValue(UiResources.Culture(), out result)) throw new InvalidDataException("Missing picture pointing culture."); return result; }
    }
}
