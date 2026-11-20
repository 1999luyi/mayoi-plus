using System;
using System.Reflection;
using HarmonyLib;

namespace Pwaat.Accessibility.Ui
{
    internal static class TanchikiHooks
    {
        private static MethodInfo M(Type type, string name, params Type[] args)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, args, null);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            return method;
        }
        private static HarmonyMethod H(string name)
        { return new HarmonyMethod(typeof(TanchikiHooks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)); }
        [ThreadStatic] private static bool _movementSound;
        private static soundData _movementData;
        internal static void PrepareSounds()
        {
            // WORKAROUND: SE451 belongs to GS1. Numeric lookup is title-dependent and
            // dereferences optional title BGM slots. Recheck on soundDataCtrl changes.
            _movementData = Array.Find(soundDataCtrl.sound_data_se_gs1, data => data != null && data.no == 451);
            if (_movementData == null || _movementData.name != "se0e3" || _movementData.path != "/Sound/se/wsd/")
                throw new InvalidOperationException("Detector movement cue record unavailable: SE451.");
        }
        internal static void PlayMovement()
        {
            if (soundCtrl.instance == null) return;
            bool previous = _movementSound;
            _movementSound = true;
            try { soundCtrl.instance.PlaySE(451, false); }
            finally { _movementSound = previous; }
        }
        private static bool MovementSoundData(int __0, ref soundData __result)
        {
            // Scope both native PlaySE lookups to this synchronous mod sound only.
            // Do not modify global sound tables, the selected game, or other sounds.
            if (!_movementSound || __0 != 451) return true;
            __result = _movementData;
            return false;
        }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(M(typeof(soundDataCtrl), "get_sound_data", typeof(int)), prefix: H("MovementSoundData"));
            harmony.Patch(M(typeof(TanchikiMiniGame), "init"), postfix: H("Started"));
            harmony.Patch(M(typeof(SubWindow_Tanchiki), "Main", typeof(SubWindow)), prefix: H("BeforeMain"), postfix: H("MainCommitted"));
            harmony.Patch(M(typeof(TanchikiMiniGame), "hit_check"), postfix: H("HitComputed"));
            foreach (string phase in new[] { "Appear", "Scroll", "Back" })
                harmony.Patch(M(typeof(SubWindow_Tanchiki), phase, typeof(SubWindow)), postfix: H("StateCommitted"));
            harmony.Patch(M(typeof(TanchikiMiniGame), "end"), prefix: H("Close"));
            harmony.Patch(M(typeof(MessageSystem), "Reset", typeof(bool)), prefix: H("Close"));
            harmony.Patch(M(typeof(advCtrl), "end"), prefix: H("Close"));
        }
        private static void Safe(Action<TanchikiHandler> action)
        {
            UiRuntime runtime = UiRuntime.Current;
            if (runtime == null || runtime.Tanchiki == null) return;
            try { action(runtime.Tanchiki); } catch (Exception error) { runtime.Tanchiki.Fail(error); }
        }
        private static void Started(TanchikiMiniGame __instance) { Safe(p => p.Begin(__instance)); }
        private static void BeforeMain(SubWindow __0) { Safe(p => p.BeforeMain(__0)); }
        private static void MainCommitted(SubWindow __0) { Safe(p => p.MainCommitted(__0)); }
        private static void HitComputed(TanchikiMiniGame __instance, int __result) { Safe(p => p.HitComputed(__instance, __result)); }
        private static void StateCommitted(SubWindow __0) { Safe(p => p.StateCommitted(__0)); }
        private static void Close() { Safe(p => p.Close()); }
    }
}
