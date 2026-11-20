using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Pwaat.Accessibility.Ui
{
    internal static class PsycheLockHooks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static MethodInfo M(Type type, string name, params Type[] args)
        {
            MethodInfo method = type.GetMethod(name, Flags, null, args, null);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            return method;
        }
        private static HarmonyMethod H(string name) { return new HarmonyMethod(typeof(PsycheLockHooks).GetMethod(name, Flags)); }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(M(typeof(GSPsylock), "psylock_move_lock_appear"), transpiler: H("AppearanceCommit"));
            harmony.Patch(M(typeof(GSPsylock), "psylock_move_lock_unlock"), transpiler: H("BreakCommits"));
            harmony.Patch(M(typeof(GSPsylock), "psylock_move_unlock_message"), transpiler: H("BannerCommits"));
            harmony.Patch(M(typeof(GSPsylock), "PsylockDisp_init", typeof(int)), prefix: H("Reset"));
            harmony.Patch(M(typeof(GSPsylock), "Set_Psylock_Mode", typeof(int), typeof(int)), prefix: H("Mode"));
            harmony.Patch(M(typeof(MessageSystem), "Reset", typeof(bool)), prefix: H("Reset"));
            harmony.Patch(M(typeof(advCtrl), "end"), prefix: H("Reset"));
        }
        private static void Safe(string source, Action<PsycheLockHandler> action)
        { UiRuntime.Safe(source, r => { if (r.PsycheLocks != null) action(r.PsycheLocks); }); }
        private static void Reset() { Safe("psyche-lock-reset", p => p.Reset()); }
        private static void Mode(int __0) { if (__0 == 11) Reset(); }
        private static void Appeared() { Safe("psyche-lock-appeared", p => p.Appeared()); }
        private static void Broken() { Safe("psyche-lock-broken", p => p.Broken()); }
        private static void Banner(AnimationObject animation) { Safe("psyche-lock-banner", p => p.Banner(animation)); }
        private static IEnumerable<CodeInstruction> AppearanceCommit(IEnumerable<CodeInstruction> input)
        {
            int count = 0;
            foreach (CodeInstruction code in input)
            {
                yield return code;
                FieldInfo field = code.operand as FieldInfo;
                if (code.opcode != OpCodes.Stfld || field == null || field.Name != "r_no_0") continue;
                count++; yield return new CodeInstruction(OpCodes.Call, M(typeof(PsycheLockHooks), "Appeared"));
            }
            if (count != 1) throw new InvalidOperationException("Psyche-lock appearance commit changed: " + count);
        }
        private static IEnumerable<CodeInstruction> BreakCommits(IEnumerable<CodeInstruction> input)
        {
            int count = 0;
            foreach (CodeInstruction code in input)
            {
                yield return code;
                FieldInfo field = code.operand as FieldInfo;
                if (code.opcode != OpCodes.Stfld || field == null || field.Name != "rest") continue;
                count++; yield return new CodeInstruction(OpCodes.Call, M(typeof(PsycheLockHooks), "Broken"));
            }
            if (count != 2) throw new InvalidOperationException("Psyche-lock break commits changed: " + count);
        }
        private static IEnumerable<CodeInstruction> BannerCommits(IEnumerable<CodeInstruction> input)
        {
            MethodInfo play = M(typeof(AnimationSystem), "PlayObject", typeof(int), typeof(int), typeof(int));
            int count = 0;
            foreach (CodeInstruction code in input)
            {
                yield return code;
                if (!code.Calls(play)) continue;
                count++; yield return new CodeInstruction(OpCodes.Dup); yield return new CodeInstruction(OpCodes.Call, M(typeof(PsycheLockHooks), "Banner", typeof(AnimationObject)));
            }
            if (count != 5) throw new InvalidOperationException("Psyche-lock unlock-banner calls changed: " + count);
        }
    }
}
