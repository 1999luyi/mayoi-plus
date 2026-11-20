using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml;
using BepInEx.Logging;
using Pwaat.Accessibility.Ui;

namespace Pwaat.Accessibility.Speech
{
    internal sealed class SpeechOutput : IDisposable
    {
        private readonly ManualLogSource _log;
        private NativeTolk _native;
        private bool _available;
        private bool _overlayOutput;
        private MonoBehaviour _host;
        private bool _draining;
        private bool _pendingOverlay;
        private bool _disposed;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly InputSpeechBatch _batch = new InputSpeechBatch();
        internal double Clock { get { return _clock.Elapsed.TotalMilliseconds; } }
        internal void Bind(MonoBehaviour host) { _host = host; }
        internal AccessibilityPreferences Preferences { get; private set; }
        internal readonly SpeechReviewHistory History = new SpeechReviewHistory();
        internal readonly InputSpeechPolicy InputPolicy = new InputSpeechPolicy();
        internal bool Available { get { return _available; } }

        private SpeechOutput(ManualLogSource log) { _log = log; }

        internal static SpeechOutput Start(ManualLogSource log, string pluginPath)
        {
            SpeechOutput probe = new SpeechOutput(log);
            try { probe.StartCore(Path.GetDirectoryName(pluginPath)); }
            catch (Exception error)
            {
                log.LogError("PWAAT_A11Y_SPEECH_START_FAILED " + error);
                probe.Dispose();
            }
            return probe;
        }

        private void StartCore(string directory)
        {
            try { Preferences = new AccessibilityPreferences(directory); }
            catch (Exception error) { _log.LogError("Accessibility preferences invalid; original preserved and session defaults applied: " + error); Preferences = new AccessibilityPreferences(directory, false); }
            bool enabled = false;
            string culture = null;
            string settingsPath = Path.Combine(directory, "speech-probe.xml");
            if (File.Exists(settingsPath))
            {
                XmlDocument settings = new XmlDocument();
                using (XmlReader reader = SafeReader(settingsPath)) { settings.Load(reader); }
                XmlElement root = settings.DocumentElement;
                if (root == null || root.Name != "speechProbe" || !bool.TryParse(root.GetAttribute("enabled"), out enabled)) { throw new InvalidDataException("Invalid speech probe configuration."); }
                culture = root.GetAttribute("language");
            }
            _native = new NativeTolk();
            string nativePath = Path.Combine(directory, "native/x86/Tolk.dll");
            _log.LogInfo("PWAAT_A11Y_TOLK_LOAD_REQUEST path=" + nativePath);
            _native.Initialize(nativePath);
            _log.LogInfo("PWAAT_A11Y_TOLK_READY path=" + _native.LoadedPath + " sha256=" + _native.Sha256 + " reader=" + _native.Reader + " hasSpeech=" + _native.HasSpeech + " ownerThread=" + Thread.CurrentThread.ManagedThreadId);
            if (!_native.HasSpeech) { throw new InvalidOperationException("Selected backend reports no speech capability."); }
            _available = true;
            if (!enabled) { _log.LogInfo("Startup speech test disabled; normal speech remains enabled."); return; }
            string message;
            if (!ReadMessages().TryGetValue(culture, out message)) { throw new InvalidDataException("Unsupported explicit probe language: " + culture); }
            bool result = Say(message, false);
            _log.LogInfo("PWAAT_A11Y_SPEECH_TEST language=" + culture + " languageSource=probe-config interrupt=false apiResult=" + result);
        }
        internal bool Say(string text, bool interrupt)
        {
            // Legacy content flags are intentionally ignored. Only ExternalTts input serials decide interruption.
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) { return false; }
            if (!_available || _native == null) { _log.LogError("Speech unavailable for current native UI event."); return false; }
            UiRuntime runtime = UiRuntime.Current;
            if (!_overlayOutput && runtime != null && runtime.Assistance != null && runtime.Assistance.IsOpen) { return false; }
            return Submit(text, true);
        }
        private bool Submit(string text, bool remember)
        {
            double now = Clock; long serial = InputPolicy.Consume(now);
            if (Preferences.Interruptions == InterruptionHandling.ScreenReader || _host == null)
            {
                InputSpeechBatch.Output previous = _batch.Take(now);
                if (previous != null) { Output(previous.Text, false, previous.Remember); }
                return Output(text, false, remember);
            }
            _pendingOverlay = _overlayOutput || (UiRuntime.Current != null && UiRuntime.Current.Assistance != null && UiRuntime.Current.Assistance.IsOpen);
            InputSpeechBatch.Output preceding = _batch.Add(text, remember, serial, now, UnityEngine.Input.anyKey);
            if (preceding != null) { Output(preceding.Text, preceding.Interrupt, preceding.Remember); }
            if (!_draining) { _draining = true; _host.StartCoroutine(Drain()); }
            return true; // Accepted for the bounded input batch; backend result is recorded on dispatch.
        }
        private IEnumerator Drain()
        {
            while (!_disposed && _batch.Pending)
            {
                yield return null;
                UiRuntime runtime = UiRuntime.Current;
                if (runtime == null) { _batch.Clear(); break; }
                InputSpeechBatch.Output output = _batch.Due(Clock);
                if (output != null) { Output(output.Text, output.Interrupt, output.Remember); }
            }
            _draining = false;
        }
        private bool Output(string text, bool requested, bool remember)
        {
            if (_disposed || !_available || _native == null) { return false; }
            try
            {
                bool interrupt = requested && Preferences.Interruptions == InterruptionHandling.ModInput;
                bool result = _native.Output(text, interrupt);
                if (result && remember) { History.Add(text); }
                if (!result) { _log.LogError("Tolk_Output returned false; speech/braille result is not retried."); }
                return result;
            }
            catch (Exception error) { _log.LogError("Tolk output failed without retry: " + error); return false; }
        }
        internal bool OverlaySay(string text)
        {
            _overlayOutput = true; try { return Say(text, false); } finally { _overlayOutput = false; }
        }
        internal void CancelOverlayPending() { if (_pendingOverlay) { _batch.Clear(); _pendingOverlay = false; } }
        internal bool Review(int delta)
        {
            string text = History.Move(delta);
            if (text == null) { return false; }
            return Submit(text, false);
        }

        private static Dictionary<string, string> ReadMessages()
        {
            XmlDocument document = new XmlDocument();
            using (Stream stream = RequiredResource("Pwaat.Accessibility.SpeechProbeMessages.xml"))
            using (XmlReader reader = XmlReader.Create(stream, SafeReaderSettings())) { document.Load(reader); }
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlElement element in document.SelectNodes("/messages/message"))
            {
                string culture = element.GetAttribute("culture");
                if (string.IsNullOrEmpty(culture) || string.IsNullOrEmpty(element.InnerText)) { throw new InvalidDataException("Incomplete localized probe message."); }
                result.Add(culture, element.InnerText);
            }
            return result;
        }

        private static Stream RequiredResource(string name)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null) { throw new InvalidDataException("Missing resource: " + name); }
            return stream;
        }

        private static XmlReader SafeReader(string path) { return XmlReader.Create(path, SafeReaderSettings()); }
        private static XmlReaderSettings SafeReaderSettings()
        {
            return new XmlReaderSettings { ProhibitDtd = true, XmlResolver = null };
        }

        /// <summary>Closes this probe without issuing a global speech-stop command.</summary>
        public void Dispose()
        {
            _disposed = true; _batch.Clear(); History.Clear();
            _available = false;
            if (_native == null) { return; }
            NativeTolk native = _native;
            _native = null;
            try { native.Dispose(); _log.LogInfo("PWAAT_A11Y_TOLK_RELEASED"); }
            catch (Exception error) { _log.LogError("Tolk release failed: " + error); }
        }
    }
}
