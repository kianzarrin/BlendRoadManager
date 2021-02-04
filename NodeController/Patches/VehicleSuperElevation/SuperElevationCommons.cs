namespace NodeController.Patches.VehicleSuperElevation {
    using System.Reflection;
    using UnityEngine;
    using KianCommons;
    using ColossalFramework;
    using static KianCommons.Patches.TranspilerUtils;
    using HarmonyLib;
    using System.Runtime.CompilerServices;
    using System;
    using System.Collections.Generic;
    using static KianCommons.Assertion;
    using System.Reflection.Emit;

    public static class SuperElevationCommons {
        delegate void SimulationStepDelegate(
            ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData,
            ushort leaderID, ref Vehicle leaderData, int lodPhysics);

        public static MethodBase TargetMethod<T>() =>
            DeclaredMethod<SimulationStepDelegate>(typeof(T), "SimulationStep");

        public static MethodBase TargetTMPEMethod<T>() =>
            DeclaredMethod<SimulationStepDelegate>(typeof(T), "CustomSimulationStep");


        static PathUnit[] pathUnitBuffer => Singleton<PathManager>.instance.m_pathUnits.m_buffer;

        public static void Postfix(ref Vehicle vehicleData, ref Vehicle.Frame frameData) {
            if (!vehicleData.GetCurrentPathPos(out var pathPos))
                return;

            float se = GetCurrentSE(pathPos, vehicleData.m_lastPathOffset*(1f/255f), ref vehicleData);


            var rot = Quaternion.Euler(0, 0f, se);
            frameData.m_rotation *= rot;
        }

        internal static bool GetCurrentPathPos(this ref Vehicle vehicleData, out PathUnit.Position pathPos) {
            byte pathIndex = vehicleData.m_pathPositionIndex;
            if (pathIndex == 255) pathIndex = 0;
            return pathUnitBuffer[vehicleData.m_path].GetPosition(pathIndex >> 1, out pathPos);
        }

        internal static NetInfo.Lane GetLaneInfo(this ref PathUnit.Position pathPos) {
            try {
                ushort segmentID = pathPos.m_segment;
                byte laneIndex = pathPos.m_lane;
                NetUtil.AssertSegmentValid(segmentID);
                var laneInfos = segmentID.ToSegment().Info.m_lanes;
                AssertGT(laneInfos.Length, laneIndex, $"lane index out of range");
                return laneInfos[laneIndex];
            }catch(Exception ex) {
                var segmentInfo = pathPos.m_segment.ToSegment().Info;
                Log.Exception(ex, $"Exception occured at segment:{pathPos.m_segment} {segmentInfo}");
                throw ex;
            }
        }

        internal static float GetCurrentSE(PathUnit.Position pathPos, float offset, ref Vehicle vehicleData) {
            if (float.IsNaN(offset) || float.IsInfinity(offset)) return 0;
            // bezier is always from start to end node regardless of direction.
            SegmentEndData segStart = SegmentEndManager.Instance.GetAt(pathPos.m_segment, true);
            SegmentEndData segEnd = SegmentEndManager.Instance.GetAt(pathPos.m_segment, false);
            float startSE = segStart == null ? 0f : segStart.CachedSuperElevationDeg;
            float endSE = segEnd == null ? 0f : -segEnd.CachedSuperElevationDeg;
            float se = startSE * (1-offset) + endSE * offset;
           
            bool invert = pathPos.m_segment.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
            bool backward = pathPos.GetLaneInfo().m_finalDirection == NetInfo.Direction.Backward;
            bool reversed = vehicleData.m_flags.IsFlagSet(Vehicle.Flags.Reversed);

            bool bidirectional = pathPos.GetLaneInfo().m_finalDirection == NetInfo.Direction.Both;
            bool avoidForward = pathPos.GetLaneInfo().m_finalDirection == NetInfo.Direction.AvoidForward;
            bool avoidBackward = pathPos.GetLaneInfo().m_finalDirection == NetInfo.Direction.AvoidBackward;
            bool avoid = avoidForward | avoidBackward;

            if (invert) se = -se;
            if (backward) se = -se;
            if (reversed & !avoid) se = -se;

            //if (bidirectional) se = 0; // is this necessary?
            return se;
        }

        #region rotation updated

        internal static FieldInfo fRotation = AccessTools.DeclaredField(
            typeof(Vehicle.Frame), nameof(Vehicle.Frame.m_rotation));

        internal static bool RotationUpdated = false;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void OnRotationUpdated() => RotationUpdated = true;


        static FieldInfo f_rotation =
            AccessTools.DeclaredField(typeof(Vehicle.Frame), nameof(Vehicle.Frame.m_rotation)) ??
            throw new Exception("f_rotation is null");


        static MethodInfo mOnRotationUpdated = AccessTools.DeclaredMethod(
            typeof(SuperElevationCommons), nameof(OnRotationUpdated)) ??
            throw new Exception("mOnRotationUpdated is null");

        public static IEnumerable<CodeInstruction> OnRotationUpdatedTranspiler(
            IEnumerable<CodeInstruction> instructions,
            MethodInfo targetMethod) {
            AssertNotNull(targetMethod, "targetMethod");
            //Log.Debug("targetMethod=" + targetMethod);

            CodeInstruction call_OnRotationUpdated = new CodeInstruction(OpCodes.Call, mOnRotationUpdated);

            int n = 0;
            foreach (var instruction in instructions) {
                yield return instruction;
                bool is_stfld_rotation =
                    instruction.opcode == OpCodes.Stfld && instruction.operand == f_rotation;
                if (is_stfld_rotation) { // it seems in CarAI the second one is the important one.
                    n++;
                    yield return call_OnRotationUpdated;
                }
            }

            Log.Debug($"TRANSPILER SuperElevationCommons: Successfully patched {targetMethod}. " +
                $"found {n} instances of Ldfld NetInfo.m_flatJunctions");
            yield break;
        }
        #endregion
    }
}
