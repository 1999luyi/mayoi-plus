using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Pwaat.Accessibility.Ui
{
    internal static class FingerprintHooks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        [ThreadStatic] private static FingerMiniGame _scopeOwner;
        [ThreadStatic] private static int _scopeGeneration;
        private static readonly MethodInfo NativeKey = Method(typeof(padCtrl), "GetKeyDown", typeof(KeyType), typeof(int), typeof(bool), typeof(KeyType));
        private static MethodInfo Method(Type type, string name, params Type[] args)
        { MethodInfo method = type.GetMethod(name, Flags, null, args, null); if (method == null) throw new MissingMethodException(type.FullName, name); return method; }
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(FingerprintHooks).GetMethod(name, Flags)); }
        internal static UnityEngine.AudioClip Clip(int id)
        {
            if (soundCtrl.instance == null) throw new InvalidOperationException("Native fingerprint audio unavailable.");
            UnityEngine.AudioClip clip = Method(typeof(soundCtrl), "GetClipSE", typeof(int)).Invoke(soundCtrl.instance, new object[] { id }) as UnityEngine.AudioClip;
            if (clip == null) throw new InvalidOperationException("Selected native fingerprint cue unavailable: " + id);
            return clip;
        }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(Method(typeof(FingerMiniGame), "Init", typeof(ushort)), prefix: H("Start"));
            harmony.Patch(Method(typeof(FingerMiniGame), "ProcCoroutine"), postfix: H("Lifetime"));
            harmony.Patch(Method(typeof(FingerMiniGame), "SelectCoroutine"), postfix: H("Select"));
            harmony.Patch(Method(typeof(FingerMiniGame), "MainCoroutine"), postfix: H("Dust"));
            harmony.Patch(Method(typeof(FingerMiniGame), "CompMainCoroutine"), postfix: H("Compare"));
            harmony.Patch(Method(typeof(FingerMiniGame), "ComparisonCoroutine"), postfix: H("Scan"));
            harmony.Patch(Method(typeof(FingerMiniGame), "TutorialCoroutine", typeof(int)), postfix: H("Tutorial"));
            harmony.Patch(Method(typeof(FingerMiniGame), "Blow", typeof(bool)), postfix: H("Blow"));
            harmony.Patch(Method(typeof(FingerMiniGame), "CompResultMessageCoroutine"), postfix: H("Result"));
            harmony.Patch(Method(typeof(FingerMiniGame), "CompResultMessage_U_Coroutine"), postfix: H("Result"));
            harmony.Patch(Method(typeof(FingerMiniGame), "LoadMainCoroutineData"), postfix: H("Loaded"));
            harmony.Patch(Method(typeof(FingerMiniGame), "DropPowder", typeof(int), typeof(int)), prefix: H("Dusting"), postfix: H("Dusted"));
            harmony.Patch(Method(typeof(FingerMiniGame), "UpdateCompCursor"), postfix: H("Person"));
            harmony.Patch(Method(typeof(MiniGameCursor), "Process"), postfix: H("Cursor"));
            harmony.Patch(Method(typeof(keyGuideBoard), "CoroutineOpen", typeof(keyGuideBase.Type)), postfix: H("GuideCoroutine"));
            harmony.Patch(Method(typeof(keyGuideBoard), "ReLoadGuid"), postfix: H("Guide"));
            harmony.Patch(Method(typeof(MessageSystem), "Reset", typeof(bool)), prefix: H("Close"));
            harmony.Patch(Method(typeof(advCtrl), "end"), prefix: H("Close"));
            foreach (string name in new string[] { "SelectCoroutine", "MainCoroutine", "CompMainCoroutine" })
            {
                Type iterator = null;
                foreach (Type nested in typeof(FingerMiniGame).GetNestedTypes(Flags)) if (nested.Name.StartsWith("<" + name + ">", StringComparison.Ordinal)) { iterator = nested; break; }
                if (iterator == null) throw new MissingMemberException("Native fingerprint iterator unavailable: " + name);
                harmony.Patch(Method(iterator, "MoveNext"), transpiler: H("InputCommit"));
            }
        }
        // Preserve the original key result exactly. This notification marks the native input loop, not a repeated-key adapter.
        private static bool ReadNativeKey(padCtrl pad, KeyType type, int ext, bool debug, KeyType controller)
        { FingerprintHandler handler = Current; if (handler != null && handler.Owns(_scopeOwner, _scopeGeneration)) Safe("fingerprint-input", p => p.InputReady()); return pad.GetKeyDown(type, ext, debug, controller); }
        private static IEnumerable<CodeInstruction> InputCommit(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> output = new List<CodeInstruction>(); int count = 0;
            MethodInfo replacement = Method(typeof(FingerprintHooks), "ReadNativeKey", typeof(padCtrl), typeof(KeyType), typeof(int), typeof(bool), typeof(KeyType));
            foreach (CodeInstruction code in source)
            {
                if (code.Calls(NativeKey)) { code.opcode = OpCodes.Call; code.operand = replacement; count++; }
                output.Add(code);
            }
            if (count == 0) throw new InvalidOperationException("Native fingerprint input commit site changed.");
            return output;
        }
        private static void Safe(string source, Action<FingerprintHandler> action)
        { UiRuntime.Safe(source, r => { if (r.Fingerprints != null) action(r.Fingerprints); }); }
        private static void Event(string source, Action<FingerprintHandler> action)
        { FingerprintHandler handler = Current; if (_scopeOwner != null && (handler == null || !handler.Owns(_scopeOwner, _scopeGeneration))) return; Safe(source, action); }
        private static bool Advance(FingerMiniGame owner, int generation, IEnumerator inner)
        {
            FingerMiniGame previous = _scopeOwner; int token = _scopeGeneration;
            _scopeOwner = owner; _scopeGeneration = generation;
            try { return inner.MoveNext(); } finally { _scopeOwner = previous; _scopeGeneration = token; }
        }
        private static FingerprintHandler Current { get { return UiRuntime.Current == null ? null : UiRuntime.Current.Fingerprints; } }
        private static void Start(FingerMiniGame __instance, ushort __0) { Safe("fingerprint-start", p => p.Start(__instance, __0)); }
        private static void Close() { Safe("fingerprint-close", p => p.Close()); }
        private static void Loaded() { Event("fingerprint-loaded", p => p.Loaded()); }
        private static void Person() { Event("fingerprint-person", p => p.PersonCommitted()); }
        private static void Cursor(MiniGameCursor __instance)
        { Event("fingerprint-held-cursor", p => p.HeldCursorCommitted(__instance)); Event("fingerprint-cursor", p => p.CursorCommitted(__instance)); }
        private static void Guide(keyGuideBase __instance) { Safe("fingerprint-guide", p => p.GuideReady(__instance)); }
        private static void Dusting(int __0, int __1, out FingerprintStamp __state)
        { FingerprintStamp stamp = null; Event("fingerprint-powder-before", p => stamp = p.BeginDust(__0, __1)); __state = stamp; }
        private static void Dusted(FingerprintStamp __state) { Event("fingerprint-powder-after", p => p.EndDust(__state)); }
        private static void Lifetime(FingerMiniGame __instance, ref IEnumerator __result) { __result = Wrap(__instance, __result, FingerprintPhase.None, 0, false); }
        private static void Select(FingerMiniGame __instance, ref IEnumerator __result) { __result = Wrap(__instance, __result, FingerprintPhase.Select, 1, false); }
        private static void Dust(FingerMiniGame __instance, ref IEnumerator __result) { __result = Wrap(__instance, __result, FingerprintPhase.Dust, 1, false); }
        private static void Compare(FingerMiniGame __instance, ref IEnumerator __result) { __result = Wrap(__instance, __result, FingerprintPhase.Compare, 1, false); }
        private static void Scan(FingerMiniGame __instance, ref IEnumerator __result) { __result = Wrap(__instance, __result, FingerprintPhase.Scan, 2, false); }
        private static void Tutorial(FingerMiniGame __instance, ref IEnumerator __result) { __result = Wrap(__instance, __result, FingerprintPhase.None, 3, false); }
        private static void Blow(FingerMiniGame __instance, bool __0, ref IEnumerator __result) { __result = Wrap(__instance, __result, FingerprintPhase.None, 4, __0); }
        private static void Result(FingerMiniGame __instance, ref IEnumerator __result) { __result = Wrap(__instance, __result, FingerprintPhase.None, 5, false); }
        private static IEnumerator Wrap(FingerMiniGame owner, IEnumerator inner, FingerprintPhase phase, int mode, bool demonstration)
        {
            FingerprintHandler handler = Current; int generation = handler == null ? -1 : handler.Generation;
            return Observe(owner, generation, inner, phase, mode, demonstration);
        }
        private static IEnumerator Observe(FingerMiniGame owner, int generation, IEnumerator inner, FingerprintPhase phase, int mode, bool demonstration)
        {
            FingerprintHandler handler = Current;
            bool valid = handler != null && handler.Owns(owner, generation), complete = false, first = true;
            if (valid)
            {
                if (mode == 1 || mode == 2) Safe("fingerprint-phase", p => p.Enter(phase));
                else if (mode == 3) Safe("fingerprint-tutorial", p => p.BeginTutorial());
                else if (mode == 4) Safe("fingerprint-blow-start", p => p.BeginBlow());
            }
            try
            {
                while (Advance(owner, generation, inner))
                {
                    if (first && valid && handler.Owns(owner, generation))
                    {
                        if (mode == 4) Safe("fingerprint-blow-evaluated", p => p.BlowEvaluated(demonstration));
                        else if (mode == 5) Safe("fingerprint-result", p => p.ResultShown());
                    }
                    first = false; yield return inner.Current;
                }
                complete = true;
            }
            finally
            {
                IDisposable disposable = inner as IDisposable; if (disposable != null) disposable.Dispose();
                if (valid && handler.Owns(owner, generation))
                {
                    if (mode == 0) Safe("fingerprint-end", p => p.Close());
                    else if (mode == 1) Safe("fingerprint-phase-end", p => p.Exit(phase));
                    else if (mode == 2) Safe("fingerprint-scan-end", p => p.Enter(FingerprintPhase.Compare));
                    else if (mode == 3) Safe("fingerprint-tutorial-end", p => p.EndTutorial());
                    else if (mode == 4) Safe("fingerprint-blow-end", p => p.EndBlow(demonstration, complete));
                }
            }
        }
        private static void GuideCoroutine(keyGuideBase __instance, ref IEnumerator __result) { __result = ObserveGuide(__instance, __result); }
        private static IEnumerator ObserveGuide(keyGuideBase owner, IEnumerator inner)
        { while (inner.MoveNext()) yield return inner.Current; Guide(owner); }
    }
}
