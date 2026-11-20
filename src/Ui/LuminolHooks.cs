using System;
using System.Reflection;
using HarmonyLib;

namespace Pwaat.Accessibility.Ui
{
    internal static class LuminolHooks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static MethodInfo Method(Type type, string name, params Type[] args)
        { MethodInfo m = type.GetMethod(name, Flags, null, args, null); if (m == null) throw new MissingMethodException(type.FullName, name); return m; }
        private static readonly MethodInfo HitMethod = Method(typeof(luminolMiniGame), "CheckHit");
        private static readonly MethodInfo CursorMethod = Method(typeof(luminolMiniGame), "UpdateCursorPosition");
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(LuminolHooks).GetMethod(name, Flags)); }
        internal static void Install(Harmony h)
        {
            h.Patch(Method(typeof(luminolMiniGame), "Init"), prefix: H("Close"), postfix: H("Ready"));
            h.Patch(Method(typeof(luminolMiniGame), "UpdateCursorPosition"), postfix: H("Committed"));
            foreach (string method in new[] { "SetExplCkData", "AcquiredCoroutine" }) h.Patch(Method(typeof(luminolMiniGame), method), postfix: H("Changed"));
            h.Patch(Method(typeof(luminolBloodstain), "AppearBlood"), prefix: H("BloodSpraying"), postfix: H("BloodChanged"));
            h.Patch(Method(typeof(luminolMiniGame), "BloodAnimEnd"), postfix: H("Revealed"));
            h.Patch(Method(typeof(luminolMiniGame), "AcquiredAnimCoroutine"), postfix: H("Viewing"));
            h.Patch(Method(typeof(luminolMiniGame), "EndMiniGame"), prefix: H("Close"));
            h.Patch(Method(typeof(MessageSystem), "Reset", typeof(bool)), prefix: H("Close"));
            h.Patch(Method(typeof(advCtrl), "end"), prefix: H("Close"));
        }
        private static void Safe(string source, Action<LuminolHandler> action)
        { UiRuntime.Safe(source, r => { if (r.Luminol != null) action(r.Luminol); }); }
        private static void Ready(luminolMiniGame __instance) { Safe("luminol-ready", p => p.Ready(__instance)); }
        private static void Committed(luminolMiniGame __instance) { Safe("luminol-commit", p => p.Committed(__instance)); }
        private static void Changed(luminolMiniGame __instance) { Safe("luminol-state", p => p.Changed(__instance)); }
        private static void BloodSpraying(luminolBloodstain __instance, out bool __state) { __state = __instance.state_ == BloodstainState.Undiscovered && __instance.discovery_count_ > 0; }
        private static void BloodChanged(luminolBloodstain __instance, bool __state) { Safe("luminol-blood", p => p.BloodChanged(__instance, __state)); }
        private static void Revealed(luminolMiniGame __instance) { Safe("luminol-revealed", p => p.Revealed(__instance)); }
        private static void Viewing(luminolMiniGame __instance) { Safe("luminol-viewing", p => p.Viewing(__instance)); }
        private static void Close() { Safe("luminol-close", p => p.Close()); }
        internal static int Hit(luminolMiniGame owner) { return (int)HitMethod.Invoke(owner, null); }
        internal static void UpdateCursor(luminolMiniGame owner) { CursorMethod.Invoke(owner, null); }
    }
}
