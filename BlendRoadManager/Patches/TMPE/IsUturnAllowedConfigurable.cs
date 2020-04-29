namespace RoadTransitionManager.Patches.TMPE {
    using System.Reflection;
    using TrafficManager.Manager.Impl;
    using CSUtil.Commons;
    using RoadTransitionManager;
    using RoadTransitionManager.Util;
    using HarmonyLib;

    [HarmonyPatch]
    public static class IsUturnAllowedConfigurable {
        public static MethodBase TargetMethod() {
            return typeof(JunctionRestrictionsManager).
                GetMethod(nameof(JunctionRestrictionsManager.IsUturnAllowedConfigurable));
        }

        public static bool Prefix(ushort segmentId, bool startNode, ref bool __result) {
            ushort nodeID = startNode ? segmentId.ToSegment().m_startNode : segmentId.ToSegment().m_endNode;
            var data = NodeManager.Instance.buffer[nodeID];
            return PrefixUtils.HandleTernaryBool(
                data?.IsUturnAllowedConfigurable(),
                ref __result);
        }
    }
}