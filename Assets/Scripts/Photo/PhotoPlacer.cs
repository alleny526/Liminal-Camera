using UnityEngine;
using System.Collections.Generic;

// 负责放置照片中捕获的内容
namespace LiminalCamera.Photo
{
    public class PhotoPlacer
    {
        private Camera mainCamera;
        private float placementDistance;
        private Transform parentTransform;

        public PhotoPlacer(Camera camera, float distance)
        {
            mainCamera = camera;
            placementDistance = distance;
        }

        // 设置生成对象的父对象
        // 和Player Generated Container有关，专门存放玩家拍照生成的Prop
        public void SetParentTransform(Transform parent)
        {
            parentTransform = parent;
        }

        // 更新放置距离
        public void UpdatePlacementDistance(float distance)
        {
            placementDistance = distance;
        }

        // 放置照片中的所有内容
        public void PlacePhoto(PhotoData heldPhoto)
        {                      
            // 放置Props
            int placedCount = PlaceProps(heldPhoto.capturedProps);
            
            // 放置地形
            PlaceTerrain(heldPhoto);
        }

        // 放置Props
        private int PlaceProps(List<CapturedPropData> capturedProps)
        {
            int placedCount = 0;
            
            foreach (CapturedPropData propData in capturedProps)
            {
                // 使用placementDistance来调整生成位置
                Vector3 adjustedLocalPosition = propData.localPosition * (placementDistance / 10f);
                Vector3 finalPosition = mainCamera.transform.TransformPoint(adjustedLocalPosition);
                Quaternion finalRotation = mainCamera.transform.rotation * propData.localRotation;
                
                float originalDistance = propData.localPosition.z;
                float newDistance = adjustedLocalPosition.z;
                float scaleMultiplier = newDistance / originalDistance;
                Vector3 finalScale = Vector3.one * propData.localScale * scaleMultiplier;
                                
                GameObject newProp = Object.Instantiate(propData.prefab, finalPosition, finalRotation);
                newProp.transform.localScale = finalScale;
                
                if (parentTransform != null)
                {
                    newProp.transform.SetParent(parentTransform);
                }
                
                placedCount++;
            }

            return placedCount;
        }

        // 放置地形
        private void PlaceTerrain(PhotoData heldPhoto)
        {
            if (heldPhoto.terrainIntersection != null)
            {
                CreateNewTerrainPiece(heldPhoto.terrainIntersection);
            }
        }

        // 创建新的地形
        private GameObject CreateNewTerrainPiece(TerrainIntersectionData terrainData)
        {
            GameObject newTerrainPiece = new GameObject("NewTerrainPiece");
            
            if (parentTransform != null)
            {
                newTerrainPiece.transform.SetParent(parentTransform);
            }
            
            Mesh newTerrainMesh = new Mesh();
            
            Vector3[] vertices = new Vector3[terrainData.localVertices.Length];
            for (int i = 0; i < terrainData.localVertices.Length; i++)
            {
                // 根据放置距离调整顶点位置
                Vector3 adjustedLocalVertex = terrainData.localVertices[i] * (placementDistance / 10f);
                vertices[i] = mainCamera.transform.TransformPoint(adjustedLocalVertex);
            }
            
            newTerrainMesh.vertices = vertices;
            newTerrainMesh.triangles = terrainData.intersectionTriangles;
            if (terrainData.intersectionUVs != null)
            {
                newTerrainMesh.uv = terrainData.intersectionUVs;
            }
            newTerrainMesh.RecalculateNormals();
            newTerrainMesh.RecalculateBounds();
            
            MeshFilter meshFilter = newTerrainPiece.AddComponent<MeshFilter>();
            meshFilter.mesh = newTerrainMesh;
            
            MeshRenderer meshRenderer = newTerrainPiece.AddComponent<MeshRenderer>();
            if (terrainData.terrainMaterial != null)
            {
                meshRenderer.material = terrainData.terrainMaterial;
            }
            
            MeshCollider meshCollider = newTerrainPiece.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = newTerrainMesh;
            meshCollider.convex = false;
            
            newTerrainPiece.layer = LayerMask.NameToLayer("Terrain");
            
            return newTerrainPiece;
        }
    }
}
