using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx.Logging;
using HarmonyLib;
using Pwaat.Accessibility.Speech;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class UiRuntime : IDisposable
    {
        private readonly List<Harmony> _patches = new List<Harmony>();
        private readonly ManualLogSource _log;
        private int _traceCount;
        private bool _budgetReported;
        internal static UiRuntime Current { get; private set; }
        internal SpeechOutput Speech { get; private set; }
        internal UiMessages Messages { get; private set; }
        internal UiSettings Settings { get; private set; }
        internal TitleMenuHandler Title { get; private set; }
        internal StartPromptHandler StartPrompt { get; private set; }
        internal LoadingStatusHandler Loading { get; private set; }
        internal DebugPageTrace Pages { get; private set; }
        internal MenuBatch Batch { get; private set; }
        internal AccessibilityHelpHandler Assistance { get; private set; }
        internal NativeAccessibilitySettings NativeSettings { get; private set; }
        internal FirstRunIntroduction Introduction { get; private set; }
        internal StoryPresentationHandler Presentation { get; private set; }
        internal StoryInputHandler StoryInput { get; private set; }
        internal StoryMessagesHandler Story { get; private set; }
        internal StoryUiHandler StoryUi { get; private set; }
        internal CourtRecordImages RecordImages { get; private set; }
        internal InvestigationHandler Investigation { get; private set; }
        internal InvestigationLabels InvestigationNames { get; private set; }
        internal PicturePointingHandler PicturePointing { get; private set; }
        internal PsycheLockHandler PsycheLocks { get; private set; }
        internal ScientificInvestigationHandler ScientificInvestigation { get; private set; }
        internal LuminolHandler Luminol { get; private set; }
        internal VasePuzzleHandler VasePuzzle { get; private set; }
        internal TanchikiHandler Tanchiki { get; private set; }
        internal DyingMessageHandler DyingMessage { get; private set; }
        internal VaseShowHandler VaseShow { get; private set; }
        internal FingerprintHandler Fingerprints { get; private set; }
        internal VideoTapeHandler VideoTape { get; private set; }
        internal SafePasswordHandler SafePassword { get; private set; }
        private UiRuntime(ManualLogSource log, SpeechOutput speech) { _log = log; Speech = speech; }
        internal static UiRuntime Start(ManualLogSource log, SpeechOutput speech, MonoBehaviour host, string directory)
        {
            UiRuntime runtime = new UiRuntime(log, speech);
            try { runtime.Initialize(host, directory); }
            catch (Exception error) { log.LogError("UI initialization failed: " + error); runtime.Dispose(); }
            return runtime;
        }
        private void Initialize(MonoBehaviour host, string directory)
        {
            Speech.Bind(host);
            Settings = UiSettings.Read(directory);
            Messages = new UiMessages();
            string gameAssembly = typeof(mainTitleCtrl).Assembly.Location;
            string expected = UiResources.Embedded("Pwaat.Accessibility.GameAssembly.sha256").Trim();
            string actual;
            using (FileStream stream = File.OpenRead(gameAssembly))
            using (SHA256 hash = SHA256.Create()) { actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""); }
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("UI hooks do not match the game assembly fingerprint."); }
            Current = this;
            Batch = new MenuBatch(this, host);
            Assistance = new AccessibilityHelpHandler(this);
            NativeSettings = new NativeAccessibilitySettings(this);
            Install("accessibility-settings", AccessibilityHooks.InstallSettings);
            Install("accessibility-help", AccessibilityHooks.InstallHelp);
            Install("guides", BatchHooks.InstallGuides);
            Install("menu-lifetime", BatchHooks.InstallLifetime);
            Install("choices", BatchHooks.InstallChoices);
            Install("options", BatchHooks.InstallOptions);
            Install("cursor-audio-input", CursorFeedbackAudio.Install);
            Install("first-run-introduction", delegate (Harmony harmony) { Introduction = new FirstRunIntroduction(this); FirstRunIntroduction.Install(harmony); });
            Install("save", BatchHooks.InstallSave);
            Install("gallery", BatchHooks.InstallGallery);
            Install("story-presentation", delegate (Harmony harmony) { Presentation = new StoryPresentationHandler(this, host); StoryInput = new StoryInputHandler(this); });
            Install("story-evidence", StoryPresentationHooks.InstallEvidence);
            Install("story-guides", StoryPresentationHooks.InstallGuides);
            Install("story-health", StoryPresentationHooks.InstallHealth);
            Install("story-images", StoryPresentationHooks.InstallImages);
            Install("story-state", StoryPresentationHooks.InstallState);
            Install("story-ui", delegate (Harmony harmony) { StoryUi = new StoryUiHandler(this, host); });
            Install("story-messages", delegate (Harmony harmony) { Story = new StoryMessagesHandler(this); StoryHooks.InstallMessages(harmony); });
            Install("story-choices", StoryHooks.InstallChoices);
            Install("story-records", StoryHooks.InstallRecords);
            Install("court-record-images", delegate (Harmony harmony) { RecordImages = new CourtRecordImages(this); CourtRecordImageHooks.Install(harmony); });
            Install("story-dialogs", StoryHooks.InstallDialogs);
            Install("investigation-labels", delegate (Harmony harmony) { InvestigationNames = new InvestigationLabels(directory); });
            Install("investigation-menus", delegate (Harmony harmony) { Investigation = new InvestigationHandler(this); InvestigationHooks.InstallMenus(harmony); });
            Install("investigation-cursor", delegate (Harmony harmony) { if (Investigation == null) { throw new InvalidOperationException("Investigation handler unavailable."); } InvestigationHooks.InstallInspection(harmony); Investigation.InspectionInstalled(); });
            Install("psyche-locks", delegate (Harmony harmony) { PsycheLocks = new PsycheLockHandler(this); PsycheLockHooks.Install(harmony); });
            Install("picture-pointing", delegate (Harmony harmony) { PicturePointing = new PicturePointingHandler(this); PicturePointingHooks.Install(harmony); });
            Install("scientific-investigation", delegate (Harmony harmony) { ScientificInvestigation = new ScientificInvestigationHandler(this); ScientificInvestigationHooks.Install(harmony); ScientificInvestigation.Installed(); });
            Install("luminol", delegate (Harmony harmony) { Luminol = new LuminolHandler(this); LuminolHooks.Install(harmony); });
            Install("safe-password", delegate (Harmony harmony) { SafePassword = new SafePasswordHandler(this); SafePasswordHooks.Install(harmony); });
            Install("video-tape", delegate (Harmony harmony) { VideoTape = new VideoTapeHandler(this); VideoTapeHooks.Install(harmony); });
            Install("fingerprint", delegate (Harmony harmony) { Fingerprints = new FingerprintHandler(this); FingerprintHooks.Install(harmony); });
            Install("tanchiki", delegate (Harmony harmony) { Tanchiki = new TanchikiHandler(this); TanchikiHooks.Install(harmony); });
            Install("dying-message", delegate (Harmony harmony) { DyingMessage = new DyingMessageHandler(this); DyingMessageHooks.Install(harmony); });
            Install("vase-show", delegate (Harmony harmony) { VaseShow = new VaseShowHandler(this); VaseShowHooks.Install(harmony); });
            Install("vase-puzzle", delegate (Harmony harmony) { VasePuzzle = new VasePuzzleHandler(this); VasePuzzleHooks.Install(harmony); });
            if (Settings.MainMenu)
            {
                Install("title", delegate (Harmony harmony) { Title = new TitleMenuHandler(this); NativeHooks.InstallTitle(harmony); });
            }
            if (Settings.StartPrompt)
            {
                Install("start", delegate (Harmony harmony) { StartPrompt = new StartPromptHandler(this, host); NativeHooks.InstallStart(harmony); });
            }
            if (Settings.LoadingStatus)
            {
                Install("loading", delegate (Harmony harmony) { Loading = new LoadingStatusHandler(this); NativeHooks.InstallLoading(harmony); });
            }
            if (Settings.Debug && Settings.TracePages)
            {
                Install("page-trace", delegate (Harmony harmony) { Pages = new DebugPageTrace(this); Pages.Install(harmony); });
            }
            _log.LogInfo("PWAAT_A11Y_UI_INSTALLED groups=" + _patches.Count + " debug=" + Settings.Debug + " tracePages=" + Settings.TracePages);
        }
        private void Install(string name, Action<Harmony> action)
        {
            Harmony harmony = new Harmony("pwaat.accessibility." + name);
            try { action(harmony); _patches.Add(harmony); _log.LogInfo("PWAAT_A11Y_UI_GROUP_INSTALLED group=" + name); }
            catch (Exception error)
            {
                try { harmony.UnpatchSelf(); } catch (Exception cleanup) { _log.LogError("Failed partial-hook cleanup: " + name + " " + cleanup); }
                _log.LogError("UI feature isolated after installation failure: " + name + " " + error);
            }
        }
        internal static void Safe(string source, Action<UiRuntime> action)
        {
            UiRuntime runtime = Current;
            if (runtime == null) { return; }
            try { action(runtime); }
            catch (Exception error) { runtime._log.LogError("Native event failed: " + source + " " + error); }
        }
        internal void Trace(string kind, string details)
        {
            if (Settings == null || !Settings.Debug) { return; }
            if (_traceCount >= Settings.TraceBudget)
            {
                if (!_budgetReported) { _budgetReported = true; _log.LogWarning("Page/UI debug budget exhausted; assistance remains active."); }
                return;
            }
            _traceCount++;
            _log.LogInfo("PWAAT_A11Y_UI event=" + kind + " seq=" + _traceCount + " utc=" + DateTime.UtcNow.ToString("o") + " " + details.Replace("\r", "\\r").Replace("\n", "\\n"));
        }
        internal void ReadFailure(string source, Exception error, string messageKey = "read_failed")
        {
            LogReadFailure(source, error);
            Speech.Say(Messages.Get(messageKey), false);
        }
        internal void LogReadFailure(string source, Exception error)
        { _log.LogError("Native UI read failed: " + source + " " + error); }
        /// <summary>Removes this runtime's hooks and unsubmitted requests only.</summary>
        public void Dispose()
        {
            if (ReferenceEquals(Current, this)) { Current = null; }
            CursorFeedbackAudio.StopAll();
            try { if (Introduction != null) { Introduction.Dispose(); } if (Assistance != null) { Assistance.Dispose(); } if (NativeSettings != null) { NativeSettings.Dispose(); } } catch (Exception error) { _log.LogError("Accessibility UI cleanup failed: " + error); }
            try { if (Investigation != null) { Investigation.Dispose(); } if (Story != null) { Story.Dispose(); } if (StoryUi != null) { StoryUi.Dispose(); } if (RecordImages != null) { RecordImages.Dispose(); } }
            catch (Exception error) { _log.LogError("Story request cleanup failed: " + error); }
            try { if (PsycheLocks != null) { PsycheLocks.Dispose(); } } catch (Exception error) { _log.LogError("Psyche-lock cleanup failed: " + error); }
            try { if (SafePassword != null) { SafePassword.Dispose(); } } catch (Exception error) { _log.LogError("Safe password cleanup failed: " + error); }
            try { if (VideoTape != null) { VideoTape.Dispose(); } } catch (Exception error) { _log.LogError("Video tape cleanup failed: " + error); }
            try { if (Fingerprints != null) { Fingerprints.Dispose(); } } catch (Exception error) { _log.LogError("Fingerprint cleanup failed: " + error); }
            try { if (Tanchiki != null) Tanchiki.Dispose(); } catch (Exception error) { _log.LogError("Detector cleanup failed: " + error); }
            try { if (DyingMessage != null) DyingMessage.Dispose(); } catch (Exception error) { _log.LogError("Dying message cleanup failed: " + error); }
            try { if (VaseShow != null) { VaseShow.Dispose(); } } catch (Exception error) { _log.LogError("Vase show cleanup failed: " + error); }
            try { if (VasePuzzle != null) { VasePuzzle.Dispose(); } } catch (Exception error) { _log.LogError("Vase puzzle cleanup failed: " + error); }
            try { if (Luminol != null) { Luminol.Dispose(); } } catch (Exception error) { _log.LogError("Luminol cleanup failed: " + error); }
            try { if (ScientificInvestigation != null) { ScientificInvestigation.Dispose(); } } catch (Exception error) { _log.LogError("Scientific investigation cleanup failed: " + error); }
            try { if (PicturePointing != null) { PicturePointing.Dispose(); } } catch (Exception error) { _log.LogError("Picture pointing cleanup failed: " + error); }
            try { if (Presentation != null) { Presentation.Dispose(); } } catch (Exception error) { _log.LogError("Story presentation cleanup failed: " + error); }
            try { if (Batch != null) { Batch.Dispose(); } } catch (Exception error) { _log.LogError("Batch request cleanup failed: " + error); }
            try { if (StartPrompt != null) { StartPrompt.Dispose(); } }
            catch (Exception error) { _log.LogError("Startup request cleanup failed: " + error); }
            try { if (Pages != null) { Pages.Dispose(); } }
            catch (Exception error) { _log.LogError("Page trace cleanup failed: " + error); }
            foreach (Harmony harmony in _patches)
            {
                try { harmony.UnpatchSelf(); }
                catch (Exception error) { _log.LogError("Own hook cleanup failed: " + harmony.Id + " " + error); }
            }
            _patches.Clear();
        }
    }
}
