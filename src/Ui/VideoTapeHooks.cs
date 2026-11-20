using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Pwaat.Accessibility.Ui
{
    internal static class VideoTapeHooks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static MethodInfo Method(Type type, string name, params Type[] args)
        { MethodInfo method = type.GetMethod(name, Flags, null, args, null); if (method == null) throw new MissingMethodException(type.FullName, name); return method; }
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(VideoTapeHooks).GetMethod(name, Flags)); }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(Method(typeof(MovieController), "Play", typeof(UnityEngine.AssetBundle)), prefix: H("Start"));
            harmony.Patch(Method(typeof(MovieController), "set_Frame", typeof(int)), prefix: H("BeforeImage"), postfix: H("Image"));
            harmony.Patch(Method(typeof(MovieController), "SetAutoPlayStatus", typeof(int)), postfix: H("Mode"));
            harmony.Patch(Method(typeof(MovieController), "SetAutoPlaySpeed", typeof(int)), postfix: H("Mode"));
            harmony.Patch(Method(typeof(MovieController), "Stop"), prefix: H("Stop"));
            harmony.Patch(Method(typeof(MovieController), "Clear"), prefix: H("Stop"));
            harmony.Patch(Method(typeof(MovieCollisionPlayer), "CollisionUpdate"), prefix: H("BeforeCollision"), postfix: H("Collision"));
            harmony.Patch(Method(typeof(FingerCursor), "UpdateCursor"), postfix: H("Cursor"));
            harmony.Patch(Method(typeof(ConfrontWithMovie), "sw_movie_thrust_main", typeof(SubWindow)), transpiler: H("InputCommit"));
            foreach (string phase in new[] { "sw_movie_thrust_b_button", "sw_movie_thrust_y_button", "sw_movie_thrust_a_button" })
                harmony.Patch(Method(typeof(ConfrontWithMovie), phase, typeof(SubWindow)), prefix: H("BeforePhase"), postfix: H("Phase"));
            harmony.Patch(Method(typeof(keyGuideBoard), "CoroutineOpen", typeof(keyGuideBase.Type)), postfix: H("GuideCoroutine"));
            harmony.Patch(Method(typeof(keyGuideBoard), "ReLoadGuid"), postfix: H("Guide"));
            harmony.Patch(Method(typeof(MessageSystem), "Reset", typeof(bool)), prefix: H("Close"));
            harmony.Patch(Method(typeof(advCtrl), "end"), prefix: H("Close"));
        }
        private static void Safe(string source, Action<VideoTapeHandler> action)
        { UiRuntime.Safe(source, r => { if (r.VideoTape != null) action(r.VideoTape); }); }
        private static void BeforePhase(SubWindow pwork, out int __state)
        { Routine current = pwork.GetCurrentRoutine(); __state = current == null ? -1 : current.r.no_1; }
        private static void Phase(SubWindow pwork, int __state)
        { Safe("video-phase", p => p.PhaseCommitted(pwork, __state)); }
        private static void Start(MovieController __instance) { Safe("video-start", p => p.Begin(__instance)); }
        private static void BeforeImage(MovieController __instance, out int __state) { __state = __instance.Frame; }
        private static void Image(MovieController __instance, int __state)
        { if (__instance.Frame != __state) Safe("video-image", p => p.ImageCommitted(__instance)); }
        private static void BeforeCollision(MovieCollisionPlayer __instance, out int __state) { __state = __instance.previousFrame; }
        private static void Collision(MovieCollisionPlayer __instance, int __state)
        { if (__instance.previousFrame != __state) Safe("video-regions", p => p.FrameCommitted(__instance)); }
        private static void Mode(MovieController __instance) { Safe("video-mode", p => p.ModeCommitted(__instance)); }
        private static void Stop(MovieController __instance) { Safe("video-stop", p => p.Stop(__instance)); }
        private static void Cursor(FingerCursor __instance) { Safe("video-cursor", p => p.CursorCommitted(__instance)); }
        private static void Close() { Safe("video-close", p => p.Close()); }
        private static void Guide(keyGuideBoard __instance) { Safe("video-guide", p => p.GuideReady(__instance)); }
        private static void GuideCoroutine(keyGuideBoard __instance, ref IEnumerator __result)
        {
            VideoTapeHandler handler = UiRuntime.Current == null ? null : UiRuntime.Current.VideoTape;
            if (handler != null) __result = CompleteGuide(__instance, __result, handler.Generation);
        }
        private static IEnumerator CompleteGuide(keyGuideBoard guide, IEnumerator inner, int generation)
        {
            try { while (inner.MoveNext()) yield return inner.Current; }
            finally { IDisposable disposable = inner as IDisposable; if (disposable != null) disposable.Dispose(); }
            Safe("video-guide-ready", p => { if (p.Owns(generation)) p.GuideReady(guide); });
        }
        private static bool ReadNativeKey(padCtrl pad, KeyType type, int ext, bool debug, KeyType controller)
        {
            bool result = pad.GetKeyDown(type, ext, debug, controller);
            Safe("video-input-ready", p => p.InputReady(ConfrontWithMovie.instance, type, result));
            return result;
        }
        private static IEnumerable<CodeInstruction> InputCommit(IEnumerable<CodeInstruction> source)
        {
            MethodInfo native = Method(typeof(padCtrl), "GetKeyDown", typeof(KeyType), typeof(int), typeof(bool), typeof(KeyType));
            MethodInfo replacement = Method(typeof(VideoTapeHooks), "ReadNativeKey", typeof(padCtrl), typeof(KeyType), typeof(int), typeof(bool), typeof(KeyType));
            int count = 0; List<CodeInstruction> output = new List<CodeInstruction>();
            foreach (CodeInstruction code in source)
            { if (code.Calls(native)) { code.opcode = OpCodes.Call; code.operand = replacement; count++; } output.Add(code); }
            if (count != 3) throw new InvalidOperationException("Native video input consumers changed.");
            return output;
        }
    }
}
