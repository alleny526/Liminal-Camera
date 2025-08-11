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
        private LayerMask capturableLayer;
        private LayerMask terrainLayer;

        public PhotoPlacer(Camera camera, LayerMask cLayer, LayerMask tLayer, float distance)
        {
            mainCamera = camera;
            capturableLayer = cLayer;
            terrainLayer = tLayer;
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
            // 首先移除与视锥相交的现有内容
            RemoveIntersectingContent(heldPhoto);

            // 放置Props
            int placedCount = PlaceProps(heldPhoto.capturedProps);

            // 放置地形
            GameObject placedTerrain = PlaceTerrain(heldPhoto);

            // 检查是否需要生成画框
            if (LevelGenerator.Instance != null)
            {
                LevelGenerator.Instance.OnPhotoPlaced(heldPhoto.capturedProps, placedTerrain);
            }
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
                if (!newProp.activeSelf)
                {
                    newProp.SetActive(true);
                }
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
        private GameObject PlaceTerrain(PhotoData heldPhoto)
        {
            if (heldPhoto.terrainIntersection != null)
            {
                return CreateNewTerrainPiece(heldPhoto.terrainIntersection);
            }
            return null;
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

            // 重新计算合并后的UV
            Vector2[] recalculatedUVs = new Vector2[vertices.Length];
            Bounds meshBounds = newTerrainMesh.bounds;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                recalculatedUVs[i] = new Vector2(
                    (v.x - meshBounds.min.x) / meshBounds.size.x,
                    (v.z - meshBounds.min.z) / meshBounds.size.z
                );
            }
            newTerrainMesh.uv = recalculatedUVs;

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

        // 移除与视锥相交的现有内容
        private void RemoveIntersectingContent(PhotoData photoData)
        {
            // RemoveIntersectingTerrainTriangles(photoData);
            
            RemoveIntersectingProps(photoData);
        }

        // 移除与视锥相交的现有地形三角形
        private void RemoveIntersectingTerrainTriangles(PhotoData photoData)
        {
            Collider[] allTerrain = Physics.OverlapSphere(mainCamera.transform.position, photoData.savedFrustumHeight, terrainLayer);

            foreach (Collider terrainCollider in allTerrain)
            {
                MeshFilter meshFilter = terrainCollider.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                Mesh terrainMesh = meshFilter.sharedMesh;
                if (!terrainMesh.isReadable)
                {
                    terrainMesh = PhotoUtil.MakeReadableMeshCopy(terrainMesh);
                }

                Transform terrainTransform = terrainCollider.transform;
                
                Vector3[] vertices = terrainMesh.vertices;
                int[] triangles = terrainMesh.triangles;
                Vector2[] uvs = terrainMesh.uv;
                
                List<Vector3> remainingVertices = new List<Vector3>();
                List<int> remainingTriangles = new List<int>();
                List<Vector2> remainingUVs = new List<Vector2>();
                Dictionary<int, int> vertexMapping = new Dictionary<int, int>();
                
                // 检查每个三角形是否与视锥相交
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int i0 = triangles[i];
                    int i1 = triangles[i + 1];
                    int i2 = triangles[i + 2];
                    
                    Vector3 v0 = terrainTransform.TransformPoint(vertices[i0]);
                    Vector3 v1 = terrainTransform.TransformPoint(vertices[i1]);
                    Vector3 v2 = terrainTransform.TransformPoint(vertices[i2]);
                    
                    // 如果三角形不与视锥相交，则保留
                    if (!PhotoUtil.IsTriangleIntersectingFrustum(v0, v1, v2, mainCamera, photoData.savedFrustumHeight, 
                                                                 photoData.savedFrustumBottomWidth, photoData.savedFrustumBottomHeight))
                    {
                        if (!vertexMapping.ContainsKey(i0))
                        {
                            vertexMapping[i0] = remainingVertices.Count;
                            remainingVertices.Add(vertices[i0]);
                            if (uvs != null && i0 < uvs.Length)
                                remainingUVs.Add(uvs[i0]);
                            else
                                remainingUVs.Add(Vector2.zero);
                        }
                        if (!vertexMapping.ContainsKey(i1))
                        {
                            vertexMapping[i1] = remainingVertices.Count;
                            remainingVertices.Add(vertices[i1]);
                            if (uvs != null && i1 < uvs.Length)
                                remainingUVs.Add(uvs[i1]);
                            else
                                remainingUVs.Add(Vector2.zero);
                        }
                        if (!vertexMapping.ContainsKey(i2))
                        {
                            vertexMapping[i2] = remainingVertices.Count;
                            remainingVertices.Add(vertices[i2]);
                            if (uvs != null && i2 < uvs.Length)
                                remainingUVs.Add(uvs[i2]);
                            else
                                remainingUVs.Add(Vector2.zero);
                        }
                        
                        remainingTriangles.Add(vertexMapping[i0]);
                        remainingTriangles.Add(vertexMapping[i1]);
                        remainingTriangles.Add(vertexMapping[i2]);
                    }
                }
                
                // 如果有三角形被移除，更新网格
                if (remainingTriangles.Count < triangles.Length)
                {
                    if (remainingTriangles.Count == 0)
                    {
                        Object.Destroy(terrainCollider.gameObject);
                    }
                    else
                    {
                        Mesh newMesh = new Mesh();
                        newMesh.vertices = remainingVertices.ToArray();
                        newMesh.triangles = remainingTriangles.ToArray();
                        if (remainingUVs.Count > 0)
                            newMesh.uv = remainingUVs.ToArray();
                        
                        newMesh.RecalculateNormals();
                        newMesh.RecalculateBounds();
                        
                        meshFilter.mesh = newMesh;

                        MeshCollider meshCollider = terrainCollider.GetComponent<MeshCollider>();
                        if (meshCollider != null)
                        {
                            meshCollider.sharedMesh = newMesh;
                        }
                    }
                }
            }
        }

        // 移除与视锥相交的现有物品
        private void RemoveIntersectingProps(PhotoData photoData)
        {
            Collider[] allProps = Physics.OverlapSphere(mainCamera.transform.position, photoData.savedFrustumHeight, capturableLayer);

            foreach (Collider propCollider in allProps)
            {
                if (PhotoUtil.IsInFrustum(propCollider.transform.position, mainCamera, photoData.savedFrustumHeight, photoData.savedFrustumBottomWidth, photoData.savedFrustumBottomHeight))
                {
                    if (propCollider.GetComponentInParent<Prop>() != null)
                    {
                        GameManager.Instance.AddHiddenProp(propCollider.GetComponentInParent<Prop>().gameObject);
                        propCollider.GetComponentInParent<Prop>().gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}