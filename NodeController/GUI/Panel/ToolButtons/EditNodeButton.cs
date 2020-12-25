namespace NodeController.GUI.Panel.ToolButtons {
    using NodeController.Tool;
    using System;
    public class EditNodeButton : ToolModeButtonBase<EditNodeButton> {
        public override NCToolMode Mode => NCToolMode.EditNode;
        public override string Tooltip => "edit node";
        public override string SpritesFileName => "EditNode.png";
    }
}
