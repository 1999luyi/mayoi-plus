using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class StoryMessagesHandler : IDisposable
    {
        private sealed class Span
        {
            internal MdtData Data;
            internal uint Start;
            internal uint End;
        }
        private readonly UiRuntime _runtime;
        private readonly Dictionary<MessageWork, Span> _spans = new Dictionary<MessageWork, Span>();
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _pending = new List<string>();
        private readonly FieldInfo _data = UiResources.Field(typeof(MdtData), "message_data_");
        private readonly MethodInfo _page = typeof(MdtData).GetMethod("isPageCommand", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly ControlGlyphs _glyphs;
        private readonly StorySpeakerState _speaker = new StorySpeakerState();
        private sealed class KeySlot { internal int Start; internal int Length; }
        private readonly Dictionary<Text, KeySlot> _renderedKeys = new Dictionary<Text, KeySlot>();
        private MessageWork _pendingWork;
        private int _moveDepth;
        private int _restoreSerial;
        internal StoryMessagesHandler(UiRuntime runtime)
        {
            _runtime = runtime;
            _glyphs = new ControlGlyphs(runtime.Messages);
            foreach (XmlElement row in UiResources.EmbeddedXml("Pwaat.Accessibility.SpeakerNames.xml").SelectNodes("/speakers/name"))
            {
                _names.Add(row.GetAttribute("path") + row.GetAttribute("bundle") + ":" + row.GetAttribute("sprite"), row.InnerText);
            }
            if (_page == null) { throw new MissingMethodException("MdtData", "isPageCommand"); }
        }
        internal void BeginMove() { _moveDepth++; }
        internal void EndMove()
        {
            if (_moveDepth > 0) { _moveDepth--; }
            if (_moveDepth != 0 || _pending.Count == 0) { return; }
            int segments = _pending.Count;
            string text = string.Join("\n", _pending.ToArray());
            MessageWork work = _pendingWork;
            _pending.Clear(); _pendingWork = null;
            if (!Allowed(work)) { return; }
            _runtime.Trace("story_segment", "window=" + work.message_type + " message=" + work.now_no + " segments=" + segments + " characters=" + text.Length);
            if (_runtime.Presentation != null) { _runtime.Presentation.Story(text); } else { _runtime.Speech.Say(text, true); }
        }
        private bool Allowed(MessageWork work)
        {
            if (work == null || work.mdt_data == null || messageBoardCtrl.instance == null || !NativeFields.Visible(messageBoardCtrl.instance)) { return false; }
            if (!ReferenceEquals(MessageSystem.GetActiveMessageWork(), work)) { return false; }
            if (optionCtrl.instance != null && optionCtrl.instance.is_open) { return false; }
            if (SaveLoadUICtrl.instance != null && SaveLoadUICtrl.instance.is_open) { return false; }
            if (_runtime.StoryUi != null && _runtime.StoryUi.BoxBlocks()) { return false; }
            if ((work.status & MessageSystem.Status.SELECT) != 0) { return false; }
            if (selectPlateCtrl.instance != null && selectPlateCtrl.instance.body_active) { return false; }
            // The CG/centered text can be visible without the speech-frame background.
            messageBoardCtrl board = messageBoardCtrl.instance;
            return board.body_active && board.text != null && board.text.activeInHierarchy;
        }
        internal void CharacterCommitted(MessageWork work)
        {
            if (!Allowed(work)) { return; }
            MdtData data = work.mdt_data;
            uint index = work.mdt_index;
            Span span;
            if (_spans.TryGetValue(work, out span) && ReferenceEquals(span.Data, data) && index >= span.Start && index < span.End) { return; }
            ushort[] words = (ushort[])_data.GetValue(data);
            if (index >= words.Length) { throw new InvalidDataException("Native message cursor outside data."); }
            if (words[index] <= 128 && words[index] != 0x7f) { return; }
            Func<ushort, bool> nativePage = (Func<ushort, bool>)Delegate.CreateDelegate(typeof(Func<ushort, bool>), data, _page);
            StoryTextDecoder.Segment segment = StoryTextDecoder.Read(words, index, MessageSystem.code_proc_arg_count_table, nativePage, InlineKey);
            if (segment.End <= index) { return; }
            _spans[work] = new Span { Data = data, Start = segment.Start, End = segment.End };
            string text = UiResources.Plain(MessageSystem.EnToHalf(segment.Text, GSStatic.global_work_.language));
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) { return; }
            string identity; string speaker = ReadSpeaker(out identity);
            bool announce = _speaker.ShouldSpeak(identity);
            _pendingWork = work;
            _pending.Add(NativeFields.Join(announce ? string.Format(_runtime.Messages.Get("story.speaker"), speaker) : null, text));
            if (announce) { _speaker.Committed(identity); }
            if (_moveDepth == 0) { EndMove(); }
        }
        private string ReadSpeaker(out string identity)
        {
            identity = null;
            AssetBundleSprite image = messageBoardCtrl.instance.sprite_name;
            if (image == null || !image.active || !image.enabled || !image.HasReference || image.obj == null || !image.obj.activeInHierarchy || image.color.a <= 0f) { return null; }
            Sprite sprite = image.sprite_renderer_ != null ? image.sprite_renderer_.sprite : (image.image_ == null ? null : image.image_.sprite);
            if (sprite == null) { return null; }
            identity = image.path_ + ReplaceLanguage.GetFileName(image.path_, image.name_) + ":" + sprite.name;
            string name;
            if (!_names.TryGetValue(identity, out name)) { throw new InvalidDataException("Unaudited active name plate: " + identity); }
            if (string.IsNullOrEmpty(name)) { identity = null; }
            return name;
        }
        private string InlineKey(int index)
        {
            KeyType[] actions = { KeyType.A, KeyType.B, KeyType.X, KeyType.R };
            if (index < 0 || index >= actions.Length) { throw new InvalidDataException("Invalid native inline key action."); }
            if (!keyGuideBase.keyguid_pad_) { return ImageText.KeyName(padCtrl.instance.GetKeyCode(actions[index])); }
            int sprite = keyGuideBase.GuideIcon.GetKeyPadSpriteNum(actions[index]);
            return _glyphs.Lookup("symbol_xbox_large", "symbol_steamDeck_" + sprite.ToString());
        }
        internal void Reset(MessageWork work) { if (work != null) { _spans.Remove(work); } }
        internal bool CanRepeat()
        {
            messageBoardCtrl board = messageBoardCtrl.instance;
            return board != null && board.body_active && NativeFields.Visible(board) && board.text != null && board.text.activeInHierarchy
                && _runtime.Presentation != null && _runtime.Presentation.Foreground();
        }
        internal void ObserveSpeaker() { string identity; ReadSpeaker(out identity); _speaker.Observe(identity); }
        internal void HideSpeaker() { _speaker.Clear(); }
        internal void RenderedKey(textKeyIconCtrl owner, Text line, int index)
        {
            if (messageBoardCtrl.instance == null || owner != messageBoardCtrl.instance.msg_key_icon || line == null) { return; }
            MessageWork work = MessageSystem.GetActiveMessageWork();
            if (work == null || work.code != 0x7f) { return; }
            string current = UiResources.Plain(line.text);
            int prefix = current.Length - work.work - 1;
            bool deck = SummaryLAUtil.IsSteamDeckMode() && keyGuideBase.keyguid_pad_;
            int length = deck ? owner.changeTextToButtonIconSpace("").Length : owner.changeTextToIconSpase("").Length;
            if (prefix < 0 || index < 0 || index >= owner.key_icon.Length) { return; }
            _renderedKeys[line] = new KeySlot { Start = prefix, Length = length };
        }
        internal string RenderedText()
        {
            if (!CanRepeat()) { return null; }
            messageBoardCtrl board = messageBoardCtrl.instance;
            List<string> text = new List<string>();
            string identity; string speaker = ReadSpeaker(out identity);
            if (!string.IsNullOrEmpty(speaker)) { text.Add(string.Format(_runtime.Messages.Get("story.speaker"), speaker)); }
            for (int i = 0; i < board.line_list.Count; i++)
            {
                Text line = board.line_list[i]; string value = NativeFields.TextValue(line);
                if (string.IsNullOrEmpty(value)) { continue; }
                textKeyIconCtrl icons = board.msg_key_icon; KeySlot slot;
                if (i < icons.key_icon.Length && icons.key_icon[i].icon_active_)
                {
                    string glyphId; string key = _glyphs.Read(icons.key_icon[i].icon, out glyphId);
                    bool knownSlot = _renderedKeys.TryGetValue(line, out slot) && slot.Start <= value.Length;
                    int count = knownSlot ? Math.Min(slot.Length, value.Length - slot.Start) : 0;
                    for (int j = 0; j < count && knownSlot; j++) { char c = value[slot.Start + j]; if (c != ' ' && c != '\u3000') { knownSlot = false; } }
                    // A restored save may supply rendered icons without a new insertion callback.
                    // Preserve every visible character and include that real icon without guessing a slot.
                    value = knownSlot ? value.Remove(slot.Start, count).Insert(slot.Start, key) : NativeFields.Join(value, key);
                }
                text.Add(value);
            }
            string evidence = _runtime.Presentation == null ? null : _runtime.Presentation.EvidenceText();
            string result = NativeFields.Join(evidence, string.Join("\n", text.ToArray()));
            return result;
        }
        internal void RepeatRendered()
        {
            string text = RenderedText();
            _runtime.Speech.Say(string.IsNullOrEmpty(text) ? _runtime.Messages.Get("story.no-text") : text, true);
        }
        internal void Restored(messageBoardCtrl owner)
        {
            _renderedKeys.Clear();
            int serial = ++_restoreSerial;
            MessageWork work = MessageSystem.GetActiveMessageWork();
            if (_runtime.Presentation == null || owner == null) return;
            _runtime.Presentation.ReadUi(owner, delegate
            {
                if (serial != _restoreSerial || owner != messageBoardCtrl.instance || work != MessageSystem.GetActiveMessageWork() || !CanRepeat()) return;
                if (recordListCtrl.instance != null && recordListCtrl.instance.is_open || selectPlateCtrl.instance != null && selectPlateCtrl.instance.body_active) return;
                if (GSStatic.menu_work.inspect_is_play || GSStatic.menu_work.move_is_play || GSStatic.menu_work.tantei_menu_is_play) return;
                string text = RenderedText();
                if (!string.IsNullOrEmpty(text)) { _runtime.Trace("story_load_restored", "characters=" + text.Length); _runtime.Presentation.Story(text); }
            });
        }
        internal void RenderedClear() { _renderedKeys.Clear(); _restoreSerial++; }
        internal void Clear()
        {
            _spans.Clear(); _pending.Clear(); _pendingWork = null; _speaker.Clear(); _renderedKeys.Clear(); _restoreSerial++;
            if (_runtime.Presentation != null) { _runtime.Presentation.Clear(); }
        }
        /// <summary>Clears unsubmitted story state without stopping shared speech.</summary>
        public void Dispose() { Clear(); _moveDepth = 0; }
    }
}
