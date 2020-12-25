namespace NodeController.GUI.Panel.ToolButtons {
    using ColossalFramework.UI;
    using System;
    using UnityEngine;
    using static KianCommons.HelpersExtensions;
    using static KianCommons.Assertion;
    using KianCommons;
    using KianCommons.UI;
    using NodeController.Tool;

    public abstract class ToolModeButtonBase<ButtonT> : ToolButtonBase<ButtonT> {
        public abstract NCToolMode Mode { get; }

        public override void Refresh() => IsActive = NodeControllerTool.ToolMode == Mode;

        protected override void OnClick(UIMouseEventParameter p) {
            base.OnClick(p);
            NodeControllerTool.ToolMode = Mode;
            Root.Refresh();
        }
    }
}
