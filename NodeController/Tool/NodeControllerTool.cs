namespace NodeController.Tool {
    using ColossalFramework;
    using KianCommons;
    using KianCommons.UI;
    using NodeController.GUI;
    using System;
    using System.Threading;
    using UnityEngine;
    using static KianCommons.HelpersExtensions;
    using static KianCommons.UI.RenderUtil;
    using System.Collections.Generic;
    using System.Linq;

    public enum NCToolMode {
        Default,
        EditNode,
        EditSegmentEnd,
        ToggleCrossing
    }

    public sealed class NodeControllerTool : KianToolBase {
        public static readonly SavedInputKey ActivationShortcut = new SavedInputKey(
            "ActivationShortcut",
            Settings.FileName,
            SavedInputKey.Encode(KeyCode.N, true, false, false),
            true);

        public static readonly SavedBool SnapToMiddleNode = new SavedBool(
            "SnapToMiddleNode", Settings.FileName, def: false, true);

        public static readonly SavedBool Hide_TMPE_Overlay = new SavedBool(
            "Hide_TMPE_Overlay", Settings.FileName, def: false, true);

        static readonly SavedInt savedToolMode_ = new SavedInt(
            "ToolMode", Settings.FileName, def: (int)NCToolMode.Default, true);

        public static NCToolMode ToolMode {
            get => (NCToolMode)savedToolMode_.value;
            set => savedToolMode_.value = (int)value;
        }

        public static NCToolMode FinalToolMode {
            get {
                if (AltIsPressed) {
                    if (ToolMode == NCToolMode.Default || ToolMode == NCToolMode.EditNode)
                        return NCToolMode.EditSegmentEnd;
                    else if (ToolMode == NCToolMode.EditSegmentEnd)
                        return NCToolMode.EditNode;
                }
                return ToolMode;
            }
        }

        public static bool EditMode => FinalToolMode == NCToolMode.EditNode || FinalToolMode == NCToolMode.EditSegmentEnd;
        public static bool LockMode => ControlIsPressed && !AltIsPressed;
        public static bool InvertLockMode => ControlIsPressed && AltIsPressed;

        NodeControllerButton Button => NodeControllerButton.Instace;
        UINodeControllerPanel NCPanel;
        UISegmentEndControllerPanel SECPanel;
        ToolModePanel TMPanel;

        NetTool.ControlPoint m_controlPoint;
        NetTool.ControlPoint m_cachedControlPoint;
        ToolErrors m_errors;
        ToolErrors m_cachedErrors;
        NetInfo m_prefab;

        private object m_cacheLock = new object();


        internal class CursorGroup {
            internal CursorInfo Searching, Fail, Success;
            public CursorGroup() { }
            public CursorGroup(string search, string sucess, string fail) {
                CreateCursor(search);
                CreateCursor(sucess);
                CreateCursor(fail);
            }
            public CursorGroup(string name) {
                string prefix = "cursor_";
                string postfix = ".png";
                Searching = CreateCursor(prefix + name + "_grey" + postfix);
                Success = CreateCursor(prefix + name + "_green" + postfix);
                Fail = CreateCursor(prefix + name + "_red" + postfix);
            }
        }

        internal static CursorInfo CreateCursor(string file) {
            if (string.IsNullOrEmpty(file)) return null;
            var ret = ScriptableObject.CreateInstance<CursorInfo>();
            ret.m_texture = TextureUtil.GetTextureFromFile(file);
            ret.m_hotspot = new Vector2(5f, 0f);
            return ret;
        }

        private CursorGroup CursorEditNode, CursorEditSegmentEnd, CursorInsertCrossing, CursorDefault;
        private CursorInfo CursorMoveCorner;

        ref SegmentEndData SelectedSegmentEndData => ref SegmentEndManager.Instance
            .GetAt(segmentID: SelectedSegmentID, nodeID: SelectedNodeID);

        protected override void Awake() {
            Log.Info("NodeControllerTool.Awake() called");
            base.Awake();

            NodeControllerButton.CreateButton();
            NCPanel = UINodeControllerPanel.Create();
            SECPanel = UISegmentEndControllerPanel.Create();
            TMPanel = ToolModePanel.Create();

            Log.Info($"NodeControllerTool.Start() was called " +
                $"this.version={this.VersionOf()} " +
                $"NodeControllerTool.version={typeof(UISliderBase).VersionOf()} " +
                $"NCPanel.version={NCPanel.VersionOf()} " +
                $"UINodeControllerPanel.instance.version={UINodeControllerPanel.Instance.VersionOf()} " +
                $"UINodeControllerPanel.version={typeof(UINodeControllerPanel).VersionOf()} ");


            // A)modify node: green pen
            // B)insert middle (highway) green node
            // C)insert pedestrian : green pedestrian
            // D)searching(mouse is not hovering over road) grey geerbox
            // E)fail insert red geerbox
            // F)fail modify (end node) red geerbox.
            // G)inside panel: normal

            CursorEditNode = new CursorGroup("edit_node");
            CursorEditSegmentEnd = new CursorGroup("edit_segment_end");
            CursorInsertCrossing = new CursorGroup("crossing");
            CursorDefault = new CursorGroup("default");

            CursorMoveCorner = CreateCursor("cursor_remove_crossing.png");
            CursorMoveCorner = CreateCursor("cursor_move.png");
        }

        public static NodeControllerTool Create() {
            Log.Info("NodeControllerTool.Create()");
            GameObject toolModControl = ToolsModifierControl.toolController.gameObject;
            //var tool = toolModControl.GetComponent<NodeControllerTool>() ?? toolModControl.AddComponent<NodeControllerTool>();
            var tool = toolModControl.AddComponent<NodeControllerTool>();
            return tool;
        }

        public static NodeControllerTool Instance {
            get {
                GameObject toolModControl = ToolsModifierControl.toolController?.gameObject;
                return toolModControl?.GetComponent<NodeControllerTool>();
            }
        }

        public static void Remove() {
            Log.Info("NodeControllerTool.Remove()");
            var tool = Instance;
            if (tool != null)
                DestroyImmediate(tool);
        }

        protected override void OnDestroy() {
            Log.Info("NodeControllerTool.OnDestroy() " +
                $"this.version={this.VersionOf()} NodeControllerTool.version={typeof(NodeControllerTool).VersionOf()}");
            Button?.Hide();
            DestroyImmediate(Button);
            DestroyImmediate(NCPanel);
            DestroyImmediate(SECPanel);
            DestroyImmediate(TMPanel);
            base.OnDestroy();
        }

        protected override void OnEnable() {
            try {
                Log.Info("NodeControllerTool.OnEnable", true);
                base.OnEnable();
                Button?.Activate();
                SelectedNodeID = 0;
                SelectedSegmentID = 0;
                handleHovered_ = false;
                SimulationManager.instance.m_ThreadingWrapper.QueueSimulationThread(delegate () {
                    NodeManager.ValidateAndHeal(false);
                });
                TMPanel.Open();
            } catch (Exception e) {
                Log.Exception(e);
            }
        }

        protected override void OnDisable() {
            Log.Info($"NodeControllerTool.OnDisable()");
            ToolCursor = null;
            Hint = null;
            base.OnDisable();
            Button?.Deactivate();
            SelectedNodeID = 0;
            SelectedSegmentID = 0;
            NCPanel?.Close();
            SECPanel?.Close();
            TMPanel?.Close();
        }

        void DragCorner() {
            SegmentEndData segEnd = SelectedSegmentEndData;
            if (SelectedSegmentEndData == null) return;
            bool positionChanged;
            bool selected = leftCornerSelected_ || rightCornerSelected_;
            if (selected) {
                ref SegmentEndData.CornerData corner = ref segEnd.Corner(leftCornerSelected_);
                ref SegmentEndData.CornerData corner2 = ref segEnd.Corner(!leftCornerSelected_);
                var pos = RaycastMouseLocation(corner.Pos.y);
                var delta = pos - corner.Pos;
                positionChanged = delta.sqrMagnitude > 1e-04;
                if (positionChanged) {
                    corner.Pos = pos;
                    if (LockMode) corner2.Pos += delta;
                    segEnd.Update(); // this will refresh panel values on update.
                }
            }
        }

        override public void SimulationStep() {
            base.SimulationStep();
            if (CornerFocusMode) {
                DragCorner();
                return;
            }

            ServiceTypeGuide optionsNotUsed = Singleton<NetManager>.instance.m_optionsNotUsed;
            if (optionsNotUsed != null && !optionsNotUsed.m_disabled) {
                optionsNotUsed.Disable();
            }

            ToolBase.ToolErrors errors = ToolBase.ToolErrors.None;
            if (m_prefab != null) {
                if (this.m_mouseRayValid && MakeControlPoint()) {
                    //Log.Debug("SimulationStep control point is " + LogControlPoint(m_controlPoint));
                    if (m_controlPoint.m_node == 0) {
                        errors = NetUtil.InsertNode(m_controlPoint, out _, test: true);
                    }
                } else {
                    errors = ToolBase.ToolErrors.RaycastFailed;
                }
            }

            m_toolController.ClearColliding();

            while (!Monitor.TryEnter(this.m_cacheLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) {
            }
            try {
                this.m_errors = errors;
            } finally {
                Monitor.Exit(this.m_cacheLock);
            }
        }


        public string GetHint() {
            if (!this.enabled || !m_mouseRayValid || handleHovered_)
                return null;

            if (CornerFocusMode)
                return "drag => move corner\n" + "control + drag => move both corners";

            bool fail = false;
            bool failCrossingNotSupported = fail;
            bool insert = false;
            bool searching = false;
            bool edit = false;
            bool editSegmentEnd = false;
            bool crossing = false;

            ToolErrors error = m_cachedErrors;
            if (IsHoverValid && m_prefab != null) {
                NetTool.ControlPoint controlPoint = m_cachedControlPoint;
                ushort nodeID = controlPoint.m_node;
                edit = nodeID != 0;
                insert = controlPoint.m_segment != 0;
                if (edit) {
                    fail = !NodeData.IsSupported(nodeID);
                    editSegmentEnd = FinalToolMode == NCToolMode.EditSegmentEnd;
                    editSegmentEnd |= nodeID.ToNode().m_flags.IsFlagSet(NetNode.Flags.End);
                } else if (EditMode) {
                    searching = true;
                } else if (insert) {
                    bool isRoad = !NetUtil.IsCSUR(m_prefab);
                    error |= m_prefab.m_netAI.CheckBuildPosition(false, false, true, true, ref controlPoint, ref controlPoint, ref controlPoint, out _, out _, out _, out _);
                    fail = error != ToolErrors.None || !isRoad;
                    crossing = m_prefab.CountPedestrianLanes() >= 2;
                    if (FinalToolMode == NCToolMode.ToggleCrossing && !crossing) {
                        fail = failCrossingNotSupported = true;
                    }
                }
            } else
                searching = true;

            string ret = "";

            if(FinalToolMode == NCToolMode.Default) {
                if (searching) {
                    ret = "hover over a network to select node";
                } else if (fail) {
                    ret = "alt + click => select segment end.\ncannot insert node here";
                    if (m_cachedErrors != ToolErrors.None)
                        ret += " because of " + m_cachedErrors;
                } else if (insert && crossing)
                    ret = "click => insert crossing\n" + "alt + click => select segment end";
                else if (insert && !crossing)
                    ret = "click => insert new middle node\n" + "alt + click => select segment end";
                else if (editSegmentEnd) {
                    ret = "click => select segment end";
                } else if (edit) {
                    ret = "click => select node\n" + "alt + click  => select segment end";
                } else
                    return null;
            } else if(FinalToolMode == NCToolMode.ToggleCrossing) {
                if (searching) {
                    ret = "hover over a network to toggle crossing";
                } else if (fail) {
                    ret = "cannot insert node here";
                    if (failCrossingNotSupported)
                        ret += ", crossing not supported";
                    if (m_cachedErrors != ToolErrors.None)
                        ret += ", " + m_cachedErrors;
                } else if (insert || edit)
                    ret = "click => toggle crossing\n";
                else
                    return null;
            } else if (editSegmentEnd) {
                if (searching) {
                    ret = "hover over a network to edit segment end";
                } else if (fail) {
                    ret = "cannot insert edit this segment end";
                    if (m_cachedErrors != ToolErrors.None)
                        ret += ", " + m_cachedErrors;
                } else if (edit)
                    ret = "click => select segment end\n" + "alt + click  => select node";
                else
                    return null;
            } else if (FinalToolMode == NCToolMode.EditNode) {
                if (searching) {
                    ret = "hover over a network to edit node";
                } else if (fail) {
                    ret = "cannot insert edit this node";
                    if (m_cachedErrors != ToolErrors.None)
                        ret += ", " + m_cachedErrors;
                } else if (edit)
                    ret = "click => select node\n" + "alt + click  => select segment end";
                else
                    return null;
            } 

            if (ShouldDrawSigns())
                ret += "\ncontrol => hide TMPE overlay";
            ret += "\npage up/down => overground/underground view";
            return ret;
        }

        CursorInfo GetCursor() {
            if (!this.enabled || !m_mouseRayValid || handleHovered_)
                return null;

            if (CornerFocusMode)
                return CursorMoveCorner;

            bool fail = false;
            bool insert = false;
            bool searching = false;
            bool edit = false;
            bool crossing = false;
            bool editSegmentEnd =false;

            if (IsHoverValid && m_prefab != null) {
                NetTool.ControlPoint controlPoint = m_cachedControlPoint;
                ushort nodeID = controlPoint.m_node;
                edit = nodeID != 0;
                insert = controlPoint.m_segment != 0;
                if (edit) {
                    fail = !NodeData.IsSupported(nodeID);
                    editSegmentEnd = FinalToolMode == NCToolMode.EditSegmentEnd;
                    editSegmentEnd |= nodeID.ToNode().m_flags.IsFlagSet(NetNode.Flags.End);
                } else if (EditMode) {
                    searching = true;
                } else if (insert) {
                    bool isRoad = !NetUtil.IsCSUR(m_prefab);
                    ToolErrors error = m_cachedErrors;
                    error |= m_prefab.m_netAI.CheckBuildPosition(false, false, true, true, ref controlPoint, ref controlPoint, ref controlPoint, out _, out _, out _, out _);
                    fail = error != ToolErrors.None || !isRoad;
                    crossing = NodeManager.CanInsertCrossing(m_prefab);
                    if (FinalToolMode == NCToolMode.ToggleCrossing) {
                        fail &= !crossing;
                    }
                }
            }

            if (fail) {
                switch (FinalToolMode) {
                    case NCToolMode.Default:
                        return CursorDefault.Fail;
                    case NCToolMode.EditNode:
                        return CursorEditNode.Fail;
                    case NCToolMode.EditSegmentEnd:
                        return CursorEditSegmentEnd.Fail;
                    case NCToolMode.ToggleCrossing:
                        return CursorInsertCrossing.Fail;
                    default:
                        throw new Exception("unhandled mode:" + FinalToolMode);
                }
            }

            if (edit) {
                if (FinalToolMode == NCToolMode.ToggleCrossing)
                    return CursorInsertCrossing.Success;
                if (editSegmentEnd)
                    return CursorEditSegmentEnd.Success;
                else
                    return CursorEditNode.Success;
            }
            if (insert) {
                if (crossing)
                    return CursorInsertCrossing.Success;
                else
                    return CursorDefault.Success;
            }

            if (searching) {
                switch (FinalToolMode) {
                    case NCToolMode.Default:
                        return CursorDefault.Searching;
                    case NCToolMode.EditNode:
                        return CursorEditNode.Searching;
                    case NCToolMode.EditSegmentEnd:
                        return CursorEditSegmentEnd.Searching;
                    case NCToolMode.ToggleCrossing:
                        return CursorInsertCrossing.Searching;
                    default:
                        throw new Exception("unhandled mode:" + FinalToolMode);
                }
            }
            return null; // race condition
        }

        public string Hint;

        protected override void OnToolUpdate() {
            base.OnToolUpdate();
            ToolCursor = GetCursor();
            Hint = GetHint();

            lock (m_cacheLock) {
                m_cachedControlPoint = m_controlPoint;
                m_cachedErrors = m_errors;
                if (HoveredSegmentId != 0) {
                    m_prefab = HoveredSegmentId.ToSegment().Info;
                } else {
                    m_prefab = null;
                }
            }
        }

        protected override void OnToolLateUpdate() {
            base.OnToolLateUpdate();
            ForceInfoMode(InfoManager.InfoMode.None, InfoManager.SubInfoMode.None);
        }

        public Color GetColor(bool error, bool newNode = false) {
            if (error)
                return base.GetToolColor(false, true);
            Color ret = newNode ? Color.green : Color.yellow;
            //ret *= 0.7f; // not too bright.

            ret.a = base.GetToolColor(false, false).a;
            return ret;
        }

        //Vector3 _cachedHitPos;
        public ushort SelectedNodeID;
        public ushort SelectedSegmentID;

        /// <param name="left">going away from junction</param>
        CornerMarker GetCornerMarker(bool left) {
            var segEnd = SelectedSegmentEndData;
            if (segEnd == null || !segEnd.CanModifyCorners()) return null;

            var pos = segEnd.Corner(left).Pos;
            float terrainY = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(pos);
            var ret = new CornerMarker {
                Position = pos,
                TerrainPosition = new Vector3(pos.x, terrainY, pos.z),
            };
            return ret;
        }

        // left/right: going away from junction
        bool leftCornerSelected_ = false, leftCornerHovered_ = false;
        bool rightCornerSelected_ = false, rightCornerHovered_ = false;
        bool CornerFocusMode =>
            SelectedSegmentID != 0 &&
            (leftCornerHovered_ | rightCornerHovered_ | leftCornerSelected_ | rightCornerSelected_);

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            base.RenderOverlay(cameraInfo);
            if (SelectedSegmentID != 0) {
                bool enlargeLeft = SECPanel?.IsRighTableHovered() ?? false; // change direction of view
                enlargeLeft |= leftCornerHovered_;
                GetCornerMarker(left: true)?.RenderOverlay(cameraInfo, Color.red, enlargeLeft, leftCornerSelected_);

                bool enlargeRight = SECPanel?.IsLeftTableHovered() ?? false; // change direction of view
                enlargeRight |= rightCornerHovered_;
                GetCornerMarker(left: false)?.RenderOverlay(cameraInfo, Color.red, enlargeRight, rightCornerSelected_);
            }
            if (CornerFocusMode)
                return;

            Color selectedColor = new Color32(225, 225, 225, 225);

            if (SelectedSegmentID != 0 && SelectedNodeID != 0) {
                DrawCutSegmentEnd(
                    cameraInfo,
                    SelectedSegmentID,
                    0.5f,
                    NetUtil.IsStartNode(segmentId: SelectedSegmentID, nodeId: SelectedNodeID),
                    selectedColor,
                    alpha: true);
                ushort nodeID = SelectedSegmentID.ToSegment().GetOtherNode(SelectedNodeID);
                if (nodeID.ToNode().m_flags.IsFlagSet(NetNode.Flags.Middle))
                    DrawNodeCircle(cameraInfo, Color.gray, nodeID, true);
            } else if (SelectedNodeID != 0) {
                DrawNodeCircle(cameraInfo, selectedColor, SelectedNodeID, false);
                foreach (var segmentID in NetUtil.IterateNodeSegments(SelectedNodeID)) {
                    ushort nodeID = segmentID.ToSegment().GetOtherNode(SelectedNodeID);
                    if (nodeID.ToNode().m_flags.IsFlagSet(NetNode.Flags.Middle))
                        DrawNodeCircle(cameraInfo, Color.gray, nodeID, true);
                }
            }
            if (!m_mouseRayValid || handleHovered_)
                return;

            if (FinalToolMode == NCToolMode.EditSegmentEnd) {
                RenderHoveredSegmentEnd(cameraInfo);
            } else if (IsHoverValid && m_prefab != null) {
                NetTool.ControlPoint controlPoint = m_cachedControlPoint;
                if (controlPoint.m_node != 0) {
                    if (controlPoint.m_node.ToNode().m_flags.IsFlagSet(NetNode.Flags.End)) {
                        RenderHoveredSegmentEnd(cameraInfo);
                    } else {
                        bool fail = !NodeData.IsSupported(controlPoint.m_node);
                        DrawNodeCircle(cameraInfo, GetColor(fail), controlPoint.m_node, false);
                    }
                } else if (controlPoint.m_segment != 0) {
                    ToolErrors error = m_cachedErrors;
                    error |= m_prefab.m_netAI.CheckBuildPosition(false, false, true, true, ref controlPoint, ref controlPoint, ref controlPoint, out _, out _, out _, out _);
                    bool fail = error != ToolErrors.None || NetUtil.IsCSUR(m_prefab);
                    if (FinalToolMode == NCToolMode.ToggleCrossing
                        && !NodeManager.CanInsertCrossing(m_prefab))
                        fail = true;
                    Color color = GetColor(fail, true);
                    RenderStripOnSegment(cameraInfo, controlPoint.m_segment, controlPoint.m_position, 1.5f, color);
                }
                //DrawOverlayCircle(cameraInfo, Color.red, raycastOutput.m_hitPos, 1, true);
            }
        }

        void RenderHoveredSegmentEnd(RenderManager.CameraInfo cameraInfo) {
            bool fail = !NodeData.IsSupported(HoveredNodeId);
            if (!fail) {
                DrawCutSegmentEnd(
                cameraInfo,
                HoveredSegmentId,
                0.5f,
                NetUtil.IsStartNode(segmentId: HoveredSegmentId, nodeId: HoveredNodeId),
                GetColor(fail),
                alpha: true);
            }
        }

        protected override void OnToolGUI(Event e) {
            base.OnToolGUI(e); // calls on click events on mosue up
            CalculateConrerSelectionMode(e);
            if (!ControlIsPressed)
                DrawSigns();
        }

        void CalculateConrerSelectionMode(Event e) {
            bool mouseDown = e.type == EventType.mouseDown && e.button == 0;
            bool mouseUp = e.type == EventType.mouseUp && e.button == 0;
            if (SelectedSegmentID == 0) {
                leftCornerHovered_ = rightCornerHovered_ = leftCornerSelected_ = rightCornerSelected_ = false;
                return;
            }

            if (e.type == EventType.mouseDown && e.button == 0) {
                leftCornerSelected_ = leftCornerHovered_ = GetCornerMarker(left: true)?.IntersectRay() ?? false;
            } else if (mouseUp) {
                leftCornerSelected_ = false;
                leftCornerHovered_ = GetCornerMarker(left: true)?.IntersectRay() ?? false;
            } else {
                leftCornerHovered_ = leftCornerSelected_ || (GetCornerMarker(left: true)?.IntersectRay() ?? false);
            }
            if (mouseDown) {
                rightCornerSelected_ = rightCornerHovered_ = GetCornerMarker(left: false)?.IntersectRay() ?? false;
            } else if (mouseUp) {
                rightCornerSelected_ = false;
                rightCornerHovered_ = GetCornerMarker(left: false)?.IntersectRay() ?? false;
            } else {
                rightCornerHovered_ = rightCornerSelected_ || (GetCornerMarker(left: false)?.IntersectRay() ?? false);
            }
        }

        /// <summary>
        /// does not take into account the control key (useful for hint).
        /// it takes into account the segment count, node type, and options state.
        /// </summary>
        bool ShouldDrawSigns() {
            NodeData nodeData = NodeManager.Instance.buffer[SelectedNodeID];
            return !Hide_TMPE_Overlay || (nodeData != null && nodeData.SegmentCount <= 2);
        }

        bool handleHovered_;
        private void DrawSigns() {
            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
            if (SelectedNodeID == 0) {
                TrafficRulesOverlay overlay =
                    new TrafficRulesOverlay(handleClick: false);
                foreach (NodeData nodeData in NodeManager.Instance.buffer) {
                    if (nodeData == null) continue;
                    overlay.DrawSignHandles(
                        nodeData.NodeID, 0, camPos: ref camPos, out _);
                }
            } else {
                if (ShouldDrawSigns()) {
                    TrafficRulesOverlay overlay =
                        new TrafficRulesOverlay(handleClick: true);
                    handleHovered_ = overlay.DrawSignHandles(
                        SelectedNodeID, SelectedSegmentID, camPos: ref camPos, out _);
                }
            }
        }

        protected override void OnPrimaryMouseClicked() {
            if (!IsHoverValid || handleHovered_ || CornerFocusMode)
                return;
            Log.Info($"OnPrimaryMouseClicked: segment {HoveredSegmentId} node {HoveredNodeId}", true);
            if (FinalToolMode == NCToolMode.EditSegmentEnd) {
                if (NodeData.IsSupported(HoveredNodeId)) {
                    SelectedSegmentID = HoveredSegmentId;
                    SelectedNodeID = HoveredNodeId;
                    SECPanel.Display(
                        segmentID: SelectedSegmentID,
                        nodeID: SelectedNodeID);
                }
                return;
            }

            if (m_errors != ToolErrors.None || m_prefab == null)
                return;
            var c = m_cachedControlPoint;

            if (c.m_node != 0) {
                bool supported = NodeData.IsSupported(c.m_node);
                if (!supported) {
                    return;
                }
                SelectedNodeID = c.m_node;
                if (SelectedNodeID.ToNode().m_flags.IsFlagSet(NetNode.Flags.End)) {
                    // for end node just show segment end panel.
                    SelectedSegmentID = SelectedNodeID.ToNode().GetFirstSegment();
                    SECPanel.Display(
                        segmentID: SelectedSegmentID,
                        nodeID: SelectedNodeID);
                } else {
                    SelectedSegmentID = 0;
                    NCPanel.Display(SelectedNodeID);
                }
            } else if (c.m_segment != 0) {
                if (FinalToolMode == NCToolMode.ToggleCrossing && !NodeManager.CanInsertCrossing(m_prefab))
                    return;
                if (!NetUtil.IsCSUR(m_prefab)) {
                    SimulationManager.instance.AddAction(delegate () {
                        NodeData nodeData = NodeManager.Instance.InsertNode(m_controlPoint);
                        SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(delegate () {
                            if (nodeData != null) {
                                SelectedNodeID = nodeData.NodeID;
                                SelectedSegmentID = 0;
                                NCPanel.Display(SelectedNodeID);
                            }
                        });
                    });
                }
            } else {
                // nothing is hovered.
            }
        }

        protected override void OnSecondaryMouseClicked() {
            handleHovered_ = false;
            if (SelectedNodeID == 0) {
                DisableTool();
            } else {
                SelectedSegmentID = SelectedNodeID = 0;
                TMPanel.Open();
            }
        }

        static string LogControlPoint(NetTool.ControlPoint c) {
            return $"<node:{c.m_node} segment:{c.m_segment} " +
                $"position:{c.m_position}" + $"elevation:{c.m_elevation}>";
        }

        bool MakeControlPoint() {
            if (!IsHoverValid) {
                //Log.Debug("MakeControlPoint: HoverValid is not valid");
                m_controlPoint = default;
                return false;
            }
            ushort segmentID0 = 0, segmentID1 = 0;
            int count = 0;
            foreach (ushort segmentID in NetUtil.IterateNodeSegments(HoveredNodeId)) {
                if (segmentID == 0)
                    continue;
                if (count == 0) segmentID0 = segmentID;
                if (count == 1) segmentID1 = segmentID;
                count++;
            }

            bool snapNode =
                count != 2 ||
                segmentID0.ToSegment().Info != segmentID1.ToSegment().Info ||
                !HoveredNodeId.ToNode().m_flags.IsFlagSet(NetNode.Flags.Moveable);

            bool edit = ToolMode == NCToolMode.EditNode;
            snapNode |= SnapToMiddleNode | edit;
            if (snapNode) {
                Vector3 diff = raycastOutput.m_hitPos - HoveredNodeId.ToNode().m_position;
                const float distance = 2 * NetUtil.MPU;
                if (edit || diff.sqrMagnitude < distance * distance) {
                    m_controlPoint = new NetTool.ControlPoint { m_node = HoveredNodeId };
                    //Log.Debug("MakeControlPoint: On node");
                    return true;
                }
            }
            ref NetSegment segment = ref HoveredSegmentId.ToSegment();
            float elevation = 0.5f * (segment.m_startNode.ToNode().m_elevation + segment.m_endNode.ToNode().m_elevation);
            m_controlPoint = new NetTool.ControlPoint {
                m_segment = HoveredSegmentId,
                m_position = segment.GetClosestPosition(raycastOutput.m_hitPos),
                m_elevation = elevation,
            };
            //Log.Debug("MakeControlPoint: on segment.");
            return true;
        }





    } //end class



}
