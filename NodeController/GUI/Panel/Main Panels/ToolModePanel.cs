namespace NodeController.GUI {
    using ColossalFramework.UI;
    using KianCommons;
    using System;
    using NodeController.GUI.Panel.ToolButtons;

    public class ToolModePanel : UIPanelBase {
        #region Instanciation
        public static ToolModePanel Instance { get; private set; }

        public static ToolModePanel Create() {
            var uiView = UIView.GetAView();
            ToolModePanel panel = uiView.AddUIComponent(typeof(ToolModePanel)) as ToolModePanel;
            return panel;
        }
        public static void Release() {
            var uiView = UIView.GetAView();
            var panel = (ToolModePanel)uiView.FindUIComponent<ToolModePanel>("ToolModePanel");
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


        public override NetworkTypeT NetworkType => throw new NotImplementedException();
        public override INetworkData GetData() => throw new NotImplementedException();
        public UIPanel Container;

        public override void Start() {
            base.Start();
            Log.Debug("TooModePanel started");

            name = "ToolModePanel";
            Caption = "Node Controller Tool";
            this.autoLayoutDirection = LayoutDirection.Horizontal;

            Container = AddPanel();
            Container.autoLayoutDirection = LayoutDirection.Horizontal;

            // add all buttons here:
            AddButton<DefaultModeButton>();
            AddButton<EditNodeButton>();
            AddButton<EditSegmentEndButton>();
            AddButton<ToggleCrossingModeButton>();

            MakeHintBox();
            AutoSize2 = true;
            Close();
        }

        ButtonT AddButton<ButtonT>() where ButtonT: UIButton, IDataControllerUI  {
            var ret = Container.AddUIComponent<ButtonT>();
            Controls.Add(ret);
            return ret;
        }
    }
}

