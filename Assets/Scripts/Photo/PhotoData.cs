using UnityEngine;
using System.Collections.Generic;

// 照片数据相关的类定义
namespace LiminalCamera.Photo
{
    // 捕获的Prop数据
    [System.Serializable]
    public class CapturedPropData 
    { 
        public GameObject prefab; 
        public Vector3 localPosition; 
        public Quaternion localRotation; 
        public float localScale; 
    }

    // 地形相交数据
    [System.Serializable]
    public class TerrainIntersectionData 
    {
        public int[] intersectionTriangles;
        public Vector2[] intersectionUVs;
        public Vector3[] localVertices;
        public Material terrainMaterial;
    }

    // 照片完整数据
    public class PhotoData 
    { 
        public Texture2D photoImage; 
        public List<CapturedPropData> capturedProps; 
        public TerrainIntersectionData terrainIntersection;
        public float savedFrustumHeight;
        public float savedFrustumBottomWidth;
        public float savedFrustumBottomHeight;
        
        public PhotoData(Texture2D i, List<CapturedPropData> p, TerrainIntersectionData t, float frustumHeight, float frustumBottomWidth, float frustumBottomHeight) 
        { 
            photoImage = i; 
            capturedProps = p; 
            terrainIntersection = t;
            savedFrustumHeight = frustumHeight;
            savedFrustumBottomWidth = frustumBottomWidth;
            savedFrustumBottomHeight = frustumBottomHeight;
        } 
    }
}
