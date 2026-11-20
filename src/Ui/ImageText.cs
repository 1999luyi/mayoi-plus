using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal static class ImageText
    {
        private static readonly Dictionary<int, string> KeyLiterals = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> KeyMessages = new Dictionary<int, string>();
        private static readonly Dictionary<string, string> BundleTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Texts = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Originals = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
        static ImageText()
        {
            foreach (XmlElement row in UiResources.EmbeddedXml("Pwaat.Accessibility.ImageText.xml").SelectNodes("/images/image")) { Texts.Add(row.GetAttribute("path") + row.GetAttribute("bundle") + ":" + row.GetAttribute("sprite"), row.InnerText); if (!BundleTexts.ContainsKey(row.GetAttribute("path") + row.GetAttribute("bundle"))) { BundleTexts.Add(row.GetAttribute("path") + row.GetAttribute("bundle"), row.InnerText); } }
            foreach (XmlElement row in UiResources.EmbeddedXml("Pwaat.Accessibility.ControlGlyphs.xml").SelectNodes("/glyphs/glyph"))
            {
                string sprite = row.GetAttribute("sprite");
                if (row.GetAttribute("atlas") != "option_list_bg_9" || !sprite.StartsWith("option_list_bg_9_", StringComparison.Ordinal)) { continue; }
                int number = int.Parse(sprite.Substring("option_list_bg_9_".Length));
                if (row.HasAttribute("text")) { KeyLiterals.Add(number, row.GetAttribute("text")); } else { KeyMessages.Add(number, row.GetAttribute("message")); }
            }
            foreach (XmlElement row in UiResources.EmbeddedXml("Pwaat.Accessibility.ArtworkDescriptions.xml").SelectNodes("/descriptions/image"))
            {
                XmlElement original = row.SelectSingleNode("original") as XmlElement;
                if (original != null) { Originals.Add(row.GetAttribute("path") + row.GetAttribute("bundle") + ":" + row.GetAttribute("sprite"), original.InnerText); }
                foreach (XmlElement text in row.SelectNodes("text")) { Descriptions.Add(row.GetAttribute("path") + row.GetAttribute("bundle") + ":" + row.GetAttribute("sprite") + ":" + text.GetAttribute("culture"), text.InnerText); }
            }
        }
        internal static string Read(AssetBundleSprite image)
        {
            if (image == null || !image.HasReference || !image.active || !image.enabled || image.obj == null || !image.obj.activeInHierarchy) { return null; }
            Sprite sprite = image.sprite_renderer_ != null ? image.sprite_renderer_.sprite : image.image_.sprite;
            return sprite == null ? null : Lookup(image.path_, ReplaceLanguage.GetFileName(image.path_, image.name_), sprite.name);
        }
        internal static string Read(AssetBundleSpriteImage image)
        {
            if (image == null || !image.HasReference || !image.active || !image.image_.enabled || !image.obj.activeInHierarchy || image.image_.sprite == null) { return null; }
            return Lookup(image.path_, ReplaceLanguage.GetFileName(image.path_, image.name_), image.image_.sprite.name);
        }
        internal static string Describe(AssetBundleSpriteImage image)
        {
            if (image == null || !image.HasReference || !image.active || !image.image_.enabled || !image.obj.activeInHierarchy || image.image_.sprite == null) { return null; }
            return Description(image.path_, ReplaceLanguage.GetFileName(image.path_, image.name_), image.image_.sprite.name);
        }
        internal static string Lookup(string path, string bundle, string sprite)
        {
            string text;
            if (!Texts.TryGetValue(path + bundle + ":" + sprite, out text)) { throw new InvalidOperationException("Image text has not been visually verified: " + path + bundle + ":" + sprite); }
            return text;
        }
        internal static string SeriesName(int index)
        {
            string suffix = GSUtility.GetResourceNameLanguage(GSStatic.global_work_.language);
            string bundle = "titlegs" + (index + 1) + suffix;
            string folder = "/GS" + (index + 1) + "/BG/";
            return BundleTexts[folder + ReplaceLanguage.GetFileName(folder, bundle)];
        }
        internal static string Description(string path, string bundle, string sprite)
        {
            string value;
            if (!Descriptions.TryGetValue(path + bundle + ":" + sprite + ":" + UiResources.Culture(), out value)) { throw new InvalidOperationException("Image description has no audited source: " + path + bundle); }
            value = string.Format(UiRuntime.Current.Messages.Get("image.description"), value);
            string original;
            return Originals.TryGetValue(path + bundle + ":" + sprite, out original) ? NativeFields.Join(value, string.Format(UiRuntime.Current.Messages.Get("image.original"), original)) : value;
        }
        internal static string Describe(AssetBundleSprite image, string sourceBundle = null)
        {
            if (image == null || !image.HasReference || !image.active || !image.enabled || image.obj == null || !image.obj.activeInHierarchy) { return null; }
            Sprite sprite = image.sprite_renderer_ != null ? image.sprite_renderer_.sprite : image.image_.sprite;
            return sprite == null ? null : Description(image.path_, ReplaceLanguage.GetFileName(image.path_, sourceBundle ?? image.name_), sprite.name);
        }
        internal static string KeyName(KeyCode key)
        {
            int number = keyGuideBase.GuideIcon.GetKeyCodeSpriteNum(key);
            string value;
            if (KeyLiterals.TryGetValue(number, out value)) { return value; }
            if (KeyMessages.TryGetValue(number, out value)) { return UiRuntime.Current.Messages.Get(value); }
            throw new InvalidOperationException("Unverified physical key glyph.");
        }
        internal static string MainHeader(mainTitleCtrl owner, string selected)
        {
            object artwork = NativeFields.Get(owner, "main_title_");
            string title = Read((AssetBundleSprite)NativeFields.Get(artwork, "title_"));
            string copyright = Read((AssetBundleSprite)NativeFields.Get(artwork, "copyright_"));
            string navigation = null;
            if (padCtrl.instance != null)
            {
                // This page actually consumes the directional actions and KeyType.A; use its current keyboard binding.
                titleSelectPlate plate = (titleSelectPlate)NativeFields.Get(owner, "select_plate_");
                List<string> directions = new List<string>();
                if ((bool)NativeFields.Get(plate, "is_horizontal_")) { directions.Add(KeyName(padCtrl.instance.GetKeyCode(KeyType.Left))); directions.Add(KeyName(padCtrl.instance.GetKeyCode(KeyType.Right))); }
                if ((bool)NativeFields.Get(plate, "is_vertical_") || plate.is_title_with_game_end) { directions.Add(KeyName(padCtrl.instance.GetKeyCode(KeyType.Up))); directions.Add(KeyName(padCtrl.instance.GetKeyCode(KeyType.Down))); }
                navigation = string.Format(UiRuntime.Current.Messages.Get("menu.navigation"), string.Join("、", directions.ToArray()), KeyName(padCtrl.instance.GetKeyCode(KeyType.A)));
            }
            if (UiRuntime.Current.Assistance != null) { navigation = UiRuntime.Current.Assistance.AutomaticHint("title-navigation", navigation); }
            return NativeFields.Join(title, selected, copyright, navigation);
        }
    }
}
