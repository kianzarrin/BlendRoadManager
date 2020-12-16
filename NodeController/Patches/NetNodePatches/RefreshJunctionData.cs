namespace NodeController.Patches {
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using KianCommons;
    using KianCommons.Patches;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using UnityEngine;
    using static KianCommons.Patches.TranspilerUtils;

    // private void NetNode.RefreshJunctionData(
    //      ushort nodeID, int segmentIndex, ushort nodeSegment, Vector3 centerPos, ref uint instanceIndex, ref RenderManager.Instance data
    [UsedImplicitly]
    [HarmonyPatch]
    static class RefreshJunctionData {
        [UsedImplicitly]
        static MethodBase TargetMethod() {
            return AccessTools.Method(
            typeof(NetNode),
            "RefreshJunctionData",
            new Type[] {
                typeof(ushort),
                typeof(int),
                typeof(ushort),
                typeof(Vector3),
                typeof(uint).MakeByRefType(),
                typeof(RenderManager.Instance).MakeByRefType()
            });
        }

        //public static Matrix4x4 HouseholderReflection(this Matrix4x4 matrix4X4, Vector3 planeNormal) {
        //    planeNormal.Normalize();
        //    Vector4 planeNormal4 = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, 0);
        //    var a = MultiplyVectorsTransposed(planeNormal4, planeNormal4);

        //    //var a = Minus(Matrix4x4.identity, planeNormal4);
        //    //    Matrix4x4 householderMatrix = Matrix4x4.identity.Minus(
        //    //        MultiplyVectorsTransposed(planeNormal4, planeNormal4).MutiplyByNumber(2));
        //    //    return householderMatrix * matrix4X4;

        //    // c = -2*(planeNorma14 * planeNormal4');
        //    // householderMatrix = c * Matrix4x4.identity;
        //    //return householderMatrix * matrix4X4;
        //}



        /// <param name="axis">0123 which represents x y z w respectively</param>
        internal static Matrix4x4 Reflection(int axis) {
            // x across the road
            // z is along the road
            Matrix4x4 ret = Matrix4x4.identity;
            ret[axis, axis] = -1;
            return ret;
        }

        internal static Matrix4x4 FlipRows(Matrix4x4 mat) {
            Matrix4x4 ret = new Matrix4x4();
            for (int row = 0; row < 4; ++row) 
                ret.SetRow(row, mat.GetRow(3 - row));
            return ret;
        }
        internal static Matrix4x4 FlipCols(Matrix4x4 mat) {
            Matrix4x4 ret = new Matrix4x4();
            for (int col = 0; col < 4; ++col)
                ret.SetColumn(col, mat.GetColumn(3 - col));
            return ret;
        }

        internal static Matrix4x4 FlipFlop(Matrix4x4 mat) {
            return FlipRows(FlipCols(mat));
        }


        internal static Matrix4x4 Rotate() {
            // y axis
            Matrix4x4 ret = Matrix4x4.identity;
            ret[0, 0] = -1;
            ret[2, 2] = -1;
            return ret;
        }



        [UsedImplicitly]
        static void Postfix(ref NetNode __instance, ref RenderManager.Instance data, ushort nodeID, [HarmonyArgument("nodeSegment")] ushort segmentID) {
            if (__instance.m_flags.IsFlagSet(NetNode.Flags.Junction)) {
                bool invert = segmentID.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
                bool startNode = segmentID.ToSegment().IsStartNode(nodeID);
                bool flip = invert != startNode;
                if (flip) {
                    Log.DebugWait($"node:{nodeID} segment:{segmentID} was flipped", seconds: 2, copyToGameLog: false);
                    //var m_dataMatrix0 = Rotate() * data.m_dataMatrix1;
                    //var m_dataMatrix1 = Rotate() * data.m_dataMatrix0;
                    //var extra_dataMatrix2 = Rotate() * data.m_extraData.m_dataMatrix3;
                    //var extra_dataMatrix3 = Rotate() * data.m_extraData.m_dataMatrix2;

                    //var m_dataMatrix0 = Reflection(0) * data.m_dataMatrix1;
                    //var m_dataMatrix1 = Reflection(0) * data.m_dataMatrix0;
                    //var extra_dataMatrix2 = Reflection(0) * data.m_extraData.m_dataMatrix3;
                    //var extra_dataMatrix3 = Reflection(0) * data.m_extraData.m_dataMatrix2;

                    //var m_dataMatrix0 = FlipRows(data.m_dataMatrix1);
                    //var m_dataMatrix1 = FlipRows(data.m_dataMatrix0);
                    //var extra_dataMatrix2 = FlipRows(data.m_extraData.m_dataMatrix3);
                    //var extra_dataMatrix3 = FlipRows(data.m_extraData.m_dataMatrix2);


                    var m_dataMatrix0 = FlipCols(data.m_dataMatrix1);
                    var m_dataMatrix1 = FlipCols(data.m_dataMatrix0);
                    var extra_dataMatrix2 = FlipCols(data.m_extraData.m_dataMatrix3);
                    var extra_dataMatrix3 = FlipCols(data.m_extraData.m_dataMatrix2);

                    //var m_dataMatrix0 = FlipFlop(data.m_dataMatrix1);
                    //var m_dataMatrix1 = FlipFlop(data.m_dataMatrix0);
                    //var extra_dataMatrix2 = FlipFlop(data.m_extraData.m_dataMatrix3);
                    //var extra_dataMatrix3 = FlipFlop(data.m_extraData.m_dataMatrix2);

                    //var m_dataMatrix0 =  data.m_dataMatrix1 * Reflection(0);
                    //var m_dataMatrix1 = data.m_dataMatrix0 * Reflection(0);
                    //var extra_dataMatrix2 = data.m_extraData.m_dataMatrix3 * Reflection(0);
                    //var extra_dataMatrix3 = data.m_extraData.m_dataMatrix2 * Reflection(0);

                    //var m_dataMatrix0 =  data.m_dataMatrix1 * Rotate();
                    //var m_dataMatrix1 = data.m_dataMatrix0 * Rotate();
                    //var extra_dataMatrix2 = data.m_extraData.m_dataMatrix3 * Rotate();
                    //var extra_dataMatrix3 = data.m_extraData.m_dataMatrix2 * Rotate();

                    data.m_dataMatrix0 = m_dataMatrix0;
                    data.m_dataMatrix1 = m_dataMatrix1;
                    data.m_extraData.m_dataMatrix2 = extra_dataMatrix2;
                    data.m_extraData.m_dataMatrix3 = extra_dataMatrix3;

                    //data.m_dataMatrix0 = m_dataMatrix1;
                    //data.m_dataMatrix1 = m_dataMatrix0;
                    //data.m_extraData.m_dataMatrix2 = extra_dataMatrix3;
                    //data.m_extraData.m_dataMatrix3 = extra_dataMatrix2;
                }
            }

            NodeData blendData = NodeManager.Instance.buffer[nodeID];
            if (blendData == null)
                return;

            if (blendData.ShouldRenderCenteralCrossingTexture()) {
                // puts crossings in the center.
                data.m_dataVector1.w = 0.01f;
            }



#if false
            if(blendData.NodeType == NodeTypeT.Stretch) {
                // should data vectors be inverted?
                ushort segmentID = __instance.GetSegment(data.m_dataInt0 & 7);
                var invert = segmentID.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
                var startNode = NetUtil.IsStartNode(segmentId:segmentID, nodeId: nodeID);
                bool flip = startNode == !invert; // tested works.
                if (flip) {// flip across x axis
                    data.m_dataVector0.x = -data.m_dataVector0.x;
                    //data.m_dataVector0.z = -data.m_dataVector0.z;
                    data.m_dataVector0.y = -data.m_dataVector0.y;
                    //data.m_dataVector0.w = -data.m_dataVector0.w;

                    //data.m_dataVector2.x = -data.m_dataVector2.x;
                    //data.m_dataVector2.z = -data.m_dataVector2.z;
                    data.m_dataVector2.y = -data.m_dataVector2.y;
                    //data.m_dataVector2.w = -data.m_dataVector2.w;
                     
                    //data.m_dataVector1.x = -data.m_dataVector1.x;
                    //data.m_dataVector1.z = -data.m_dataVector1.z;
                    //data.m_dataVector1.y = -data.m_dataVector1.y;
                    //data.m_dataVector1.w = -data.m_dataVector1.w;

                    //data.m_dataVector3.z = -data.m_dataVector3.z;
                    //data.m_dataVector3.y = -data.m_dataVector3.y;
                    //data.m_dataVector3.x = -data.m_dataVector3.x;
                    //data.m_dataVector3.w = -data.m_dataVector3.w;
                }
            }
#endif
        }

        static MethodInfo mCalculateCorner = typeof(NetSegment)
            .GetMethod(nameof(NetSegment.CalculateCorner), BindingFlags.Public | BindingFlags.Instance) ??
            throw new Exception("could not find NetSegment.CalculateCorner");
        static MethodInfo mSwapCorners = GetMethod(typeof(RefreshJunctionData), nameof(SwapCorners));

        [UsedImplicitly]
        static IEnumerable<CodeInstruction> Transpiler_off(IEnumerable<CodeInstruction> instructions, MethodBase original) {
            var codes = instructions.ToCodeList();
            var ldSegmentID = GetLDArg(original, "nodeSegment");
            var ldNodeID = GetLDArg(original, "nodeID");
            var callSwapCorners = new CodeInstruction(OpCodes.Call, mSwapCorners);

            CodeInstruction ldCornerA = default, ldDirA = default;
            int indexA = default;
            for (int n = 1; ; ++n) {
                if (n % 2 == 1) {
                    // odd
                    indexA = GetCalculateCorner(codes, n, out ldCornerA, out ldDirA);
                    if (indexA == -1)
                        break;
                    else
                        continue;
                }
                // even:
                var indexB = GetCalculateCorner(codes, n, out var ldCornerB, out var ldDirB);
                Assertion.Assert(indexB != -1, "indexB != -1");
                codes.InsertInstructions(indexB + 1, // insert after
                    new CodeInstruction[] {
                        ldSegmentID.Clone(),
                        ldNodeID.Clone(),
                        ldCornerA.Clone(),
                        ldCornerB.Clone(),
                        callSwapCorners.Clone(),

                        ldSegmentID.Clone(),
                        ldNodeID.Clone(),
                        ldDirA.Clone(),
                        ldDirB.Clone(),
                        callSwapCorners.Clone(),
                    });
            }

            return codes;
        }

        public static int GetCalculateCorner(List<CodeInstruction> codes, int count,
            out CodeInstruction ldCorner, out CodeInstruction ldDir) {
            var index = codes.Search(c => c.Calls(mCalculateCorner), count: count, throwOnError: false);
            if (index != -1) {
                ldCorner = codes[index - 3];
                ldDir = codes[index - 2];
            } else {
                ldCorner = ldDir = null;
            }
            return index; //codes[index];
        }

        static void SwapCorners(ushort segmentID, ushort nodeID, ref Vector3 a, ref Vector3 b) {
            bool invert = segmentID.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
            bool startNode = NetUtil.IsStartNode(segmentId: segmentID, nodeId: nodeID);
            bool swap = startNode != invert; // see RefreshBendData 
            if (swap) {
                var temp = a;
                a = b;
                b = temp;
            }
        }
    }
}