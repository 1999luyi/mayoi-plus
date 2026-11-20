using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal static class AccessibilityHooks
    {
        private static HarmonyMethod H(string method) { return new HarmonyMethod(typeof(AccessibilityHooks).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)); }
        private static MethodInfo M(Type type, string name, params Type[] args)
        { MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, args, null); if (method == null || method.IsAbstract) throw new MissingMethodException(type.FullName, name); return method; }
        internal static void InstallSettings(Harmony h)
        {
            h.Patch(M(typeof(optionCtrl), "Init"), prefix: H("SettingsInit"));
            h.Patch(M(typeof(optionCategoryItem), "Init"), prefix: H("CategoryInit"));
            h.Patch(M(typeof(optionCtrl), "SetTextCat", typeof(optionCtrl.Category)), prefix: H("CategoryDescription"));
            h.Patch(M(typeof(optionCtrl), "SetText", typeof(optionCtrl.OptionItem)), prefix: H("ItemDescription"));
            h.Patch(M(typeof(optionCtrl), "SetCategoriesList", typeof(optionCtrl.OptionType)), postfix: H("SettingsLayout"));
            h.Patch(M(typeof(optionCtrl), "ChangeCategory", typeof(optionCtrl.Category)), postfix: H("PageChanged"));
            h.Patch(M(typeof(optionCtrl), "ChangeMenuState", typeof(optionCtrl.OptionMenuState)), postfix: H("SettingsStateChanged"));
            h.Patch(AccessTools.EnumeratorMoveNext(M(typeof(optionCtrl), "MenuRoutine")), transpiler: H("SettingsMenuStarted"));
            h.Patch(M(typeof(optionCtrl), "Close"), postfix: H("PageLeft"));
            h.Patch(M(typeof(optionCtrl), "getMoveKey"), postfix: H("NavigationInput"));
        }
        internal static void InstallHelp(Harmony h)
        {
            h.Patch(M(typeof(InputCtrl), "Update"), prefix: H("InputFrame"));
            h.Patch(M(typeof(titleCtrlRoot), "Scene", typeof(titleCtrlRoot.SceneType)), prefix: H("SceneChanging"));
            h.Patch(M(typeof(recordListCtrl), "set_body_active", typeof(bool)), postfix: H("PageVisibility"));
            h.Patch(M(typeof(selectPlateCtrl), "set_body_active", typeof(bool)), postfix: H("PageVisibility"));
            foreach (Type type in new[] { typeof(InputCtrl), typeof(padCtrl), typeof(TouchSystem) })
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (method.ReturnType == typeof(bool) && (method.Name == "GetKey" || method.Name == "GetKeyDown" || method.Name == "GetKeyUp" || method.Name.StartsWith("InputGetKey", StringComparison.Ordinal) || method.Name.StartsWith("InputGetMouseButton", StringComparison.Ordinal) || method.Name == "GetWheelMoveUp" || method.Name == "GetWheelMoveDown" || method.Name == "GetTouch" || method.Name == "GetTouched" || method.Name == "GetDown" || method.Name == "GetTouchDownQuestioning" || method.Name == "InputGetButtonDown" || method.Name == "InputAnyKey" || method.Name == "InputAnyKeyBord" || method.Name == "InputAnyKeyPad" || method.Name == "InControlInputWasPressedAction1" || method.Name == "GetLeftStick" || method.Name == "GetRightStick" || method.Name == "GetTriggerLeft" || method.Name == "GetTriggerRight"))
                    { h.Patch(method, prefix: H("GateBool"), postfix: (method.Name == "GetKeyDown" || method.Name == "InputGetKeyDown" || method.Name == "InputGetMouseButtonDown" || method.Name == "GetWheelMoveUp" || method.Name == "GetWheelMoveDown") ? H("InputEdge") : null); }
                    if (type == typeof(padCtrl) && method.ReturnType == typeof(float) && (method.Name == "InputGetAxis" || method.Name == "InputGetAxisRaw")) { h.Patch(method, prefix: H("GateAxis")); }
                }
            }
            h.Patch(M(typeof(TouchSystem), "UpdateTouch"), prefix: H("GateVoid"));
            h.Patch(M(typeof(TouchSystem), "UpdateDownEvent"), prefix: H("GateVoid"));
        }
        private static bool Blocks { get { UiRuntime r = UiRuntime.Current; return r != null && r.Assistance != null && r.Assistance.BlocksInput && !r.Assistance.BypassInput; } }
        private static void NavigationInput(KeyType __result) { if (__result != KeyType.None) { UiRuntime.Safe("input-navigation-serial", r => r.Speech.InputPolicy.Pressed(r.Speech.Clock)); } }
        private static void InputEdge(bool __result) { if (__result) { UiRuntime.Safe("input-serial", r => r.Speech.InputPolicy.Pressed(r.Speech.Clock)); } }
        private static void InputFrame() { UiRuntime.Safe("accessibility-input", r => { if (r.StoryInput != null) r.StoryInput.Dispatch(); }); }
        private static bool GateBool(object[] __args, ref bool __result)
        {
            bool review = false;
            if (__args.Length > 0 && __args[0] is KeyCode) { KeyCode key = (KeyCode)__args[0]; review = (key == KeyCode.PageUp || key == KeyCode.PageDown) && (UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl)); }
            if (!Blocks && !review) return true; __result = false; return false;
        }
        private static bool GateAxis(ref float __result) { if (!Blocks) return true; __result = 0f; return false; }
        private static bool GateVoid() { return !Blocks; }
        private static void SettingsInit(optionCtrl __instance)
        {
            UiRuntime.Safe("accessibility-native-init", r => { if (r.NativeSettings != null) { try { r.NativeSettings.Ensure(__instance); } catch { r.NativeSettings.Dispose(); throw; } } });
        }
        private static bool CategoryInit(optionCategoryItem __instance)
        {
            UiRuntime r = UiRuntime.Current; if (r == null || r.NativeSettings == null || (int)__instance.category != NativeAccessibilitySettings.CategoryId) return true;
            r.NativeSettings.InitCategory(__instance); return false;
        }
        private static bool CategoryDescription(optionCtrl __instance, optionCtrl.Category __0)
        {
            UiRuntime r = UiRuntime.Current; if (r == null || r.NativeSettings == null || (int)__0 != NativeAccessibilitySettings.CategoryId) return true;
            r.NativeSettings.Description(__instance, true, 0); return false;
        }
        private static bool ItemDescription(optionCtrl __instance, optionCtrl.OptionItem __0)
        {
            UiRuntime r = UiRuntime.Current; if (r == null || r.NativeSettings == null || (int)__0 < NativeAccessibilitySettings.FirstItem) return true;
            r.NativeSettings.Description(__instance, false, (int)__0); return false;
        }
        private static void SettingsLayout(optionCtrl __instance, optionCtrl.OptionType __0) { UiRuntime.Safe("accessibility-layout", r => { if (r.NativeSettings != null) r.NativeSettings.Layout(__instance, __0); }); }
        private static void PageVisibility(bool __0) { UiRuntime.Safe("accessibility-page-visible", r => { if (r.Assistance != null) { if (__0) r.Assistance.EnterPage(); else r.Assistance.LeavePage(); } }); }
        private static void SettingsStateChanged(optionCtrl __instance, optionCtrl.OptionMenuState __0)
        {
            UiRuntime.Safe("settings-state-committed", r =>
            {
                if (r.Assistance != null) { r.Assistance.EnterPage(); }
                if (r.Batch == null) return;
                if (__0 == optionCtrl.OptionMenuState.Category) { r.Batch.OptionDescriptionCommitted(__instance); }
            });
        }
        private static IEnumerable<CodeInstruction> SettingsMenuStarted(IEnumerable<CodeInstruction> input, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(input);
            FieldInfo owner = UiResources.Field(__originalMethod.DeclaringType, "$this");
            MethodInfo reset = M(typeof(optionCtrl), "resetInput");
            int count = code.FindAll(c => (c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && Equals(c.operand, reset)).Count;
            if (count != 1) { throw new InvalidOperationException("Native settings menu entry changed: " + count); }
            foreach (CodeInstruction instruction in code)
            {
                yield return instruction;
                if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) || !Equals(instruction.operand, reset)) { continue; }
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, owner);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AccessibilityHooks), "SettingsMenuReady"));
            }
        }
        private static void SettingsMenuReady(optionCtrl owner)
        {
            UiRuntime.Safe("settings-menu-ready", r =>
            {
                if (r.Assistance != null) { r.Assistance.EnterPage(); }
                if (r.Batch != null) { r.Batch.OptionsEntered(owner); }
            });
        }
        private static void PageChanged() { UiRuntime.Safe("accessibility-page", r => { if (r.Assistance != null) r.Assistance.EnterPage(); }); }
        private static void PageLeft() { UiRuntime.Safe("accessibility-page-left", r => { if (r.Assistance != null) r.Assistance.LeavePage(); }); }
        private static void SceneChanging(titleCtrlRoot.SceneType __0) { UiRuntime.Safe("accessibility-scene", r => { if (r.Assistance != null) { r.Assistance.Scene = (int)__0; r.Assistance.LeavePage(); } }); }
    }
}
