using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class ScientificPointLabels
    {
        private readonly Dictionary<string, Dictionary<string, string>> _names = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        internal ScientificPointLabels(XmlDocument document)
        {
            foreach (XmlElement point in document.SelectNodes("/sciencePointLabels/point"))
            {
                int obj = int.Parse(point.GetAttribute("object"), CultureInfo.InvariantCulture);
                int index = int.Parse(point.GetAttribute("index"), CultureInfo.InvariantCulture);
                if (obj < 0 || index < 0) throw new InvalidDataException("Invalid scientific point identity.");
                Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (XmlElement text in point.SelectNodes("text"))
                {
                    string culture = text.GetAttribute("culture");
                    if (culture.Length == 0 || text.InnerText.Trim().Length == 0 || names.ContainsKey(culture)) throw new InvalidDataException("Invalid scientific point language.");
                    names.Add(culture, text.InnerText);
                }
                foreach (XmlElement model in point.SelectNodes("model"))
                {
                    string key = Key(obj, point.GetAttribute("bundle"), model.GetAttribute("name"), point.GetAttribute("hit"), index);
                    if (_names.ContainsKey(key)) throw new InvalidDataException("Duplicate scientific point identity.");
                    _names.Add(key, names);
                }
            }
        }
        private static string Key(int obj, string bundle, string model, string hit, int index)
        { return obj.ToString(CultureInfo.InvariantCulture) + "|" + bundle + "|" + model + "|" + hit + "|" + index.ToString(CultureInfo.InvariantCulture); }
        internal string Get(int obj, string bundle, string model, string hit, int index, string culture)
        {
            Dictionary<string, string> names; string name;
            return index >= 0 && model != null && _names.TryGetValue(Key(obj, bundle, model, hit, index), out names) && names.TryGetValue(culture, out name) ? name : null;
        }
    }
}
