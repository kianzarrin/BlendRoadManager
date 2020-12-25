namespace NodeController.Tool {
    using ColossalFramework;
    using ColossalFramework.UI;
    using KianCommons;
    using UnityEngine;

    public abstract class KianToolBase : ToolBase {
        public bool ToolEnabled => ToolsModifierControl.toolController?.CurrentTool == this;

        protected override void OnDestroy() {
            DisableTool();
            base.OnDestroy();
            if (this) {
                Log.Error($"Failed to destroy object {GetType().Name} V{this.VersionOf()}");
            } else {
                Log.Debug($"Sucessfully destroyed object {GetType().Name} V{this.VersionOf()}");
            }
        }

        protected abstract void OnPrimaryMouseClicked();
        protected abstract void OnSecondaryMouseClicked();

        static InfoManager infoMan => Singleton<InfoManager>.instance;
        public static void SetUnderGroundView() => infoMan.SetCurrentMode(
            InfoManager.InfoMode.Underground, InfoManager.SubInfoMode.UndergroundTunnels);
        public static void SetOverGroundView() => infoMan.SetCurrentMode(
            InfoManager.InfoMode.None, InfoManager.SubInfoMode.None);

        protected virtual void OnPageDown() {
            Log.Info("KianToolBase.OnPageDown()");
            if (m_mouseRayValid)
                SetUnderGroundView();
        }

        protected virtual void OnPageUp() {
            if (m_mouseRayValid)
                SetOverGroundView();
        }

        public void ToggleTool() {
            Log.Debug("ToggleTool: called");
            if (!ToolEnabled)
                EnableTool();
            else
                DisableTool();
        }

        public void EnableTool() {
            Log.Info("EnableTool: called");
            //WorldInfoPanel.HideAllWorldInfoPanels();
            //GameAreaInfoPanel.Hide();
            ToolsModifierControl.toolController.CurrentTool = this;
        }

        public void DisableTool() {
            Log.Info("DisableTool: called");
            if (ToolsModifierControl.toolController?.CurrentTool == this)
                ToolsModifierControl.SetTool<DefaultTool>();
        }

        // avoid responding to mouse event if mouse was down at UI and up on road.
        static bool primaryMouseWasDown_, secondaryMouseWasDown_;

        protected override void OnToolGUI(Event e) {
            base.OnToolGUI(e);
            if ( e.type == EventType.MouseUp && m_mouseRayValid) {
                if (primaryMouseWasDown_ && e.button == 0) OnPrimaryMouseClicked();
                else if (secondaryMouseWasDown_ && e.button == 1) OnSecondaryMouseClicked();
            }
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp) {
                primaryMouseWasDown_ = e.type == EventType.MouseDown && e.button == 0 && m_mouseRayValid;
                secondaryMouseWasDown_ = e.type == EventType.MouseDown && e.button == 1 && m_mouseRayValid;
            }

            if (e.type == EventType.keyDown && e.keyCode == KeyCode.Escape) {
                e.Use();
                DisableTool();
            }
            if (e.type == EventType.keyDown && e.keyCode == KeyCode.PageUp) {
                e.Use();
                OnPageUp();
            }
            if (e.type == EventType.keyDown && e.keyCode == KeyCode.PageDown) {
                e.Use();
                OnPageDown();
            }
        }

        #region hover
        internal Ray m_mouseRay;
        internal float m_mouseRayLength;

        /// <summary>mouse ray in CS but not inside UI panel</summary>
        internal bool m_mouseRayValid;
        internal Vector3 m_mousePosition;
        internal RaycastOutput raycastOutput;

        public override void SimulationStep() {
            IsHoverValid = DetermineHoveredElements();
        }

        UIComponent PauseMenu { get; } = UIView.library.Get("PauseMenu");

        protected override void OnToolUpdate() {
            base.OnToolUpdate();
            // respond to escape key.
            if (PauseMenu?.isVisible == true) {
                UIView.library.Hide("PauseMenu");
                DisableTool();
                return;
            }
        }

        protected override void OnToolLateUpdate() {
            base.OnToolLateUpdate();
            m_mousePosition = Input.mousePosition;
            m_mouseRay = Camera.main.ScreenPointToRay(m_mousePosition);
            m_mouseRayLength = Camera.main.farClipPlane;
            m_mouseRayValid = !UIView.IsInsideUI() && Cursor.visible;
        }

        public ushort HoveredNodeId { get; private set; } = 0;
        public ushort HoveredSegmentId { get; private set; } = 0;
        protected bool IsHoverValid { get; private set; }


        //  copy modified from DefaultTool.GetService()
        public virtual RaycastService GetService() {
            var currentMode = Singleton<InfoManager>.instance.CurrentMode;
            var currentSubMode = Singleton<InfoManager>.instance.CurrentSubMode;
            ItemClass.Availability avaliblity = Singleton<ToolManager>.instance.m_properties.m_mode;
            if ((avaliblity & ItemClass.Availability.MapAndAsset) == ItemClass.Availability.None) {
                switch (currentMode) {
                    case InfoManager.InfoMode.TrafficRoutes:
                    case InfoManager.InfoMode.Tours:
                        break;
                    case InfoManager.InfoMode.Underground:
                        if (currentSubMode == InfoManager.SubInfoMode.Default) {
                            return new RaycastService { m_itemLayers = ItemClass.Layer.MetroTunnels };
                        }
                        // ignore water pipes:
                        return new RaycastService { m_itemLayers =  ItemClass.Layer.Default };
                    default:
                        if (currentMode != InfoManager.InfoMode.Water) {
                            if (currentMode == InfoManager.InfoMode.Transport) {
                                return new RaycastService(
                                    ItemClass.Service.PublicTransport,
                                    ItemClass.SubService.None,
                                    ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels
                                    /*| ItemClass.Layer.MetroTunnels | ItemClass.Layer.BlimpPaths | ItemClass.Layer.HelicopterPaths | ItemClass.Layer.FerryPaths*/
                                    );
                            }
                            if (currentMode == InfoManager.InfoMode.Traffic) {
                                break;
                            }
                            if (currentMode != InfoManager.InfoMode.Heating) {
                                return new RaycastService { m_itemLayers = ItemClass.Layer.Default };
                            }
                        }
                        // ignore water pipes:
                        //return new RaycastService(ItemClass.Service.Water, ItemClass.SubService.None, ItemClass.Layer.Default | ItemClass.Layer.WaterPipes);
                        return new RaycastService { m_itemLayers = ItemClass.Layer.Default };
                    case InfoManager.InfoMode.Fishing:
                        // ignore fishing
                        //return new RaycastService(ItemClass.Service.Fishing, ItemClass.SubService.None, ItemClass.Layer.Default | ItemClass.Layer.FishingPaths);
                        return new RaycastService { m_itemLayers = ItemClass.Layer.Default };
                }
                return new RaycastService { m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels };
            }
            if (currentMode != InfoManager.InfoMode.Underground) {
                if (currentMode != InfoManager.InfoMode.Tours) {
                    if (currentMode == InfoManager.InfoMode.Transport) {
                        return new RaycastService {
                            m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels
                            /*| ItemClass.Layer.AirplanePaths | ItemClass.Layer.ShipPaths | ItemClass.Layer.Markers*/
                        };
                    }
                    if (currentMode != InfoManager.InfoMode.Traffic) {
                        return new RaycastService { m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.Markers };
                    }
                }
                return new RaycastService {
                    m_itemLayers = ItemClass.Layer.Default | ItemClass.Layer.MetroTunnels | ItemClass.Layer.Markers
                };
            }
            return new RaycastService { m_itemLayers = ItemClass.Layer.MetroTunnels };
        }

        // simulation thread
        protected bool DetermineHoveredElements() {
            HoveredSegmentId = 0;
            HoveredNodeId = 0;
            if (!m_mouseRayValid) {
                return false;
            }

            // find currently hovered node
            RaycastInput nodeInput = new RaycastInput(m_mouseRay, m_mouseRayLength) {
                m_netService = GetService(),
                m_ignoreTerrain = true,
                m_ignoreNodeFlags = NetNode.Flags.None,
            };

            if (RayCast(nodeInput, out raycastOutput)) {
                HoveredNodeId = raycastOutput.m_netNode;
            } 

            HoveredSegmentId = GetSegmentFromNode(raycastOutput.m_hitPos);

            if (HoveredSegmentId != 0) {
                Debug.Assert(HoveredNodeId != 0, "unexpected: HoveredNodeId == 0");
                return true;
            }

            // find currently hovered segment
            var segmentInput = new RaycastInput(m_mouseRay, m_mouseRayLength) {
                m_netService = GetService(),
                m_ignoreTerrain = true,
                m_ignoreSegmentFlags = NetSegment.Flags.None
            };

            if (RayCast(segmentInput, out raycastOutput)) {
                HoveredSegmentId = raycastOutput.m_netSegment;
            }

            if (HoveredNodeId <= 0 && HoveredSegmentId > 0) {
                // alternative way to get a node hit: check distance to start and end nodes
                // of the segment
                ushort startNodeId = HoveredSegmentId.ToSegment().m_startNode;
                ushort endNodeId = HoveredSegmentId.ToSegment().m_endNode;

                var vStart = raycastOutput.m_hitPos - startNodeId.ToNode().m_position;
                var vEnd = raycastOutput.m_hitPos - endNodeId.ToNode().m_position;

                float startDist = vStart.magnitude;
                float endDist = vEnd.magnitude;

                if (startDist < endDist && startDist < 75f) {
                    HoveredNodeId = startNodeId;
                } else if (endDist < startDist && endDist < 75f) {
                    HoveredNodeId = endNodeId;
                }
            }
            return HoveredNodeId != 0 || HoveredSegmentId != 0;
        }

        internal ushort GetSegmentFromNode(Vector3 hitPos) {
            if (HoveredNodeId == 0) {
                return 0;
            }
            ushort minSegId = 0;
            NetNode node = NetManager.instance.m_nodes.m_buffer[HoveredNodeId];
            float minDistance = float.MaxValue;
            foreach (ushort segmentId in NetUtil.IterateNodeSegments(HoveredNodeId)) {
                Vector3 pos = segmentId.ToSegment().GetClosestPosition(hitPos);
                float distance = (hitPos - pos).sqrMagnitude;
                if (distance < minDistance) {
                    minDistance = distance;
                    minSegId = segmentId;
                }
            }
            return minSegId;
        }

        private static float prev_H = 0f;
        private static float prev_H_Fixed;

        /// <summary>Maximum error of HitPos field.</summary>
        internal const float MAX_HIT_ERROR = 2.5f;

        /// <summary>
        /// Calculates accurate vertical element of raycast hit position.
        /// </summary>
        internal float GetAccurateHitHeight() {
            // cache result.
            float current_hitH = raycastOutput.m_hitPos.y;
            if (KianCommons.Math.MathUtil.EqualAprox(current_hitH, prev_H, 1e-12f)) {
                return prev_H_Fixed;
            }
            prev_H = current_hitH;

            if (HoveredSegmentId.ToSegment().GetClosestLanePosition(
                raycastOutput.m_hitPos,
                NetInfo.LaneType.All,
                VehicleInfo.VehicleType.All,
                out Vector3 pos,
                out uint laneId,
                out int laneIndex,
                out float laneOffset)) {
                return prev_H_Fixed = pos.y;
            }
            return prev_H_Fixed = current_hitH + 0.5f;
        }

        internal Vector3 RaycastMouseLocation() {
            RaycastInput input = new RaycastInput(m_mouseRay, Camera.main.farClipPlane) {
                m_ignoreTerrain = false
            };
            RayCast(input, out RaycastOutput output);

            return output.m_hitPos;
        }

        internal Vector3 RaycastMouseLocation(float y) {
            RaycastInput input = new RaycastInput(m_mouseRay, Camera.main.farClipPlane) {
                m_ignoreTerrain = false
            };
            RayCast(input, out RaycastOutput output);
            var pos = output.m_hitPos;
            return new Vector3(pos.x, y, pos.z);
        }
        #endregion
    }
}
