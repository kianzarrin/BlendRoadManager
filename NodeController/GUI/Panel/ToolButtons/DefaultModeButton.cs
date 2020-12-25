namespace NodeController.GUI.Panel.ToolButtons {
    using NodeController.Tool;
    using System;
    public class DefaultModeButton : ToolModeButtonBase<DefaultModeButton> {
        public override NCToolMode Mode => NCToolMode.Default;
        public override string Tooltip => "insert/edit node";
        public override string SpritesFileName => "DefaultMode.png";
    }
}
