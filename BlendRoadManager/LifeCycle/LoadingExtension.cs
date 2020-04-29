﻿namespace RoadTransitionManager.LifeCycle
{
    using ICities;
    using RoadTransitionManager.Util;

    public class LoadingExtention : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            Log.Debug("LoadingExtention.OnLevelLoaded");
            if (mode == LoadMode.LoadGame || mode == LoadMode.NewGame)
                LifeCycle.Load();
        }

        public override void OnLevelUnloading()
        {
            Log.Debug("LoadingExtention.OnLevelUnloading");
            LifeCycle.Release();
        }
    }
}
