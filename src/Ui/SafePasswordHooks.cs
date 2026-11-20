using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Pwaat.Accessibility.Ui
{
    internal static class SafePasswordHooks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly MethodInfo NativeKey = Method(typeof(padCtrl), "GetKeyDown", typeof(KeyType), typeof(int), typeof(bool), typeof(KeyType));
        private static MethodInfo Method(Type type, string name, params Type[] args)
        { MethodInfo method = type.GetMethod(name, Flags, null, args, null); if (method == null) throw new MissingMethodException(type.FullName, name); return method; }
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(SafePasswordHooks).GetMethod(name, Flags)); }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(Method(typeof(KinkoMiniGame), "Process"), prefix: H("ProcessAllowed"));
            harmony.Patch(Method(typeof(KinkoMiniGame), "Init"), postfix: H("Started"));
            harmony.Patch(Method(typeof(KinkoMiniGame), "StartAnimUpdate"), transpiler: H("ReadyCommit"));
            harmony.Patch(Method(typeof(KinkoMiniGame), "CursorUpDate"), transpiler: H("InputCommit"));
            harmony.Patch(Method(typeof(KinkoMiniGame), "SetNumber", typeof(int)), prefix: H("NumberBefore"), postfix: H("NumberAfter"));
            harmony.Patch(Method(typeof(KinkoMiniGame), "EndFadeInUpdate"), transpiler: H("EndCommit"));
            harmony.Patch(Method(typeof(MessageSystem), "Reset", typeof(bool)), prefix: H("Closed"));
            harmony.Patch(Method(typeof(advCtrl), "end"), prefix: H("Closed"));
        }
        private static void Safe(string source, Action<SafePasswordHandler> action)
        { UiRuntime.Safe(source, r => { if (r.SafePassword != null) action(r.SafePassword); }); }
        // This keypad advances in its own FixedUpdate, independently of the paused adventure controller.
        private static bool ProcessAllowed()
        { UiRuntime runtime = UiRuntime.Current; return runtime == null || runtime.Assistance == null || !runtime.Assistance.BlocksInput; }
        private static void Started(KinkoMiniGame __instance) { Safe("safe-password-start", p => p.Start(__instance)); }
        private static void Closed() { Safe("safe-password-close", p => p.Close()); }
        private static void Ready(KinkoMiniGame owner) { Safe("safe-password-ready", p => p.Ready(owner)); }
        private static void Ended(KinkoMiniGame owner) { Safe("safe-password-end", p => p.Ended(owner)); }
        private static void NumberBefore(KinkoMiniGame __instance, out int __state)
        { int previous = -1; Safe("safe-password-number-before", p => previous = p.BeforeNumber(__instance)); __state = previous; }
        private static void NumberAfter(KinkoMiniGame __instance, int __0, int __state)
        { Safe("safe-password-number-after", p => p.NumberCommitted(__instance, __0, __state)); }
        private static IEnumerable<CodeInstruction> AfterStore(IEnumerable<CodeInstruction> source, string fieldName, string callback)
        {
            List<CodeInstruction> output = new List<CodeInstruction>(); int sites = 0;
            FieldInfo field = UiResources.Field(typeof(KinkoMiniGame), fieldName);
            foreach (CodeInstruction code in source)
            {
                output.Add(code);
                if (code.opcode != OpCodes.Stfld || !field.Equals(code.operand)) continue;
                output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                output.Add(new CodeInstruction(OpCodes.Call, Method(typeof(SafePasswordHooks), callback, typeof(KinkoMiniGame)))); sites++;
            }
            if (sites != 1) throw new InvalidOperationException("Native safe password lifecycle commit changed: " + fieldName);
            return output;
        }
        private static IEnumerable<CodeInstruction> ReadyCommit(IEnumerable<CodeInstruction> source) { return AfterStore(source, "is_answer_", "Ready"); }
        private static IEnumerable<CodeInstruction> EndCommit(IEnumerable<CodeInstruction> source) { return AfterStore(source, "is_end_", "Ended"); }
        private static IEnumerable<CodeInstruction> InputCommit(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> output = new List<CodeInstruction>(); int sites = 0;
            foreach (CodeInstruction code in source)
            {
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) && NativeKey.Equals(code.operand))
                { code.opcode = OpCodes.Call; code.operand = Method(typeof(SafePasswordHooks), "ReadKey", typeof(padCtrl), typeof(KeyType), typeof(int), typeof(bool), typeof(KeyType)); sites++; }
                output.Add(code);
            }
            if (sites != 9) throw new InvalidOperationException("Native safe password input consumers changed.");
            return output;
        }
        private static bool ReadKey(padCtrl pad, KeyType type, int ext, bool debug, KeyType controller)
        {
            bool result = pad.GetKeyDown(type, ext, debug, controller);
            if (result) Safe("safe-password-input", p => p.InputObserved());
            return result;
        }
    }
}
