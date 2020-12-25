namespace NodeController.GUI.Panel.ToolButtons {
    using NodeController.Tool;
    using System;
    public class EditSegmentEndButton : ToolModeButtonBase<EditSegmentEndButton> {
        public override NCToolMode Mode => NCToolMode.EditSegmentEnd;
        public override string Tooltip => "edit segment end";
        public override string SpritesFileName => "EditSegmentEnd.png";
    }
}
