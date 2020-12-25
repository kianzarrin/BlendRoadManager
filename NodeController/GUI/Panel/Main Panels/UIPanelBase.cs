namespace NodeController.GUI {
    using ColossalFramework.UI;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using ColossalFramework;
    using KianCommons;
    using KianCommons.UI;
    using System;
    using ColossalFramework.Threading;

    public abstract class UIPanelBase : UIAutoSizePanel, IDataControllerUI {
        public static UIPanelBase ActivePanel;

        public static readonly SavedFloat SavedX = new SavedFloat(
            "PanelX", Settings.FileName, 87, true);
        public static readonly SavedFloat SavedY = new SavedFloat(
            "PanelY", Settings.FileName, 58, true);

        public static Vector2 CELL_SIZE = new Vector2(100, 20);
        public static Vector2 CELL_SIZE2 = new Vector2(70, 20); // corner table

        public List<IDataControllerUI> Controls;
        public UIResetButton ResetButton;

        public string Caption {
            get => lblCaption_.text;
            set => lblCaption_.text = value;
        }
        UILabel lblCaption_;
        UISprite sprite_;
        UIDragHandle dragHandle_;
        bool Started => sprite_ != null;

        public abstract NetworkTypeT NetworkType { get; }

        public string HintHotkeys => throw new NotImplementedException();

        public string HintDescription => throw new NotImplementedException();

        /// <summary>
        /// if data id is 0, returns null. otherwise calls *Manager.GetORCreateData(...)
        /// </summary>
        /// <returns></returns>
        public abstract INetworkData GetData();

        public override void Awake() {
            base.Awake();
            Controls = new List<IDataControllerUI>();
        }

        public override void Start() {
            base.Start();
            Log.Debug("UIPanelBase started");

            width = 400;
            name = "UIPanelBase";
            backgroundSprite = "MenuPanel2";
            absolutePosition = new Vector3(SavedX, SavedY);

            isVisible = false;

            {
                dragHandle_ = AddUIComponent<UIDragHandle>();
                dragHandle_.width = width;
                dragHandle_.height = 42;
                dragHandle_.relativePosition = Vector3.zero;
                dragHandle_.target = parent;

                lblCaption_ = dragHandle_.AddUIComponent<UILabel>();
                lblCaption_.text = "network controller";
                lblCaption_.relativePosition = new Vector2(70, 14);

                sprite_ = dragHandle_.AddUIComponent<UISprite>();
                sprite_.size = new Vector2(40, 40);
                sprite_.relativePosition = new Vector2(5, 3);
                sprite_.atlas = TextureUtil.GetAtlas(NodeControllerButton.AtlasName);
                sprite_.spriteName = NodeControllerButton.NodeControllerIconActive;
            }
        }

        protected virtual UIAutoSizePanel AddPanel() {
            int pad_horizontal = 10;
            int pad_vertical = 5;
            UIAutoSizePanel panel = AddUIComponent<UIAutoSizePanel>();
            panel.width = width - pad_horizontal * 2;
            panel.autoLayoutPadding =
                new RectOffset(pad_horizontal, pad_horizontal, pad_vertical, pad_vertical);
            return panel;
        }

        protected override void OnPositionChanged() {
            base.OnPositionChanged();
            Log.Debug("OnPositionChanged called");

            Vector2 resolution = GetUIView().GetScreenResolution();

            absolutePosition = new Vector2(
                Mathf.Clamp(absolutePosition.x, 0, resolution.x - width),
                Mathf.Clamp(absolutePosition.y, 0, resolution.y - height));

            SavedX.value = absolutePosition.x;
            SavedY.value = absolutePosition.y;
            Log.Debug("absolutePosition: " + absolutePosition);
        }

        public void Apply() {
            foreach (IDataControllerUI control in Controls ?? Enumerable.Empty<IDataControllerUI>())
                control.Apply();
        }

        public HintBox Hintbox;

        public bool IsStarted => Hintbox != null;

        public virtual void Refresh() {
            if (!IsStarted) {
                ThreadHelper.dispatcher.Dispatch(Refresh);
            }

            Unfocus();
            autoLayout = true;
            foreach (IDataControllerUI control in Controls ?? Enumerable.Empty<IDataControllerUI>())
                control.Refresh();
            RefreshSizeRecursive();
            Hintbox.width = Hintbox.parent.width = this.width;
            autoLayout = false;

            // calcualte captions position:
            float spriteEndX = sprite_.relativePosition.x + sprite_.width;
            float x = spriteEndX + 0.5f * (dragHandle_.width - spriteEndX - lblCaption_.width);
            lblCaption_.relativePosition = new Vector2(x, 14);

            Hintbox?.RefreshValues();
            Hintbox.height = 600;
            Invalidate();
        }

        public virtual void RefreshValues() {
            //Log.Debug(GetType() + ".RefreshValues() was called\n"+ Environment.StackTrace); // TODO delete
            foreach (IDataControllerUI control in Controls ?? Enumerable.Empty<IDataControllerUI>()) {
                control?.RefreshValues();
            }
        }

        public override void OnDestroy() {
            Log.Debug($"UIPanelBase.OnDestroy called for {GetType().Name} V{this.VersionOf()}at\n " + Environment.StackTrace);
            Close();
            gameObject.SetActive(false);
            base.OnDestroy();
        }

        // base.Open and base.Closed should be called at the end of Show()/Close()
        public virtual void Close() {
            if (!Started)
                return;
            Disable(); //Disable before unfocus to prevent OnSubmit from text fields
            Unfocus();// unfocus text fields
            Hide();
            //gameObject.SetActive(false);
            if (ActivePanel == this)
                ActivePanel = null;
        }

        // base.Open and base.Closed should be called at the end of Display/Close
        public virtual void Open() {
            if (!IsStarted) {
                ThreadHelper.dispatcher.Dispatch(Open);
            }
                
            if(ActivePanel!=this)
                ActivePanel?.Close();
            //gameObject.SetActive(true);
            Unfocus(); // preven selected text field value to get copied when I select a new panel
            Enable(); // enable after unfocus to prevent OnSubmit from text fields
            Show();
            ActivePanel = this;
            Refresh();
            Log.Debug($"UIPanelBase.Open(): isEnabled = {isEnabled} for {GetType().Name} V{this.VersionOf()}");
        }

        public IDataControllerUI GethoveredController() {
            foreach (IDataControllerUI c in this.Controls) {
                UIComponent component = c as UIComponent;
                if (!component.isVisible)
                    continue;
                bool hovered = component.containsMouse;
                if (!hovered && component is NodeTypeDropDown dd)
                    hovered = dd.Popup?.containsMouse ?? false;

                if (hovered)
                    return c;
            }
            return null;
        }

        public override void Update() {
            base.Update();
            var del = Input.GetKeyDown(KeyCode.Delete);
            if (del) {
                IDataControllerUI control = GethoveredController();
                //Log.Debug("calling reset for " + control.GetType());
                control.Reset();
                GetData()?.Update();
            }
        }

        public new void Reset() { } // do nothing.

        public void MakeHintBox() {
            var panel = AddUIComponent<UIPanel>();
            panel.width = width;
            panel.height = 0;
            Hintbox = panel.AddUIComponent<HintBox>();
            Hintbox.Hint3 = "Alt + Click : select segment end\nClick : select/insert node\nControl : Hide TMPE signs";
        }

        /// <param name="container">container to add section to</param>
        /// <param name="label">label of the section</param>
        /// <param name="panel0">add slider to this</param>
        /// <param name="row1">add text field labels here if any</param>
        /// <param name="row2">add text fields here</param>
        /// <returns>top contianer of the section. hide this to hide the section</returns>
        public static UIAutoSizePanel MakeSliderSection(UIPanel container,
            out UILabel label, out UIAutoSizePanel panel0, out UIPanel row1, out UIPanel row2) {
            UIAutoSizePanel section = container.AddUIComponent<UIAutoSizePanel>();
            section.autoLayoutDirection = LayoutDirection.Horizontal;
            section.AutoSize2 = true;
            section.padding = new RectOffset(5, 5, 5, 5);
            section.autoLayoutPadding = new RectOffset(0, 4, 0, 0);
            section.name = "section";
            {
                panel0 = section.AddUIComponent<UIAutoSizePanel>();
                panel0.AutoSize2 = false;
                panel0.width = 290; // set slider width
                label = panel0.AddUIComponent<UILabel>();
                panel0.padding = new RectOffset(5, 0, 0, 0);
            }
            {
                var table = section.AddUIComponent<UIAutoSizePanel>();
                table.name = "table";
                table.autoLayoutDirection = LayoutDirection.Vertical;
                table.AutoSize2 = true;
                row1 = AddTableRow(table);
                row2 = AddTableRow(table);
                row1.width = row2.width = CELL_SIZE2.x * 2;
            }
            return section;
        }
        static public UIAutoSizePanel AddTableRow(UIPanel container) {
            var panel = container.AddUIComponent<UIAutoSizePanel>();
            panel.autoLayout = true;
            panel.autoSize = true;
            panel.AutoSize2 = false;
            panel.autoLayoutDirection = LayoutDirection.Horizontal;
            panel.size = CELL_SIZE;
            return panel;
        }

        static public UILabel AddTableLable(UIPanel container, string text, bool center = true) {
            var lbl = container.AddUIComponent<UILabel>();
            lbl.text = text;
            lbl.name = text;
            if (center)
                lbl.textAlignment = UIHorizontalAlignment.Center;
            lbl.autoSize = false;
            lbl.size = CELL_SIZE;
            return lbl;
        }
    }
}
