namespace NodeController.GUI.Panel.ToolButtons {
    using ColossalFramework.UI;
    using System;
    using UnityEngine;
    using static KianCommons.HelpersExtensions;
    using static KianCommons.Assertion;
    using KianCommons;
    using KianCommons.UI;

    public abstract class ToolButtonBase<ButtonT> : UIButton, IDataControllerUI {
        public ToolButtonBase<ButtonT> Instance { get; private set; }

        // Ignore:
        public void Apply() { }
        public void Reset() { }

        public void RefreshValues() { Refresh(); }
        public abstract void Refresh();

        public string HintHotkeys => null;
        public string HintDescription => null;
        public abstract string Tooltip { get; }

        const string IconNormal = "IconNormal";
        const string IconHovered = "IconHovered";
        const string IconPressed = "IconPressed";
        const string IconDisabled = "IconDisabled";
        public abstract string SpritesFileName { get; }
        public virtual string Name => GetType().Name;
        public string AtlasName => $"{GetType().FullName}_rev" + typeof(ButtonBase).VersionOf();
        public const int SIZE = 40;
        public ToolModePanel Root { get; private set; }

        public bool active_ = false;
        public bool IsActive {
            get => active_;
            set { if (value) UseActiveSprites(); else UseDeactiveSprites(); }
        }

        public override void Awake() {
            base.Awake();
            isVisible = true;
            size = new Vector2(SIZE, SIZE);
            canFocus = false;
            name = Name;
            Root = GetRootContainer() as ToolModePanel;
            tooltip = Tooltip;
            Instance = this;
        }

        public override void Start() {
            Log.Debug("ToolButtonBase.Start() is called for " + Name, false);
            base.Start();
            SetupSprites();
        }

        public UITextureAtlas SetupSprites() {
            string[] spriteNames = new string[] { IconNormal, IconHovered, IconPressed, IconDisabled };
            var atlas = TextureUtil.GetAtlas(AtlasName);
            if (atlas == UIView.GetAView().defaultAtlas) {
                atlas = TextureUtil.CreateTextureAtlas(SpritesFileName, AtlasName, SIZE, SIZE, spriteNames);
            }
            Log.Debug("atlas name is: " + atlas.name, false);
            this.atlas = atlas;
            UseDeactiveSprites();
            return atlas;
        }

        public void UseActiveSprites() {
            // focusedBgSprite = can focus is set to false.
            normalBgSprite = IconPressed;
            hoveredBgSprite = IconPressed;
            pressedBgSprite = IconPressed;
            disabledBgSprite = IconDisabled;
            Invalidate();
            active_ = true;
        }

        public void UseDeactiveSprites() {
            // focusedBgSprite = can focus is set to false.
            normalBgSprite = IconNormal;
            hoveredBgSprite = IconHovered;
            pressedBgSprite = IconPressed;
            disabledBgSprite = IconDisabled;
            Invalidate();
            active_ = false;
        }

    }
}
