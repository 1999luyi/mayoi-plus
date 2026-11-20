using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class CourtRecordImages : IDisposable
    {
        private readonly UiRuntime _runtime;
        private readonly Dictionary<string, string> _descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _originals = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _hints = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _hintMessages = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _empty = new HashSet<string>(StringComparer.Ordinal);
        private recordDetailCtrl _detail;
        private recordListCtrl _record;
        private int _entryNote = -1;
        private int _title = -1;
        private int _page;
        private int _generation;
        private bool _ready;
        internal CourtRecordImages(UiRuntime runtime)
        {
            _runtime = runtime;
            XmlDocument document = UiResources.EmbeddedXml("Pwaat.Accessibility.CourtRecordImages.xml");
            foreach (XmlElement row in document.SelectNodes("/courtRecordImages/image"))
            {
                string key = row.GetAttribute("path") + row.GetAttribute("bundle") + ":" + row.GetAttribute("sprite");
                if (row.GetAttribute("empty") == "true") { _empty.Add(key); }
                foreach (XmlElement text in row.SelectNodes("description"))
                { _descriptions.Add(key + ":" + text.GetAttribute("culture") + ":" + row.GetAttribute("note"), text.InnerText); }
                XmlElement original = row.SelectSingleNode("original") as XmlElement;
                if (original != null) { _originals.Add(key, original.InnerText); }
            }
            foreach (XmlElement row in document.SelectNodes("/courtRecordImages/entity/text"))
            { _names.Add(((XmlElement)row.ParentNode).GetAttribute("id") + ":" + row.GetAttribute("culture"), row.InnerText); }
            foreach (XmlElement row in document.SelectNodes("/courtRecordImages/hint"))
            { string key = row.GetAttribute("bundle") + ":" + row.GetAttribute("sprite"); _hints.Add(key, row.InnerText); if (row.HasAttribute("keyMessage")) { _hintMessages.Add(key, row.GetAttribute("keyMessage")); } }
        }
        private static Sprite Visible(AssetBundleSprite image)
        {
            if (image == null || !image.HasReference || !image.active || !image.enabled || image.obj == null || !image.obj.activeInHierarchy) { return null; }
            if (image.sprite_renderer_ != null) { return image.sprite_renderer_.enabled && image.sprite_renderer_.color.a > 0f ? image.sprite_renderer_.sprite : null; }
            return image.image_ != null && image.image_.enabled && image.image_.color.a > 0f ? image.image_.sprite : null;
        }
        internal string DetailHint(recordListCtrl owner)
        {
            if (owner == null || !owner.is_open || !owner.body_active || owner.detail_open || owner.is_page_changing) { return null; }
            AssetBundleSprite image = NativeFields.Get(owner, "info_") as AssetBundleSprite;
            Sprite sprite = Visible(image); if (sprite == null) { return null; }
            string bundle = ReplaceLanguage.GetFileName(image.path_, image.name_);
            string text; if (!_hints.TryGetValue(bundle + ":" + sprite.name, out text)) { throw new InvalidOperationException("Unaudited record detail hint: " + bundle + ":" + sprite.name); }
            string message;
            return _hintMessages.TryGetValue(bundle + ":" + sprite.name, out message) ? NativeFields.Join(_runtime.Messages.Get(message), text) : text;
        }
        private string Description(string folder, string bundle, Sprite sprite, int note,
            string visibleLayer = null, string markers = null, string[] hiddenLabels = null)
        {
            if (sprite == null) { return null; }
            string key = folder + bundle + ":" + sprite.name;
            if (_empty.Contains(key)) { return null; }
            string text; string prefix = key + ":" + UiResources.Culture() + ":";
            if (!_descriptions.TryGetValue(prefix + note, out text) && !_descriptions.TryGetValue(prefix, out text))
            { throw new InvalidOperationException("Unaudited court record image: " + key + " note=" + note); }
            // Name tokens are only authored for the reviewed acquisition/context of this exact note.
            text = Regex.Replace(text, @"\{n:(\d+)\}", delegate (Match match) { return ProfileName(int.Parse(match.Groups[1].Value), null); });
            text = Regex.Replace(text, @"\{p:(\d+):([^{}]+)\}", delegate (Match match) { return ProfileName(int.Parse(match.Groups[1].Value), match.Groups[2].Value); });
            text = Regex.Replace(text, @"\{e:([a-z0-9-]+)\}", delegate (Match match) { return _names[match.Groups[1].Value + ":" + UiResources.Culture()]; });
            text = visibleLayer ?? text;
            string result = string.IsNullOrEmpty(text) ? null : string.Format(_runtime.Messages.Get("image.description"), text);
            string original;
            if (_originals.TryGetValue(key, out original))
            {
                if (hiddenLabels != null && hiddenLabels.Length > 0)
                {
                    List<string> visibleLines = new List<string>();
                    foreach (string line in original.Split('\n'))
                    {
                        bool hidden = Array.Exists(hiddenLabels, delegate (string label)
                        { return !string.IsNullOrEmpty(label) && line.Trim() == label.Trim(); });
                        if (!hidden) { visibleLines.Add(line); }
                    }
                    original = string.Join("\n", visibleLines.ToArray());
                }
                if (original.Trim().Length > 0) { result = NativeFields.Join(result, string.Format(_runtime.Messages.Get("image.original"), original)); }
            }
            return NativeFields.Join(result, markers);
        }
        private static string ProfileName(int id, string unknown)
        {
            if (piceDataCtrl.instance == null || id < 0 || id >= piceDataCtrl.instance.note_data.Count) { throw new InvalidOperationException("Missing reviewed profile name: " + id); }
            piceData reference = piceDataCtrl.instance.note_data[id];
            recordListCtrl list = recordListCtrl.instance;
            if (list != null)
            {
                IList categories = NativeFields.Get(list, "record_data_") as IList;
                if (categories != null && categories.Count > 1)
                {
                    IList available = NativeFields.Get(categories[1], "note_list_") as IList;
                    if (available != null) foreach (int current in available)
                    {
                        piceData candidate = piceDataCtrl.instance.note_data[current];
                        if (candidate.type == 1 && candidate.file_id == reference.file_id && candidate.path == reference.path) { return candidate.name; }
                    }
                }
            }
            return unknown ?? reference.name;
        }
        internal string ListDescription(recordListCtrl owner)
        {
            AssetBundleSprite image = NativeFields.Get(owner, "icon_") as AssetBundleSprite;
            Sprite sprite = Visible(image);
            return sprite == null ? null : Description(image.path_, ReplaceLanguage.GetFileName(image.path_, image.name_), sprite, owner.note_id);
        }
        internal int Starting(recordDetailCtrl detail)
        {
            Clear(); _detail = detail; _record = recordListCtrl.instance;
            if (_record != null) { _entryNote = _record.note_id; }
            _title = (int)GSStatic.global_work_.title; return _generation;
        }
        internal void Ended(recordDetailCtrl owner, int generation) { if (owner == _detail && generation == _generation) { Clear(); } }
        private bool Foreground()
        {
            return _detail != null && _record != null && _detail.is_open && _record.detail_open && _entryNote == _record.note_id
                && _title == (int)GSStatic.global_work_.title && NativeFields.Visible(_detail)
                && Application.isFocused && (optionCtrl.instance == null || !optionCtrl.instance.is_open)
                && (SaveLoadUICtrl.instance == null || !SaveLoadUICtrl.instance.is_open)
                && (_runtime.StoryUi == null || !_runtime.StoryUi.BoxBlocks())
                && (_runtime.Assistance == null || !_runtime.Assistance.IsOpen);
        }
        internal void Opened(keyGuideBoard guide)
        {
            if (_ready || _detail == null || guide != keyGuideCtrl.instance || (guide.current_guide != keyGuideBase.Type.DETAIL_VIEW && guide.current_guide != keyGuideBase.Type.TOP_VIEW)) { return; }
            _ready = true; _page = 0; Read();
        }
        internal int Generation { get { return _generation; } }
        internal void Closing(keyGuideBoard guide)
        { if (guide == keyGuideCtrl.instance && _ready && (guide.current_guide == keyGuideBase.Type.DETAIL_VIEW || guide.current_guide == keyGuideBase.Type.TOP_VIEW)) { Clear(); } }
        internal void Page(recordDetailCtrl detail, int page, int generation)
        { if (_detail != detail || !_ready || generation != _generation) { return; } _page = page; Read(); }
        private void Read()
        {
            int generation = _generation;
            recordDetailCtrl owner = _detail;
            if (owner == null || _runtime.Presentation == null) { return; }
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (generation != _generation || owner != _detail || !_ready || !Foreground()) { return; }
                try
                {
                    AssetBundleSprite image = NativeFields.Get(owner, "detail_sprite_") as AssetBundleSprite;
                    Sprite sprite = Visible(image);
                    string folder; string bundle; string visibleLayer = null, markers = null; string[] hiddenLabels = new string[0];
                    if (sprite != null) { folder = image.path_; bundle = ReplaceLanguage.GetFileName(folder, image.name_); }
                    else
                    {
                        // CheckTopView deliberately hides detail_sprite_ and renders the actual background.
                        piceDetailData data = (piceDetailData)NativeFields.Get(owner, "detailData_");
                        if (bgCtrl.instance == null || bgCtrl.instance.bg_no != data.bg_id) { return; }
                        SpriteRenderer renderer = NativeFields.Get(bgCtrl.instance, "sprite_renderer_") as SpriteRenderer;
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.color.a <= 0f) { return; }
                        sprite = renderer.sprite; folder = "/GS" + (_title + 1) + "/BG/";
                        bundle = ReplaceLanguage.GetFileName(folder, bgCtrl.instance.GetBGName((int)data.bg_id));
                        if (_runtime.PicturePointing != null) { visibleLayer = _runtime.PicturePointing.RecordBackgroundLayer((int)data.bg_id, sprite, out markers, out hiddenLabels); }
                    }
                    string text = Description(folder, bundle, sprite, _entryNote, visibleLayer, markers, hiddenLabels);
                    _runtime.Trace("court_record_detail", "note=" + _entryNote + " page=" + _page + " bundle=" + bundle + " sprite=" + (sprite == null ? "null" : sprite.name));
                    if (!string.IsNullOrEmpty(text)) { _runtime.Presentation.Ui(owner, null, text); }
                }
                catch (Exception error) { _runtime.ReadFailure("court-record-detail", error); }
            });
        }
        internal void Clear()
        {
            _generation++; _ready = false;
            if (_detail != null && _runtime.Presentation != null) { _runtime.Presentation.ClearUi(_detail); }
            _detail = null; _record = null; _entryNote = -1; _title = -1;
        }
        /// <summary>Releases this module's pending visual description only.</summary>
        public void Dispose() { Clear(); }
    }
}
