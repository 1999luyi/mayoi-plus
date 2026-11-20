using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal static class PicturePointingHooks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static MethodInfo M(Type type, string name, params Type[] args)
        { MethodInfo result = type.GetMethod(name, Flags, null, args, null); if (result == null) throw new MissingMethodException(type.FullName, name); return result; }
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(PicturePointingHooks).GetMethod(name, Flags)); }
        private static void Safe(string source, Action<PicturePointingHandler> action)
        { UiRuntime.Safe(source, r => { if (r.PicturePointing != null) action(r.PicturePointing); }); }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.EnumeratorMoveNext(M(typeof(PointMiniGame), "MainCoroutine")), transpiler: H("ReadyTranspiler"));
            harmony.Patch(M(typeof(MiniGameCursor), "Process"), prefix: H("CursorStarting"), postfix: H("CursorCommitted"));
            harmony.Patch(M(typeof(keyGuideBoard), "open", typeof(keyGuideBase.Type)), prefix: H("GuideOpening"));
            harmony.Patch(M(typeof(keyGuideBoard), "CoroutineOpen", typeof(keyGuideBase.Type)), postfix: H("GuideCoroutine"));
            harmony.Patch(M(typeof(PointMiniGame), "ResetCursorKeyType"), prefix: H("Closed"));
            harmony.Patch(M(typeof(bgCtrl), "SetSprite", typeof(int), typeof(bool)), postfix: H("VisualChanged"));
            harmony.Patch(M(typeof(bgCtrl), "SetSeal", typeof(int), typeof(bool)), postfix: H("VisualChanged"));
            harmony.Patch(M(typeof(bgCtrl), "CoroutineSlider"), postfix: H("SliderCoroutine"));
            harmony.Patch(M(typeof(GSMapIcon), "SetPositionByIndex", typeof(int), typeof(int), typeof(int), typeof(int)), postfix: H("VisualChanged"));
            harmony.Patch(M(typeof(GSMapIcon), "SetVisibleByIndex", typeof(int), typeof(bool)), postfix: H("VisualChanged"));
            harmony.Patch(M(typeof(GSMapIcon), "LoadSpriteByIndex", typeof(int), typeof(int)), postfix: H("VisualChanged"));
            harmony.Patch(M(typeof(GSMapIcon), "UnloadSprite", typeof(int)), postfix: H("VisualChanged"));
            harmony.Patch(M(typeof(MessageSystem), "Reset", typeof(bool)), prefix: H("Closed"));
            harmony.Patch(M(typeof(advCtrl), "end"), prefix: H("Closed"));
        }
        private static void Ready() { Safe("picture-pointing-ready", p => p.Ready()); }
        private static void CursorStarting(MiniGameCursor __instance, out Vector3 __state) { __state = __instance.cursor_position; }
        private static void CursorCommitted(Vector3 __state) { Safe("picture-pointing-cursor", p => p.CursorCommitted(__state)); }
        private static void VisualChanged() { Safe("picture-pointing-visual", p => p.VisualChanged()); }
        private static void Closed() { Safe("picture-pointing-close", p => p.Close()); }
        private static void GuideOpening(keyGuideBoard __instance, keyGuideBase.Type __0) { if (__instance == keyGuideCtrl.instance && __0 == keyGuideBase.Type.NO_GUIDE) Closed(); }
        private static void GuideCoroutine(keyGuideBoard __instance, keyGuideBase.Type __0, ref IEnumerator __result) { __result = GuideFinished(__result, __instance, __0); }
        private static IEnumerator GuideFinished(IEnumerator native, keyGuideBoard owner, keyGuideBase.Type type)
        {
            try { while (native.MoveNext()) yield return native.Current; }
            finally { IDisposable disposable = native as IDisposable; if (disposable != null) disposable.Dispose(); }
            Safe("picture-pointing-guide-ready", p => p.GuideReady(owner, type));
        }
        private static void SliderCoroutine(ref IEnumerator __result) { __result = SliderFinished(__result); }
        private static IEnumerator SliderFinished(IEnumerator native)
        {
            try { while (native.MoveNext()) yield return native.Current; }
            finally { IDisposable disposable = native as IDisposable; if (disposable != null) disposable.Dispose(); }
            VisualChanged();
        }
        private static IEnumerable<CodeInstruction> ReadyTranspiler(IEnumerable<CodeInstruction> input)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(input);
            MethodInfo setBusy = typeof(SubWindow).GetProperty("busy_", Flags).GetSetMethod(true);
            int count = 0;
            for (int i = 0; i < code.Count; i++)
            {
                yield return code[i];
                if (i == 0 || !code[i].Calls(setBusy) || !code[i - 1].LoadsConstant(0)) continue;
                count++; yield return new CodeInstruction(OpCodes.Call, M(typeof(PicturePointingHooks), "Ready"));
            }
            if (count != 1) throw new InvalidOperationException("Picture pointing input-ready commit changed: " + count);
        }
    }
}
