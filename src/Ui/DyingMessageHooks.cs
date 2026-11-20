using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace Pwaat.Accessibility.Ui
{
    internal static class DyingMessageHooks
    {
        private static MethodInfo M(Type type, string name, params Type[] args)
        { MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, args, null); if (method == null) throw new MissingMethodException(type.FullName, name); return method; }
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(DyingMessageHooks).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)); }
        internal static void Install(Harmony h)
        {
            h.Patch(M(typeof(DyingMessageMiniGame), "StartDyingMessage"), prefix: H("Close"), postfix: H("Started"));
            Type state = typeof(DyingMessageMiniGame).GetNestedType("SwDiemesProc", BindingFlags.NonPublic);
            if (state == null) throw new MissingMemberException("Dying message state enum missing.");
            h.Patch(M(typeof(DyingMessageMiniGame), "_chengeStaete", state), postfix: H("StateChanged"));
            h.Patch(M(typeof(DyingMessageUtil), "body_die_message"), prefix: H("BeforeBody"), postfix: H("AfterBody"));
            h.Patch(M(typeof(MiniGameCursor), "Process"), prefix: H("CursorAllowed"));
            h.Patch(M(typeof(MiniGameCursor), "UpdateCursorPosition"), postfix: H("CursorMoved"));
            h.Patch(M(typeof(DyingMessageMiniGame), "_checkDieMessage", typeof(Language)), prefix: H("Result"));
            h.Patch(M(typeof(DyingMessageUtil), "end_die_message"), postfix: H("Close"));
            h.Patch(M(typeof(MessageSystem), "Reset", typeof(bool)), prefix: H("Close"));
            h.Patch(M(typeof(advCtrl), "end"), prefix: H("Close"));
            h.Patch(M(typeof(optionCtrl), "Init"), prefix: H("Hidden"));
            h.Patch(M(typeof(optionCtrl), "Close"), postfix: H("Restored"));
            h.Patch(M(typeof(SaveLoadUICtrl), "Open", typeof(SaveLoadUICtrl.SlotType)), prefix: H("Hidden"));
            h.Patch(M(typeof(SaveLoadUICtrl), "Close"), postfix: H("Restored"));
            foreach (string name in new[] { "GetDown", "GetTouched", "GetTouch" }) h.Patch(M(typeof(TouchSystem), name, typeof(KeyType)), prefix: H("TouchAllowed"));
        }
        private static void Safe(Action<DyingMessageHandler> action)
        { UiRuntime r = UiRuntime.Current; if (r == null || r.DyingMessage == null) return; try { action(r.DyingMessage); } catch (Exception e) { r.DyingMessage.Fail(e); } }
        private static void Started(DyingMessageMiniGame __instance) { Safe(p => p.Begin(__instance)); }
        private static void StateChanged(DyingMessageMiniGame __instance) { Safe(p => p.StateCommitted(__instance)); }
        private static bool BeforeBody(DyingMessageUtil __instance, out bool __state)
        {
            bool run = true; __state = false; UiRuntime r = UiRuntime.Current;
            if (r == null || r.DyingMessage == null || !r.DyingMessage.OwnsUtility(__instance)) return true;
            Safe(p => run = p.BeforeBody());
            if (run && padCtrl.instance != null) __state = padCtrl.instance.GetKeyDown(KeyType.A) || padCtrl.instance.GetKeyDown(KeyType.B);
            return run;
        }
        private static void AfterBody(bool __state) { if (__state) Safe(p => p.NativeActionCommitted()); }
        private static bool CursorAllowed(MiniGameCursor __instance) { UiRuntime r = UiRuntime.Current; return r == null || r.DyingMessage == null || !r.DyingMessage.SuppressCursor(__instance); }
        private static void CursorMoved(MiniGameCursor __instance) { Safe(p => p.CursorCommitted(__instance)); }
        private static bool Result(DyingMessageMiniGame __instance, ref bool __result)
        { UiRuntime r = UiRuntime.Current; bool result; if (r != null && r.DyingMessage != null && r.DyingMessage.TryResult(__instance, out result)) { __result = result; return false; } return true; }
        private static bool TouchAllowed(KeyType __0, ref bool __result)
        { UiRuntime r = UiRuntime.Current; if (__0 != KeyType.A || r == null || r.DyingMessage == null || !r.DyingMessage.IsAdapted) return true; __result = false; return false; }
        private static void Close() { Safe(p => p.Close()); }
        private static void Hidden() { Safe(p => p.Hide()); }
        private static void Restored() { Safe(p => p.Resume()); }
    }
}
