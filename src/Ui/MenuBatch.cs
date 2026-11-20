using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    internal static class NativeFields
    {
        private static readonly Dictionary<string, MemberInfo> Cache = new Dictionary<string, MemberInfo>();
        internal static object Get(object owner, string name)
        {
            if (owner == null) { return null; }
            Type type = owner as Type ?? owner.GetType();
            string key = type.FullName + ":" + name;
            MemberInfo member;
            if (!Cache.TryGetValue(key, out member))
            {
                for (Type current = type; current != null && member == null; current = current.BaseType)
                {
                    member = (MemberInfo)current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) ?? current.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                }
                if (member == null) { throw new MissingMemberException(type.FullName, name); }
                Cache.Add(key, member);
            }
            FieldInfo field = member as FieldInfo;
            return field != null ? field.GetValue(owner is Type ? null : owner) : ((PropertyInfo)member).GetValue(owner is Type ? null : owner, null);
        }
        internal static int Number(object owner, string name) { return Convert.ToInt32(Get(owner, name)); }
        internal static string Text(object owner, string name) { return TextValue(Get(owner, name) as Text); }
        internal static string TextValue(Text text) { return text != null && text.enabled && text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text) ? UiResources.Plain(text.text).TrimEnd('\r') : null; }
        internal static string Texts(object value)
        {
            List<string> text = new List<string>();
            IEnumerable list = value as IEnumerable;
            if (list != null) { foreach (object item in list) { string s = TextValue(item as Text); if (!string.IsNullOrEmpty(s)) { text.Add(s); } } }
            return string.Join("\n", text.ToArray());
        }
        internal static string Join(params string[] values) { List<string> v = new List<string>(); foreach (string s in values) { if (!string.IsNullOrEmpty(s)) { v.Add(s); } } return string.Join("\n", v.ToArray()); }
        internal static bool Visible(Component owner)
        {
            if (owner == null || !owner.gameObject.activeInHierarchy) { return false; }
            foreach (CanvasGroup group in owner.GetComponentsInParent<CanvasGroup>()) { if (group.alpha <= 0f) { return false; } }
            sceneCtrl scene = owner as sceneCtrl;
            return scene == null || scene.body == null || scene.body.activeInHierarchy;
        }
    }
    internal sealed class MenuBatch : IDisposable
    {
        private sealed class Pending { internal Component Owner; internal Action Read; internal bool Cancelled; }
        private sealed class OptionSnapshot { internal int Item; internal string Value; internal string Description; }
        private sealed class Snapshot { internal string Identity; internal string Text; }
        private readonly UiRuntime _runtime;
        private readonly ControlGlyphs _glyphs;
        private readonly MonoBehaviour _host;
        private readonly Dictionary<string, Pending> _pending = new Dictionary<string, Pending>();
        private readonly Dictionary<string, Snapshot> _spoken = new Dictionary<string, Snapshot>();
        private readonly Dictionary<int, string> _optionCategoryFocus = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _optionGuideSpoken = new Dictionary<int, string>();
        private readonly Dictionary<int, OptionSnapshot> _optionFocus = new Dictionary<int, OptionSnapshot>();
        private readonly Dictionary<int, Component> _owners = new Dictionary<int, Component>();
        private readonly HashSet<int> _plates = new HashSet<int>();
        private readonly Dictionary<int, GalleryActionStudioListItemContent[]> _studioLists = new Dictionary<int, GalleryActionStudioListItemContent[]>();
        private readonly HashSet<int> _shownDialogs = new HashSet<int>();
        internal MenuBatch(UiRuntime runtime, MonoBehaviour host) { _runtime = runtime; _host = host; _glyphs = new ControlGlyphs(runtime.Messages); }
        internal void Request(Component owner, string channel, Action read)
        {
            if (owner == null) { return; }
            _owners[owner.GetInstanceID()] = owner;
            string key = owner.GetInstanceID() + ":" + channel;
            Pending task;
            if (_pending.TryGetValue(key, out task)) { task.Read = read; return; }
            task = new Pending { Owner = owner, Read = read };
            _pending.Add(key, task);
            _host.StartCoroutine(AfterCommit(key, task));
        }
        private IEnumerator AfterCommit(string key, Pending task)
        {
            yield return null;
            Pending current;
            if (_pending.TryGetValue(key, out current) && ReferenceEquals(current, task)) { _pending.Remove(key); }
            if (task.Cancelled || !NativeFields.Visible(task.Owner))
            {
                if (key.EndsWith(":options", StringComparison.Ordinal)) { _runtime.Trace("settings_read_skipped", "request=" + key + " reason=" + (task.Cancelled ? "cancelled" : "invisible")); }
                yield break;
            }
            try { task.Read(); }
            catch (Exception error) { _runtime.ReadFailure("menu-batch/" + key, error); }
        }
        private void Emit(Component owner, string channel, string identity, string text, bool interrupt = true)
        {
            if (owner == null || string.IsNullOrEmpty(text)) { return; }
            string key = owner.GetInstanceID() + ":" + channel;
            Snapshot old;
            if (_spoken.TryGetValue(key, out old) && old.Identity == identity && old.Text == text) { return; }
            _spoken[key] = new Snapshot { Identity = identity, Text = text };
            _runtime.Trace("menu_snapshot", "page=" + owner.GetType().FullName + " channel=" + channel + " identity=" + identity + " text=" + text);
            _runtime.Speech.Say(text, interrupt);
        }
        internal void Clear(Component owner)
        {
            if (owner == null) { return; }
            string prefix = owner.GetInstanceID() + ":";
            foreach (string key in new List<string>(_pending.Keys)) { if (key.StartsWith(prefix, StringComparison.Ordinal)) { _pending[key].Cancelled = true; _pending.Remove(key); } }
            foreach (string key in new List<string>(_spoken.Keys)) { if (key.StartsWith(prefix, StringComparison.Ordinal)) { _spoken.Remove(key); } }
            _optionCategoryFocus.Remove(owner.GetInstanceID()); _optionGuideSpoken.Remove(owner.GetInstanceID()); _optionFocus.Remove(owner.GetInstanceID()); _owners.Remove(owner.GetInstanceID());
            _plates.Remove(owner.GetInstanceID()); _shownDialogs.Remove(owner.GetInstanceID()); _studioLists.Remove(owner.GetInstanceID());
        }
        internal void PlateShown(titleSelectPlate plate)
        {
            if (_runtime.Title != null && _runtime.Title.Owns(plate)) { return; }
            _plates.Add(plate.GetInstanceID());
            ClearSnapshots(plate);
            Request(plate, "plate", delegate { ReadPlate(plate, true); });
        }
        internal void PlateChanged(titleSelectPlate plate)
        {
            if (!_plates.Contains(plate.GetInstanceID()) || (_runtime.Title != null && _runtime.Title.Owns(plate))) { return; }
            Request(plate, "plate", delegate { ReadPlate(plate, false); });
        }
        private void ClearSnapshots(Component owner)
        {
            string prefix = owner.GetInstanceID() + ":";
            foreach (string key in new List<string>(_spoken.Keys)) { if (key.StartsWith(prefix, StringComparison.Ordinal)) { _spoken.Remove(key); } }
        }
        private void ReadPlate(titleSelectPlate plate, bool initial)
        {
            if (!plate.body_active || plate.is_play_animation || !_plates.Contains(plate.GetInstanceID())) { return; }
            IList items = (IList)NativeFields.Get(plate, "select_list_");
            int index = plate.cursor_no + NativeFields.Number(plate, "index_offset_");
            if (index < 0 || index >= items.Count) { throw new InvalidOperationException("Native plate index outside current list."); }
            string text = NativeFields.Text(items[index], "text_");
            if (!_spoken.ContainsKey(plate.GetInstanceID() + ":plate"))
            {
                string header = FindPlateHeader(plate);
                text = NativeFields.Join(header, text);
            }
            Emit(plate, "plate", index.ToString(), text, !initial);
        }
        private string FindPlateHeader(Component plate)
        {
            SaveConfirmationWindow save = plate.GetComponentInParent<SaveConfirmationWindow>();
            if (save != null) { object window = NativeFields.Get(save, "window_"); return NativeFields.Text(window, "confirmation_text_"); }
            SavePriorConfirmation prior = plate.GetComponentInParent<SavePriorConfirmation>();
            if (prior != null) { return NativeFields.Texts(NativeFields.Get(prior, "confirmation_text_")); }
            messageBoxCtrl box = plate.GetComponentInParent<messageBoxCtrl>();
            if (box != null) { return NativeFields.Texts(NativeFields.Get(box, "text_list_")); }
            scenarioSelectCtrl scenario = plate.GetComponentInParent<scenarioSelectCtrl>();
            if (scenario != null) { return NativeFields.Texts(NativeFields.Get(scenario, "start_text_")); }
            ChapterJumpInMenuCtrl chapter = plate.GetComponentInParent<ChapterJumpInMenuCtrl>();
            if (chapter != null) { return NativeFields.Texts(NativeFields.Get(chapter, "start_text_")); }
            optionCtrl option = plate.GetComponentInParent<optionCtrl>();
            if (option != null) { return NativeFields.Texts(NativeFields.Get(option, "title_back_text_")); }
            return null;
        }
        internal void General(GeneralSelectPlateCtrl plate, bool initial)
        {
            if (initial) { ClearSnapshots(plate); }
            Request(plate, "general", delegate
            {
                IList items = (IList)NativeFields.Get(plate, "select_list_"); int index = plate.cursor_no;
                if (index < 0 || index >= items.Count) { return; }
                Emit(plate, "general", index.ToString(), NativeFields.Text(items[index], "text_"), !initial);
            });
        }
        internal void Series(seriesTitleSelectCtrl owner)
        {
            Request(owner, "series", delegate
            {
                if (owner.is_decide) { return; }
                int index = NativeFields.Number(owner, "title_no_");
                IList titles = (IList)NativeFields.Get(owner, "title_list_");
                if (index < 0 || index >= titles.Count) { return; }
                string title = ImageText.Read((AssetBundleSprite)NativeFields.Get(titles[index], "title_"));
                Emit(owner, "series", index.ToString(), title);
            });
        }
        internal void Scenario(scenarioSelectCtrl owner)
        {
            Request(owner, "scenario", delegate
            {
                int story = NativeFields.Number(owner, "current_num_");
                int title = titleCtrlRoot.instance.data_system.title_no;
                TextDataCtrl.TitleTextID id = title == 0 ? TextDataCtrl.TitleTextID.GS1_SCENARIO_NAME : title == 1 ? TextDataCtrl.TitleTextID.GS2_SCENARIO_NAME : TextDataCtrl.TitleTextID.GS3_SCENARIO_NAME;
                IList pictures = (IList)NativeFields.Get(owner, "story_title_");
                AssetBundleSprite picture = (AssetBundleSprite)NativeFields.Get(pictures[story], "title_");
                Sprite sprite = picture.sprite_renderer_ != null ? picture.sprite_renderer_.sprite : picture.image_.sprite;
                string description = sprite == null ? null : ImageText.Description("/GS" + (title + 1) + "/BG/", "storygs" + (title + 1), sprite.name);
                string prefix = ImageText.Read((AssetBundleSprite)NativeFields.Get(pictures[story], "title_text_"));
                Emit(owner, "scenario", title + ":" + story, NativeFields.Join(NativeFields.Join(prefix, TextDataCtrl.GetText(id, story)), description));
            });
        }
        internal void Chapter(ChapterJumpInMenuCtrl owner)
        {
            Request(owner, "chapter", delegate
            {
                int episode = NativeFields.Number(owner, "episode_cursor");
                IList names = (IList)NativeFields.Get(owner, "name_table");
                int title = NativeFields.Number(owner, "title_cursor");
                if (names == null || episode < 0 || episode >= names.Count) { return; }
                string episodeName = TextDataCtrl.GetText(title == 0 ? TextDataCtrl.TitleTextID.GS1_SCENARIO_NAME : title == 1 ? TextDataCtrl.TitleTextID.GS2_SCENARIO_NAME : TextDataCtrl.TitleTextID.GS3_SCENARIO_NAME, episode);
                IList pictures = (IList)NativeFields.Get(owner, "story");
                string description = episode >= 0 && episode < pictures.Count ? ImageText.Describe((AssetBundleSprite)NativeFields.Get(pictures[episode], "storySprite")) : null;
                string prefix = ImageText.Read((AssetBundleSprite)NativeFields.Get(pictures[episode], "textSprite"));
                Emit(owner, "chapter", title + ":" + episode, NativeFields.Join(NativeFields.Join(prefix, episodeName), description));
            });
        }
        internal void OptionsEntered(optionCtrl owner)
        {
            if (owner == null) return;
            _optionFocus.Remove(owner.GetInstanceID());
            ClearSnapshots(owner);
            _runtime.Trace("settings_menu_ready", "owner=" + owner.GetInstanceID() + " category=" + NativeFields.Number(owner, "current_cat") + " index=" + NativeFields.Number(owner, "current_num_"));
            // The native MenuRoutine has activated input and reset its input state.
            // The existing request channel coalesces SelectItemSet and description callbacks.
            Options(owner);
        }
        private string SettingsText(string title, string value, string description)
        {
            string heading = string.IsNullOrEmpty(title) ? null : string.Format(_runtime.Messages.Get("settings.label"), title);
            return NativeFields.Join(heading, value, description);
        }
        internal void Options(optionCtrl owner)
        {
            Request(owner, "options", delegate
            {
                if (!owner.is_open || (optionCtrl.OptionMenuState)NativeFields.Get(owner, "menuState_") != optionCtrl.OptionMenuState.Menu) { _runtime.Trace("settings_read_skipped", "reason=inactive owner=" + owner.GetInstanceID()); return; }
                if (ModalBlocks(owner)) { _runtime.Trace("settings_read_skipped", "reason=modal owner=" + owner.GetInstanceID()); return; }
                IList items = (IList)NativeFields.Get(owner, "available_option_"); int index = NativeFields.Number(owner, "current_num_");
                if (index < 0 || index >= items.Count) { _runtime.Trace("settings_read_skipped", "reason=index index=" + index + " count=" + items.Count); return; }
                optionItem item = (optionItem)items[index];
                _optionCategoryFocus.Remove(owner.GetInstanceID());
                string value = OptionValue(item);
                string description = OptionDescription(owner, false);
                OptionSnapshot old;
                bool sameItem = _optionFocus.TryGetValue(owner.GetInstanceID(), out old) && old.Item == item.GetInstanceID();
                if (sameItem && old.Value == value && old.Description == description && !OptionGuideChanged(owner)) { _runtime.Trace("settings_read_skipped", "reason=unchanged item=" + item.GetInstanceID()); return; }
                _optionFocus[owner.GetInstanceID()] = new OptionSnapshot { Item = item.GetInstanceID(), Value = value, Description = description };
                string output = sameItem ? (old.Value != value ? value : description) : SettingsText(NativeFields.Text(item, "option_title_"), value, description);
                Emit(owner, "options", item.GetInstanceID() + ":" + value + ":" + description, AppendOptionGuide(owner, output));
            });
        }
        private string OptionDescription(optionCtrl owner, bool category)
        {
            if ((!category && NativeFields.Number(owner, "current_num_") + NativeFields.Number(owner, "begin_num_") >= NativeAccessibilitySettings.FirstItem)
                || (category && NativeFields.Number(owner, "current_cat") == NativeAccessibilitySettings.CategoryId))
            {
                if (!category)
                {
                    IList items = (IList)NativeFields.Get(owner, "available_option_");
                    AccessibilityOptionItem item = items[NativeFields.Number(owner, "current_num_")] as AccessibilityOptionItem;
                    if (item != null && item.DescriptionControl != null) return NativeFields.TextValue(item.DescriptionControl);
                }
                return NativeFields.Texts(NativeFields.Get(owner, "title_back_text_"));
            }
            TextDataCtrl.OptionTextID[] ids = (TextDataCtrl.OptionTextID[])NativeFields.Get(owner, category ? "option_category_description_" : "option_description_");
            int index = category ? NativeFields.Number(owner, "current_cat") : NativeFields.Number(owner, "current_num_") + NativeFields.Number(owner, "begin_num_");
            if (index < 0 || index >= ids.Length) { throw new InvalidOperationException("Native option description index outside current table."); }
            IList displayed = (IList)NativeFields.Get(owner, "title_back_text_");
            textKeyIconCtrl icons = (textKeyIconCtrl)NativeFields.Get(owner, "key_icon_");
            List<string> lines = new List<string>();
            for (int line = 0; line < displayed.Count; line++)
            {
                string original = TextDataCtrl.GetText(ids[index], line);
                if (string.IsNullOrEmpty(original)) { continue; }
                string shown = NativeFields.TextValue(displayed[line] as Text);
                if (shown == null) { continue; }
                string expected = original.Contains("【】") ? original.Replace("【】", icons.changeTextToIconSpase(string.Empty)) : original;
                if (!string.Equals(UiResources.Plain(expected).TrimEnd('\r'), shown, StringComparison.Ordinal)) { throw new InvalidOperationException("Current description control does not match native description source."); }
                if (original.Contains("【】"))
                {
                    if (line >= icons.key_icon.Length) { throw new InvalidOperationException("Native option icon slot missing."); }
                    string identity; original = original.Replace("【】", _glyphs.Read(icons.key_icon[line].icon, out identity));
                }
                lines.Add(UiResources.Plain(original));
            }
            if (!category)
            {
                IList items = (IList)NativeFields.Get(owner, "available_option_");
                int selected = NativeFields.Number(owner, "current_num_");
                if (selected >= 0 && selected < items.Count)
                {
                    optionStory story = items[selected] as optionStory;
                    if (story != null && story.active && NativeFields.Visible(story))
                    {
                        string warning = NativeFields.Texts(NativeFields.Get(story, "warning_text"));
                        if (!string.IsNullOrEmpty(warning)) lines.Add(warning);
                    }
                }
            }
            return string.Join("\n", lines.ToArray());
        }
        internal void OptionDescriptionCommitted(optionCtrl owner)
        {
            if (owner == null || !owner.is_open) { return; }
            if ((optionCtrl.OptionMenuState)NativeFields.Get(owner, "menuState_") == optionCtrl.OptionMenuState.Menu) { Options(owner); return; }
            IList categories = (IList)NativeFields.Get(owner, "categoryItem");
            foreach (optionCategoryItem category in categories) { if (category.category == (optionCtrl.Category)NativeFields.Get(owner, "current_cat")) { Category(category); break; } }
        }
        internal void OptionValueChanged(optionItem item)
        {
            optionCtrl owner = optionCtrl.instance;
            if (owner != null && owner.is_open) { Options(owner); }
        }
        internal void KeyCapture(KeyConfigButtonSprite sprite, bool input)
        {
            optionItem item = sprite.GetComponentInParent<optionItem>();
            if (item == null || optionCtrl.instance == null || !optionCtrl.instance.is_open) { return; }
            if (input) { Request(item, "key-capture", delegate { Emit(item, "key-capture", "waiting", NativeFields.Texts(NativeFields.Get(optionCtrl.instance, "title_back_text_"))); }); }
            else { Clear(item); OptionValueChanged(item); }
        }
        private string OptionValue(optionItem item)
        {
            AccessibilityOptionItem added = item as AccessibilityOptionItem; if (added != null) { return added.Value; }
            if (item is optionSelectItem) { return NativeFields.Text(NativeFields.Get(item, "select_"), "text_"); }
            if (item is optionBgm || item is optionSe) { return NativeFields.Text(NativeFields.Get(item, "gauge_"), "count_text_"); }
            if (item is optionKeyConfigButton || item is optionSummaryLAKeyConfig) { return ImageText.KeyName((KeyCode)NativeFields.Get(item, "current_key_code")); }
            return SpecificOptionValue(item);
        }
        private string SpecificOptionValue(optionItem item)
        {
            // These are native Toggle controls, with localized display labels owned by the game.
            if (item is optionToggleItem || item is optionSkip || item is optionVibration || item is optionWindow)
            {
                IList labels = (IList)NativeFields.Get(NativeFields.Get(item, "select_"), "count_text_"); int selected = NativeFields.Number(item, "setting_value_");
                return selected >= 0 && selected < labels.Count ? NativeFields.TextValue(labels[selected] as Text) : null;
            }
            if (item is optionKeyConfig)
            {
                IList buttons = (IList)NativeFields.Get(item, "button_list_"); int selected = NativeFields.Number(item, "list_count_");
                return selected >= 0 && selected < buttons.Count ? NativeFields.Join(NativeFields.Text(buttons[selected], "option_title_"), ImageText.KeyName((KeyCode)NativeFields.Get(buttons[selected], "current_key_code"))) : null;
            }
            if (item is optionSave)
            {
                IList choices = (IList)NativeFields.Get(item, "select_plate_"); int selected = NativeFields.Number(item, "cursor_num_");
                return selected >= 0 && selected < choices.Count ? NativeFields.Text(choices[selected], "text_") : null;
            }
            if (item is optionCredit || item is optionPrivacyPolicy) { return NativeFields.Text(NativeFields.Get(item, "select_"), "text_"); }
            return null;
        }
        internal void Category(optionCategoryItem category)
        {
            Request(category, "category", delegate
            {
                optionCtrl owner = category.GetComponentInParent<optionCtrl>();
                if (owner == null || !owner.is_open || (optionCtrl.OptionMenuState)NativeFields.Get(owner, "menuState_") != optionCtrl.OptionMenuState.Category || ModalBlocks(owner)) { return; }
                string text = SettingsText(NativeFields.Text(category, "m_Text"), null, OptionDescription(owner, true));
                string identity = category.category + ":" + text + ":" + ReadGuideText(owner.key_guide); string previous;
                if (_optionCategoryFocus.TryGetValue(owner.GetInstanceID(), out previous) && previous == identity) { return; }
                _optionCategoryFocus[owner.GetInstanceID()] = identity;
                _optionFocus.Remove(owner.GetInstanceID()); ClearSnapshots(owner);
                Emit(category, "category", identity, AppendOptionGuide(owner, text));
            });
        }
        internal void Save(SaveLoadUICtrl owner, bool initial)
        {
            if (initial) { ClearSnapshots(owner); }
            Request(owner, "save", delegate
            {
                if (!owner.is_open || ModalBlocks(owner)) { return; }
                int index = NativeFields.Number(owner, "select_num_"); SaveSlot[] slots = (SaveSlot[])NativeFields.Get(owner, "slot_list_");
                if (index < 0 || index >= slots.Length) { return; }
                SaveSlot.Slot slot = slots[index].slot;
                string text = NativeFields.Join(NativeFields.TextValue(slot.number), slot.save_active ? NativeFields.Join(NativeFields.TextValue(slot.title_), NativeFields.TextValue(slot.scenario_), NativeFields.TextValue(slot.progress), NativeFields.TextValue(slot.time_)) : NativeFields.TextValue(slot.no_data_));
                Emit(owner, "save", owner.current_type_ + ":" + index, text, !initial);
            });
        }
        internal void Closed(Component owner)
        {
            SaveConfirmationWindow confirmation = owner as SaveConfirmationWindow;
            if (confirmation != null && !confirmation.is_loaded)
            {
                SaveLoadUICtrl list = confirmation.GetComponentInParent<SaveLoadUICtrl>();
                if (list != null) { ClearSnapshots(list); Save(list, true); }
            }
            if (owner is creditCtrl && optionCtrl.instance != null && optionCtrl.instance.is_open)
            {
                optionCtrl option = optionCtrl.instance; _optionFocus.Remove(option.GetInstanceID()); ClearSnapshots(option); Options(option);
            }
            SaveLoadUICtrl save = owner as SaveLoadUICtrl;
            if (save != null && !save.is_loaded && optionCtrl.instance != null && optionCtrl.instance.is_open)
            {
                optionCtrl option = optionCtrl.instance; _optionFocus.Remove(option.GetInstanceID()); ClearSnapshots(option); Options(option);
            }
        }
        internal void SaveIo(SaveConfirmationWindow owner)
        {
            Request(owner, "save-io", delegate
            {
                if (!owner.is_open) { return; }
                object window = NativeFields.Get(owner, "window_");
                Emit(owner, "save-io", "active", NativeFields.Text(window, "save_text_"), false);
            });
        }
        internal void Message(messageBoxCtrl owner)
        {
            Request(owner, "message", delegate { Emit(owner, "message", "opened", NativeFields.Texts(NativeFields.Get(owner, "text_list_")), false); });
        }
        internal void Confirm(ConfirmDialog owner, bool shown)
        {
            if (shown) { _shownDialogs.Add(owner.GetInstanceID()); ClearSnapshots(owner); }
            if (!_shownDialogs.Contains(owner.GetInstanceID())) { return; }
            Request(owner, "confirm", delegate
            {
                IList choices = (IList)NativeFields.Get(owner, "m_Select"); int index = Convert.ToInt32(owner.CurrentDirection) - 1;
                if (index < 0 || index >= choices.Count) { return; }
                string label = NativeFields.Text(choices[index], "m_Text");
                bool initial = !_spoken.ContainsKey(owner.GetInstanceID() + ":confirm");
                Emit(owner, "confirm", index.ToString(), initial ? NativeFields.Join(NativeFields.Texts(NativeFields.Get(owner, "m_MessageTarget")), label) : label);
            });
        }
        internal void TextSelector(TextEasySelectorListItem owner)
        {
            Request(owner, "selector", delegate { Emit(owner, "selector", owner.GetPageIndex().ToString(), NativeFields.TextValue(((Text[])NativeFields.Get(owner, "m_SelectText"))[0])); });
        }
        internal void StudioListOpened(GalleryActionStudioListWindowCtrl owner, GalleryActionStudioListItemContent[] contents) { _studioLists[owner.GetInstanceID()] = contents; ClearSnapshots(owner); }
        internal void StudioList(GalleryActionStudioListWindowCtrl owner)
        {
            Request(owner, "studio-list", delegate
            {
                GalleryActionStudioListItemContent[] contents;
                if (!_studioLists.TryGetValue(owner.GetInstanceID(), out contents) || !owner.IsActiveInput) { return; }
                int index = NativeFields.Number(owner, "m_SelectedIdx");
                if (index >= 0 && index < contents.Length) { Emit(owner, "studio-list", contents[index].Identifer.ToString(), contents[index].BodyMessage); }
            });
        }
        internal void Gallery(Component owner, string method)
        {
            string channel = method == "UpdatePlayMode" || method == "ToggleSettingBgmModeIfNeeded" || method == "SetInformationSongText" ? method : "gallery";
            Request(owner, channel, delegate { ReadGallery(owner, method); });
        }
        private void ReadGallery(Component owner, string method)
        {
            if (optionCtrl.instance != null && optionCtrl.instance.is_open) { return; }
            GalleryCtrl root = owner as GalleryCtrl;
            if (root != null)
            {
                if (!(bool)NativeFields.Get(root, "IsExecuting")) { return; }
                CanvasGroup[] groups = (CanvasGroup[])NativeFields.Get(root, "m_SceneGroup");
                if (groups == null || groups.Length == 0 || groups[0] == null || groups[0].alpha <= 0f) { return; }
                int index = NativeFields.Number(root, "m_CurrentIndex"); IDictionary table = (IDictionary)NativeFields.Get(typeof(GalleryCtrl), "TABLE");
                object row = table[Enum.ToObject(typeof(GalleryCtrl).GetNestedType("ContentKind", BindingFlags.Public | BindingFlags.NonPublic), index + 1)];
                if (row != null) { Emit(owner, "gallery", index.ToString(), TextDataCtrl.GetText((TextDataCtrl.GalleryTextID)NativeFields.Get(row, "ButtonText"), 0)); }
                return;
            }
            GalleryOrchestraCtrl music = owner as GalleryOrchestraCtrl;
            if (music != null)
            {
                if (!music.IsInit || !(bool)NativeFields.Get(music, "m_IsExecuting")) { return; }
                int index = NativeFields.Number(music, "m_CurrentSelectIndex"); object title = NativeFields.Get(music, "m_CurrentSelectTitle");
                IDictionary table = (IDictionary)NativeFields.Get(typeof(GalleryOrchestraCtrl), "ALBUM_TABLE"); IList rows = table[title] as IList;
                if (rows == null || index < 0 || index >= rows.Count) { return; }
                string song = TextDataCtrl.GetText((TextDataCtrl.GalleryTextID)NativeFields.Get(rows[index], "NameText"), 0);
                if (method == "ToggleSettingBgmModeIfNeeded") { Emit(owner, "bgm-setting", NativeFields.Text(music, "m_BgmSettingText"), NativeFields.Text(music, "m_BgmSettingText")); return; }
                if (method == "SetInformationSongText") { Emit(owner, "playing-song", NativeFields.TextValue(((Text[])NativeFields.Get(music, "m_SongText"))[0]), NativeFields.TextValue(((Text[])NativeFields.Get(music, "m_SongText"))[0]), false); return; }
                if (method == "UpdatePlayMode") { Emit(owner, "play-mode", NativeFields.Text(music, "m_ModeText"), NativeFields.Text(music, "m_ModeText")); return; }
                Emit(owner, "gallery", title + ":" + index, song); return;
            }
            GalleryArtLibraryKindSelectorCtrl kind = owner as GalleryArtLibraryKindSelectorCtrl;
            if (kind != null)
            {
                if (!kind.IsShow) { return; }
                int index = NativeFields.Number(kind, "CurrentContent");
                IList labels = (IList)NativeFields.Get(kind, "m_SelectorText");
                Emit(owner, "gallery", index.ToString(), ImageText.Read((AssetBundleSpriteImage)labels[index])); return;
            }
            GalleryArtLibraryContentsCtrl content = owner as GalleryArtLibraryContentsCtrl;
            if (content != null) { ReadArtwork(content); return; }
            GalleryActionStudioCtrl studio = owner as GalleryActionStudioCtrl;
            if (studio != null)
            {
                if (!(bool)NativeFields.Get(studio, "m_IsExecuting") || (bool)NativeFields.Get(studio, "m_IsExecShowList")) { return; }
                int current = NativeFields.Number(studio, "m_CurrentContent"); IList controls = (IList)NativeFields.Get(studio, "m_ListItem");
                if (controls == null || current < 0 || current >= controls.Count || controls[current] == null) { return; }
                object control = controls[current]; IList contents = (IList)NativeFields.Get(control, "m_Contents");
                if (contents == null) { return; } // Native initialization has not populated this column yet.
                int index = NativeFields.Number(control, "CurrentSelectIndex");
                string text = index >= 0 && index < contents.Count && contents[index] != null ? (string)NativeFields.Get(contents[index], "BodyMessage") : null;
                Emit(owner, "gallery", current + ":" + index, text); return;
            }
            GalleryActionStudioSelectorCtrl selector = owner as GalleryActionStudioSelectorCtrl;
            if (selector != null) { int index = NativeFields.Number(selector, "m_CurrentIndex"); Emit(owner, "gallery", index.ToString(), ImageText.SeriesName(index)); }
        }
        internal void ArtTag(GalleryArtLibraryTitleSelector owner)
        {
            Request(owner, "art-tag", delegate
            {
                int index = NativeFields.Number(owner, "CurrentIndex"); string[] names = (string[])NativeFields.Get(owner, "TagNames");
                if (names != null && index >= 0 && index < names.Length) { Emit(owner, "art-tag", index.ToString(), names[index]); }
            });
        }
        internal void StudioReference(object reference)
        {
            foreach (Component component in new List<Component>(_owners.Values))
            {
                GalleryActionStudioCtrl studio = component as GalleryActionStudioCtrl;
                if (studio == null || !NativeFields.Visible(studio)) { continue; }
                IList controls = (IList)NativeFields.Get(studio, "m_ListItem"); int selected = NativeFields.Number(studio, "m_CurrentContent");
                if (controls != null && selected >= 0 && selected < controls.Count && ReferenceEquals(controls[selected], reference)) { Gallery(studio, "current-index"); return; }
            }
        }
        internal void Voice(VoiceEasySelectorListItem owner)
        {
            Request(owner, "voice", delegate
            {
                GalleryActionStudioCtrl studio = owner.GetComponentInParent<GalleryActionStudioCtrl>();
                if (studio == null || !(bool)NativeFields.Get(studio, "m_IsExecuting")) { return; }
                int index = owner.GetPageIndex(); IList voices = (IList)NativeFields.Get(typeof(GalleryActionStudioCtrl), "VOICE_TABLE");
                if (index < 0 || index >= voices.Count) { return; }
                int characterIcon = NativeFields.Number(voices[index], "CharacterSpriteIndex"); string character = null;
                IDictionary table = (IDictionary)NativeFields.Get(typeof(GalleryActionStudioCtrl), "CHARACTER_TABLE");
                foreach (IList rows in table.Values)
                {
                    foreach (object row in rows) { if (NativeFields.Number(row, "IconNoteNo") == characterIcon) { character = TextDataCtrl.GetText((TextDataCtrl.GalleryTextID)NativeFields.Get(row, "NameText"), 0); break; } }
                    if (character != null) { break; }
                }
                Image bubble = (Image)NativeFields.Get(owner, "m_BubbleImage");
                if (bubble == null || bubble.sprite == null || !bubble.gameObject.activeInHierarchy) { return; }
                string bundle = "actioneditor_bigfont_" + GSUtility.GetLanguageFooter(GSStatic.global_work_.language);
                Emit(owner, "voice", index.ToString(), NativeFields.Join(character, ImageText.Lookup("/Gallery/ActionStudio/", bundle, bubble.sprite.name)));
            });
        }
        private void ReadArtwork(GalleryArtLibraryContentsCtrl owner)
        {
            if (!owner.IsShow) { return; }
            Func<GalleryArtLibraryTable.ContentKind> getKind = (Func<GalleryArtLibraryTable.ContentKind>)NativeFields.Get(owner, "GetNowArtKindFunc");
            GalleryArtLibraryTable.ContentKind kind = getKind();
            int index = NativeFields.Number(owner, "CurrentIndex");
            if (owner is GalleryArtLibraryImageSelectorCtrl) { index = GalleryArtLibraryTable.pageindex2index(kind, NativeFields.Number(owner, "CurrentPage"), index); }
            GalleryArtLibraryTable.ImageInfo info = GalleryArtLibraryTable.GetInfoAtIndex(kind, index);
            string title = TextDataCtrl.GetText(info.textID, 0);
            string description = null;
            if (!(owner is GalleryArtLibraryImageSelectorCtrl))
            {
                AssetBundleSpriteImage image = (AssetBundleSpriteImage)NativeFields.Get(owner, owner is GalleryArtLibraryViewerCtrl ? "CurrentImage" : "ContentImage");
                if (image == null || !image.HasReference || !image.active || !image.image_.enabled || image.obj == null || !image.obj.activeInHierarchy) { return; }
                string actualName = ReplaceLanguage.GetFileName(image.path_, image.name_);
                if (image.path_ != info.path || actualName != ReplaceLanguage.GetFileName(info.path, info.name)) { throw new InvalidOperationException("Artwork identity has not committed to the native image."); }
                description = ImageText.Describe(image);
            }
            Emit(owner, "gallery", kind + ":" + index, NativeFields.Join(title, description));
        }
        internal void StartupDialog(Component owner)
        {
            Request(owner, "startup-dialog", delegate
            {
                bool initial = !_spoken.ContainsKey(owner.GetInstanceID() + ":startup-dialog");
                if (owner is DialogReleaseNoteCtrl)
                {
                    string button = NativeFields.Text(NativeFields.Get(owner, "m_DecidePlate"), "text_");
                    Emit(owner, "startup-dialog", "release", NativeFields.Join(NativeFields.Text(owner, "m_TitleText"), NativeFields.Text(owner, "m_MessageText"), button), false);
                    return;
                }
                int current = NativeFields.Number(owner, "cursor_no_"); IList choices = (IList)NativeFields.Get(owner, "select_list_");
                if (current < 0 || current >= choices.Count) { return; }
                string selected = NativeFields.Text(choices[current], "text_");
                Emit(owner, "startup-dialog", current.ToString(), initial ? NativeFields.Join(NativeFields.Text(owner, "message_text_"), selected) : selected, !initial);
            });
        }
        internal void Guide(keyGuideBase guide)
        {
            if (guide == null || !(guide is titleGuideCtrl) && guide.GetComponentInParent<optionCtrl>() == null && guide.GetComponentInParent<SaveLoadUICtrl>() == null && guide.GetComponentInParent<creditCtrl>() == null && guide.GetComponentInParent<GalleryCtrl>() == null && guide.GetComponentInParent<GalleryNestSceneCtrl>() == null) { return; }
            optionKeyGuide optionGuide = guide as optionKeyGuide;
            if (optionGuide != null) { OptionDescriptionCommitted(optionCtrl.instance); return; }
            Request(guide, "guide", delegate
            {
                string text = ReadGuideText(guide);
                if (_runtime.Assistance != null) { text = _runtime.Assistance.AutomaticGuide(guide, text); }
                Emit(guide, "guide", text, text, false);
            });
        }
        internal string ReadGuideText(keyGuideBase guide)
        {
            if (guide == null || !NativeFields.Visible(guide)) { return null; }
            IList icons = (IList)NativeFields.Get(guide, "guide_list_");
            List<string> parts = new List<string>();
            foreach (keyGuideBase.GuideIcon icon in icons)
            {
                if (!icon.active || icon.sprite_ == null || !icon.sprite_.active || !icon.sprite_.enabled || icon.sprite_.color.a <= 0f || icon.touch_ == null) { continue; }
                string label = NativeFields.TextValue(icon.text_);
                if (string.IsNullOrEmpty(label)) { continue; }
                string identity; string key = _glyphs.Read(icon.sprite_, out identity);
                keyGuideBoard board = guide as keyGuideBoard;
                if (board != null && board == keyGuideCtrl.instance && icon == (keyGuideBase.GuideIcon)icons[0]
                    && (board.current_guide == keyGuideBase.Type.KAGAKU_SYOUSAI || board.current_guide == keyGuideBase.Type.FORCE_KAGAKU_SYOUSAI
                        || board.current_guide == keyGuideBase.Type.PRESENT_KAGAKU_SYOUSAI || board.current_guide == keyGuideBase.Type.ENDING_KAGAKU_SYOUSAI
                        || board.current_guide == keyGuideBase.Type.VASE_SHOW)
                    && !keyGuideBase.keyguid_pad_ && (bool)NativeFields.Get(board, "rot_union"))
                {
                    List<string> rotation = new List<string>();
                    foreach (AssetBundleSprite extra in (IList)NativeFields.Get(board, "rot_symbol_list_"))
                    { if (extra != null && extra.HasReference && extra.active && extra.enabled && extra.sprite_renderer_ != null && extra.sprite_renderer_.enabled && extra.color.a > 0f && extra.obj != null && extra.obj.activeInHierarchy) rotation.Add(_glyphs.Read(extra, out identity)); }
                    rotation.Add(key); key = string.Join(", ", rotation.ToArray());
                }
                parts.Add(label + " " + key);
            }
            return string.Join("\n", parts.ToArray());
        }
        private bool OptionGuideChanged(optionCtrl owner)
        {
            string text = ReadGuideText(owner.key_guide); string previous;
            return !string.IsNullOrEmpty(text) && (!_optionGuideSpoken.TryGetValue(owner.GetInstanceID(), out previous) || previous != text);
        }
        private string AppendOptionGuide(optionCtrl owner, string text)
        {
            string guide = ReadGuideText(owner.key_guide); string previous;
            if (string.IsNullOrEmpty(guide)) { return text; }
            if (_runtime.Assistance == null && _optionGuideSpoken.TryGetValue(owner.GetInstanceID(), out previous) && previous == guide) { return text; }
            _optionGuideSpoken[owner.GetInstanceID()] = guide;
            if (_runtime.Assistance != null) { guide = _runtime.Assistance.AutomaticGuide(owner.key_guide, guide); }
            return NativeFields.Join(text, guide);
        }
        internal void Credits(creditCtrl owner)
        {
            Request(owner, "credits", delegate
            {
                if (!owner.is_play || !(bool)NativeFields.Get(owner, "body_active_")) { return; }
                IList rows = (IList)NativeFields.Get(owner, "credit_text_data_");
                List<string> lines = new List<string>();
                foreach (object row in rows) { string text = (string)NativeFields.Get(row, "credit_text"); if (!string.IsNullOrEmpty(text)) { lines.Add(UiResources.Plain(text)); } }
                Emit(owner, "credits", "native-current-credit-document", NativeFields.Join(TextDataCtrl.GetText(TextDataCtrl.OptionTextID.ITEM_CREDITS), string.Join("\n", lines.ToArray())), false);
            });
        }
        internal void GuideReady(keyGuideBase guide)
        {
            creditCtrl credits = guide.GetComponentInParent<creditCtrl>(); if (credits != null) { Credits(credits); }
            GalleryCtrl root = guide.GetComponentInParent<GalleryCtrl>(); if (root != null) { Gallery(root, "ready"); }
            GalleryActionStudioCtrl studio = guide.GetComponentInParent<GalleryActionStudioCtrl>(); if (studio != null) { Gallery(studio, "ready"); }
            GalleryActionStudioSelectorCtrl selector = guide.GetComponentInParent<GalleryActionStudioSelectorCtrl>(); if (selector != null) { Gallery(selector, "ready"); }
            Guide(guide);
        }
        internal void ClearTree(Component root)
        {
            if (root == null) { return; }
            foreach (int id in new List<int>(_owners.Keys))
            {
                Component owner = _owners[id];
                if (owner == null)
                {
                    _owners.Remove(id); string prefix = id + ":";
                    foreach (string key in new List<string>(_pending.Keys)) { if (key.StartsWith(prefix, StringComparison.Ordinal)) { _pending[key].Cancelled = true; _pending.Remove(key); } }
                    foreach (string key in new List<string>(_spoken.Keys)) { if (key.StartsWith(prefix, StringComparison.Ordinal)) { _spoken.Remove(key); } }
                    _optionCategoryFocus.Remove(id); _optionGuideSpoken.Remove(id); _optionFocus.Remove(id); _plates.Remove(id); _shownDialogs.Remove(id); _studioLists.Remove(id);
                }
                else if (owner == root || owner.transform.IsChildOf(root.transform)) { Clear(owner); }
            }
            Clear(root); // A settings root can own introduction state before its first item request.
        }
        private bool ModalBlocks(Component owner)
        {
            if (messageBoxCtrl.instance != null && _runtime.Title != null && _runtime.Title.MessageBoxBlocks()) { return true; }
            SaveLoadUICtrl save = owner as SaveLoadUICtrl;
            if (save != null)
            {
                SaveConfirmationWindow window = (SaveConfirmationWindow)NativeFields.Get(save, "save_window_");
                if (window != null && window.is_open) { return true; }
            }
            optionCtrl option = owner as optionCtrl;
            if (option != null)
            {
                titleSelectPlate confirmation = (titleSelectPlate)NativeFields.Get(option, "confirmation_select_");
                if (confirmation != null && confirmation.body_active && !confirmation.is_end) { return true; }
                if (SaveLoadUICtrl.instance != null && SaveLoadUICtrl.instance.is_open) { return true; }
                if (creditCtrl.instance != null && creditCtrl.instance.is_play) { return true; }
            }
            return false;
        }
        /// <summary>Invalidates only unsubmitted snapshots and native-page identity caches.</summary>
        public void Dispose() { foreach (Pending task in _pending.Values) { task.Cancelled = true; } _pending.Clear(); _spoken.Clear(); _plates.Clear(); _studioLists.Clear(); _owners.Clear(); _optionFocus.Clear(); _optionGuideSpoken.Clear(); _optionCategoryFocus.Clear(); }
    }
}
