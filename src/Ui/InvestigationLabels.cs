using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class InvestigationLabelIndex
    {
        private readonly Dictionary<string, Dictionary<string, string>> _labels = new Dictionary<string, Dictionary<string, string>>();
        private readonly Dictionary<string, Dictionary<string, string>> _visibleText = new Dictionary<string, Dictionary<string, string>>();
        internal InvestigationLabelIndex(XmlDocument document, bool editable = false)
        {
            foreach (XmlElement point in document.SelectNodes("/investigationLabels/point"))
            {
                string key = editable
                    ? MatchKey(Context(point.GetAttribute("title"), point.GetAttribute("scenario"), point.GetAttribute("room"), point.GetAttribute("background")),
                        Region(point.GetAttribute("message"), point.GetAttribute("place"), point.GetAttribute("item"), point.GetAttribute("polygon")))
                    : point.GetAttribute("match");
                if (key.Length == 0) throw new InvalidDataException("Missing investigation label match key.");
                Dictionary<string, string> names = new Dictionary<string, string>();
                foreach (XmlElement text in point.SelectNodes("text"))
                {
                    string culture = text.GetAttribute("culture");
                    if (culture.Length == 0 || text.InnerText.Trim().Length == 0 || names.ContainsKey(culture)) throw new InvalidDataException("Invalid investigation label language: " + point.GetAttribute("id"));
                    names.Add(culture, text.InnerText);
                }
                if (_labels.ContainsKey(key)) throw new InvalidDataException("Duplicate investigation region label: " + point.GetAttribute("id"));
                _labels.Add(key, names);
                foreach (XmlElement layer in point.SelectNodes("visibleText"))
                {
                    Dictionary<string, string> originals = new Dictionary<string, string>();
                    foreach (XmlElement sprite in layer.SelectNodes("sprite")) { originals.Add(sprite.GetAttribute("name"), sprite.InnerText); }
                    _visibleText.Add(key + ":" + layer.GetAttribute("seal"), originals);
                }
            }
        }
        internal bool HasVisibleText(string key)
        { return key != null && _visibleText.ContainsKey(key + ":1"); }
        internal string VisibleText(string key, int seal, string sprite)
        {
            Dictionary<string, string> originals; string original;
            return key != null && _visibleText.TryGetValue(key + ":" + seal.ToString(CultureInfo.InvariantCulture), out originals) && originals.TryGetValue(sprite, out original) ? original : null;
        }
        internal static string Context(string title, string scenario, string room, string background)
        { return title + ":" + scenario + ":" + room + ":" + background; }
        internal static string Region(string message, string place, string item, string polygon)
        { return message + ":" + place + ":" + item + ":" + polygon; }
        internal static string MatchKey(string context, string region)
        {
            if (context == null) return null;
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(context + ":" + region))).Replace("-", "").ToLowerInvariant();
        }
        internal string Get(string key, string culture)
        {
            Dictionary<string, string> names; string name;
            return key != null && _labels.TryGetValue(key, out names) && names.TryGetValue(culture, out name) ? name : null;
        }
        internal string Get(string context, string region, string culture)
        { return Get(MatchKey(context, region), culture); }
        internal string VisibleText(string context, string region, int seal, string sprite)
        { return VisibleText(MatchKey(context, region), seal, sprite); }
    }
    internal sealed class InvestigationLabels
    {
        private readonly InvestigationLabelIndex _index;
        private readonly InvestigationLabelFile _editable;
        internal InvestigationLabels(string directory)
        {
            XmlDocument document = UiResources.EmbeddedXml("Pwaat.Accessibility.InvestigationLabels.xml");
            // Validate source images once per runtime; focus navigation never hashes or reloads assets.
            foreach (XmlElement source in document.SelectNodes("/investigationLabels/source"))
            {
                string file = Path.Combine(Application.streamingAssetsPath, source.GetAttribute("path"));
                string actual;
                using (FileStream stream = File.OpenRead(file))
                using (SHA256 sha = SHA256.Create()) { actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
                if (!string.Equals(actual, source.GetAttribute("sha256"), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Investigation label source fingerprint changed: " + source.GetAttribute("path"));
            }
            _index = new InvestigationLabelIndex(document);
            _editable = new InvestigationLabelFile(directory, UiResources.Embedded("Pwaat.Accessibility.GameAssembly.sha256").Trim());
            try { _editable.Reload(); } catch (Exception error) { UiRuntime.Safe("investigation-label-file", r => r.LogReadFailure("investigation-label-file", error)); }
        }
        internal string Context()
        {
            if (bgCtrl.instance == null || GSStatic.global_work_ == null) return null;
            SpriteRenderer background = (SpriteRenderer)NativeFields.Get(bgCtrl.instance, "sprite_renderer_");
            if (background == null || !background.enabled || !background.gameObject.activeInHierarchy || background.sprite == null || background.flipX || background.flipY) return null;
            GlobalWork work = GSStatic.global_work_;
            return InvestigationLabelIndex.Context(((int)work.title).ToString(CultureInfo.InvariantCulture), work.scenario.ToString(CultureInfo.InvariantCulture), work.Room.ToString(CultureInfo.InvariantCulture), background.sprite.name);
        }
        internal static string Region(INSPECT_DATA data)
        {
            uint[] coordinates = { data.x0, data.y0, data.x1, data.y1, data.x2, data.y2, data.x3, data.y3 };
            string[] parts = Array.ConvertAll(coordinates, number => number.ToString(CultureInfo.InvariantCulture));
            return InvestigationLabelIndex.Region(data.message.ToString(CultureInfo.InvariantCulture), data.place.ToString(CultureInfo.InvariantCulture), data.item.ToString(CultureInfo.InvariantCulture), string.Join(",", parts));
        }
        internal bool Reload() { return _editable.Reload(); }
        internal string Number(string context, INSPECT_DATA data) { return _editable.Number(context, Region(data)); }
        internal string Location(string context, INSPECT_DATA data) { return _editable.Location(context, Region(data)); }
        internal string Get(string context, INSPECT_DATA data)
        {
            string region = Region(data);
            string culture = UiResources.Culture();
            string key = InvestigationLabelIndex.MatchKey(context, region);
            string name = _editable.Get(context, region, culture) ?? _index.Get(key, culture);
            if (name == null || !_index.HasVisibleText(key) || bgCtrl.instance == null) return name;
            System.Collections.IList seals = (System.Collections.IList)NativeFields.Get(bgCtrl.instance, "seal_list_");
            foreach (bgCtrl.SealData layer in seals)
            {
                SpriteRenderer visual = layer.sprite_;
                if (!layer.active || visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy || visual.color.a <= 0f || visual.sprite == null) continue;
                string original = _index.VisibleText(key, layer.id_, visual.sprite.name);
                if (original != null) return string.Format(UiRuntime.Current.Messages.Get("investigation.visible-text"), name, original);
            }
            return name;
        }
    }
}
