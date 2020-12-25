namespace NodeController.GUI {
    using ColossalFramework.UI;
    using KianCommons;
    using KianCommons.UI;
    using UnityEngine;

    public class UINodeControllerPanel : UIPanelBase {
        #region Instanciation
        public static UINodeControllerPanel Instance { get; private set; }

        public static UINodeControllerPanel Create() {
            var uiView = UIView.GetAView();
            UINodeControllerPanel panel = uiView.AddUIComponent(typeof(UINodeControllerPanel)) as UINodeControllerPanel;
            return panel;
        }

        public static void Release() {
            var uiView = UIView.GetAView();
            var panel = (UINodeControllerPanel)uiView.FindUIComponent<UINodeControllerPanel>("UINodeControllerPanel");
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

        public override NetworkTypeT NetworkType => NetworkTypeT.Node;
        public override INetworkData GetData() => NodeData;

        public ushort NodeID { get; private set; }

        public NodeData NodeData {
            get {
                if (NodeID == 0) return null;
                return NodeManager.Instance.GetOrCreate(NodeID);
            }
        }

        UIAutoSizePanel offsetPanel_, embankmentPanel_, stretchPanel_, slopePanel_;

        public override void Start() {
            base.Start();
            Log.Debug("UINodeControllerPanel started");

            name = "UINodeControllerPanel";
            Caption = "Node Controller";

            {
                var panel = AddPanel();
                var label = panel.AddUIComponent<UILabel>();
                label.text = "Choose node type";

                var dropdown_ = panel.AddUIComponent<NodeTypeDropDown>();
                Controls.Add(dropdown_);
            }

            {
                var panel = AddPanel();
                panel.autoLayoutDirection = LayoutDirection.Horizontal;
                panel.AutoSize2 = true;
                Controls.Add(panel.AddUIComponent<FlattenButton>());
                Controls.Add(panel.AddUIComponent<SlopeButton>());
                
            }

            const bool extendedSlider = true;
            { // offset
                offsetPanel_ = MakeSliderSection(this, out var label, out var panel0, out var row1, out var row2);
                label.text = "Corner offset";
                label.tooltip = "Adjusts Corner offset for smooth junction transition.";
                if (extendedSlider) panel0.width += CELL_SIZE2.x;
                var slider_ = panel0.AddUIComponent<UIOffsetSlider>();
                Controls.Add(slider_);

                var corner = row2.AddUIComponent<UICornerTextField>();
                if (extendedSlider) corner.size = CELL_SIZE2;
                corner.allowNegative = false;
                corner.name = "NoedCornerOffset";
                corner.GetData = () => NodeData.CornerOffset;
                corner.SetData = val => NodeData.CornerOffset = val > 0 ? val : 0;
                corner.IsMixed = () => !NodeData.HasUniformCornerOffset();
                Controls.Add(corner);
            }

            { // embankment
                embankmentPanel_ = MakeSliderSection(this, out var label, out var panel0, out var row1, out var row2);
                label.text = "Embankment";
                label.tooltip = "twist road sideways (superelevation)";
                var slider_ = panel0.AddUIComponent<EmbankmentSlider>();
                Controls.Add(slider_);

                var fieldAngle = row2.AddUIComponent<UICornerTextField>();
                fieldAngle.size = CELL_SIZE2;
                fieldAngle.GetData = () => NodeData.EmbankmentAngle;
                fieldAngle.SetData = val => NodeData.EmbankmentAngle = Mathf.Clamp(val, -180, +180);
                fieldAngle.IsMixed = () => !NodeData.HasUniformEmbankmentAngle();
                fieldAngle.PostFix = "°";
                Controls.Add(fieldAngle);

                var fieldPercent = row2.AddUIComponent<UICornerTextField>();
                fieldPercent.size = CELL_SIZE2;
                fieldPercent.GetData = () => NodeData.EmbankmentPercent;
                fieldPercent.SetData = val => NodeData.EmbankmentPercent = val;
                fieldPercent.IsMixed = () => !NodeData.HasUniformEmbankmentAngle();
                fieldPercent.PostFix = "%";
                Controls.Add(fieldPercent);
            }

            { // slope
                slopePanel_ = MakeSliderSection(this, out var label, out var panel0, out var row1, out var row2);
                label.text = "Slope";
                label.tooltip = "+90=>up -90=>down\n";
                if (extendedSlider) panel0.width += CELL_SIZE2.x;
                var slider_ = panel0.AddUIComponent<SlopeSlider>();
                Controls.Add(slider_);

                var fieldAngle = row2.AddUIComponent<UICornerTextField>();
                if (extendedSlider) fieldAngle.size = CELL_SIZE2;
                fieldAngle.GetData = () => NodeData.SlopeAngleDeg;
                fieldAngle.SetData = val => NodeData.SlopeAngleDeg = Mathf.Clamp(val, -180, +180);
                fieldAngle.IsMixed = () => !NodeData.HasUniformSlopeAngle();
                fieldAngle.ResetToDefault = () => {
                    foreach (var segmentEndData in NodeData.IterateSegmentEndDatas()) {
                        segmentEndData.DeltaSlopeAngleDeg = 0;
                    }
                }; 
                fieldAngle.PostFix = "°";
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
                fieldPercent.GetData = () => NodeData.Stretch + 100;
                fieldPercent.SetData = val => NodeData.Stretch = val - 100;
                fieldPercent.IsMixed = () => !NodeData.HasUniformStretch();
                fieldPercent.PostFix = "%";
                Controls.Add(fieldPercent);
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

        public void Display(ushort nodeID) {
            Unfocus();
            NodeManager.Instance.UpdateData(NodeID); // refresh previous node data if any.
            NodeID = nodeID;
            NodeManager.Instance.UpdateData(nodeID); // refresh current node data
            base.Open();
        }

        public override void Close() {
            NodeManager.Instance.UpdateData(NodeID);
            NodeID = 0;
            base.Close();
        }

        public override void Refresh() {
            Log.Debug("UINodeControllerPanel.Refresh() called");
            offsetPanel_.isVisible = NodeData?.CanModifyOffset() ?? false;
            slopePanel_.isVisible = stretchPanel_.isVisible = embankmentPanel_.isVisible =
                 NodeData?.CanMassEditNodeCorners() ?? false;

            base.Refresh();
        }
    }
}

