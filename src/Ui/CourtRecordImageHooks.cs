using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace Pwaat.Accessibility.Ui
{
    internal static class CourtRecordImageHooks
    {
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(CourtRecordImageHooks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)); }
        private static MethodInfo M(Type type, string name, params Type[] signature)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, signature, null);
            if (method == null) { throw new MissingMethodException(type.FullName, name); }
            return method;
        }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(M(typeof(recordDetailCtrl), "CoroutineViewDetail", typeof(int)), postfix: H("View"));
            harmony.Patch(M(typeof(recordDetailCtrl), "ChengPage", typeof(int)), postfix: H("Page"));
            harmony.Patch(M(typeof(keyGuideBoard), "CoroutineOpen", typeof(keyGuideBase.Type)), postfix: H("Guide"));
            harmony.Patch(M(typeof(recordListCtrl), "infoLoad"), postfix: H("Hint"));
            harmony.Patch(M(typeof(keyGuideBoard), "CoroutineClose"), prefix: H("Closing"));
        }
        private static void View(recordDetailCtrl __instance, ref IEnumerator __result) { __result = ViewLifetime(__result, __instance); }
        private static IEnumerator ViewLifetime(IEnumerator native, recordDetailCtrl owner)
        {
            int generation = -1;
            UiRuntime.Safe("record-image-start", r => { if (r.RecordImages != null) { generation = r.RecordImages.Starting(owner); } });
            try { while (native.MoveNext()) { yield return native.Current; } }
            finally
            {
                IDisposable disposable = native as IDisposable; if (disposable != null) { disposable.Dispose(); }
                UiRuntime.Safe("record-image-end", r => { if (r.RecordImages != null) { r.RecordImages.Ended(owner, generation); } });
            }
        }
        private static void Page(recordDetailCtrl __instance, int __0, ref IEnumerator __result)
        {
            UiRuntime runtime = UiRuntime.Current; int generation = runtime == null || runtime.RecordImages == null ? -1 : runtime.RecordImages.Generation;
            __result = Completed(__result, delegate (UiRuntime r) { if (r.RecordImages != null) { r.RecordImages.Page(__instance, __0, generation); } });
        }
        private static void Closing(keyGuideBoard __instance) { UiRuntime.Safe("record-image-closing", r => { if (r.RecordImages != null) { r.RecordImages.Closing(__instance); } }); }
        private static void Guide(keyGuideBoard __instance, ref IEnumerator __result) { __result = Completed(__result, delegate (UiRuntime r) { if (r.RecordImages != null) { r.RecordImages.Opened(__instance); } }); }
        private static IEnumerator Completed(IEnumerator native, Action<UiRuntime> read)
        {
            try { while (native.MoveNext()) { yield return native.Current; } }
            finally { IDisposable disposable = native as IDisposable; if (disposable != null) { disposable.Dispose(); } }
            UiRuntime.Safe("record-image-commit", read);
        }
        private static void Hint(recordListCtrl __instance) { UiRuntime.Safe("record-image-hint", r => { if (r.StoryUi != null) { r.StoryUi.Record(__instance); } }); }
    }
}
