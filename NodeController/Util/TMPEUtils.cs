using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using KianCommons;
using TrafficManager.API.Manager;
using TrafficManager;

namespace NodeController.Util {
    public static class TMPEUtils {
        internal static bool WorldToScreenPoint(Vector3 worldPos, out Vector3 screenPos) {
            screenPos = Camera.main.WorldToScreenPoint(worldPos);
            screenPos.y = Screen.height - screenPos.y;

            return screenPos.z >= 0;
        }
        internal static bool IsMouseOver(Rect boundingBox) {
            return boundingBox.Contains(Event.current.mousePosition);
        }

        delegate float dGetHandleAlphaT_(bool hovered);
        static dGetHandleAlphaT_ dGetHandleAlpha_;
        internal static float GetHandleAlpha(bool hovered) {
            if(dGetHandleAlpha_ == null) {
                var mGetHandleAlpha = AccessTools.DeclaredMethod(
                    typeof(TrafficManager.UI.TrafficManagerTool),
                    "GetHandleAlpha");
                dGetHandleAlpha_ = (dGetHandleAlphaT_)Delegate.CreateDelegate(
                    typeof(dGetHandleAlphaT_), mGetHandleAlpha);
            }
            return dGetHandleAlpha_(hovered);
        }

        internal static float GetBaseZoom() {
            return Screen.height / 1200f;
        }

        static IJunctionRestrictionsManager jrMan => Constants.ManagerFactory.JunctionRestrictionsManager;

        public static bool CanToggleCrossing(ushort segmentId, ushort nodeId) {
            return jrMan.IsPedestrianCrossingAllowedConfigurable(
                segmentId,
                segmentId.ToSegment().IsStartNode(nodeId),
                ref nodeId.ToNode());
        }

        public static bool CanToggleCrossing(ushort nodeId) {
            return nodeId.ToNode().IterateSegments().Any(_segmentId =>
                CanToggleCrossing(segmentId: _segmentId, nodeId: nodeId));
        }

        public static bool HasCrossing(ushort segmentId, ushort nodeId) {
            return jrMan.IsPedestrianCrossingAllowed(
                segmentId,
                segmentId.ToSegment().IsStartNode(nodeId));
        }

        public static bool SetCrossing(ushort segmentId, ushort nodeId, bool value) {
            return jrMan.SetPedestrianCrossingAllowed(
                segmentId,
                segmentId.ToSegment().IsStartNode(nodeId),
                value);
        }


        internal static bool ToggleCrossing(ushort nodeId) {
            var segments = nodeId.ToNode().IterateSegments().Where( _segmentId =>
                CanToggleCrossing(segmentId:_segmentId, nodeId: nodeId));
            if (!segments.Any()) return false;
            bool hasCrossing = segments.Any(_segmentId =>
                HasCrossing(segmentId: _segmentId, nodeId: nodeId));
            foreach (ushort segmentId in segments)
                SetCrossing(segmentId: segmentId, nodeId: nodeId, !hasCrossing);
            return true;
        }

    }
}
