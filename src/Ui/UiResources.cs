using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    internal static class UiResources
    {
        internal static XmlReaderSettings XmlSettings() { return new XmlReaderSettings { ProhibitDtd = true, XmlResolver = null }; }
        internal static XmlDocument LoadXml(string path)
        {
            XmlDocument document = new XmlDocument();
            using (XmlReader reader = XmlReader.Create(path, XmlSettings())) { document.Load(reader); }
            return document;
        }
        internal static string Embedded(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) { throw new InvalidDataException("Missing embedded resource: " + name); }
                using (StreamReader reader = new StreamReader(stream)) { return reader.ReadToEnd(); }
            }
        }
        internal static XmlDocument EmbeddedXml(string name)
        {
            XmlDocument document = new XmlDocument();
            using (StringReader text = new StringReader(Embedded(name)))
            using (XmlReader reader = XmlReader.Create(text, XmlSettings())) { document.Load(reader); }
            return document;
        }
        internal static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) { throw new MissingFieldException(type.FullName, name); }
            return field;
        }
        internal static FieldInfo StaticField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) { throw new MissingFieldException(type.FullName, name); }
            return field;
        }
        internal static string Culture()
        {
            if (GSStatic.global_work_ == null) { return "en"; } // Mod-owned error fallback only; never substitutes game text.
            switch (GSStatic.global_work_.language)
            {
                case Language.JAPAN: return "ja";
                case Language.USA: return "en";
                case Language.FRANCE: return "fr";
                case Language.GERMAN: return "de";
                case Language.KOREA: return "ko";
                case Language.CHINA_S: return "zh-Hans";
                case Language.CHINA_T: return "zh-Hant";
                case Language.Pt_BR: return "pt-BR";
                case Language.ES_419: return "es-419";
                default: return "en";
            }
        }
        internal static string Plain(string text)
        {
            if (text == null) { throw new InvalidDataException("Native text is null."); }
            if (text.IndexOf("<quad", StringComparison.OrdinalIgnoreCase) >= 0) { throw new InvalidDataException("Unresolved native inline glyph."); }
            return Regex.Replace(text, @"</?(?:b|i|size|color|material)(?:=[^<>]*)?>", "", RegexOptions.IgnoreCase);
        }
        internal static string TextValue(Text text)
        {
            if (text == null || !text.enabled || !text.gameObject.activeInHierarchy) { throw new InvalidDataException("Native text object is not visible."); }
            string value = text.supportRichText ? Plain(text.text) : text.text;
            if (string.IsNullOrEmpty(value) || value.Trim().Length == 0) { throw new InvalidDataException("Native text is empty."); }
            return value;
        }
    }
    internal sealed class UiSettings
    {
        internal bool Debug;
        internal bool TracePages;
        internal bool MainMenu = true;
        internal bool StartPrompt = true;
        internal bool LoadingStatus = true;
        internal int TraceBudget = 1024;
        internal static UiSettings Read(string directory)
        {
            UiSettings settings = new UiSettings();
            string path = Path.Combine(directory, "ui-settings.xml");
            if (!File.Exists(path)) { return settings; }
            XmlElement root = UiResources.LoadXml(path).DocumentElement;
            if (root == null || root.Name != "accessibility") { throw new InvalidDataException("Invalid UI settings root."); }
            settings.Debug = ReadBool(root, "debug", false);
            settings.TracePages = ReadBool(root, "tracePages", false);
            settings.MainMenu = ReadBool(root, "mainMenu", true);
            settings.StartPrompt = ReadBool(root, "startPrompt", true);
            settings.LoadingStatus = ReadBool(root, "loadingStatus", true);
            if (root.HasAttribute("traceBudget")) { settings.TraceBudget = int.Parse(root.GetAttribute("traceBudget")); }
            if (settings.TraceBudget < 32 || settings.TraceBudget > 8192) { throw new InvalidDataException("Trace budget outside 32..8192."); }
            return settings;
        }
        private static bool ReadBool(XmlElement root, string name, bool fallback)
        {
            if (!root.HasAttribute(name)) { return fallback; }
            bool result;
            if (!bool.TryParse(root.GetAttribute(name), out result)) { throw new InvalidDataException("Invalid flag: " + name); }
            return result;
        }
    }
    internal sealed class UiMessages
    {
        private readonly Dictionary<string, string> _messages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal UiMessages()
        {
            XmlDocument document = UiResources.EmbeddedXml("Pwaat.Accessibility.UiMessages.xml");
            foreach (XmlElement entry in document.SelectNodes("/messages/message")) { _messages.Add(entry.GetAttribute("culture") + ":" + entry.GetAttribute("key"), entry.InnerText); }
        }
        internal string Get(string key)
        {
            string value;
            if (!_messages.TryGetValue(UiResources.Culture() + ":" + key, out value)) { throw new InvalidDataException("Missing mod-owned message: " + key); }
            return value;
        }
    }
    internal sealed class ControlGlyphs
    {
        private readonly Dictionary<string, string> _literal = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _symbols = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly UiMessages _messages;
        internal ControlGlyphs(UiMessages messages)
        {
            _messages = messages;
            XmlDocument document = UiResources.EmbeddedXml("Pwaat.Accessibility.ControlGlyphs.xml");
            foreach (XmlElement entry in document.SelectNodes("/glyphs/glyph"))
            {
                string key = entry.GetAttribute("atlas") + ":" + entry.GetAttribute("sprite");
                if (entry.HasAttribute("text")) { _literal.Add(key, entry.GetAttribute("text")); }
                else { _symbols.Add(key, entry.GetAttribute("message")); }
            }
        }
        internal string Lookup(string atlas, string sprite)
        {
            string value; string identity = atlas + ":" + sprite;
            if (_literal.TryGetValue(identity, out value)) { return value; }
            if (_symbols.TryGetValue(identity, out value)) { return _messages.Get(value); }
            throw new InvalidDataException("Unverified native key icon: " + identity);
        }
        internal string Read(AssetBundleSprite icon, out string identity)
        {
            if (icon == null || !icon.active || !icon.enabled || icon.obj == null || !icon.obj.activeInHierarchy) { throw new InvalidDataException("Native key icon is not active."); }
            Sprite sprite = icon.sprite_renderer_ != null ? icon.sprite_renderer_.sprite : (icon.image_ != null ? icon.image_.sprite : null);
            if (sprite == null) { throw new InvalidDataException("Native key icon has no sprite."); }
            if (icon.path_ != "/menu/option/" && !(icon.path_ == "/menu/common/" && (icon.name_ == "symbol_pc" || icon.name_ == "symbol_xbox"))) { throw new InvalidDataException("Unverified native icon asset path."); }
            identity = icon.name_ + ":" + sprite.name;
            string value;
            if (_literal.TryGetValue(identity, out value)) { return value; }
            if (_symbols.TryGetValue(identity, out value)) { return _messages.Get(value); }
            throw new InvalidDataException("Unverified native key icon: " + identity);
        }
    }
}
