using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace Pwaat.Accessibility.Ui
{
    internal static class VasePuzzleHooks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static MethodInfo Method(Type type, string name, params Type[] args)
        { MethodInfo m = type.GetMethod(name, Flags, null, args, null); if (m == null) throw new MissingMethodException(type.FullName, name); return m; }
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(VasePuzzleHooks).GetMethod(name, Flags)); }
        private static readonly MethodInfo Clip = Method(typeof(soundCtrl), "GetClipSE", typeof(int));
        internal static void PrepareSounds()
        {
            if (soundCtrl.instance == null) throw new InvalidOperationException("Native audio unavailable.");
            foreach (int id in new[] { 365, 419 })
                if (Clip.Invoke(soundCtrl.instance, new object[] { id }) == null) throw new InvalidOperationException("Native puzzle cue unavailable: " + id);
        }
        internal static void Install(Harmony h)
        {
            h.Patch(Method(typeof(VasePuzzleMiniGame), "startVasePuzzle", typeof(bool)), prefix: H("Close"), postfix: H("Started"));
            Type phase = typeof(VasePuzzleMiniGame).GetNestedType("Proc", BindingFlags.NonPublic);
            if (phase == null) throw new MissingMemberException("VasePuzzleMiniGame.Proc");
            h.Patch(Method(typeof(VasePuzzleMiniGame), "_chengeStaete", phase), postfix: H("StateChanged"));
            h.Patch(Method(typeof(VasePuzzleModelCtrl), "setPiecesStatus", typeof(PiecesStatus[]), typeof(int)), postfix: H("ModelCommitted"));
            h.Patch(Method(typeof(VasePuzzleModelCtrl), "setFlashing", typeof(bool)), postfix: H("ReadyShown"));
            h.Patch(Method(typeof(VasePuzzleMiniGame), "_assemble_vase"), postfix: H("Attempted"));
            h.Patch(Method(typeof(keyGuideBoard), "CoroutineOpen", typeof(keyGuideBase.Type)), postfix: H("GuideCoroutine"));
            h.Patch(Method(typeof(keyGuideBoard), "ReLoadGuid"), postfix: H("GuideReady"));
            h.Patch(Method(typeof(VasePuzzleMiniGame), "DeleteEndBackGround"), prefix: H("Close"));
            h.Patch(Method(typeof(MessageSystem), "Reset", typeof(bool)), prefix: H("Close"));
            h.Patch(Method(typeof(advCtrl), "end"), prefix: H("Close"));
        }
        private static void Safe(string source, Action<VasePuzzleHandler> action)
        { UiRuntime.Safe(source, r => { if (r.VasePuzzle != null) action(r.VasePuzzle); }); }
        private static void Started(VasePuzzleMiniGame __instance) { Safe("vase-start", p => p.Start(__instance)); }
        private static void StateChanged(VasePuzzleMiniGame __instance) { Safe("vase-state", p => p.StateChanged(__instance)); }
        private static void ModelCommitted(PiecesStatus[] __0, int __1) { Safe("vase-focus", p => p.ModelCommitted(__0, __1)); }
        private static void ReadyShown(bool __0) { Safe("vase-ready", p => p.ReadyShown(__0)); }
        private static void Attempted(VasePuzzleMiniGame __instance) { Safe("vase-attempt", p => p.Attempted(__instance)); }
        private static void GuideReady(keyGuideBase __instance) { Safe("vase-guide", p => p.GuideReady(__instance)); }
        private static void GuideCoroutine(keyGuideBase __instance, keyGuideBase.Type __0, ref IEnumerator __result)
        { if (__0 == keyGuideBase.Type.POT_PAZZLE && __result != null) __result = AfterGuide(__result, __instance); }
        private static IEnumerator AfterGuide(IEnumerator native, keyGuideBase owner)
        {
            try { while (native.MoveNext()) yield return native.Current; }
            finally { IDisposable d = native as IDisposable; if (d != null) d.Dispose(); }
            GuideReady(owner);
        }
        private static void Close() { Safe("vase-close", p => p.Close()); }
    }
}
