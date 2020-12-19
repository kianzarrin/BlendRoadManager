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

        public static bool LockMode => ControlIsPressed && !AltIsPressed;
        public static bool InvertLockMode => ControlIsPressed && AltIsPressed;

        NodeControllerButton Button => NodeControllerButton.Instace;
        UINodeControllerPanel NCPanel;
        UISegmentEndControllerPanel SECPanel;

        NetTool.ControlPoint m_controlPoint;
        NetTool.ControlPoint m_cachedControlPoint;
        ToolErrors m_errors;
        ToolErrors m_cachedErrors;
        NetInfo m_prefab;

        private object m_cacheLock = new object();

        private CursorInfo CursorInsert, CursorInsertCrossing,
            CursorEdit, CursorSearching, CursorError, CursorMoveCorner;

        ref SegmentEndData SelectedSegmentEndData => ref SegmentEndManager.Instance
            .GetAt(segmentID: SelectedSegmentID, nodeID: SelectedNodeID);

        protected override void Awake() {
            Log.Info("NodeControllerTool.Awake() called");
            base.Awake();

            NodeControllerButton.CreateButton();
            NCPanel = UINodeControllerPanel.Create();
            SECPanel = UISegmentEndControllerPanel.Create();

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

            CursorEdit = ScriptableObject.CreateInstance<CursorInfo>();
            CursorEdit.m_texture = TextureUtil.GetTextureFromFile("cursor_edit.png"); // green pen
            CursorEdit.m_hotspot = new Vector2(5f, 0f);

            CursorInsert = ScriptableObject.CreateInstance<CursorInfo>();
            CursorInsert.m_texture = TextureUtil.GetTextureFromFile("cursor_insert.png"); // green T node
            CursorInsert.m_hotspot = new Vector2(5f, 0f);

            CursorInsertCrossing = ScriptableObject.CreateInstance<CursorInfo>();
            CursorInsertCrossing.m_texture = TextureUtil.GetTextureFromFile("cursor_insert_crossing.png"); // green crossing.
            CursorInsertCrossing.m_hotspot = new Vector2(5f, 0f);

            CursorError = ScriptableObject.CreateInstance<CursorInfo>();
            CursorError.m_texture = TextureUtil.GetTextureFromFile("cursor_error.png"); // red gear
            CursorError.m_hotspot = new Vector2(5f, 0f);

            CursorSearching = ScriptableObject.CreateInstance<CursorInfo>();
            CursorSearching.m_texture = TextureUtil.GetTextureFromFile("cursor_searching.png"); // grey gear
            CursorSearching.m_hotspot = new Vector2(5f, 0f);

            CursorMoveCorner = ScriptableObject.CreateInstance<CursorInfo>();
            CursorMoveCorner.m_texture = TextureUtil.GetTextureFromFile("cursor_move.png"); // 
            CursorMoveCorner.m_hotspot = new Vector2(5f, 0f);
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
                Destroy(tool);
        }

        protected override void OnDestroy() {
            Log.Info("NodeControllerTool.OnDestroy() " +
                $"this.version={this.VersionOf()} NodeControllerTool.version={typeof(NodeControllerTool).VersionOf()}");

            Button?.Hide();
            Destroy(Button);
            Destroy(NCPanel);
            Destroy(SECPanel);
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
        }

        void DragCorner() {
            SegmentEndData segEnd = SelectedSegmentEndData;
            if (SelectedSegmentEndData == null) return;
            bool positionChanged = false;
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
                    errors |= ToolBase.ToolErrors.RaycastFailed;
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
            // A)modify node: 
            // B)insert middle (highway) 
            // C)insert pedestrian
            // D)searching(mouse is not hovering over road) 
            // E)fail insert 
            // G)inside panel 

            if (!this.enabled || !m_mouseRayValid || handleHovered_)
                return null;

            if (CornerFocusMode)
                return "drag => move corner\n" + "control + drag => move both corners";

            bool fail = false;
            bool insert = false;
            bool searching = false;
            bool edit = false;
            bool editSegmentEnd = fail;
            bool crossing = false;

            ToolErrors error = m_cachedErrors;
            if (IsHoverValid && m_prefab != null) {
                NetTool.ControlPoint controlPoint = m_cachedControlPoint;
                ushort nodeID = controlPoint.m_node;
                edit = nodeID != 0;
                insert = controlPoint.m_segment != 0;
                if (edit) {
                    fail = !NodeData.IsSupported(nodeID);
                    editSegmentEnd = nodeID.ToNode().m_flags.IsFlagSet(NetNode.Flags.End);
                } else if (AltIsPressed) {
                    searching = true;
                } else if (insert) {
                    bool isRoad = !NetUtil.IsCSUR(m_prefab);
                    error |= m_prefab.m_netAI.CheckBuildPosition(false, false, true, true, ref controlPoint, ref controlPoint, ref controlPoint, out _, out _, out _, out _);
                    fail = error != ToolErrors.None || !isRoad;
                    crossing = m_prefab.CountPedestrianLanes() >= 2;
                }
            } else
                searching = true;

            string ret = "";

            if (searching)
                ret = "hover over a network to select/insert node";
            else if (fail) {
                ret = "alt + click => select segment end.\ncannot insert node here ";
                if (m_cachedErrors != ToolErrors.None)
                    ret = "alt + click => select segment end.\ncannot insert node here because of " + error;
                else
                    ret += "alt + click => select segment end.\ncannot insert node here ";
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

            if (ShouldDrawSigns())
                ret += "\ncontrol => hide TMPE overlay";
            ret += "\npage up/down => overground/underground view";
            return ret;
        }

        CursorInfo GetCursor() {
            // A)modify node: green pen
            // B)insert middle (highway) green node
            // C)insert pedestrian : green pedestrian
            // D)searching(mouse is not hovering over road) grey geerbox
            // E)fail insert red geerbox
            // F)fail modify (end node) red geerbox.
            // G)inside panel: normal

            if (!this.enabled || !m_mouseRayValid || handleHovered_) // G
                return null;

            if (CornerFocusMode)
                return CursorMoveCorner;

            bool fail = false;
            bool insert = false;
            bool searching = false;
            bool edit = false;
            bool crossing = false;

            if (IsHoverValid && m_prefab != null) {
                NetTool.ControlPoint controlPoint = m_cachedControlPoint;
                ushort nodeID = controlPoint.m_node;
                edit = nodeID != 0;
                insert = controlPoint.m_segment != 0;
                if (edit) {
                    fail = !NodeData.IsSupported(nodeID);
                } else if (AltIsPressed) {
                    searching = true;
                } else if (insert) {
                    bool isRoad = !NetUtil.IsCSUR(m_prefab);
                    ToolErrors error = m_cachedErrors;
                    error |= m_prefab.m_netAI.CheckBuildPosition(false, false, true, true, ref controlPoint, ref controlPoint, ref controlPoint, out _, out _, out _, out _);
                    fail = error != ToolErrors.None || !isRoad;
                    crossing = m_prefab.CountPedestrianLanes() >= 2;
                }
            } else
                searching = true;

            if (searching)
                return CursorSearching;

            if (fail)
                return CursorError;

            if (insert && crossing)
                return CursorInsertCrossing;

            if (insert && !crossing)
                return CursorInsert;

            if (edit)
                return CursorEdit;

            return null; // race condition
        }

        public string Hint;

        protected override void OnToolUpdate() {
            base.OnToolUpdate();
            ToolCursor = GetCursor();
            Hint = GetHint();

            while (!Monitor.TryEnter(this.m_cacheLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) {
            }
            try {
                m_cachedControlPoint = m_controlPoint;
                m_cachedErrors = m_errors;
            } finally {
                Monitor.Exit(this.m_cacheLock);
            }

            if (HoveredSegmentId != 0) {
                m_prefab = HoveredSegmentId.ToSegment().Info;
            } else {
                m_prefab = null;
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

        static bool CanSelectSegmentEnd(ushort segmentID, ushort nodeID) {
            if (nodeID == 0 || segmentID == 0)
                return false;
            if (!NodeData.IsSupported(nodeID))
                return false;
            return true;// nodeID.ToNode().m_flags.IsFlagSet(NetNode.Flags.Junction);
        }

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

            if (AltIsPressed) {
                RenderHoveredSegmentEnd(cameraInfo);
            } else if (IsHoverValid && m_prefab != null) {
                NetTool.ControlPoint controlPoint = m_cachedControlPoint;
                if (controlPoint.m_node != 0) {
                    bool fail = !NodeData.IsSupported(controlPoint.m_node);
                    if (controlPoint.m_node.ToNode().m_flags.IsFlagSet(NetNode.Flags.End)) {
                        RenderHoveredSegmentEnd(cameraInfo);
                    } else {
                        DrawNodeCircle(cameraInfo, GetColor(fail), controlPoint.m_node, false);
                    }
                } else if (controlPoint.m_segment != 0) {
                    ToolErrors error = m_cachedErrors;
                    error |= m_prefab.m_netAI.CheckBuildPosition(false, false, true, true, ref controlPoint, ref controlPoint, ref controlPoint, out _, out _, out _, out _);
                    bool fail = error != ToolErrors.None || NetUtil.IsCSUR(m_prefab);
                    Color color = GetColor(fail, true);
                    RenderStripOnSegment(cameraInfo, controlPoint.m_segment, controlPoint.m_position, 1.5f, color);
                }
                //DrawOverlayCircle(cameraInfo, Color.red, raycastOutput.m_hitPos, 1, true);
            }
        }

        void RenderHoveredSegmentEnd(RenderManager.CameraInfo cameraInfo) {
            if (CanSelectSegmentEnd(nodeID: HoveredNodeId, segmentID: HoveredSegmentId)) {
                DrawCutSegmentEnd(
                cameraInfo,
                HoveredSegmentId,
                0.5f,
                NetUtil.IsStartNode(segmentId: HoveredSegmentId, nodeId: HoveredNodeId),
                Color.yellow,
                alpha: true);
            }
#if DEBUG
            // TEST CODE
            if (HoveredNodeId.ToNode().m_flags.IsFlagSet(NetNode.Flags.Junction)) {
                GetSegments(HoveredSegmentId, HoveredNodeId, out var segmentIdA, out var segmentIdB);
                if (segmentIdA != 0) 
                    RenderSegmnetOverlay(cameraInfo, segmentIdA, Color.green);
                if (segmentIdB != 0) 
                    RenderSegmnetOverlay(cameraInfo, segmentIdB, Color.blue);
            }
#endif

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
            if (AltIsPressed) {
                if (CanSelectSegmentEnd(nodeID: HoveredNodeId, segmentID: HoveredSegmentId)) {
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
                if (!NetUtil.IsCSUR(m_prefab)) {
                    SimulationManager.instance.AddAction(delegate () {
                        NodeData nodeData = NodeManager.Instance.InsertNode(c);
                        if (nodeData != null) {
                            SelectedNodeID = nodeData.NodeID;
                            SelectedSegmentID = 0;
                            NCPanel.Display(SelectedNodeID);
                        }
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
                NCPanel.Close();
                SECPanel.Close();
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

            snapNode |= SnapToMiddleNode;

            if (snapNode) {
                Vector3 diff = raycastOutput.m_hitPos - HoveredNodeId.ToNode().m_position;
                const float distance = 2 * NetUtil.MPU;
                if (diff.sqrMagnitude < distance * distance) {
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

        public static void GetSegments(ushort segmentID, ushort nodeID, out ushort segmentID_A, out ushort segmentID_B) {
            segmentID_A = segmentID_B = 0;
            ref NetSegment segment = ref segmentID.ToSegment();
            NetInfo info = segment.Info;
            ItemClass connectionClass = info.GetConnectionClass();
            bool bStartNode = nodeID == segment.m_startNode;
            Vector3 dir = !bStartNode ? segment.m_endDirection : segment.m_startDirection;
            float dot_A = -4f;
            float dot_B = -4f;
            for (int i = 0; i < 8; i++) {
                ushort segmentID2 = nodeID.ToNode().GetSegment(i);
                if (segmentID2 == 0 || segmentID2 == segmentID)
                    continue;
                NetInfo info2 = segmentID2.ToSegment().Info;
                ItemClass connectionClass2 = info2.GetConnectionClass();
                if (connectionClass.m_service != connectionClass2.m_service)
                    continue;

                NetSegment segment2 = segmentID2.ToSegment();
                bool bStartNode2 = nodeID != segment2.m_startNode;
                Vector3 dir2 = !bStartNode2 ? segment2.m_endDirection : segment2.m_startDirection;
                float dot = dir.x * dir2.x + dir.z * dir2.z;
                float determinent = dir2.z * dir.x - dir2.x * dir.z;
                bool bRight = determinent > 0;
                bool bWide = dot < 0;
                // 180 -> det=0 dot=-1
                if (!bRight) {
                    if (dot > dot_A) // most accute
                    {
                        dot_A = dot;
                        segmentID_A = segmentID2;
                    }
                    dot = -2f - dot;
                    if (dot > dot_B) // widest
                    {
                        dot_B = dot;
                        segmentID_B = segmentID2;
                    }
                } else {
                    if (dot > dot_B) // most accute
                    {
                        dot_B = dot;
                        segmentID_B = segmentID2;
                    }
                    dot = -2f - dot;
                    if (dot > dot_A) // widest
                    {
                        dot_A = dot;
                        segmentID_A = segmentID2;
                    }

                }
            }
        }


    } //end class
}
