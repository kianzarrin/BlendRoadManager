namespace NodeController.GUI {
    using ColossalFramework.UI;
    using KianCommons;
    using KianCommons.UI;
    using UnityEngine;

    public class UISegmentEndControllerPanel : UIPanelBase {
        #region Instanciation
        public static UISegmentEndControllerPanel Instance { get; private set; }

        public static UISegmentEndControllerPanel Create() {
            var uiView = UIView.GetAView();
            UISegmentEndControllerPanel panel =
                uiView.AddUIComponent(typeof(UISegmentEndControllerPanel)) as UISegmentEndControllerPanel;
            return panel;
        }

        public static void Release() {
            var uiView = UIView.GetAView();
            var panel = uiView.FindUIComponent<UISegmentEndControllerPanel>("UISegmentEndControllerPanel");
            Destroy(panel);
        }
        public override void Awake() {
            base.Awake();
            Instance = this;
        }
        public override void OnDestroy() {
            Instance = null;
            base.OnDestroy();
        }
        #endregion Instanciation

        public ushort SegmentID { get; private set; }
        public ushort NodeID { get; private set; }
        public bool StartNode => NetUtil.IsStartNode(segmentId: SegmentID, nodeId: NodeID);

        public SegmentEndData SegmentEndData {
            get {
                if (NodeID == 0) return null;
                NodeManager.Instance.GetOrCreate(NodeID);
                return SegmentEndManager.Instance.GetOrCreate(SegmentID, StartNode);
            }
        }

        public override NetworkTypeT NetworkType => NetworkTypeT.Node;
        public override INetworkData GetData() => SegmentEndData;

        public override void Start() {
            base.Start();
            Log.Debug("UISegmentEndControllerPanel started");

            name = "UISegmentEndControllerPanel";
            Caption = "Segment End Controller";

            {
                var panel = AddPanel();
                panel.padding = new RectOffset(10, 10, 5, 5);
                panel.autoLayoutPadding = default;
                var checkBox = panel.AddUIComponent<UIUnFlattenJunctionsCheckbox>();
                Controls.Add(checkBox);

                var panel2 = panel.AddUIComponent<UIAutoSizePanel>();
                panel2.padding = new RectOffset(20, 0, 4, 0);
                var checkBox2 = panel2.AddUIComponent<TwistCheckbox>();
                Controls.Add(checkBox2);
            }

            { // offset
                offsetPanel_ = MakeSliderSection(this,out var label,out var panel0, out var row1, out var row2);
                label.text = "Corner offset";
                label.tooltip = "Adjusts Corner offset for smooth junction transition.";
                var slider_ = panel0.AddUIComponent<UIOffsetSlider>();
                Controls.Add(slider_);

                AddTableLable(row1, "Left").size = CELL_SIZE2;
                AddTableLable(row1, "Right").size = CELL_SIZE2;

                var lcorner = row2.AddUIComponent<UICornerTextField>();
                lcorner.allowNegative = false;
                lcorner.size = CELL_SIZE2;
                lcorner.GetData = () => SegmentEndData.RightCorner.Offset;
                lcorner.SetData = val => SegmentEndData.RightCorner.Offset = val > 0 ? val : 0;
                Controls.Add(lcorner);

                var rcorner = row2.AddUIComponent<UICornerTextField>();
                rcorner.allowNegative = false;
                rcorner.size = CELL_SIZE2;
                rcorner.GetData = () => SegmentEndData.LeftCorner.Offset;
                rcorner.SetData = val => SegmentEndData.LeftCorner.Offset = val > 0 ? val : 0;
                Controls.Add(rcorner);

                lcorner.Mirror = rcorner;
                rcorner.Mirror = lcorner;

                leftCornerOffset_ = lcorner;
                rightCornerOffset_ = rcorner;
                
            }

            { // embankment
                embankmentPanel_ = MakeSliderSection(this, out var label, out var panel0, out var row1, out var row2);
                label.text = "Embankment";
                label.tooltip = "twist road sideways (superelevation)";
                var slider_ = panel0.AddUIComponent<EmbankmentSlider>();
                Controls.Add(slider_);


                var fieldAngle = row2.AddUIComponent<UICornerTextField>();
                fieldAngle.size = CELL_SIZE2;
                fieldAngle.GetData = () => SegmentEndData.EmbankmentAngleDeg;
                fieldAngle.SetData = val => SegmentEndData.EmbankmentAngleDeg = Mathf.Clamp(val, -180, +180);
                fieldAngle.PostFix = "°";
                Controls.Add(fieldAngle);

                var fieldPercent = row2.AddUIComponent<UICornerTextField>();
                fieldPercent.size = CELL_SIZE2;
                fieldPercent.GetData = () => SegmentEndData.EmbankmentPercent;
                fieldPercent.SetData = val => SegmentEndData.EmbankmentPercent = val;
                fieldPercent.PostFix = "%";
                Controls.Add(fieldPercent);
            }

            const bool extendedSlider = true;
            { // slope
                slopePanel_ = MakeSliderSection(this, out var label, out var panel0, out var row1, out var row2);
                label.text = "Slope";
                label.tooltip = "+90=>up -90=>down\n";
                if(extendedSlider) panel0.width += CELL_SIZE2.x; 
                var slider_ = panel0.AddUIComponent<SlopeSlider>();
                Controls.Add(slider_);

                var fieldAngle = row2.AddUIComponent<UICornerTextField>();
                if(extendedSlider) fieldAngle.size = CELL_SIZE2;
                fieldAngle.GetData = () => SegmentEndData.SlopeAngleDeg;
                fieldAngle.SetData = val => SegmentEndData.SlopeAngleDeg = Mathf.Clamp(val, -180, +180);
                fieldAngle.PostFix = "°";
                fieldAngle.ResetToDefault = () => SegmentEndData.DeltaSlopeAngleDeg = 0;
                Controls.Add(fieldAngle);
            }

            { // Stretch
                stretchPanel_ = MakeSliderSection(this, out var label, out var panel0, out var row1, out var row2);
                label.text = "Stretch";
                label.tooltip = "change the width of the segment end";
                if (extendedSlider) panel0.width += CELL_SIZE2.x;
                var slider_ = panel0.AddUIComponent<StretchSlider>();
                Controls.Add(slider_);

                var fieldPercent = row2.AddUIComponent<UICornerTextField>();
                if (extendedSlider) fieldPercent.size = CELL_SIZE2;
                fieldPercent.GetData = () => SegmentEndData.Stretch + 100;
                fieldPercent.SetData = val => SegmentEndData.Stretch = val - 100;
                fieldPercent.PostFix = "%";
                Controls.Add(fieldPercent);
            }

            {
                var panel = AddPanel();
                panel.padding = new RectOffset(10, 10, 5, 5);
                panel.AutoSize2 = true;
                MakeCornerTable(panel);
            }

            AddPanel().name = "Space";

            {
                var panel = AddPanel();
                var checkBox = panel.AddUIComponent<UIHideMarkingsCheckbox>();
                Controls.Add(checkBox);
            }


            {
                var panel = AddPanel();
                ResetButton = panel.AddUIComponent<UIResetButton>();
                Controls.Add(ResetButton);
            }

            MakeHintBox();

            AutoSize2 = true;
            Close();
        }

        UIAutoSizePanel offsetPanel_, embankmentPanel_, stretchPanel_, slopePanel_;
        UIAutoSizePanel tableLeft_, tableRight_;
        UICornerTextField leftCornerOffset_, rightCornerOffset_;

        public bool IsRighTableHovered() {
            var b1 = tableRight_?.containsMouse ?? false;
            var b2 = rightCornerOffset_?.containsMouse ?? false;
            return b1 || b2;
        }
        public bool IsLeftTableHovered() {
            var b1 = tableLeft_?.containsMouse ?? false;
            var b2 = leftCornerOffset_?.containsMouse ?? false;
            return b1 || b2;
        }

        public void MakeCornerTable(UIPanel container) {
            UICornerTextField lposx, lposy, lposz,
                              rposx, rposy, rposz,
                              ldirx, ldiry, ldirz,
                              rdirx, rdiry, rdirz,
                              llen, rlen;
            LockDirCheckbox llock, rlock;

            {
                UIAutoSizePanel table = tableLeft_ = container.AddUIComponent<UIAutoSizePanel>();
                table.name = "table_left";
                table.autoLayoutDirection = LayoutDirection.Vertical;
                table.AutoSize2 = true;

                var label = table.AddUIComponent<UILabel>();
                label.text = "Left corner";

                // header :  axis: outward, vertical, backward
                var row1 = AddTableRow(table);
                AddTableLable(row1, "axis:", center: false);
                AddTableLable(row1, "outward");
                AddTableLable(row1, "backward");
                AddTableLable(row1, "vertical");

                var row2 = AddTableRow(table);
                AddTableLable(row2, "pos:", center: false);

                lposx = row2.AddUIComponent<UICornerTextField>();
                lposx.GetData = () => SegmentEndData.RightCorner.DeltaPos.x;
                lposx.SetData = val => SegmentEndData.RightCorner.DeltaPos.x = val;
                lposx.name = "lposx";
                Controls.Add(lposx);

                lposz = row2.AddUIComponent<UICornerTextField>();
                lposz.GetData = () => SegmentEndData.RightCorner.DeltaPos.z;
                lposz.SetData = val => SegmentEndData.RightCorner.DeltaPos.z = val;
                lposz.name = "lposz";
                Controls.Add(lposz);

                lposy = row2.AddUIComponent<UICornerTextField>();
                lposy.GetData = () => SegmentEndData.RightCorner.DeltaPos.y;
                lposy.SetData = val => SegmentEndData.RightCorner.DeltaPos.y = val;
                lposy.name = "lposy";
                Controls.Add(lposy);

                var row3 = AddTableRow(table);
                AddTableLable(row3, "dir:", center: false);

                ldirx = row3.AddUIComponent<UICornerTextField>();
                ldirx.GetData = () => SegmentEndData.RightCorner.Dir.x;
                ldirx.SetData = val => SegmentEndData.RightCorner.SetDirI(val, 0);
                ldirx.MouseWheelRatio = 0.1f;
                ldirx.name = "ldrix";
                Controls.Add(ldirx);

                ldirz = row3.AddUIComponent<UICornerTextField>();
                ldirz.GetData = () => SegmentEndData.RightCorner.Dir.z;
                ldirz.SetData = val => SegmentEndData.RightCorner.SetDirI(val, 2);
                ldirz.ResetToDefault = () => SegmentEndData.RightCorner.ResetDeltaDirI(2);
                ldirz.MouseWheelRatio = 0.1f;
                ldirz.name = "ldriz";
                Controls.Add(ldirz);

                ldiry = row3.AddUIComponent<UICornerTextField>();
                ldiry.GetData = () => SegmentEndData.RightCorner.Dir.y;
                ldiry.SetData = val => SegmentEndData.RightCorner.SetDirI(val, 1);
                ldiry.ResetToDefault = () => SegmentEndData.RightCorner.ResetDeltaDirI(1);
                ldiry.MouseWheelRatio = 0.1f;
                ldiry.name = "ldiry";
                Controls.Add(ldiry);

                var row4 = AddTableRow(table);
                row4.padding = new RectOffset(0, 0, 4, 0);
                AddTableLable(row4, "dir length:", center: false);

                llen = row4.AddUIComponent<UICornerTextField>();
                llen.GetData = () => SegmentEndData.RightCorner.DirLength;
                llen.SetData = val => SegmentEndData.RightCorner.DirLength = val; //clamped by setter.
                ldirz.ResetToDefault = () => SegmentEndData.RightCorner.DirLength =
                    ((Vector3)SegmentEndData.RightCorner.Dir0).magnitude;
                llen.name = "llen";
                Controls.Add(llen);

                var row4panel = row4.AddUIComponent<UIAutoSizePanel>();
                row4panel.padding = new RectOffset(10, 0, 0, 0);
                row4panel.AutoSize2 = true;
                llock = row4panel.AddUIComponent<LockDirCheckbox>();
                llock.size = CELL_SIZE;
                llock.Left = false; // ui left is the opposite of backend left.
                Controls.Add(llock);
            }

            {
                UIAutoSizePanel table = tableRight_ = container.AddUIComponent<UIAutoSizePanel>();
                table.name = "table_right";
                table.autoLayoutDirection = LayoutDirection.Vertical;
                table.AutoSize2 = true;

                var label = table.AddUIComponent<UILabel>();
                label.text = "Right corner";

                // header :  axis: outward, vertical, backward
                var row1 = AddTableRow(table);
                AddTableLable(row1, "axis:", center: false);
                AddTableLable(row1, "outward");
                AddTableLable(row1, "backward");
                AddTableLable(row1, "vertical");

                var row2 = AddTableRow(table);
                AddTableLable(row2, "pos:", center: false);

                rposx = row2.AddUIComponent<UICornerTextField>();
                rposx.GetData = () => SegmentEndData.LeftCorner.DeltaPos.x;
                rposx.SetData = val => SegmentEndData.LeftCorner.DeltaPos.x = val;
                rposx.name = "rposx";
                Controls.Add(rposx);

                rposz = row2.AddUIComponent<UICornerTextField>();
                rposz.GetData = () => SegmentEndData.LeftCorner.DeltaPos.z;
                rposz.SetData = val => SegmentEndData.LeftCorner.DeltaPos.z = val;
                rposz.name = "rposz";
                Controls.Add(rposz);

                rposy = row2.AddUIComponent<UICornerTextField>();
                rposy.GetData = () => SegmentEndData.LeftCorner.DeltaPos.y;
                rposy.SetData = val => SegmentEndData.LeftCorner.DeltaPos.y = val;
                rposy.name = "rposy";
                Controls.Add(rposy);

                var row3 = AddTableRow(table);
                AddTableLable(row3, "dir:", center: false);

                rdirx = row3.AddUIComponent<UICornerTextField>();
                rdirx.GetData = () => SegmentEndData.LeftCorner.Dir.x;
                rdirx.SetData = val => SegmentEndData.LeftCorner.SetDirI(val, 0);
                rdirx.MouseWheelRatio = 0.1f;
                rdirx.name = "rdirx";
                Controls.Add(rdirx);

                rdirz = row3.AddUIComponent<UICornerTextField>();
                rdirz.GetData = () => SegmentEndData.LeftCorner.Dir.z;
                rdirz.SetData = val => SegmentEndData.LeftCorner.SetDirI(val, 2);
                rdirz.ResetToDefault = () => SegmentEndData.LeftCorner.ResetDeltaDirI(2);
                rdirz.MouseWheelRatio = 0.1f;
                rdirz.name = "rdirz";
                Controls.Add(rdirz);

                rdiry = row3.AddUIComponent<UICornerTextField>();
                rdiry.GetData = () => SegmentEndData.LeftCorner.Dir.y;
                rdiry.SetData = val => SegmentEndData.LeftCorner.SetDirI(val, 1);
                rdiry.ResetToDefault = () => SegmentEndData.LeftCorner.ResetDeltaDirI(1);
                rdiry.MouseWheelRatio = 0.1f;
                rdiry.name = "rdiry";
                Controls.Add(rdiry);

                var row4 = AddTableRow(table);
                row4.padding = new RectOffset(0, 0, 4, 0);
                AddTableLable(row4, "dir length:", center: false);

                rlen = row4.AddUIComponent<UICornerTextField>();
                rlen.GetData = () => SegmentEndData.LeftCorner.DirLength;
                rlen.SetData = val => SegmentEndData.LeftCorner.DirLength = val;
                rlen.ResetToDefault = () => SegmentEndData.LeftCorner.DirLength =
                    ((Vector3)SegmentEndData.LeftCorner.Dir0).magnitude;
                rlen.name = "rlen";
                Controls.Add(rlen);

                var row4panel = row4.AddUIComponent<UIAutoSizePanel>();
                row4panel.padding = new RectOffset(10, 0, 0, 0);
                row4panel.AutoSize2 = true;
                rlock = row4panel.AddUIComponent<LockDirCheckbox>();
                rlock.size = CELL_SIZE;
                rlock.Left = true; // ui left is the opposite of backend left.
                Controls.Add(rlock);
            }

            lposx.Mirror = rposx; rposx.Mirror = lposx;
            ldirx.Mirror = rdirx; rdirx.Mirror = ldirx;
            lposz.Mirror = rposz; rposz.Mirror = lposz;
            ldirz.Mirror = rdirz; rdirz.Mirror = ldirz;
            lposy.Mirror = rposy; rposy.Mirror = lposy;
            ldiry.Mirror = rdiry; rdiry.Mirror = ldiry;
            llen.Mirror = rlen; rlen.Mirror = llen;
            llock.Mirror = rlock; rlock.Mirror = llock;
        }

        public void Display(ushort segmentID, ushort nodeID) {
            if (Instance != this) Log.Error("Assertion Failed: Instance == this");
            Unfocus();
            SegmentEndManager.Instance.UpdateData(SegmentID, StartNode); // update previous segment data if any.
            SegmentID = segmentID;
            NodeID = nodeID;
            SegmentEndManager.Instance.UpdateData(SegmentID, StartNode);
            base.Open();
        }

        public override void Close() {
            SegmentEndManager.Instance.UpdateData(SegmentID, StartNode);
            SegmentID = NodeID = 0;
            base.Close();
        }

        public override void Refresh() {
            Log.Debug("SegmentEndController.Refresh() called\n"/* + Environment.StackTrace*/);
            tableLeft_.isVisible = tableRight_.isVisible =
                SegmentEndData?.CanModifyCorners() ?? false;
            offsetPanel_.isVisible = SegmentEndData?.CanModifyOffset() ?? false;
            slopePanel_.isVisible = stretchPanel_.isVisible = embankmentPanel_.isVisible =
                 SegmentEndData?.CanModifyCorners() ?? false;

            base.Refresh();
        }
    }
}
