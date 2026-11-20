using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using Pwaat.Accessibility.Speech;
using Pwaat.Accessibility.Ui;

namespace Pwaat.Accessibility
{
    /// <summary>Records loading evidence and initializes independent native UI and speech services.</summary>
    [BepInPlugin("pwaat.accessibility", "PWAAT Accessibility", "0.20.5")]
    [BepInProcess("PWAAT.exe")]
    public sealed class BootstrapPlugin : BaseUnityPlugin
    {
        private const string BuildId = "0.20.5-public-source.1";

        private SpeechOutput _speech;
        private UiRuntime _ui;

        private void Awake()
        {
            try
            {
                string line = string.Format(
                    CultureInfo.InvariantCulture,
                    "PWAAT_A11Y_BOOTSTRAP utc={0} build={1} pointerBits={2} loader={3} executable={4} plugin={5}",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    BuildId,
                    IntPtr.Size * 8,
                    typeof(BaseUnityPlugin).Assembly.GetName().Version,
                    OneLine(Paths.ExecutablePath),
                    OneLine(Assembly.GetExecutingAssembly().Location));
                string logPath = Path.Combine(Paths.BepInExRootPath, "Pwaat.Accessibility.bootstrap.log");
                using (StreamWriter writer = new StreamWriter(logPath, true, new UTF8Encoding(false)))
                {
                    writer.WriteLine(line);
                    writer.Flush();
                }
                Logger.LogInfo(line);
            }
            catch (Exception error)
            {
                Logger.LogError("Bootstrap evidence could not be written: " + error);
            }
            _speech = SpeechOutput.Start(Logger, Assembly.GetExecutingAssembly().Location);
            _ui = UiRuntime.Start(Logger, _speech, this, Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        }

        private void Update() { UiRuntime.Safe("story-hotkey", r => { CursorFeedbackAudio.InputFrame(); if (r.StoryInput != null) { r.StoryInput.Dispatch(); } }); }

        private void OnApplicationFocus(bool focused) { if (!focused) { CursorFeedbackAudio.StopAll(); } }

        private void OnApplicationQuit() { ShutdownSpeech(); }
        private void OnDestroy() { ShutdownSpeech(); }

        private void ShutdownSpeech()
        {
            if (_ui != null) { _ui.Dispose(); _ui = null; }
            if (_speech != null) { _speech.Dispose(); _speech = null; }
        }

        private static string OneLine(string value)
        {
            return value == null ? "<null>" : value.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
