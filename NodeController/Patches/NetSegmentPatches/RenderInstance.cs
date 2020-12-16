namespace NodeController.Patches {
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using KianCommons.Patches;
    using NodeController.Util;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using static KianCommons.Patches.TranspilerUtils;
    using KianCommons;

    [HarmonyPatch]
    class RenderInstance {
        [UsedImplicitly]
        static MethodBase TargetMethod() {
            return typeof(NetSegment).GetMethod(
                    nameof(NetSegment.RenderInstance),
                    BindingFlags.NonPublic | BindingFlags.Instance) ??
                    throw new System.Exception("RenderInstance Could not find target method.");
        }

        public static bool Flip(bool turnAround, ushort segmentID, ref NetInfo.Segment segmentInfo) {
            if (turnAround)
                return turnAround; // already turned around
            var flags = segmentID.ToSegment().m_flags;
            if (!flags.CheckFlags(segmentInfo.m_backwardRequired, segmentInfo.m_backwardForbidden))
                return turnAround; // can't turn around.

            // it works both way. turn around oneway inverted segment:
            bool invert = segmentID.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
            var info = segmentID.ToSegment().Info;
            bool oneway = info.m_hasForwardVehicleLanes != info.m_hasBackwardVehicleLanes;
            return oneway && invert; 
        }

        static MethodInfo mFlip = GetMethod(typeof(RenderInstance), nameof(Flip));
        static MethodInfo mCheckFlags = GetMethod(typeof(NetInfo.Segment), nameof(NetInfo.Segment.CheckFlags));

        [HarmonyBefore(CSURUtil.HARMONY_ID)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) {
            var codes = instructions.ToCodeList();
            int index = codes.FindIndex(c => c.Calls(mCheckFlags));
            var ldaTurnAround = codes[index - 1];
            var loc = (LocalBuilder)ldaTurnAround.operand;
            Log.Debug($"loc={loc.LocalIndex}");
            index = codes.FindIndex(c => c.IsLdLoc(loc.LocalIndex));
            codes.InsertInstructions(index + 1, // insert after
                new[]{
                    // ldloc turnAround is already in the stack
                    GetLDArg(original,"segmentID"),
                    new CodeInstruction(OpCodes.Ldarga_S, 0), // load ref this
                    new CodeInstruction(OpCodes.Call, mFlip)
                });

            return codes;
        }

    }
}
