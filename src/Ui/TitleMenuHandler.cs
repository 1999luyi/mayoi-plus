using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class TitleMenuHandler
    {
        private sealed class Panel
        {
            internal titleSelectPlate Owner;
            internal bool Shown;
            internal int Generation;
            internal int LastLogical = -1;
            internal int LastGeneration = -1;
            internal bool Exit;
        }
        private readonly UiRuntime _runtime;
        private readonly FieldInfo _main = UiResources.Field(typeof(mainTitleCtrl), "select_plate_");
        private readonly FieldInfo _exit = UiResources.Field(typeof(mainTitleCtrl), "game_end_select_");
        private readonly FieldInfo _items = UiResources.Field(typeof(titleSelectPlate), "select_list_");
        private readonly FieldInfo _body = UiResources.Field(typeof(titleSelectPlate), "body_");
        private readonly FieldInfo _offset = UiResources.Field(typeof(titleSelectPlate), "index_offset_");
        private readonly FieldInfo _exitText = UiResources.Field(typeof(mainTitleCtrl), "message_text_list_");
        private readonly FieldInfo _messageBody = UiResources.Field(typeof(messageBoxCtrl), "body_");
        private readonly MessageBoxLifecycle _messageBox = new MessageBoxLifecycle();
        private mainTitleCtrl _owner;
        private Panel _mainPanel;
        private Panel _exitPanel;
        internal TitleMenuHandler(UiRuntime runtime) { _runtime = runtime; }
        internal titleSelectPlate VisiblePlate()
        {
            if (_exitPanel != null && _exitPanel.Shown && _exitPanel.Owner != null && _exitPanel.Owner.body_active) { return _exitPanel.Owner; }
            return _mainPanel != null && _mainPanel.Shown && _mainPanel.Owner != null && _mainPanel.Owner.body_active ? _mainPanel.Owner : null;
        }
        internal bool Owns(titleSelectPlate plate) { return Find(plate) != null; }
        internal void Bind(mainTitleCtrl owner)
        {
            _owner = owner;
            _mainPanel = new Panel { Owner = (titleSelectPlate)_main.GetValue(owner) };
            _exitPanel = new Panel { Owner = (titleSelectPlate)_exit.GetValue(owner), Exit = true };
            _runtime.Trace("title_bound", owner.GetType().Name + " id=" + owner.GetInstanceID() + " mainPanel=" + Id(_mainPanel.Owner) + " exitPanel=" + Id(_exitPanel.Owner));
            _runtime.Trace("title_startup_gates", "agreedPrivacy=" + GSStatic.summaryla_data.isAgreedPrivacyPolicy + " savedReleaseVersion=" + GSStatic.summaryla_data.releaseNoteVersion + " currentReleaseVersion=" + systemCtrl.instance.release_note_ver);
        }
        private Panel Find(titleSelectPlate owner)
        {
            if (owner == null || _owner == null) { return null; }
            if (_mainPanel != null && _mainPanel.Owner == owner) { return _mainPanel; }
            return _exitPanel != null && _exitPanel.Owner == owner ? _exitPanel : null;
        }
        internal void Hide(titleSelectPlate owner)
        {
            Panel panel = Find(owner);
            _runtime.Trace("title_hidden", Context(owner, panel));
            if (panel != null) { panel.Shown = false; }
        }
        internal void Rebuild(titleSelectPlate owner)
        {
            Panel panel = Find(owner);
            _runtime.Trace("title_rebuild", Context(owner, panel));
            if (panel != null) { panel.Generation++; panel.Shown = false; }
        }
        internal void Close(mainTitleCtrl owner)
        {
            _runtime.Trace("title_close", "incoming=" + Id(owner) + " bound=" + Id(_owner));
            if (_owner != owner) { return; }
            _owner = null; _mainPanel = null; _exitPanel = null;
        }
        internal void Shown(titleSelectPlate owner)
        {
            Panel panel = Find(owner);
            _runtime.Trace("title_shown_received", Context(owner, panel));
            if (panel == null) { return; }
            panel.Shown = true;
            panel.LastLogical = -1;
            panel.LastGeneration = -1;
            Emit(panel, owner.cursor_no + (int)_offset.GetValue(owner), true);
        }
        internal void Focus(titleSelectPlate owner, int index)
        {
            Panel panel = Find(owner);
            _runtime.Trace("title_commit_received", Context(owner, panel) + " committed=" + index);
            if (panel != null && panel.Shown) { Emit(panel, index, false); }
            else { _runtime.Trace("title_rejected", "reason=" + (panel == null ? "unbound-owner" : "entry-not-shown") + " " + Context(owner, panel)); }
        }
        internal void Requested(titleSelectPlate owner)
        {
            _runtime.Trace("title_cursor_requested", Context(owner, Find(owner)));
        }
        internal void IteratorCreated(titleSelectPlate owner, System.Collections.IEnumerator iterator)
        {
            _runtime.Trace("title_iterator_created", Context(owner, Find(owner)) + " iterator=" + (iterator == null ? "<null>" : iterator.GetType().FullName));
        }
        internal void Entered(titleSelectPlate owner)
        {
            _runtime.Trace("title_entry_started", Context(owner, Find(owner)));
        }
        private static string Id(UnityEngine.Object value) { return value != null ? value.GetInstanceID().ToString() : "<none>"; }
        private string Context(titleSelectPlate owner, Panel panel)
        {
            return "incoming=" + Id(owner) + " mainOwner=" + Id(_owner) + " mainPanel=" + (_mainPanel == null ? "<unbound>" : Id(_mainPanel.Owner)) + " exitPanel=" + (_exitPanel == null ? "<unbound>" : Id(_exitPanel.Owner)) + " recognized=" + (panel != null) + " shown=" + (panel != null && panel.Shown);
        }
        private void Reject(string reason, Panel panel)
        {
            _runtime.Trace("title_rejected", "reason=" + reason + " " + Context(panel.Owner, panel));
        }
        internal void MessageBoxOpened(messageBoxCtrl owner)
        {
            _messageBox.Open(owner);
            _runtime.Trace("title_messagebox_opened", "owner=" + Id(owner));
        }
        internal void MessageBoxClosed(messageBoxCtrl owner)
        {
            _messageBox.Close(owner);
            _runtime.Trace("title_messagebox_closed", "owner=" + Id(owner));
        }
        internal bool MessageBoxBlocks()
        {
            messageBoxCtrl owner = messageBoxCtrl.instance;
            if (owner == null) { return false; }
            UnityEngine.GameObject body = (UnityEngine.GameObject)_messageBody.GetValue(owner);
            bool local = body != null && body.activeSelf;
            bool hierarchy = body != null && body.activeInHierarchy;
            bool owned = _messageBox.IsOwned(owner);
            bool blocked = _messageBox.Blocks(owner, local, hierarchy);
            _runtime.Trace("title_messagebox_gate", "owner=" + Id(owner) + " openedByNativeLifecycle=" + owned + " activeSelf=" + local + " activeInHierarchy=" + hierarchy + " blocked=" + blocked);
            return blocked;
        }
        private void Emit(Panel panel, int index, bool initial)
        {
            try
            {
                titleSelectPlate owner = panel.Owner;
                if (_owner == null || owner == null) { Reject("owner-invalid", panel); return; }
                if (!_owner.body_active || _owner.body == null || !_owner.body.activeInHierarchy) { Reject("main-body-hidden", panel); return; }
                if (!owner.body_active || !((UnityEngine.GameObject)_body.GetValue(owner)).activeInHierarchy) { Reject("selection-body-hidden", panel); return; }
                if (optionCtrl.instance != null && optionCtrl.instance.is_open) { Reject("options-open", panel); return; }
                if (SaveLoadUICtrl.instance != null && SaveLoadUICtrl.instance.is_open) { Reject("save-load-open", panel); return; }
                if (MessageBoxBlocks()) { Reject("message-box-open-and-visible", panel); return; }
                int logical = owner.cursor_no;
                int offset = (int)_offset.GetValue(owner);
                if (index != logical + offset) { throw new InvalidDataException("Committed visual index does not match native logical selection."); }
                List<titleSelectPlate.SelectPlate> items = (List<titleSelectPlate.SelectPlate>)_items.GetValue(owner);
                if (index < 0 || index >= items.Count || !items[index].active) { throw new InvalidDataException("Native selected item is outside the active list."); }
                if (panel.LastLogical == logical && panel.LastGeneration == panel.Generation) { Reject("same-logical-item-and-generation", panel); return; }
                string label = UiResources.TextValue(items[index].text_);
                string output = label;
                if (initial && panel.Exit)
                {
                    StringBuilder header = new StringBuilder();
                    foreach (Text text in (List<Text>)_exitText.GetValue(_owner))
                    {
                        if (text != null && text.enabled && text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text)) { if (header.Length > 0) { header.Append('\n'); } header.Append(UiResources.TextValue(text)); }
                    }
                    if (header.Length > 0) { output = header + "\n" + label; }
                }
                if (initial && !panel.Exit) { output = ImageText.MainHeader(_owner, label); }
                panel.LastLogical = logical; panel.LastGeneration = panel.Generation;
                _runtime.Trace("title_focus", "panel=" + (panel.Exit ? "exit" : "main") + " id=" + owner.GetInstanceID() + " logical=" + logical + " physical=" + index + " generation=" + panel.Generation + " shown=" + initial + " text=" + label);
                _runtime.Speech.Say(output, !initial || panel.Exit);
            }
            catch (Exception error) { panel.LastLogical = -1; panel.LastGeneration = -1; _runtime.ReadFailure("title-menu", error); }
        }
    }
}
