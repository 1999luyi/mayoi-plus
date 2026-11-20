using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class StoryUiHandler : IDisposable
    {
        private sealed class Pending { internal Component Owner; internal Action Read; internal bool Cancelled; }
        private sealed class Focus { internal string Identity; internal string Text; }
        private readonly UiRuntime _runtime;
        private readonly MonoBehaviour _host;
        private readonly ControlGlyphs _glyphs;
        private readonly Dictionary<Component, Pending> _pending = new Dictionary<Component, Pending>();
        private readonly Dictionary<Component, Focus> _focus = new Dictionary<Component, Focus>();
        private readonly Dictionary<int, string> _boxTemplates = new Dictionary<int, string>();
        private readonly MessageBoxLifecycle _box = new MessageBoxLifecycle();
        private messageBoxCtrl _boxOwner;
        private messageBoxCtrl _templateOwner;
        private int _recordType = -1;
        private bool _recordReady;
        private bool _boxSelecting;
        private bool _boxReady;
        private bool _boxPresented;
        private int _boxCursor = -1;
        private string _boxBody;
        internal StoryUiHandler(UiRuntime runtime, MonoBehaviour host) { _runtime = runtime; _host = host; _glyphs = new ControlGlyphs(runtime.Messages); }
        internal bool BoxBlocks()
        {
            return _boxOwner != null && _box.Blocks(_boxOwner, _boxOwner.active, NativeFields.Visible(_boxOwner));
        }
        private bool Available(Component owner)
        {
            bool modal = owner is messageBoxCtrl;
            return NativeFields.Visible(owner) && (modal || (optionCtrl.instance == null || !optionCtrl.instance.is_open) && (SaveLoadUICtrl.instance == null || !SaveLoadUICtrl.instance.is_open));
        }
        private void Request(Component owner, Action read)
        {
            if (owner == null) { return; }
            Pending task;
            if (_pending.TryGetValue(owner, out task)) { task.Read = read; return; }
            task = new Pending { Owner = owner, Read = read }; _pending.Add(owner, task);
            if (_runtime.Presentation != null) { _runtime.Presentation.ReadUi(owner, delegate { ReadCommitted(task); }); }
            else { _host.StartCoroutine(AfterCommit(task)); }
        }
        private IEnumerator AfterCommit(Pending task)
        {
            yield return null; // One bounded native-commit coalescing step; never a readiness retry.
            ReadCommitted(task);
        }
        private void ReadCommitted(Pending task)
        {
            if (task.Cancelled) { return; }
            _pending.Remove(task.Owner);
            if (!Available(task.Owner)) { return; }
            try { task.Read(); }
            catch (Exception error) { _runtime.ReadFailure("story-ui/" + task.Owner.GetType().Name, error); }
        }
        private void Speak(Component owner, string identity, string text, string prefix = null, string suffix = null)
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) { return; }
            Focus old;
            if (_focus.TryGetValue(owner, out old) && old.Identity == identity && old.Text == NativeFields.Join(text, suffix)) { return; }
            _focus[owner] = new Focus { Identity = identity, Text = NativeFields.Join(text, suffix) };
            _runtime.Trace("story_ui", "owner=" + owner.GetType().Name + " identity=" + identity + " characters=" + text.Length);
            if (_runtime.Presentation != null) { _runtime.Presentation.Ui(owner, NativeFields.Join(prefix, text), suffix); }
            else { _runtime.Speech.Say(NativeFields.Join(prefix, text, suffix), true); }
        }
        internal void Choice(selectPlateCtrl owner)
        {
            Request(owner, delegate
            {
                if (!owner.body_active || owner.is_end || BoxBlocks() || (recordListCtrl.instance != null && recordListCtrl.instance.is_open)) { return; }
                IList items = (IList)NativeFields.Get(owner, "select_list_");
                int index = owner.cursor_no;
                if (index < 0 || index >= items.Count || index >= NativeFields.Number(owner, "cursor_num_")) { return; }
                string text = NativeFields.Text(items[index], "text_");
                if (_runtime.Investigation != null) { text = _runtime.Investigation.TopicText(owner, index, text); }
                Speak(owner, index.ToString(), text);
            });
        }
        internal void Record(recordListCtrl owner)
        {
            Request(owner, delegate
            {
                if (!_recordReady || !owner.is_open || !owner.body_active || owner.is_page_changing || owner.detail_open || BoxBlocks()) { return; }
                if (owner.note_id < 0 || piceDataCtrl.instance == null || owner.note_id >= piceDataCtrl.instance.note_data.Count) { return; }
                int type = NativeFields.Number(owner, "record_type_");
                string category = _recordType == type ? null : TextDataCtrl.GetText(type == 0 ? TextDataCtrl.CommonTextID.EVIDENCE : TextDataCtrl.CommonTextID.PROFILES);
                string text = _runtime.Presentation.RecordText(NativeFields.TextValue(owner.icon_name), NativeFields.Texts(owner.comment.line_));
                _runtime.Presentation.Guide(owner.record_guide);
                string description = null;
                if (_runtime.RecordImages != null)
                {
                    try { text = NativeFields.Join(text, _runtime.RecordImages.DetailHint(owner)); description = _runtime.RecordImages.ListDescription(owner); }
                    catch (Exception error) { _runtime.LogReadFailure("court-record-list-image", error); description = _runtime.Messages.Get("read_failed"); }
                }
                Speak(owner, type + ":" + owner.note_id + ":" + NativeFields.Number(owner, "page_num_"), text, category, description);
                _recordType = type;
            });
        }
        internal void BoxText(messageBoxCtrl owner, string template, int index)
        {
            if (_templateOwner != owner) { _boxTemplates.Clear(); _templateOwner = owner; }
            if (index >= 0) { _boxTemplates[index] = template; }
        }
        internal void BoxTexts(messageBoxCtrl owner, string[] templates)
        {
            if (_templateOwner != owner) { _boxTemplates.Clear(); _templateOwner = owner; }
            _boxTemplates.Clear();
            if (templates == null) { return; }
            for (int i = 0; i < templates.Length; i++) { _boxTemplates[i] = templates[i]; }
        }
        internal void RecordStarting(recordListCtrl owner)
        {
            _recordReady = false;
            Clear(owner);
        }
        internal void RecordReady(recordListCtrl owner)
        {
            if (owner != null && owner.is_open && owner.body_active) { _recordReady = true; Record(owner); }
        }
        internal void BoxOpened(messageBoxCtrl owner, bool selecting)
        {
            _boxOwner = owner; _box.Open(owner); _focus.Remove(owner);
            _boxSelecting = selecting; _boxReady = !selecting; _boxPresented = false; _boxBody = null; _boxCursor = -1;
            if (_boxReady) { Box(owner); }
        }
        internal void BoxClosed(messageBoxCtrl owner)
        {
            _box.Close(owner); Clear(owner);
            if (ReferenceEquals(_templateOwner, owner)) { _boxTemplates.Clear(); _templateOwner = null; }
            if (ReferenceEquals(_boxOwner, owner)) { _boxOwner = null; }
            _boxReady = false; _boxSelecting = false; _boxPresented = false; _boxBody = null; _boxCursor = -1;
        }
        internal void Box(messageBoxCtrl owner)
        {
            if (!_box.IsOwned(owner) || !_boxReady) { return; }
            Request(owner, delegate
            {
                if (!owner.active || !_box.IsOwned(owner)) { return; }
                Text[] lines = (Text[])NativeFields.Get(owner, "text_list_");
                List<string> text = new List<string>();
                for (int i = 0; i < lines.Length; i++)
                {
                    string actual = NativeFields.TextValue(lines[i]);
                    if (string.IsNullOrEmpty(actual)) { continue; }
                    string template;
                    if (_boxTemplates.TryGetValue(i, out template) && template.IndexOf("【】", StringComparison.Ordinal) >= 0)
                    {
                        textKeyIconCtrl icons = (textKeyIconCtrl)NativeFields.Get(owner, "key_icon_");
                        if (!keyGuideBase.keyguid_pad_) { actual = UiResources.Plain(template.Replace("【】", ImageText.KeyName(padCtrl.instance.GetKeyCode(KeyType.A)))); }
                        else { string identity; actual = UiResources.Plain(template.Replace("【】", _glyphs.Read(icons.key_icon[0].icon, out identity))); }
                    }
                    text.Add(actual);
                }
                titleSelectPlate plate = (titleSelectPlate)NativeFields.Get(owner, "select_plate_");
                string selection = null;
                int cursor = -1;
                if (plate != null && plate.body_active && !plate.is_end)
                {
                    cursor = plate.cursor_no;
                    IList rows = (IList)NativeFields.Get(plate, "select_list_");
                    if (cursor >= 0 && cursor < rows.Count) { selection = NativeFields.Text(rows[cursor], "text_"); }
                }
                string body = string.Join("\n", text.ToArray());
                if (_boxPresented && _boxCursor == cursor && _boxBody == body) { return; }
                bool full = !_boxPresented || _boxBody != body;
                Speak(owner, cursor.ToString(), full ? NativeFields.Join(body, selection) : selection);
                _boxPresented = true; _boxCursor = cursor; _boxBody = body;
            });
        }
        internal void BoxPlate(titleSelectPlate plate, bool shown)
        {
            messageBoxCtrl box = _boxOwner;
            if (box != null && ReferenceEquals(NativeFields.Get(box, "select_plate_"), plate)) { if (shown) { _boxReady = true; } if (!_boxSelecting || _boxReady) { Box(box); } }
        }
        internal void Clear(Component owner)
        {
            if (ReferenceEquals(owner, null)) { return; }
            Pending task;
            if (_pending.TryGetValue(owner, out task)) { task.Cancelled = true; _pending.Remove(owner); }
            _focus.Remove(owner);
            if (_runtime.Presentation != null) { _runtime.Presentation.ClearUi(owner); }
            if (owner is recordListCtrl) { _recordType = -1; _recordReady = false; }
        }
        /// <summary>Releases only this module's pending event requests and identity state.</summary>
        public void Dispose()
        {
            foreach (Pending p in _pending.Values) { p.Cancelled = true; }
            _pending.Clear(); _focus.Clear(); _boxTemplates.Clear(); _box.Close(_boxOwner); _boxOwner = null; _templateOwner = null; _recordReady = false;
        }
    }
}
