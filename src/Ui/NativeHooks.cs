using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal static class NativeHooks
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Patch(Harmony harmony, MethodBase original, string prefix, string postfix, string transpiler)
        {
            if (original == null) { throw new MissingMethodException("Verified native hook target is missing."); }
            harmony.Patch(original, H(prefix), H(postfix), H(transpiler));
            if (harmony.Id == "pwaat.accessibility.title")
            {
                UiRuntime.Safe("title-patch-installed", r => r.Trace("title_patch_installed", "target=" + original.DeclaringType.FullName + "." + original.Name + " token=" + original.MetadataToken + " prefix=" + prefix + " postfix=" + postfix + " transpiler=" + transpiler));
            }
        }
        private static HarmonyMethod H(string name)
        {
            if (name == null) { return null; }
            MethodInfo method = typeof(NativeHooks).GetMethod(name, Flags);
            if (method == null) { throw new MissingMethodException(name); }
            return new HarmonyMethod(method);
        }
        internal static void InstallTitle(Harmony harmony)
        {
            MethodInfo coroutine = AccessTools.Method(typeof(titleSelectPlate), "CoroutineCursor", Type.EmptyTypes);
            MethodInfo moveNext = AccessTools.EnumeratorMoveNext(coroutine);
            Patch(harmony, AccessTools.Method(typeof(mainTitleCtrl), "Init", Type.EmptyTypes), null, "MenuBound", null);
            Patch(harmony, AccessTools.Method(typeof(mainTitleCtrl), "End", Type.EmptyTypes), "MenuEnded", null, null);
            Patch(harmony, AccessTools.Method(typeof(titleSelectPlate), "mainTitleInit"), "ListRebuilding", null, null);
            Patch(harmony, AccessTools.Method(typeof(titleSelectPlate), "playCursor", new[] { typeof(int) }), "CursorRequested", null, null);
            Patch(harmony, AccessTools.Method(typeof(titleSelectPlate), "CoroutineCursor", Type.EmptyTypes), null, "IteratorCreated", null);
            Patch(harmony, AccessTools.Method(typeof(titleSelectPlate), "stopCursor", Type.EmptyTypes), "CursorHidden", null, null);
            Patch(harmony, AccessTools.Method(typeof(titleSelectPlate), "playEnable", Type.EmptyTypes), "CursorHidden", null, null);
            Patch(harmony, AccessTools.Method(typeof(titleSelectPlate), "End", Type.EmptyTypes), "CursorHidden", null, null);
            Patch(harmony, AccessTools.Method(typeof(titleSelectPlate), "updateCursorPosition", new[] { typeof(int) }), null, null, "FocusTranspiler");
            Patch(harmony, moveNext, null, null, "ShownTranspiler");
            Patch(harmony, AccessTools.Method(typeof(messageBoxCtrl), "OpenWindow", Type.EmptyTypes), null, "MessageBoxOpened", null);
            Patch(harmony, AccessTools.Method(typeof(messageBoxCtrl), "OpenWindowSelect", Type.EmptyTypes), null, "MessageBoxOpened", null);
            Patch(harmony, AccessTools.Method(typeof(messageBoxCtrl), "CloseWindow", Type.EmptyTypes), null, "MessageBoxClosed", null);
            Patch(harmony, AccessTools.Method(typeof(messageBoxCtrl), "CloseWindowSelect", Type.EmptyTypes), null, "MessageBoxClosed", null);
            Patch(harmony, AccessTools.Method(typeof(messageBoxCtrl), "init", Type.EmptyTypes), null, "MessageBoxClosed", null);
            Patch(harmony, AccessTools.PropertySetter(typeof(messageBoxCtrl), "active"), null, "MessageBoxActive", null);
        }
        internal static void InstallStart(Harmony harmony)
        {
            Patch(harmony, AccessTools.Method(typeof(startCtrl), "keyIconSet", new[] { typeof(string) }), null, "StartSource", null);
            Patch(harmony, AccessTools.PropertySetter(typeof(startCtrl), "push_active"), null, "StartActive", null);
            Patch(harmony, AccessTools.Method(typeof(sceneCtrl), "End", Type.EmptyTypes), "StartEnded", null, null);
            Patch(harmony, AccessTools.Method(typeof(textKeyIconCtrl), "iconSet", new[] { typeof(KeyCode), typeof(int) }), null, "KeyboardIcon", null);
            Patch(harmony, AccessTools.PropertySetter(typeof(textKeyIconCtrl.KeyIcon), "icon_active_"), null, "PadIcon", null);
        }
        internal static void InstallLoading(Harmony harmony)
        {
            Patch(harmony, AccessTools.Method(typeof(loadingCtrl), "play", new[] { typeof(loadingCtrl.Type) }), "LoadingRequested", null, null);
            Patch(harmony, AccessTools.PropertySetter(typeof(loadingCtrl), "active"), null, "LoadingVisible", null);
        }
        private static void MessageBoxOpened(messageBoxCtrl __instance) { UiRuntime.Safe("message-box-open", r => { if (r.Title != null) { r.Title.MessageBoxOpened(__instance); } }); }
        private static void MessageBoxClosed(messageBoxCtrl __instance) { UiRuntime.Safe("message-box-close", r => { if (r.Title != null) { r.Title.MessageBoxClosed(__instance); } }); }
        private static void MessageBoxActive(messageBoxCtrl __instance, bool __0) { if (!__0) { MessageBoxClosed(__instance); } }
        private static void MenuBound(mainTitleCtrl __instance) { UiRuntime.Safe("menu-init", r => { if (r.Title != null) { r.Title.Bind(__instance); } }); }
        private static void MenuEnded(mainTitleCtrl __instance) { UiRuntime.Safe("menu-end", r => { if (r.Title != null) { r.Title.Close(__instance); } }); }
        private static void ListRebuilding(titleSelectPlate __instance) { UiRuntime.Safe("list-rebuild", r => { if (r.Title != null) { r.Title.Rebuild(__instance); } }); }
        private static void CursorHidden(titleSelectPlate __instance) { UiRuntime.Safe("cursor-hide", r => { if (r.Title != null) { r.Title.Hide(__instance); } if (r.Batch != null) { r.Batch.Clear(__instance); } }); }
        private static void CursorCommitted(titleSelectPlate owner, int index) { UiRuntime.Safe("cursor-commit", r => { if (r.Title != null) { r.Title.Focus(owner, index); } if (r.Batch != null) { r.Batch.PlateChanged(owner); } }); }
        private static void CursorRequested(titleSelectPlate __instance) { UiRuntime.Safe("cursor-request", r => { if (r.Title != null) { r.Title.Requested(__instance); } }); }
        private static void IteratorCreated(titleSelectPlate __instance, System.Collections.IEnumerator __result) { UiRuntime.Safe("iterator-created", r => { if (r.Title != null) { r.Title.IteratorCreated(__instance, __result); } }); }
        private static void CursorEntered(titleSelectPlate owner) { UiRuntime.Safe("cursor-entered", r => { if (r.Title != null) { r.Title.Entered(owner); } }); }
        private static void CursorShown(titleSelectPlate owner) { UiRuntime.Safe("cursor-shown", r => { if (r.Title != null) { r.Title.Shown(owner); } if (r.Batch != null) { r.Batch.PlateShown(owner); } }); }
        private static void StartSource(startCtrl __instance, string __0) { UiRuntime.Safe("start-source", r => { if (r.StartPrompt != null) { r.StartPrompt.SourceCommitted(__instance, __0); } }); }
        private static void StartActive(startCtrl __instance, bool __0) { UiRuntime.Safe("start-active", r => { if (r.StartPrompt != null) { r.StartPrompt.Hidden(__instance, __0); } }); }
        private static void StartEnded(sceneCtrl __instance) { UiRuntime.Safe("start-end", r => { if (r.StartPrompt != null && __instance is startCtrl) { r.StartPrompt.Hidden((startCtrl)__instance, false); } }); }
        private static void KeyboardIcon(textKeyIconCtrl __instance, int __1) { if (__1 != 0) { return; } UiRuntime.Safe("key-icon", r => { if (r.StartPrompt != null) { r.StartPrompt.KeyboardIconCommitted(__instance); } }); }
        private static void PadIcon(textKeyIconCtrl.KeyIcon __instance, bool __0) { if (!__0) { return; } UiRuntime.Safe("pad-icon", r => { if (r.StartPrompt != null) { r.StartPrompt.PadIconCommitted(__instance); } }); }
        private static void LoadingRequested(loadingCtrl __instance, loadingCtrl.Type __0) { UiRuntime.Safe("loading-request", r => { if (r.Loading != null) { r.Loading.Requested(__instance, __0); } }); }
        private static void LoadingVisible(loadingCtrl __instance, bool __0) { UiRuntime.Safe("loading-visible", r => { if (r.Loading != null) { r.Loading.Visible(__instance, __0); } r.Trace("loading-visible", "active=" + __0); }); }

        private static IEnumerable<CodeInstruction> FocusTranspiler(IEnumerable<CodeInstruction> original)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(original);
            FieldInfo field = UiResources.Field(typeof(titleSelectPlate), "current_cursor_idx_");
            int count = 0;
            foreach (CodeInstruction instruction in code) { if (instruction.opcode == OpCodes.Stfld && Equals(instruction.operand, field)) { count++; } }
            if (count != 1) { throw new InvalidOperationException("Expected exactly one native focus commit."); }
            int store = code.FindIndex(c => c.opcode == OpCodes.Stfld && Equals(c.operand, field));
            bool guarded = false;
            for (int i = 0; i + 2 < store; i++)
            {
                if (code[i].opcode != OpCodes.Ldfld || !Equals(code[i].operand, field) || code[i + 1].opcode != OpCodes.Ldarg_1 || (code[i + 2].opcode != OpCodes.Beq && code[i + 2].opcode != OpCodes.Beq_S) || !(code[i + 2].operand is Label)) { continue; }
                Label target = (Label)code[i + 2].operand;
                for (int j = store + 1; j < code.Count; j++) { if (code[j].labels.Contains(target)) { guarded = true; } }
            }
            if (!guarded) { throw new InvalidOperationException("Native unchanged-index branch no longer skips the commit."); }
            UiRuntime.Safe("title-focus-il", r => r.Trace("title_focus_il_verified", "conditionalStoreCount=" + count + " guarded=" + guarded));
            MethodInfo callback = typeof(NativeHooks).GetMethod("CursorCommitted", Flags);
            foreach (CodeInstruction instruction in code)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Stfld && Equals(instruction.operand, field))
                {
                    // The original equality branch skips this store and all added code when unchanged.
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, callback);
                }
            }
        }
        private static IEnumerable<CodeInstruction> ShownTranspiler(IEnumerable<CodeInstruction> original, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(original);
            MethodInfo alpha = AccessTools.Method(typeof(titleSelectPlate), "setAlpha", new[] { typeof(float) });
            FieldInfo owner = UiResources.Field(__originalMethod.DeclaringType, "$this");
            int count = 0;
            foreach (CodeInstruction instruction in code) { if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && Equals(instruction.operand, alpha)) { count++; } }
            if (count != 1) { throw new InvalidOperationException("Expected exactly one native entry-animation completion."); }
            int call = code.FindIndex(c => (c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && Equals(c.operand, alpha));
            if (call <= 0 || code[call - 1].opcode != OpCodes.Ldc_R4 || !(code[call - 1].operand is float) || (float)code[call - 1].operand != 1f) { throw new InvalidOperationException("Entry completion alpha is not the verified native 1.0 commit."); }
            MethodInfo start = AccessTools.PropertySetter(typeof(titleSelectPlate), "body_active");
            int starts = code.FindAll(c => (c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && Equals(c.operand, start)).Count;
            if (starts != 1) { throw new InvalidOperationException("Expected exactly one native entry visibility event."); }
            UiRuntime.Safe("title-shown-il", r => r.Trace("title_shown_il_verified", "method=" + __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name + " alphaCommits=" + count + " bodyShows=" + starts));
            MethodInfo entered = typeof(NativeHooks).GetMethod("CursorEntered", Flags);
            MethodInfo callback = typeof(NativeHooks).GetMethod("CursorShown", Flags);
            foreach (CodeInstruction instruction in code)
            {
                yield return instruction;
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && Equals(instruction.operand, start))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, owner);
                    yield return new CodeInstruction(OpCodes.Call, entered);
                }
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && Equals(instruction.operand, alpha))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, owner);
                    yield return new CodeInstruction(OpCodes.Call, callback);
                }
            }
        }
        internal static void PageBefore(object __instance, MethodBase __originalMethod) { UiRuntime.Safe("page-call", r => { if (r.Pages != null) { r.Pages.Call(__instance, __originalMethod, "call"); } }); }
        internal static void PageAfter(object __instance, MethodBase __originalMethod) { UiRuntime.Safe("page-return", r => { if (r.Pages != null) { r.Pages.Call(__instance, __originalMethod, "return"); } }); }
        internal static void PageFlag(object __instance, bool __0, MethodBase __originalMethod) { UiRuntime.Safe("page-flag", r => { if (r.Pages != null) { r.Pages.Call(__instance, __originalMethod, "flag=" + __0); } }); }
        internal static void SceneRequest(titleCtrlRoot __instance, titleCtrlRoot.SceneType __0) { UiRuntime.Safe("scene-request", r => { if (r.Pages != null) { r.Pages.SceneRequest(__instance, __0); } }); }
    }
}
