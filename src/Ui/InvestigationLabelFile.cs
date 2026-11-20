using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class InvestigationLabelFile
    {
        internal const string FileName = "investigation-labels.xml";
        private InvestigationLabelIndex _index;
        private Dictionary<string, string> _numbers = new Dictionary<string, string>();
        private Dictionary<string, string> _locations = new Dictionary<string, string>();
        private readonly string _path;
        private readonly string _assemblyHash;
        internal InvestigationLabelFile(string directory, string assemblyHash) { _path = Path.Combine(directory, FileName); _assemblyHash = assemblyHash; }
        internal bool Reload()
        {
            if (!File.Exists(_path)) { _index = null; _numbers.Clear(); _locations.Clear(); return false; }
            if (new FileInfo(_path).Length > 24 * 1024 * 1024) throw new InvalidDataException("Investigation label file exceeds 24 MiB.");
            XmlDocument document = UiResources.LoadXml(_path);
            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "investigationLabels" || !string.Equals(root.GetAttribute("gameAssemblySha256"), _assemblyHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Investigation label file does not match the current game assembly.");
            Dictionary<string, string> numbers = new Dictionary<string, string>();
            Dictionary<string, string> locations = new Dictionary<string, string>();
            HashSet<string> ids = new HashSet<string>();
            foreach (XmlElement point in root.SelectNodes("point"))
            {
                string id = point.GetAttribute("id");
                if (id.Length == 0 || !ids.Add(id)) throw new InvalidDataException("Missing or duplicate editable point ID.");
                foreach (string attribute in new[] { "title", "scenario", "room", "message", "place", "item" })
                { uint value; if (!uint.TryParse(point.GetAttribute(attribute), out value)) throw new InvalidDataException("Invalid region attribute: " + attribute); }
                if (point.GetAttribute("background").Length == 0) throw new InvalidDataException("Missing background identity.");
                string[] coordinates = point.GetAttribute("polygon").Split(',');
                if (coordinates.Length != 8) throw new InvalidDataException("Expected four polygon vertices.");
                foreach (string coordinate in coordinates) { uint value; if (!uint.TryParse(coordinate, out value)) throw new InvalidDataException("Invalid polygon coordinate."); }
                string identity = Identity(point);
                if (numbers.ContainsKey(identity)) throw new InvalidDataException("Duplicate editable point identity.");
                numbers.Add(identity, id);
                locations.Add(identity, point.GetAttribute("chapter") + " / " + point.GetAttribute("script") + " / " + point.GetAttribute("symbol"));
                HashSet<string> cultures = new HashSet<string>();
                List<XmlElement> emptyRows = new List<XmlElement>();
                foreach (XmlElement text in point.SelectNodes("text"))
                {
                    string culture = text.GetAttribute("culture");
                    if (Array.IndexOf(new[] { "zh-Hans", "zh-Hant", "en", "ja", "fr", "de", "ko", "pt-BR", "es-419" }, culture) < 0 || !cultures.Add(culture)) throw new InvalidDataException("Invalid or duplicate language: " + id);
                    // Empty rows intentionally retain the embedded name or the location fallback.
                    if (text.InnerText.Trim().Length == 0) emptyRows.Add(text);
                    else if (text.InnerText.Length > 1000) throw new InvalidDataException("Investigation label exceeds 1000 characters: " + id);
                }
                // Do not mutate the selected node list while enumerating it: Unity Mono can skip siblings.
                foreach (XmlElement text in emptyRows) point.RemoveChild(text);
            }
            InvestigationLabelIndex next = new InvestigationLabelIndex(document, true);
            _index = next; _numbers = numbers; _locations = locations;
            return true;
        }
        internal static string Identity(XmlElement point)
        {
            return InvestigationLabelIndex.Context(point.GetAttribute("title"), point.GetAttribute("scenario"), point.GetAttribute("room"), point.GetAttribute("background")) + ":" + InvestigationLabelIndex.Region(point.GetAttribute("message"), point.GetAttribute("place"), point.GetAttribute("item"), point.GetAttribute("polygon"));
        }
        internal string Get(string context, string region, string culture) { return _index == null ? null : _index.Get(context, region, culture); }
        internal string Location(string context, string region)
        { string location; return context != null && _locations.TryGetValue(context + ":" + region, out location) ? location : null; }
        internal string Number(string context, string region)
        { string id; return context != null && _numbers.TryGetValue(context + ":" + region, out id) ? id : null; }
    }
}
