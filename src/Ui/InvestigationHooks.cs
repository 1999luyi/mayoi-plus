using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Pwaat.Accessibility.Ui
{
    internal static class InvestigationHooks
    {
        private static INSPECT_DATA _queryData;
        private static readonly MethodInfo Refresh = typeof(inspectCtrl).GetMethod("Set_InspectCursor", BindingFlags.Instance | BindingFlags.NonPublic);
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static MethodInfo M(Type type, string name, params Type[] args)
        {
            MethodInfo method = type.GetMethod(name, Flags, null, args, null);
            if (method == null) { throw new MissingMethodException(type.FullName, name); }
            return method;
        }
        private static MethodInfo MByName(string name) { return typeof(InvestigationHooks).GetMethod(name, Flags); }
        private static void After(Harmony h, Type type, string method, string hook, params Type[] args)
        { h.Patch(M(type, method, args), postfix: new HarmonyMethod(MByName(hook))); }
        private static void Before(Harmony h, Type type, string method, string hook, params Type[] args)
        { h.Patch(M(type, method, args), prefix: new HarmonyMethod(MByName(hook))); }
        internal static void InstallMenus(Harmony h)
        {
            After(h, typeof(tanteiMenu), "UpdateCursorPos", "MenuChanged");
            Before(h, typeof(tanteiMenu), "stop", "MenuClosed");
            Before(h, typeof(tanteiMenu), "close", "MenuClosed");
            After(h, typeof(moveCtrl), "move_set_thumbnail_image", "MoveChanged");
            Before(h, typeof(moveCtrl), "stop", "MoveClosed");
            After(h, typeof(selectPlateCtrl), "setRead", "ReadMark", typeof(int), typeof(bool), typeof(bool));
            After(h, typeof(selectPlateCtrl), "entryCursor", "TopicsReady", typeof(int), typeof(selectPlateCtrl.FromEntryRequest));
            Before(h, typeof(MessageSystem), "Reset", "WorldReset", typeof(bool));
            Before(h, typeof(advCtrl), "end", "WorldReset");
            h.Patch(AccessTools.EnumeratorMoveNext(M(typeof(tanteiMenu), "CoroutinePlay", typeof(int))), transpiler: new HarmonyMethod(MByName("InputReadyTranspiler")));
            h.Patch(AccessTools.EnumeratorMoveNext(M(typeof(moveCtrl), "CoroutinePlay", typeof(int), typeof(int))), transpiler: new HarmonyMethod(MByName("InputReadyTranspiler")));
        }
        internal static void InstallInspection(Harmony h)
        {
            // Reuse the original pure hit test with explicit coordinates. No cursor or script writes.
            Harmony.ReversePatch(M(typeof(SubWindow_Inspect), "finger_pos_check", typeof(bool)), new HarmonyMethod(MByName("HitAt")), MByName("HitCoordinates"), null);
            h.Patch(AccessTools.EnumeratorMoveNext(M(typeof(inspectCtrl), "CoroutinePlay")), transpiler: new HarmonyMethod(MByName("InputReadyTranspiler")));
            After(h, typeof(inspectCtrl), "Set_InspectCursor", "CursorCommitted");
            h.Patch(M(typeof(SubWindow_Inspect), "finger_pos_check", typeof(bool)), postfix: new HarmonyMethod(MByName("NativeHit")), transpiler: new HarmonyMethod(MByName("CaptureRegions")));
            h.Patch(M(typeof(GSFlag), "Set", typeof(uint), typeof(uint), typeof(uint)), prefix: new HarmonyMethod(MByName("FlagStarting")), postfix: new HarmonyMethod(MByName("FlagCommitted")));
            Before(h, typeof(inspectCtrl), "Set_InspectCursor", "CursorStarting");
            h.Patch(AccessTools.EnumeratorMoveNext(M(typeof(inspectCtrl), "CoroutinePlay")), transpiler: new HarmonyMethod(MByName("InspectExitTranspiler")));
            After(h, typeof(inspectCtrl), "set_active", "InspectActive", typeof(bool));
            Before(h, typeof(inspectCtrl), "stop", "InspectClosed");
            Before(h, typeof(inspectCtrl), "end", "InspectClosed");
            After(h, typeof(bgCtrl), "SetSeal", "VisualChanged", typeof(int), typeof(bool));
            h.Patch(AccessTools.EnumeratorMoveNext(M(typeof(bgCtrl), "CoroutineSlider")), transpiler: new HarmonyMethod(MByName("SliderFinished")));
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static uint HitAt(bool inspectCheck, float x, float y) { throw new NotSupportedException("Native hit-test reverse patch is not installed."); }
        internal static uint QueryAt(float x, float y, out INSPECT_DATA region)
        { _queryData = null; uint message = HitAt(true, x, y); region = _queryData; return message; }
        internal static void RefreshCursor(inspectCtrl owner) { Refresh.Invoke(owner, null); }
        private static void RememberQuery(INSPECT_DATA data) { _queryData = data; }
        private static void RememberNative(INSPECT_DATA data, bool inspectCheck) { if (inspectCheck) UiRuntime.Safe("investigation-native-region", r => { if (r.Investigation != null) r.Investigation.NativeRegion(data); }); }
        private static IEnumerable<CodeInstruction> CaptureRegions(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo message = typeof(INSPECT_DATA).GetField("message");
            int count = 0;
            foreach (CodeInstruction c in instructions)
            {
                if (c.opcode == OpCodes.Ldfld && Equals(c.operand, message))
                {
                    count++; CodeInstruction dup = new CodeInstruction(OpCodes.Dup); dup.labels.AddRange(c.labels); c.labels.Clear(); dup.blocks.AddRange(c.blocks); c.blocks.Clear();
                    yield return dup; yield return new CodeInstruction(OpCodes.Ldarg_0); yield return new CodeInstruction(OpCodes.Call, MByName("RememberNative"));
                }
                yield return c;
            }
            if (count == 0) throw new InvalidOperationException("Native hit test no longer returns an investigation region.");
        }
        private static void MenuChanged(tanteiMenu __instance) { UiRuntime.Safe("investigation-menu", r => { if (r.Investigation != null) r.Investigation.MenuChanged(__instance); }); }
        private static void MenuClosed(tanteiMenu __instance) { UiRuntime.Safe("investigation-menu-close", r => { if (r.Investigation != null) r.Investigation.MenuClosed(__instance); }); }
        private static void MoveChanged(moveCtrl __instance) { UiRuntime.Safe("investigation-move", r => { if (r.Investigation != null) r.Investigation.MoveChanged(__instance); }); }
        private static void MoveClosed(moveCtrl __instance) { UiRuntime.Safe("investigation-move-close", r => { if (r.Investigation != null) r.Investigation.MoveClosed(__instance); }); }
        private static void ReadMark(selectPlateCtrl __instance, int __0) { UiRuntime.Safe("investigation-talk-mark", r => { if (r.Investigation != null) r.Investigation.ReadMark(__instance, __0); }); }
        private static void TopicsReady(selectPlateCtrl __instance, selectPlateCtrl.FromEntryRequest __1) { UiRuntime.Safe("investigation-talk-entry", r => { if (r.Investigation != null) r.Investigation.TopicsReady(__instance, __1); }); }
        private static void WorldReset() { UiRuntime.Safe("investigation-world-reset", r => { if (r.Investigation != null) r.Investigation.Reset(); }); }
        private static void NativeHit(bool __0, uint __result) { if (__0) UiRuntime.Safe("investigation-native-hit", r => { if (r.Investigation != null) r.Investigation.NativeHit(__result); }); }
        private static void FlagStarting(uint __0, uint __1, out bool __state) { __state = GSFlag.Check(__0, __1); }
        private static void FlagCommitted(uint __0, uint __1, bool __state) { if (GSFlag.Check(__0, __1) != __state) UiRuntime.Safe("investigation-flag", r => { if (r.Investigation != null) r.Investigation.StateChanged(); }); }
        private static void CursorStarting(inspectCtrl __instance) { UiRuntime.Safe("investigation-cursor-start", r => { if (r.Investigation != null) r.Investigation.CursorStarting(__instance); }); }
        private static void CursorCommitted(inspectCtrl __instance) { UiRuntime.Safe("investigation-cursor", r => { if (r.Investigation != null) r.Investigation.CursorCommitted(__instance); }); }
        private static void InspectClosed(inspectCtrl __instance) { UiRuntime.Safe("investigation-inspect-close", r => { if (r.Investigation != null) r.Investigation.InspectClosed(__instance); }); }
        private static void InspectActive(inspectCtrl __instance, bool __0) { if (!__0) InspectClosed(__instance); }
        private static IEnumerable<CodeInstruction> InspectExitTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            FieldInfo owner = UiResources.Field(__originalMethod.DeclaringType, "$this");
            FieldInfo leaving = UiResources.Field(typeof(inspectCtrl), "select_animation_playing_");
            int count = 0; CodeInstruction previous = null;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Stfld && Equals(instruction.operand, leaving) && previous != null && previous.opcode == OpCodes.Ldc_I4_1)
                {
                    count++;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, owner);
                    yield return new CodeInstruction(OpCodes.Call, MByName("InspectClosed"));
                }
                previous = instruction;
            }
            if (count != 1) throw new InvalidOperationException("Native inspection input-exit commit changed: " + count);
        }
        private static void InputReady(object owner)
        {
            UiRuntime.Safe("investigation-input-ready", r =>
            {
                if (r.Investigation == null) return;
                if (owner is tanteiMenu) r.Investigation.MenuReady((tanteiMenu)owner);
                else if (owner is moveCtrl) r.Investigation.MoveReady((moveCtrl)owner);
                else if (owner is inspectCtrl) r.Investigation.InspectReady((inspectCtrl)owner);
            });
        }
        private static void VisualChanged() { UiRuntime.Safe("investigation-visual", r => { if (r.Investigation != null) r.Investigation.VisualChanged(); }); }
        private static void SideReady() { UiRuntime.Safe("investigation-side", r => { if (r.Investigation != null) r.Investigation.SideReady(); }); }
        private static IEnumerable<CodeInstruction> InputReadyTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            MethodInfo wait = M(typeof(systemCtrl), "get_key_wait");
            FieldInfo owner = AccessTools.Field(__originalMethod.DeclaringType, "$this");
            int count = 0;
            for (int i = 0; i < code.Count; i++)
            {
                yield return code[i];
                if (i == 0 || !code[i - 1].Calls(wait)) continue;
                if (code[i].opcode != OpCodes.Stfld || owner == null) throw new InvalidOperationException("Native input-entry store changed.");
                count++;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, owner);
                yield return new CodeInstruction(OpCodes.Call, MByName("InputReady"));
            }
            if (count != 1) throw new InvalidOperationException("Native input entry is not unique: " + count);
        }
        private static IEnumerable<CodeInstruction> HitCoordinates(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            MethodInfo instance = M(typeof(inspectCtrl), "get_instance");
            int x = 0, y = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (i + 1 < code.Count && code[i].Calls(instance))
                {
                    bool isX = code[i + 1].Calls(M(typeof(inspectCtrl), "get_pos_x"));
                    bool isY = code[i + 1].Calls(M(typeof(inspectCtrl), "get_pos_y"));
                    if (isX || isY)
                    {
                        CodeInstruction replacement = new CodeInstruction(isX ? OpCodes.Ldarg_1 : OpCodes.Ldarg_2);
                        replacement.labels.AddRange(code[i].labels); replacement.labels.AddRange(code[i + 1].labels);
                        replacement.blocks.AddRange(code[i].blocks); replacement.blocks.AddRange(code[i + 1].blocks);
                        if (isX) x++; else y++;
                        yield return replacement; i++; continue;
                    }
                }
                if (code[i].opcode == OpCodes.Ldfld && Equals(code[i].operand, typeof(INSPECT_DATA).GetField("message")))
                {
                    CodeInstruction dup = new CodeInstruction(OpCodes.Dup); dup.labels.AddRange(code[i].labels); code[i].labels.Clear(); dup.blocks.AddRange(code[i].blocks); code[i].blocks.Clear();
                    yield return dup; yield return new CodeInstruction(OpCodes.Call, MByName("RememberQuery"));
                }
                yield return code[i];
            }
            if (x != 1 || y != 1) throw new InvalidOperationException("Native hit-test coordinates changed: " + x + "/" + y);
        }
        private static IEnumerable<CodeInstruction> SliderFinished(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo sliding = UiResources.Field(typeof(bgCtrl), "is_slider_");
            int count = 0; CodeInstruction previous = null;
            foreach (CodeInstruction c in instructions)
            {
                yield return c;
                if (c.opcode == OpCodes.Stfld && Equals(c.operand, sliding) && previous != null && previous.opcode == OpCodes.Ldc_I4_0)
                { count++; yield return new CodeInstruction(OpCodes.Call, MByName("SideReady")); }
                previous = c;
            }
            if (count != 1) throw new InvalidOperationException("Native slide completion changed: " + count);
        }
    }
}
