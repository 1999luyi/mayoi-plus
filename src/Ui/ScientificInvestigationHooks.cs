using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace Pwaat.Accessibility.Ui
{
    internal static class ScientificInvestigationHooks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly MethodInfo Select = typeof(scienceInvestigationCtrl).GetMethod("GetSelectingCheckIndex", Flags);
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(ScientificInvestigationHooks).GetMethod(name, Flags)); }
        private static MethodInfo M(Type type, string name, params Type[] args)
        { MethodInfo method = type.GetMethod(name, Flags, null, args, null); if (method == null) throw new MissingMethodException(type.FullName, name); return method; }
        internal static void Install(Harmony harmony)
        {
            if (Select == null) throw new MissingMethodException("Native scientific-investigation hit test missing.");
            harmony.Patch(M(typeof(scienceInvestigationCtrl), "StateMainInputCoroutine"), postfix: H("Ready"));
            harmony.Patch(M(typeof(scienceInvestigationCtrl), "ChangeGuideIconSprite"), postfix: H("Committed"));
            harmony.Patch(M(typeof(evidenceObjectManager), "ChangeModel", typeof(string)), postfix: H("ModelChanged"));
            harmony.Patch(M(typeof(scienceInvestigationCtrl), "Close", typeof(bool)), prefix: H("Closed"));
            harmony.Patch(M(typeof(scienceInvestigationCtrl), "Release"), prefix: H("Closed"));
            harmony.Patch(M(typeof(scienceInvestigationCtrl), "end"), prefix: H("Closed"));
            harmony.Patch(M(typeof(advCtrl), "end"), prefix: H("Closed"));
        }
        private static void Safe(string source, Action<ScientificInvestigationHandler> action)
        { UiRuntime.Safe(source, r => { if (r.ScientificInvestigation != null) action(r.ScientificInvestigation); }); }
        private static void Ready(scienceInvestigationCtrl __instance) { Safe("science-ready", h => h.Ready(__instance)); }
        private static void Committed(scienceInvestigationCtrl __instance) { Safe("science-commit", h => h.Committed(__instance)); }
        private static void ModelChanged(evidenceObjectManager __instance) { Safe("science-model", h => h.ModelChanged(__instance)); }
        private static void Closed() { Safe("science-close", h => h.Close()); }
        internal static int SelectingIndex(scienceInvestigationCtrl owner) { return (int)Select.Invoke(owner, null); }
    }
}
