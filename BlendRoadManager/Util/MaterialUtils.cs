using System;
using System.Linq;
using UnityEngine;

// TODO check out material.MainTextureScale
// regarding weird nodes, what if we return a copy of the material?
// Loading screens Mod owner wrote this about LODs: https://steamcommunity.com/workshop/filedetails/discussion/667342976/1636416951459546732/
namespace RoadTransitionManager.Util {
    using static TextureUtils;
    public static class MaterialUtils {
        public static Texture2D TryGetTexture2D(this Material material, int textureID) {
            try {
                if (material.HasProperty(textureID))
                {
                    Texture texture = material.GetTexture(textureID);
                    if (texture is Texture2D)
                        return texture as Texture2D;
                }
            }
            catch { }
            //Log.Info($"Warning: failed to get {getTexName(textureID)} texture from material :" + material.name);
            return null;
        }

        public static Material GetSegmentMaterial(NetInfo info, int textureID) {
            NetInfo.Segment segmentInfo = null;
            foreach (var segmentInfo2 in info.m_segments ?? Enumerable.Empty<NetInfo.Segment>()) {
                if (segmentInfo2.m_segmentMaterial.TryGetTexture2D(textureID) != null) {
                    segmentInfo = segmentInfo2;
                    break;
                }
            }
            return segmentInfo?.m_segmentMaterial;
        }

        public static Material ContinuesMedian(Material material, Material segMaterial, NetInfo info, bool lod = false) {
            if (material == null) throw new ArgumentNullException("material");
            if (info == null) throw new ArgumentNullException("info");
            segMaterial = segMaterial ?? GetSegmentMaterial(info, ID_APRMap);
            material = new Material(material);

            Texture2D tex;
            tex = segMaterial?.TryGetTexture2D(ID_Defuse);
            if (tex != null) material.SetTexture(ID_Defuse, tex);
            tex = segMaterial?.TryGetTexture2D(ID_APRMap);
            if (tex != null) material.SetTexture(ID_APRMap, tex);
            tex = segMaterial?.TryGetTexture2D(ID_XYSMap);
            if (tex != null) material.SetTexture(ID_XYSMap, tex);

            return material;
        } 
    } // end class
} // end namesapce

