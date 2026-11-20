using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class StartPromptHandler : IDisposable
    {
        private readonly UiRuntime _runtime;
        private readonly MonoBehaviour _host;
        private readonly FieldInfo _icons = UiResources.Field(typeof(startCtrl), "key_icon_");
        private readonly ControlGlyphs _glyphs;
        private startCtrl _owner;
        private textKeyIconCtrl _controller;
        private string _template;
        private string _lastIdentity;
        private string _lastTemplate;
        private int _generation;
        private int _failedGeneration = -1;
        private Coroutine _pending;
        internal StartPromptHandler(UiRuntime runtime, MonoBehaviour host) { _runtime = runtime; _host = host; _glyphs = new ControlGlyphs(runtime.Messages); }
        internal void SourceCommitted(startCtrl owner, string original)
        {
            bool samePage = _owner != null && _owner == owner && _owner.push_active;
            Cancel();
            _owner = owner; _controller = (textKeyIconCtrl)_icons.GetValue(owner); _template = original;
            if (!samePage) { _lastIdentity = null; _lastTemplate = null; }
            int generation = _generation;
            // The caller commits the first blink color and input phase before its next yield.
            _pending = _host.StartCoroutine(ReadAfterNativeCommit(generation));
        }
        private IEnumerator ReadAfterNativeCommit(int generation)
        {
            yield return null;
            _pending = null;
            if (generation == _generation) { ReadCurrent(false); }
        }
        internal void KeyboardIconCommitted(textKeyIconCtrl owner)
        {
            if (_controller == owner && _pending == null) { ReadCurrent(true); }
        }
        internal void PadIconCommitted(textKeyIconCtrl.KeyIcon icon)
        {
            if (_controller != null && _controller.key_icon.Length > 0 && ReferenceEquals(_controller.key_icon[0], icon) && _pending == null) { ReadCurrent(true); }
        }
        internal void Hidden(startCtrl owner, bool active)
        {
            if (!active && _owner == owner) { Cancel(); _owner = null; _controller = null; _template = null; }
        }
        private void ReadCurrent(bool userInput)
        {
            if (_owner == null || _owner.body == null || !_owner.body.activeInHierarchy || !_owner.push_active || _controller == null || string.IsNullOrEmpty(_template)) { return; }
            try
            {
                string identity;
                string key = _glyphs.Read(_controller.key_icon[0].icon, out identity);
                if (_lastIdentity == identity && _lastTemplate == _template) { return; }
                string text = UiResources.Plain(_template);
                if (!text.Contains("【】")) { throw new InvalidDataException("Native continuation template has no verified icon slot."); }
                text = text.Replace("【】", key);
                _lastIdentity = identity; _lastTemplate = _template; _failedGeneration = -1;
                _runtime.Trace("start_prompt", "owner=" + _owner.GetInstanceID() + " glyph=" + identity + " text=" + text);
                _runtime.Speech.Say(text, userInput);
            }
            catch (Exception error)
            {
                if (_failedGeneration != _generation) { _failedGeneration = _generation; _lastIdentity = null; _lastTemplate = null; _runtime.ReadFailure("start-prompt", error, "start_unavailable"); }
            }
        }
        private void Cancel()
        {
            _generation++;
            if (_pending != null && _host != null) { _host.StopCoroutine(_pending); _pending = null; }
        }
        /// <summary>Cancels only this handler's unsubmitted one-shot request.</summary>
        public void Dispose() { Cancel(); _owner = null; _controller = null; }
    }
    internal sealed class LoadingStatusHandler
    {
        private readonly UiRuntime _runtime;
        private readonly FieldInfo _text = UiResources.Field(typeof(loadingCtrl), "text_");
        private loadingCtrl _owner;
        private int _request;
        private int _spoken = -1;
        private loadingCtrl.Type _kind;
        internal LoadingStatusHandler(UiRuntime runtime) { _runtime = runtime; }
        internal void Requested(loadingCtrl owner, loadingCtrl.Type kind) { _owner = owner; _kind = kind; _request++; }
        internal void Visible(loadingCtrl owner, bool active)
        {
            if (!active || owner == null || owner != _owner || _spoken == _request) { return; }
            _spoken = _request;
            try
            {
                Text nativeText = (Text)_text.GetValue(owner);
                if (nativeText == null || !nativeText.gameObject.activeInHierarchy) { return; }
                string text = UiResources.TextValue(nativeText);
                _spoken = _request;
                _runtime.Trace("loading_status", "owner=" + owner.GetInstanceID() + " kind=" + _kind + " request=" + _request + " text=" + text.Replace("\n", "\\n"));
                _runtime.Speech.Say(text, false);
            }
            catch (Exception error) { _runtime.ReadFailure("loading-status", error); }
        }
    }
}
