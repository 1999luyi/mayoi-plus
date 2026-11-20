using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Pwaat.Accessibility.Ui
{
    internal static class VaseShowHooks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static MethodInfo Method(Type type, string name, params Type[] args)
        { MethodInfo method = type.GetMethod(name, Flags, null, args, null); if (method == null) throw new MissingMethodException(type.FullName, name); return method; }
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(VaseShowHooks).GetMethod(name, Flags)); }
        private static readonly MethodInfo GetClip = Method(typeof(soundCtrl), "GetClipSE", typeof(int));
        // Explicitly selected by the player; native save-screen usage is not assumed.
        internal const int ReadySoundId = 64;
        internal static AudioClip Clip(int id) { return (AudioClip)GetClip.Invoke(soundCtrl.instance, new object[] { id }); }
        internal static void PrepareSounds()
        {
            if (soundCtrl.instance == null) throw new InvalidOperationException("Native vase audio unavailable.");
            foreach (int id in new[] { 132, 362, 116, 428, 51, 363 })
                if (Clip(id) == null) throw new InvalidOperationException("Native vase cue unavailable: " + id);
            if (Clip(ReadySoundId) == null) throw new InvalidOperationException("Native ready cue unavailable.");
        }
        internal static void PlayReadySound() { if (soundCtrl.instance != null) soundCtrl.instance.PlaySE(ReadySoundId); }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(Method(typeof(VaseShowMiniGame), "Init"), prefix: H("Close"), postfix: H("Started"));
            harmony.Patch(Method(typeof(VaseShowMiniGame), "InputRotateKey"), prefix: H("BeforeInput"), postfix: H("AfterInput"));
            harmony.Patch(Method(typeof(evidenceObjectManager), "AddRotate", typeof(float), typeof(float), typeof(float)), postfix: H("Rotated"));
            harmony.Patch(Method(typeof(VaseShowMiniGame), "<InitTouch>m__1", typeof(Vector3)), prefix: H("BeforeInput"), postfix: H("AfterInput"));
            harmony.Patch(Method(typeof(VaseShowMiniGame), "<InitTouch>m__2", typeof(Vector3)), postfix: H("TouchReleased"));
            Type iterator = typeof(VaseShowMiniGame).GetNestedType("<VaseShowMain>c__Iterator0", BindingFlags.NonPublic);
            if (iterator == null) throw new MissingMemberException("Native vase coroutine changed.");
            harmony.Patch(Method(iterator, "MoveNext"), transpiler: H("CameraLifecycle"));
            harmony.Patch(Method(typeof(keyGuideBoard), "CoroutineOpen", typeof(keyGuideBase.Type)), postfix: H("GuideCoroutine"));
            harmony.Patch(Method(typeof(keyGuideBoard), "ReLoadGuid"), postfix: H("GuideReady"));
            harmony.Patch(Method(typeof(VaseShowMiniGame), "end"), prefix: H("Close"));
            harmony.Patch(Method(typeof(MessageSystem), "Reset", typeof(bool)), prefix: H("Close"));
            harmony.Patch(Method(typeof(advCtrl), "end"), prefix: H("Close"));
        }
        private static void Safe(string source, Action<VaseShowHandler> action)
        { UiRuntime.Safe(source, runtime => { if (runtime.VaseShow != null) action(runtime.VaseShow); }); }
        private static void Started(VaseShowMiniGame __instance) { Safe("vase-show-start", p => p.Begin(__instance)); }
        private static void BeforeInput(VaseShowMiniGame __instance) { Safe("vase-show-input", p => p.BeginInput(__instance)); }
        private static void AfterInput(VaseShowMiniGame __instance) { Safe("vase-show-input-end", p => p.EndInput(__instance)); }
        private static void Rotated(evidenceObjectManager __instance, float __0, float __1, float __2)
        { Safe("vase-show-rotation", p => p.RotationCommitted(__instance, __0, __1, __2)); }
        private static void TouchReleased(VaseShowMiniGame __instance) { Safe("vase-show-touch-up", p => p.TouchReleased(__instance)); }
        private static void CameraSetActive(GameObject camera, bool active)
        {
            camera.SetActive(active);
            Safe("vase-show-camera", p => p.CameraCommitted(camera, active));
        }
        private static IEnumerable<CodeInstruction> CameraLifecycle(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            FieldInfo camera = UiResources.Field(typeof(VaseShowMiniGame), "vase_camera_");
            MethodInfo setActive = Method(typeof(GameObject), "SetActive", typeof(bool));
            MethodInfo replacement = Method(typeof(VaseShowHooks), "CameraSetActive", typeof(GameObject), typeof(bool));
            int count = 0;
            for (int i = 2; i < list.Count; i++)
            {
                if (!list[i].Calls(setActive) || list[i - 2].opcode != OpCodes.Ldfld || !camera.Equals(list[i - 2].operand)) continue;
                if (list[i - 1].opcode != OpCodes.Ldc_I4_0 && list[i - 1].opcode != OpCodes.Ldc_I4_1) throw new InvalidOperationException("Native vase camera transition changed.");
                list[i].opcode = OpCodes.Call; list[i].operand = replacement; count++;
            }
            if (count != 2) throw new InvalidOperationException("Expected two native vase camera commits, got " + count);
            return list;
        }
        private static void GuideReady(keyGuideBase __instance) { Safe("vase-show-guide", p => p.GuideReady(__instance)); }
        private static void GuideCoroutine(keyGuideBase __instance, keyGuideBase.Type __0, ref IEnumerator __result)
        { if (__0 == keyGuideBase.Type.VASE_SHOW && __result != null) __result = AfterGuide(__result, __instance); }
        private static IEnumerator AfterGuide(IEnumerator native, keyGuideBase owner)
        {
            try { while (native.MoveNext()) yield return native.Current; }
            finally { IDisposable disposable = native as IDisposable; if (disposable != null) disposable.Dispose(); }
            GuideReady(owner);
        }
        private static void Close() { Safe("vase-show-close", p => p.Close()); }
    }
}
