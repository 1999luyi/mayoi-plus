using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal static class StoryHooks
    {
        private static HarmonyMethod H(string name) { return name == null ? null : new HarmonyMethod(typeof(StoryHooks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)); }
        private static MethodInfo Method(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, parameters, null);
            if (method == null || method.IsAbstract) { throw new MissingMethodException(type.FullName, name); }
            return method;
        }
        private static void Patch(Harmony h, Type type, string name, string before, string after, params Type[] parameters)
        {
            h.Patch(Method(type, name, parameters), prefix: H(before), postfix: H(after));
        }
        internal static void InstallMessages(Harmony h)
        {
            Patch(h, typeof(MessageSystem), "Message_move", "MoveStarting", "MoveFinished", typeof(MessageWork));
            Patch(h, typeof(MessageSystem), "SetCharacter", null, "CharacterCommitted", typeof(MessageWork), typeof(char));
            Patch(h, typeof(MessageSystem), "ClearText", null, "TextCleared", typeof(MessageWork));
            Patch(h, typeof(MessageSystem), "Message_init_sub", null, "WorkReset", typeof(MessageWork), typeof(bool));
            foreach (string code in new[] { "CodeProc_0d", "CodeProc_2a", "CodeProc_35", "CodeProc_36", "CodeProc_6a", "CodeProc_7a" }) { Patch(h, typeof(MessageSystem), code, null, "WorkReset", typeof(MessageWork)); }
            Patch(h, typeof(MessageSystem), "Reset", null, "MessagesClear", typeof(bool));
            Patch(h, typeof(MessageSystem), "end", "MessagesClear", null);
            Patch(h, typeof(messageBoardCtrl), "End", "MessagesClear", null);
            Patch(h, typeof(messageBoardCtrl), "OnDisable", "MessagesClear", null);
        }
        internal static void InstallChoices(Harmony h)
        {
            Patch(h, typeof(selectPlateCtrl), "SetCursorNo", null, "ChoiceCommit", typeof(int));
            Patch(h, typeof(selectPlateCtrl), "TouchItem", null, "ChoiceCommit", typeof(TouchParameter));
            Patch(h, typeof(selectPlateCtrl), "entryCursor", "ChoiceClear", null, typeof(int), typeof(selectPlateCtrl.FromEntryRequest));
            Patch(h, typeof(selectPlateCtrl), "end", "ChoiceClear", null);
            Patch(h, typeof(selectPlateCtrl), "stopCursor", "ChoiceClear", null);
            Patch(h, typeof(selectPlateCtrl), "Terminate", "ChoiceClear", null);
            MethodInfo next = AccessTools.EnumeratorMoveNext(Method(typeof(selectPlateCtrl), "CoroutineCursor"));
            if (next == null) { throw new MissingMethodException("selectPlateCtrl", "CoroutineCursor.MoveNext"); }
            h.Patch(next, transpiler: H("ChoiceTranspiler"));
        }
        internal static void InstallRecords(Harmony h)
        {
            Patch(h, typeof(recordListCtrl), "playRecord", "RecordStarting", null, typeof(bool));
            Patch(h, typeof(recordListCtrl), "cursorRecord", null, "RecordCommit", typeof(int), typeof(int));
            Patch(h, typeof(recordListCtrl), "ActiveRecodeListTouch", null, "RecordReady");
            Patch(h, typeof(recordListCtrl), "set_body_active", null, "RecordActive", typeof(bool));
            Patch(h, typeof(recordListCtrl), "recordPageChange", "RecordStarting", null);
            MethodInfo page = Method(typeof(recordListCtrl), "CoroutinePageChabge");
            h.Patch(page, postfix: H("RecordPageRequested"));
            Patch(h, typeof(recordDetailCtrl), "CoroutineViewDetail", "RecordDetailStarting", "RecordDetailRequested", typeof(int));
        }
        internal static void InstallDialogs(Harmony h)
        {
            Patch(h, typeof(messageBoxCtrl), "SetText", "DialogText", "DialogTextCommit", typeof(string), typeof(int));
            Patch(h, typeof(messageBoxCtrl), "SetText", "DialogTexts", "DialogTextsCommit", typeof(string[]));
            Patch(h, typeof(messageBoxCtrl), "OpenWindow", null, "DialogOpened");
            Patch(h, typeof(messageBoxCtrl), "OpenWindowSelect", null, "DialogSelectOpened");
            Patch(h, typeof(messageBoxCtrl), "CloseWindow", null, "DialogClosed");
            Patch(h, typeof(messageBoxCtrl), "CloseWindowSelect", null, "DialogClosed");
            Patch(h, typeof(messageBoxCtrl), "init", null, "DialogClosed");
            Patch(h, typeof(messageBoxCtrl), "set_active", null, "DialogActive", typeof(bool));
            Patch(h, typeof(titleSelectPlate), "updateCursorPosition", null, "DialogPlateCommit", typeof(int));
            Patch(h, typeof(titleSelectPlate), "setAlpha", null, "DialogPlateAlpha", typeof(float));
        }
        private static void MoveStarting() { UiRuntime.Safe("story-move-start", r => { if (r.Story != null) { r.Story.BeginMove(); } }); }
        private static void MoveFinished() { UiRuntime.Safe("story-move-end", r => { if (r.Story != null) { r.Story.EndMove(); } }); }
        private static void CharacterCommitted(MessageWork __0) { UiRuntime.Safe("story-glyph", r => { if (r.Story != null) { r.Story.CharacterCommitted(__0); } }); }
        private static void TextCleared(MessageWork __0) { UiRuntime.Safe("story-rendered-clear", r => { if (r.Story != null) { r.Story.Reset(__0); r.Story.RenderedClear(); } }); }
        private static void WorkReset(MessageWork __0) { UiRuntime.Safe("story-work-reset", r => { if (r.Story != null) { r.Story.Reset(__0); } }); }
        private static void MessagesClear() { UiRuntime.Safe("story-close", r => { if (r.Story != null) { r.Story.Clear(); } }); }
        private static void ChoiceCommit(selectPlateCtrl __instance) { ChoiceNotify(__instance); }
        private static void ChoiceNotify(selectPlateCtrl owner) { UiRuntime.Safe("story-choice", r => { if (r.StoryUi != null) { r.StoryUi.Choice(owner); } }); }
        private static void ChoiceClear(selectPlateCtrl __instance) { UiRuntime.Safe("story-choice-close", r => { if (r.StoryUi != null) { r.StoryUi.Clear(__instance); } }); }
        private static void RecordCommit(recordListCtrl __instance) { UiRuntime.Safe("story-record", r => { if (r.StoryUi != null) { r.StoryUi.Record(__instance); } }); }
        private static void RecordStarting(recordListCtrl __instance) { UiRuntime.Safe("story-record-start", r => { if (r.StoryUi != null) { r.StoryUi.RecordStarting(__instance); } }); }
        private static void RecordReady(recordListCtrl __instance) { UiRuntime.Safe("story-record-ready", r => { if (r.StoryUi != null) { r.StoryUi.RecordReady(__instance); } }); }
        private static void RecordDetailStarting(recordDetailCtrl __instance) { UiRuntime.Safe("story-record-detail", r => { if (r.StoryUi != null && recordListCtrl.instance != null) { r.StoryUi.Clear(recordListCtrl.instance); } }); }
        private static void RecordPageRequested(recordListCtrl __instance, ref IEnumerator __result) { __result = AfterRecordCoroutine(__result, __instance); }
        private static void RecordDetailRequested(recordDetailCtrl __instance, int __0, ref IEnumerator __result) { __result = AfterDetailCoroutine(__result); }
        private static IEnumerator AfterRecordCoroutine(IEnumerator native, recordListCtrl owner) { try { while (native.MoveNext()) { yield return native.Current; } } finally { IDisposable d = native as IDisposable; if (d != null) { d.Dispose(); } } RecordReady(owner); }
        private static IEnumerator AfterDetailCoroutine(IEnumerator native) { try { while (native.MoveNext()) { yield return native.Current; } } finally { IDisposable d = native as IDisposable; if (d != null) { d.Dispose(); } } if (recordListCtrl.instance != null) { RecordReady(recordListCtrl.instance); } }
        private static void RecordActive(recordListCtrl __instance, bool __0) { if (!__0) { UiRuntime.Safe("story-record-close", r => { if (r.StoryUi != null) { r.StoryUi.Clear(__instance); } }); } }
        private static void DialogText(messageBoxCtrl __instance, string __0, int __1) { UiRuntime.Safe("story-dialog-template", r => { if (r.StoryUi != null) { r.StoryUi.BoxText(__instance, __0, __1); } }); }
        private static void DialogTexts(messageBoxCtrl __instance, string[] __0) { UiRuntime.Safe("story-dialog-templates", r => { if (r.StoryUi != null) { r.StoryUi.BoxTexts(__instance, __0); } }); }
        private static void DialogTextCommit(messageBoxCtrl __instance) { UiRuntime.Safe("story-dialog-text", r => { if (r.StoryUi != null) { r.StoryUi.Box(__instance); } }); }
        private static void DialogTextsCommit(messageBoxCtrl __instance) { DialogTextCommit(__instance); }
        private static void DialogOpened(messageBoxCtrl __instance) { UiRuntime.Safe("story-dialog-open", r => { if (r.StoryUi != null) { r.StoryUi.BoxOpened(__instance, false); } }); }
        private static void DialogSelectOpened(messageBoxCtrl __instance) { UiRuntime.Safe("story-dialog-select-open", r => { if (r.StoryUi != null) { r.StoryUi.BoxOpened(__instance, true); } }); }
        private static void DialogClosed(messageBoxCtrl __instance) { UiRuntime.Safe("story-dialog-close", r => { if (r.StoryUi != null) { r.StoryUi.BoxClosed(__instance); } }); }
        private static void DialogActive(messageBoxCtrl __instance, bool __0) { if (!__0) { DialogClosed(__instance); } }
        private static void DialogPlateCommit(titleSelectPlate __instance) { UiRuntime.Safe("story-dialog-cursor", r => { if (r.StoryUi != null) { r.StoryUi.BoxPlate(__instance, false); } }); }
        private static void DialogPlateAlpha(titleSelectPlate __instance, float __0) { if (__0 == 1f) { UiRuntime.Safe("story-dialog-shown", r => { if (r.StoryUi != null) { r.StoryUi.BoxPlate(__instance, true); } }); } }
        private static IEnumerable<CodeInstruction> ChoiceTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            FieldInfo cursor = UiResources.Field(typeof(selectPlateCtrl), "cursor_no_");
            FieldInfo owner = UiResources.Field(__originalMethod.DeclaringType, "$this");
            MethodInfo shown = Method(typeof(selectPlateCtrl), "setAlpha", typeof(float));
            int writes = code.FindAll(c => c.opcode == OpCodes.Stfld && Equals(c.operand, cursor)).Count;
            int shows = code.FindAll(c => (c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && Equals(c.operand, shown)).Count;
            if (writes != 4 || shows != 1) { throw new InvalidOperationException("Story native choice pattern changed: " + writes + "/" + shows); }
            int seen = 0;
            foreach (CodeInstruction c in code)
            {
                yield return c;
                bool input = c.opcode == OpCodes.Stfld && Equals(c.operand, cursor) && ++seen <= 2;
                bool entry = (c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && Equals(c.operand, shown);
                // The last two cursor assignments are unconditional clamping, not input events.
                if (!input && !entry) { continue; }
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, owner);
                yield return new CodeInstruction(OpCodes.Call, typeof(StoryHooks).GetMethod("ChoiceNotify", BindingFlags.NonPublic | BindingFlags.Static));
            }
        }
    }
}
