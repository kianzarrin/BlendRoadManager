namespace NodeController.GUI.Panel.ToolButtons {
    using NodeController.Tool;
    using System;
    public class ToggleCrossingModeButton : ToolModeButtonBase<ToggleCrossingModeButton> {
        public override NCToolMode Mode => NCToolMode.ToggleCrossing;
        public override string Tooltip => "toggle crossing";
        public override string SpritesFileName => "B.png";
    }
}
