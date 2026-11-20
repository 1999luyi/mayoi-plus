using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class FirstRunIntroduction : IDisposable
    {
        private readonly UiRuntime _runtime;
        private DialogReleaseNoteCtrl _dialog;
        private GameObject _background;
        private bool _confirmed, _cancelled, _running;
        internal FirstRunIntroduction(UiRuntime runtime) { _runtime = runtime; }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(mainTitleCtrl), "stateCoroutine"), postfix: new HarmonyMethod(typeof(FirstRunIntroduction), "Created"));
            harmony.Patch(AccessTools.Method(typeof(mainTitleCtrl), "End"), prefix: new HarmonyMethod(typeof(FirstRunIntroduction), "Ended"));
            harmony.Patch(AccessTools.Method(typeof(StoryInputHandler), "Dispatch"), prefix: new HarmonyMethod(typeof(FirstRunIntroduction), "AllowModInput"));
        }
        private static void Created(mainTitleCtrl __instance, ref IEnumerator __result)
        {
            UiRuntime runtime = UiRuntime.Current;
            if (runtime != null && runtime.Introduction != null && !runtime.Speech.Preferences.FirstRunCompleted)
                __result = runtime.Introduction.Run(__instance, __result);
        }
        private static bool AllowModInput()
        {
            UiRuntime runtime = UiRuntime.Current;
            return runtime == null || runtime.Introduction == null || runtime.Introduction._dialog == null;
        }
        private static void Ended() { UiRuntime.Safe("introduction-close", r => { if (r.Introduction != null) r.Introduction.Dispose(); }); }
        private bool Held()
        {
            return padCtrl.instance != null && (padCtrl.instance.GetKey(KeyType.A) || padCtrl.instance.GetKey(KeyType.R)) || Input.GetMouseButton(0);
        }
        private bool Show(mainTitleCtrl owner)
        {
            try
            {
                Transform parent = (Transform)NativeFields.Get(owner, "canvas_transform_");
                GameObject prefab = (GameObject)NativeFields.Get(owner, "dialog_release_note_prefab_");
                GameObject background = (GameObject)NativeFields.Get(owner, "dialog_background_prefab_");
                if (parent == null || prefab == null || background == null) throw new InvalidOperationException("Native introduction dialog prefab unavailable.");
                _background = UnityEngine.Object.Instantiate(background, parent);
                _dialog = UnityEngine.Object.Instantiate(prefab, parent).GetComponent<DialogReleaseNoteCtrl>();
                ((Text)NativeFields.Get(_dialog, "m_TitleText")).text = _runtime.Messages.Get("introduction.title");
                ((Text)NativeFields.Get(_dialog, "m_MessageText")).text = _runtime.Messages.Get("introduction.body");
                _dialog.RegisterDecideEvent(Confirmed);
                _runtime.Batch.StartupDialog(_dialog);
                return true;
            }
            catch (Exception error) { _runtime.ReadFailure("first-run-introduction", error); Cleanup(); return false; }
        }
        private void Confirmed()
        {
            if (_cancelled || _confirmed) return;
            _confirmed = true;
            AccessibilityPreferences preferences = _runtime.Speech.Preferences;
            preferences.FirstRunCompleted = true;
            try { preferences.Save(); }
            catch (Exception error) { preferences.FirstRunCompleted = false; _runtime.ReadFailure("first-run-save", error, "introduction.save-failed"); }
        }
        private IEnumerator Run(mainTitleCtrl owner, IEnumerator original)
        {
            if (_running) { while (original.MoveNext()) yield return original.Current; yield break; }
            _running = true; _confirmed = _cancelled = false;
            try
            {
                // Advance the native Init and its first fade yield. All serialized UI,
                // fonts and sounds are now initialized; no menu input coroutine exists yet.
                if (!original.MoveNext()) yield break;
                yield return original.Current;
                while (!_cancelled && Held()) yield return null;
                if (!_cancelled && owner != null && Show(owner))
                {
                    // Wait for the native explicit confirmation event, never speech duration.
                    while (!_confirmed && !_cancelled && owner != null && _dialog != null) yield return null;
                    while (!_cancelled && Held()) yield return null;
                    Cleanup();
                }
                if (!_cancelled) while (original.MoveNext()) yield return original.Current;
            }
            finally { Cleanup(); _running = false; IDisposable disposable = original as IDisposable; if (disposable != null) disposable.Dispose(); }
        }
        private void Cleanup()
        {
            if (_dialog != null) { _dialog.UnregisterDecideEvent(Confirmed); _runtime.Batch.Clear(_dialog); UnityEngine.Object.Destroy(_dialog.gameObject); _dialog = null; }
            if (_background != null) { UnityEngine.Object.Destroy(_background); _background = null; }
        }
        public void Dispose() { _cancelled = true; Cleanup(); }
    }
}
