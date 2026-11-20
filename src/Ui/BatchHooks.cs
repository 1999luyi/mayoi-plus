using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal static class BatchHooks
    {
        private const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static HarmonyMethod Hook(string name) { return new HarmonyMethod(typeof(BatchHooks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)); }
        private static void After(Harmony h, Type t, string name, Predicate<MethodInfo> filter = null)
        {
            int installed = 0;
            foreach (MethodInfo method in t.GetMethods(Methods))
            {
                if (method.Name != name || (filter != null && !filter(method))) { continue; }
                h.Patch(method, postfix: Hook(method.ReturnType == typeof(IEnumerator) ? "CoroutineCommitted" : "Committed")); installed++;
            }
            if (installed == 0) { throw new MissingMethodException(t.FullName, name); }
        }
        private static bool IntFirst(MethodInfo m) { return m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(int); }
        internal static void InstallChoices(Harmony h)
        {
            After(h, typeof(seriesTitleSelectCtrl), "changeTitle");
            After(h, typeof(seriesTitleSelectCtrl), "CoroutineSlideMove");
            After(h, typeof(scenarioSelectCtrl), "titlePosSet");
            After(h, typeof(scenarioSelectCtrl), "CoroutineSlideMove");
            After(h, typeof(ChapterJumpInMenuCtrl), "ForceEnd");
            After(h, typeof(ChapterJumpInMenuCtrl), "EpisodeDispRefresh");
            After(h, typeof(GeneralSelectPlateCtrl), "SetCursorNo");
            After(h, typeof(GeneralSelectPlateCtrl), "TouchItem");
            After(h, typeof(GeneralSelectPlateCtrl), "stopCursor");
            After(h, typeof(GeneralSelectPlateCtrl), "end");
            MethodInfo next = AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(GeneralSelectPlateCtrl), "CoroutineCursor"));
            h.Patch(next, transpiler: Hook("GeneralCursorTranspiler"));
        }
        internal static void InstallOptions(Harmony h)
        {
            After(h, typeof(optionCtrl), "SelectItemSet");
            After(h, typeof(optionCtrl), "SetText");
            After(h, typeof(optionCtrl), "SetTextCat");
            After(h, typeof(optionCtrl), "LoadKeyIcon_KeyPad");
            After(h, typeof(optionCtrl), "LoadKeyIcon_KeyBoard");
            After(h, typeof(KeyConfigButtonSprite), "SetInputFase");
            After(h, typeof(optionCtrl), "Close");
            After(h, typeof(creditCtrl), "End");
            After(h, typeof(DialogReleaseNoteCtrl), "Awake");
            After(h, typeof(DialogTermsOfServiceCtrl), "playCursor");
            After(h, typeof(DialogTermsOfServiceCtrl), "TouchSelect");
            After(h, typeof(DialogTermsOfServiceCtrl), "stopCursor");
            h.Patch(AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(DialogTermsOfServiceCtrl), "CoroutineCursor")), transpiler: Hook("TermsCursorTranspiler"));
            After(h, typeof(optionCategoryItem), "setSelect");
            foreach (Type type in typeof(optionItem).Assembly.GetTypes())
            {
                if (!typeof(optionItem).IsAssignableFrom(type)) { continue; }
                foreach (MethodInfo method in type.GetMethods(Methods))
                {
                    if (method.IsAbstract || method.ReturnType == typeof(IEnumerator)) { continue; }
                    if (method.Name == "ChangeValue" || method.Name == "OnTouch" || method.Name == "EnterValue" || method.Name == "ChangeKeyConfig" || method.Name == "ValueSet" || method.Name == "SelectTextSet" || method.Name == "PlayDecide" || method.Name == "SetSprite" || method.Name == "SelectEntry") { h.Patch(method, postfix: Hook("Committed")); }
                }
            }
        }
        internal static void InstallSave(Harmony h)
        {
            After(h, typeof(SaveLoadUICtrl), "Open");
            After(h, typeof(SaveLoadUICtrl), "Close");
            After(h, typeof(SaveLoadUICtrl), "UpdateCursorPosition");
            After(h, typeof(SaveLoadUICtrl), "SlotSet");
            After(h, typeof(SaveSlot), "SlotDataSet");
            After(h, typeof(SaveConfirmationWindow), "End");
            After(h, typeof(SaveConfirmationWindow), "CloseWindow");
            After(h, typeof(SaveConfirmationWindow), "LoadingPlay");
            After(h, typeof(messageBoxCtrl), "OpenWindow");
            After(h, typeof(messageBoxCtrl), "OpenWindowSelect");
            After(h, typeof(messageBoxCtrl), "CloseWindow");
            After(h, typeof(messageBoxCtrl), "CloseWindowSelect");
            After(h, typeof(ConfirmDialog), "Show");
            After(h, typeof(ConfirmDialog), "UpdateCursor");
            After(h, typeof(ConfirmDialog), "Hide");
        }
        internal static void InstallGallery(Harmony h)
        {
            After(h, typeof(GalleryCtrl), "SelectItem", IntFirst);
            After(h, typeof(GalleryOrchestraCtrl), "set_IsInit");
            After(h, typeof(GalleryOrchestraCtrl), "SelectSong", IntFirst);
            After(h, typeof(GalleryOrchestraCtrl), "UpdatePage");
            After(h, typeof(GalleryOrchestraCtrl), "UpdatePlayMode", m => m.GetParameters()[0].ParameterType.Name == "PlayMode");
            After(h, typeof(GalleryOrchestraCtrl), "SetInformationSongText");
            After(h, typeof(GalleryOrchestraCtrl), "ToggleSettingBgmModeIfNeeded");
            After(h, typeof(GalleryArtLibraryKindSelectorCtrl), "SelectItem");
            After(h, typeof(GalleryArtLibraryImageSelectorCtrl), "SelectItem");
            After(h, typeof(GalleryArtLibraryImageSelectorCtrl), "LoadPage");
            After(h, typeof(GalleryArtLibraryViewerCtrl), "NotifyNowTag");
            After(h, typeof(GalleryArtLibraryExpansionViewerCtrl), "SetImage");
            After(h, typeof(GalleryArtLibraryContentsCtrl), "set_IsShow");
            After(h, typeof(GalleryArtLibraryTitleSelector), "setTagText");
            After(h, typeof(GalleryArtLibraryTitleSelector), "Show");
            After(h, typeof(GalleryArtLibraryTitleSelector), "PlayEffectAsync");
            After(h, typeof(GalleryArtLibraryTitleSelector), "Hide");
            After(h, typeof(GalleryArtLibraryTitleSelector), "Stop");
            MethodInfo notify = typeof(EasySelector).GetMethod("NotifyCallback", Methods, null, new[] { typeof(EasySelector.ObservableKind), typeof(int) }, null);
            if (notify == null || notify.IsAbstract || notify.GetMethodBody() == null) { throw new MissingMethodException(typeof(EasySelector).FullName, "NotifyCallback(ObservableKind,int)"); }
            h.Patch(notify, postfix: Hook("SelectorNotified"));
            After(h, typeof(GalleryActionStudioCtrl), "SelectItem");
            After(h, typeof(GalleryActionStudioCtrl), "UpdateCharacterTable");
            After(h, typeof(GalleryActionStudioCtrl), "UpdateActionTable");
            After(h, typeof(GalleryActionStudioCtrl), "UpdateBackgroundTable");
            After(h, typeof(GalleryActionStudioCtrl), "PlayChangeGuideAsync");
            After(h, typeof(GalleryActionStudioListWindowCtrl), "Show");
            After(h, typeof(GalleryActionStudioListWindowCtrl), "OnSelectItem");
            After(h, typeof(GalleryActionStudioListWindowCtrl), "Close");
            Type reference = typeof(GalleryActionStudioCtrl).GetNestedType("SelectReference", BindingFlags.NonPublic);
            h.Patch(reference.GetProperty("CurrentSelectIndex").GetSetMethod(true), postfix: Hook("StudioReferenceCommitted"));
            After(h, typeof(VoiceEasySelectorListItem), "UpdateItemAsync");
            MethodInfo text = typeof(TextEasySelectorListItem).GetMethod("UpdateItemAsync", Methods);
            h.Patch(text, postfix: Hook("TextSelectorCoroutine"));
        }
        internal static void InstallGuides(Harmony h)
        {
            After(h, typeof(keyGuideBase), "ActiveTouch");
            foreach (Type type in typeof(keyGuideBase).Assembly.GetTypes())
            {
                if (!typeof(keyGuideBase).IsAssignableFrom(type)) { continue; }
                foreach (MethodInfo m in type.GetMethods(Methods)) { if ((m.Name == "guideIconSet" || m.Name == "ReLoadGuid") && !m.IsAbstract && m.ReturnType == typeof(void)) { h.Patch(m, postfix: Hook("Committed")); } }
            }
        }
        internal static void InstallLifetime(Harmony h)
        {
            foreach (Type type in typeof(sceneCtrl).Assembly.GetTypes())
            {
                if (!typeof(sceneCtrl).IsAssignableFrom(type)) { continue; }
                foreach (MethodInfo method in type.GetMethods(Methods))
                {
                    if ((method.Name == "End" || method.Name == "PreTransition" || method.Name == "OnForceClose") && !method.IsAbstract && method.GetParameters().Length == 0) { h.Patch(method, postfix: Hook("Committed")); }
                }
            }
        }
        private static void SelectorNotified(EasySelector __instance, EasySelector.ObservableKind __0)
        {
            if (__0 != EasySelector.ObservableKind.OnNext_UpdateItem && __0 != EasySelector.ObservableKind.OnNext_UpdatePage && __0 != EasySelector.ObservableKind.OnCompleted_Show) { return; }
            GalleryActionStudioSelectorTitleSelector selector = __instance as GalleryActionStudioSelectorTitleSelector;
            if (selector == null) { return; }
            GalleryActionStudioSelectorCtrl owner = selector.GetComponentInParent<GalleryActionStudioSelectorCtrl>();
            UiRuntime.Safe("studio-title-selector", r => { if (r.Batch != null && owner != null) { r.Batch.Gallery(owner, "selection"); } });
        }
        private static void StudioReferenceCommitted(object __instance)
        {
            UiRuntime.Safe("studio-reference", r => { if (r.Batch != null) { r.Batch.StudioReference(__instance); } });
        }
        private static void CoroutineCommitted(Component __instance, MethodBase __originalMethod, object[] __args, ref IEnumerator __result) { __result = AfterNative(__result, __instance, __originalMethod, __args); }
        private static IEnumerator AfterNative(IEnumerator native, Component owner, MethodBase method, object[] args)
        {
            try { while (native.MoveNext()) { yield return native.Current; } }
            finally { IDisposable d = native as IDisposable; if (d != null) { d.Dispose(); } }
            Committed(owner, method, args);
        }
        private static void TextSelectorCoroutine(TextEasySelectorListItem __instance, ref IEnumerator __result) { __result = AfterText(__result, __instance); }
        private static IEnumerator AfterText(IEnumerator native, TextEasySelectorListItem owner)
        {
            try { while (native.MoveNext()) { yield return native.Current; } }
            finally { IDisposable d = native as IDisposable; if (d != null) { d.Dispose(); } }
            UiRuntime.Safe("text-selector", r => { if (r.Batch != null) { r.Batch.TextSelector(owner); } });
        }
        private static void Committed(object __instance, MethodBase __originalMethod, object[] __args)
        {
            UiRuntime.Safe("batch-commit/" + __originalMethod.Name, r =>
            {
                MenuBatch batch = r.Batch;
                Component owner = __instance as Component;
                if (batch == null || owner == null) { return; }
                string name = __originalMethod.Name;
                if (name == "PreTransition" || name == "OnForceClose" || name == "Stop" || name == "ForceEnd" || name == "End" || name == "Close" || name == "Hide" || name == "CloseWindow" || name == "CloseWindowSelect" || name == "stopCursor" || name == "end") { batch.ClearTree(owner); batch.Closed(owner); return; }
                if (owner is DialogReleaseNoteCtrl || owner is DialogTermsOfServiceCtrl) { batch.StartupDialog(owner); return; }
                if (owner is keyGuideBase) { if (name == "ActiveTouch") { batch.GuideReady((keyGuideBase)owner); } else { batch.Guide((keyGuideBase)owner); } return; }
                if (owner is KeyConfigButtonSprite) { batch.KeyCapture((KeyConfigButtonSprite)owner, (bool)__args[0]); return; }
                if (owner is GalleryArtLibraryTitleSelector) { batch.ArtTag((GalleryArtLibraryTitleSelector)owner); return; }
                if (owner is VoiceEasySelectorListItem) { batch.Voice((VoiceEasySelectorListItem)owner); return; }
                if (owner is SaveConfirmationWindow) { batch.SaveIo((SaveConfirmationWindow)owner); return; }
                if (owner is SaveSlot) { if (SaveLoadUICtrl.instance != null) { batch.Save(SaveLoadUICtrl.instance, false); } return; }
                if (owner is seriesTitleSelectCtrl) { batch.Series((seriesTitleSelectCtrl)owner); return; }
                if (owner is scenarioSelectCtrl) { batch.Scenario((scenarioSelectCtrl)owner); return; }
                if (owner is ChapterJumpInMenuCtrl) { batch.Chapter((ChapterJumpInMenuCtrl)owner); return; }
                if (owner is GeneralSelectPlateCtrl) { batch.General((GeneralSelectPlateCtrl)owner, false); return; }
                if (owner is optionCategoryItem) { if (__args.Length > 0 && (bool)__args[0]) { batch.Category((optionCategoryItem)owner); } else { batch.Clear(owner); } return; }
                if (owner is optionCtrl) { if (name == "SelectItemSet") { batch.Options((optionCtrl)owner); } else { batch.OptionDescriptionCommitted((optionCtrl)owner); } return; }
                if (owner is optionItem) { batch.OptionValueChanged((optionItem)owner); return; }
                if (owner is SaveLoadUICtrl) { batch.Save((SaveLoadUICtrl)owner, name == "Open"); return; }
                if (owner is messageBoxCtrl) { if (name == "OpenWindow") { batch.Message((messageBoxCtrl)owner); } return; }
                if (owner is ConfirmDialog) { batch.Confirm((ConfirmDialog)owner, name == "Show"); return; }
                if (owner is GalleryActionStudioListWindowCtrl)
                {
                    if (name == "Show") { batch.StudioListOpened((GalleryActionStudioListWindowCtrl)owner, (GalleryActionStudioListItemContent[])__args[1]); }
                    batch.StudioList((GalleryActionStudioListWindowCtrl)owner); return;
                }
                if ((name == "set_IsShow" || name == "set_IsInit") && !((bool)__args[0])) { batch.Clear(owner); return; }
                batch.Gallery(owner, name);
            });
        }
        private static void TermsCommit(DialogTermsOfServiceCtrl owner) { UiRuntime.Safe("terms-native-commit", r => { if (r.Batch != null) { r.Batch.StartupDialog(owner); } }); }
        private static IEnumerable<CodeInstruction> TermsCursorTranspiler(IEnumerable<CodeInstruction> input, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(input);
            FieldInfo owner = UiResources.Field(__originalMethod.DeclaringType, "$this");
            FieldInfo cursor = UiResources.Field(typeof(DialogTermsOfServiceCtrl), "cursor_no_");
            int writes = code.FindAll(c => c.opcode == OpCodes.Stfld && Equals(c.operand, cursor)).Count;
            if (writes != 4) { throw new InvalidOperationException("Terms native cursor pattern changed: " + writes); }
            int seen = 0;
            foreach (CodeInstruction c in code)
            {
                yield return c;
                if (c.opcode != OpCodes.Stfld || !Equals(c.operand, cursor) || ++seen > 2) { continue; }
                // Only the two native input branches request a read; clamp assignments run without input.
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, owner);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BatchHooks), "TermsCommit"));
            }
        }
        private static void GeneralCommit(GeneralSelectPlateCtrl owner) { UiRuntime.Safe("general-native-commit", r => { if (r.Batch != null) { r.Batch.General(owner, false); } }); }
        private static void GeneralShown(GeneralSelectPlateCtrl owner) { UiRuntime.Safe("general-native-shown", r => { if (r.Batch != null) { r.Batch.General(owner, true); } }); }
        private static IEnumerable<CodeInstruction> GeneralCursorTranspiler(IEnumerable<CodeInstruction> input, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(input);
            FieldInfo owner = UiResources.Field(__originalMethod.DeclaringType, "$this");
            FieldInfo cursor = UiResources.Field(typeof(GeneralSelectPlateCtrl), "cursor_no_");
            MethodInfo alpha = AccessTools.Method(typeof(GeneralSelectPlateCtrl), "setAlpha");
            int commits = code.FindAll(c => c.opcode == OpCodes.Stfld && Equals(c.operand, cursor)).Count;
            int shows = code.FindAll(c => (c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && Equals(c.operand, alpha)).Count;
            if (commits != 4 || shows != 1) { throw new InvalidOperationException("General native selection commit pattern changed: " + commits + "/" + shows); }
            foreach (CodeInstruction c in code)
            {
                yield return c;
                MethodInfo callback = null;
                if (c.opcode == OpCodes.Stfld && Equals(c.operand, cursor)) { callback = AccessTools.Method(typeof(BatchHooks), "GeneralCommit"); }
                if ((c.opcode == OpCodes.Call || c.opcode == OpCodes.Callvirt) && Equals(c.operand, alpha)) { callback = AccessTools.Method(typeof(BatchHooks), "GeneralShown"); }
                if (callback == null) { continue; }
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, owner);
                yield return new CodeInstruction(OpCodes.Call, callback);
            }
        }
    }
}
