using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal static class StoryPresentationHooks
    {
        private static HarmonyMethod H(string name) { return name == null ? null : new HarmonyMethod(typeof(StoryPresentationHooks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)); }
        private static MethodInfo Target(Type type, string name, params Type[] signature)
        {
            MethodInfo target = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, signature, null);
            if (target == null || target.IsAbstract) { throw new MissingMethodException(type.FullName, name); }
            return target;
        }
        private static void After(Harmony h, Type type, string method, string callback, params Type[] signature) { h.Patch(Target(type, method, signature), postfix: H(callback)); }
        internal static void InstallEvidence(Harmony h)
        {
            After(h, typeof(picePlateCtrl), "entryPice", "EvidenceEntry", typeof(int), typeof(int));
            After(h, typeof(picePlateCtrl), "set_body_active", "EvidenceActive", typeof(bool));
            After(h, typeof(picePlateCtrl), "CoroutinePice", "Coroutine", typeof(float));
        }
        internal static void InstallGuides(Harmony h)
        {
            After(h, typeof(guideCtrl), "settingSprite", "Guide", typeof(guideCtrl.GuideType));
            After(h, typeof(guideCtrl), "setEnables", "Guide", typeof(bool));
            After(h, typeof(guideCtrl), "CoroutineOpen", "Coroutine", typeof(guideCtrl.GuideType));
            After(h, typeof(guideCtrl), "CoroutineRotationChange", "Coroutine", typeof(guideCtrl.GuideType));
            After(h, typeof(guideCtrl), "CoroutineClose", "Coroutine");
            After(h, typeof(guideCtrl), "Close", "GuideClosed");
            After(h, typeof(keyGuideBoard), "setting", "Guide", typeof(keyGuideBase.Type), typeof(bool));
            After(h, typeof(keyGuideBoard), "ReLoadGuid", "Guide");
            After(h, typeof(recordGuideCtrl), "uniqueGuideSet", "Guide", typeof(int));
            After(h, typeof(recordGuideCtrl), "recordGuideSet", "Guide", typeof(int));
            After(h, typeof(recordGuideCtrl), "ReLoadGuid", "Guide");
            After(h, typeof(keyGuideBoard), "CoroutineOpen", "Coroutine", typeof(keyGuideBase.Type));
            After(h, typeof(keyGuideBoard), "CoroutineClose", "Coroutine");
        }
        internal static void InstallHealth(Harmony h)
        {
            After(h, typeof(lifeGaugeCtrl), "UpdateRest", "HealthUpdated");
            After(h, typeof(lifeGaugeCtrl), "GaugeMoveMoment", "HealthMoment", typeof(bool));
            After(h, typeof(lifeGaugeCtrl), "CoroutineGaugeMove", "Coroutine", typeof(bool));
            After(h, typeof(lifeGaugeCtrl), "ReduceRest", "DamageCoroutine");
            After(h, typeof(lifeGaugeCtrl), "set_body_active", "HealthActive", typeof(bool));
            After(h, typeof(lifeGaugeCtrl), "set_gauge_active", "HealthActive", typeof(bool));
        }
        internal static void InstallImages(Harmony h)
        {
            After(h, typeof(judgmentCtrl), "SetSprite", "JudgmentStart", typeof(int));
            After(h, typeof(judgmentCtrl), "end", "JudgmentEnd");
            foreach (string name in new[] { "CoroutineJapanese", "CoroutineUSA", "CoroutinePtBr" })
            {
                MethodInfo next = AccessTools.EnumeratorMoveNext(Target(typeof(judgmentCtrl), name, typeof(int)));
                h.Patch(next, transpiler: H("JudgmentTranspiler"));
            }
            h.Patch(AccessTools.EnumeratorMoveNext(Target(typeof(objMoveCtrl), "ObjEtc00B", typeof(int))), transpiler: H("BalloonTranspiler"));
        }
        internal static void InstallState(Harmony h)
        {
            After(h, typeof(messageBoardCtrl), "LoadMsgSet", "StoryRestored");
            After(h, typeof(messageBoardCtrl), "name_plate", "SpeakerChanged", typeof(bool), typeof(int), typeof(int));
            After(h, typeof(messageBoardCtrl), "board", "BoardChanged", typeof(bool), typeof(bool));
            After(h, typeof(textKeyIconCtrl), "textIconSet", "RenderedKey", typeof(UnityEngine.UI.Text), typeof(KeyType), typeof(int));
            After(h, typeof(textKeyIconCtrl), "textIconSet", "RenderedKey", typeof(UnityEngine.UI.Text), typeof(KeyCode), typeof(int));
        }
        private static void Safe(string source, Action<StoryPresentationHandler> action)
        {
            UiRuntime.Safe(source, r => { if (r.Presentation != null) { try { action(r.Presentation); } catch (Exception error) { r.ReadFailure(source, error); } } });
        }
        private static void EvidenceEntry(picePlateCtrl __instance, int __1) { Safe("evidence-entry", p => p.EvidenceEntry(__instance, __1)); }
        private static void EvidenceActive(picePlateCtrl __instance, bool __0) { Safe("evidence-active", p => { if (__0) { p.EvidenceReady(__instance); } else { p.EvidenceHidden(__instance); } }); }
        private static void Guide(keyGuideBase __instance) { Safe("story-guide", p => p.Guide(__instance)); UiRuntime.Safe("luminol-guide", r => { if (r.Luminol != null) r.Luminol.GuideReady(__instance); }); }
        private static void GuideClosed(keyGuideBase __instance) { Safe("story-guide-close", p => p.GuideClosed(__instance)); }
        private static void HealthUpdated() { Safe("health-update", p => p.HealthUpdated()); }
        private static void HealthMoment(bool __0) { Safe("health-moment", p => { if (__0) { p.HealthEvent(); } else { p.HealthHidden(); } }); }
        private static void HealthActive(bool __0) { if (!__0) { Safe("health-hidden", p => p.HealthHidden()); } }
        private static void JudgmentStart(judgmentCtrl __instance, int __0) { Safe("judgment-start", p => p.JudgmentStarting(__instance, __0)); }
        private static void JudgmentEnd(judgmentCtrl __instance) { Safe("judgment-end", p => p.JudgmentEnded(__instance)); }
        private static void JudgmentImpact(judgmentCtrl owner) { Safe("judgment-impact", p => p.JudgmentImpact(owner)); }
        private static void BalloonShown(objMoveCtrl owner) { Safe("balloon-visible", p => p.BalloonVisible(owner)); }
        private static void StoryRestored(messageBoardCtrl __instance) { UiRuntime.Safe("story-load-restored", r => { if (r.Story != null) r.Story.Restored(__instance); }); }
        private static void SpeakerChanged() { UiRuntime.Safe("speaker-plate", r => { if (r.Story != null) { r.Story.ObserveSpeaker(); } }); }
        private static void BoardChanged(bool __0) { if (!__0) { UiRuntime.Safe("story-board-hidden", r => { if (r.Story != null) { r.Story.HideSpeaker(); } }); } }
        private static void RenderedKey(textKeyIconCtrl __instance, UnityEngine.UI.Text __0, int __2)
        { UiRuntime.Safe("story-rendered-key", r => { if (r.Story != null) { r.Story.RenderedKey(__instance, __0, __2); } }); }
        private static void Coroutine(object __instance, MethodBase __originalMethod, object[] __args, ref IEnumerator __result)
        { __result = Completed(__result, __instance, __originalMethod.Name, __args); }
        private static IEnumerator Completed(IEnumerator native, object owner, string name, object[] args)
        {
            try { while (native.MoveNext()) { yield return native.Current; } }
            finally { IDisposable disposable = native as IDisposable; if (disposable != null) { disposable.Dispose(); } }
            if (owner is picePlateCtrl) { Safe("evidence-ready", p => p.EvidenceReady((picePlateCtrl)owner, true)); }
            else if (owner is keyGuideBase)
            { Safe("story-guide-ready", p => { if (name == "CoroutineClose") { p.GuideClosed((keyGuideBase)owner); } else { p.Guide((keyGuideBase)owner); } }); if (name != "CoroutineClose") UiRuntime.Safe("luminol-guide-ready", r => { if (r.Luminol != null) r.Luminol.GuideReady((keyGuideBase)owner); }); }
            else if (owner is lifeGaugeCtrl)
            { Safe("health-ready", p => { if (args.Length > 0 && (bool)args[0]) { p.HealthEvent(); } else { p.HealthHidden(); } }); }
        }
        private static void DamageCoroutine(ref IEnumerator __result) { __result = Damage(__result); }
        private static IEnumerator Damage(IEnumerator native)
        {
            Safe("health-damage-start", p => p.BeginDamage());
            try { while (native.MoveNext()) { yield return native.Current; } }
            finally { IDisposable d = native as IDisposable; if (d != null) { d.Dispose(); } Safe("health-damage-end", p => p.EndDamage()); }
        }
        private static IEnumerable<CodeInstruction> JudgmentTranspiler(IEnumerable<CodeInstruction> input, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(input);
            FieldInfo owner = UiResources.Field(__originalMethod.DeclaringType, "$this");
            int hits = 0;
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction instruction = code[i]; yield return instruction;
                MethodInfo target = instruction.operand as MethodInfo;
                if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) || target == null || target.DeclaringType != typeof(soundCtrl) || target.Name != "PlaySE") { continue; }
                if (i < 1 || !code[i - 1].LoadsConstant(1) || i < 2 || !code[i - 2].LoadsConstant(86)) { throw new InvalidOperationException("Unexpected judgment audio call pattern."); }
                hits++;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, owner);
                yield return new CodeInstruction(OpCodes.Call, typeof(StoryPresentationHooks).GetMethod("JudgmentImpact", BindingFlags.Static | BindingFlags.NonPublic));
            }
            int expected = __originalMethod.DeclaringType.Name.Contains("Japanese") ? 2 : (__originalMethod.DeclaringType.Name.Contains("PtBr") ? 1 : 3);
            if (hits != expected) { throw new InvalidOperationException("Judgment impact events changed: " + hits + "/" + expected); }
        }
        private static IEnumerable<CodeInstruction> BalloonTranspiler(IEnumerable<CodeInstruction> input, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(input);
            FieldInfo owner = UiResources.Field(__originalMethod.DeclaringType, "$this"); int hits = 0;
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction instruction = code[i]; yield return instruction;
                MethodInfo target = instruction.operand as MethodInfo;
                if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) || target == null || target.DeclaringType != typeof(AssetBundleSprite) || target.Name != "set_active" || i < 1 || !code[i - 1].LoadsConstant(1)) { continue; }
                hits++;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, owner);
                yield return new CodeInstruction(OpCodes.Call, typeof(StoryPresentationHooks).GetMethod("BalloonShown", BindingFlags.Static | BindingFlags.NonPublic));
            }
            if (hits != 1) { throw new InvalidOperationException("Balloon visibility commit changed: " + hits); }
        }
    }
}
