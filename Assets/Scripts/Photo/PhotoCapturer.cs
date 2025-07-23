using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 负责捕获视锥体内的Props和地形
namespace LiminalCamera.Photo
{
    public class PhotoCapturer
    {
        private Camera photoCamera;
        private LayerMask capturableLayer;
        private LayerMask terrainLayer;
        private float currentFrustumHeight;
        private float frustumBottomWidth;
        private float frustumBottomHeight;

        public PhotoCapturer(Camera camera, LayerMask cLayer, LayerMask tLayer, float frustumHeight, float width, float height)
        {
            photoCamera = camera;
            capturableLayer = cLayer;
            terrainLayer = tLayer;
            currentFrustumHeight = frustumHeight;
            frustumBottomWidth = width;
            frustumBottomHeight = height;
        }

        // 更新锥体参数
        public void UpdateFrustumParameters(float frustumHeight)
        {
            currentFrustumHeight = frustumHeight;
        }

        // 捕获视锥体内的所有Props
        // TODO: 目前没有部分在内即捕获的功能，需要添加
        public List<CapturedPropData> CapturePropsInView()
        {
            List<CapturedPropData> propsInView = new List<CapturedPropData>();

            // 先捕获以photo camera为中心，以锥体高度为半径的球体内的所有可捕获Props
            // TODO: 方法目前比较妥协，后续需要改进 -- 考虑设置一个trigger collider？--也需从性能层考虑
            Collider[] allProps = Physics.OverlapSphere(photoCamera.transform.position, currentFrustumHeight, capturableLayer);

            foreach (Collider propCollider in allProps)
            {
                Prop prop = propCollider.GetComponentInParent<Prop>();
                if (prop != null)
                {
                    if (prop.prefab != null
                         && !propsInView.Any(p => p.localPosition == photoCamera.transform.InverseTransformPoint(prop.transform.position))
                         && prop.canCapture)
                    {
                        // 检查Prop是否在锥形视锥体内
                        // TODO: 只有部分在锥形体内的Prop不会被捕获 -- 看的是prop.transform.position
                        if (IsInFrustum(prop.transform.position))
                        {
                            CapturedPropData capturedData = new CapturedPropData
                            {
                                prefab = prop.prefab,
                                localPosition = photoCamera.transform.InverseTransformPoint(prop.transform.position),
                                localRotation = Quaternion.Inverse(photoCamera.transform.rotation) * prop.transform.rotation,
                                localScale = prop.transform.localScale.x
                            };
                            propsInView.Add(capturedData);
                        }
                    }
                }
            }

            return propsInView;
        }
        
        // 捕获地形与拍照锥体的相交面
        // TODO: 直接循环判断三角形方法比较粗暴，研究下会不会有更好的方法
        public TerrainIntersectionData CaptureTerrainIntersectionData()
        {
            Collider[] allTerrain = Physics.OverlapSphere(photoCamera.transform.position, currentFrustumHeight, terrainLayer);

            if (allTerrain.Length == 0)
            {
                return null;
            }

            List<Vector3> intersectionVertices = new List<Vector3>();
            List<int> intersectionTriangles = new List<int>();
            List<Vector2> intersectionUVs = new List<Vector2>();
            Material terrainMaterial = null;

            int vertexOffset = 0;

            foreach (Collider terrainMeshCollider in allTerrain)
            {
                MeshFilter meshFilter = terrainMeshCollider.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                if (terrainMaterial == null)
                {
                    Renderer renderer = terrainMeshCollider.GetComponent<Renderer>();
                    if (renderer != null) terrainMaterial = renderer.material;
                }

                Mesh terrainMesh = meshFilter.sharedMesh;
                Transform terrainTransform = terrainMeshCollider.transform;

                // 获取地形Mesh的顶点、UV和三角形
                Vector3[] terrainVertices = new Vector3[terrainMesh.vertexCount];
                for (int i = 0; i < terrainMesh.vertexCount; i++)
                {
                    terrainVertices[i] = terrainTransform.TransformPoint(terrainMesh.vertices[i]);
                }

                Vector2[] meshUVs = terrainMesh.uv;
                int[] meshTriangles = terrainMesh.triangles;

                // 检查每个三角形是否与锥体相交
                for (int i = 0; i < meshTriangles.Length; i += 3)
                {
                    Vector3 v0 = terrainVertices[meshTriangles[i]];
                    Vector3 v1 = terrainVertices[meshTriangles[i + 1]];
                    Vector3 v2 = terrainVertices[meshTriangles[i + 2]];

                    if (IsTriangleIntersectingFrustum(v0, v1, v2))
                    {
                        // 添加三角形顶点
                        intersectionVertices.Add(v0);
                        intersectionVertices.Add(v1);
                        intersectionVertices.Add(v2);

                        // 添加三角形索引
                        intersectionTriangles.Add(vertexOffset);
                        intersectionTriangles.Add(vertexOffset + 1);
                        intersectionTriangles.Add(vertexOffset + 2);

                        // 添加UV坐标
                        if (meshUVs != null && i < meshUVs.Length)
                        {
                            intersectionUVs.Add(meshUVs[meshTriangles[i]]);
                            intersectionUVs.Add(meshUVs[meshTriangles[i + 1]]);
                            intersectionUVs.Add(meshUVs[meshTriangles[i + 2]]);
                        }
                        else
                        {
                            intersectionUVs.Add(Vector2.zero);
                            intersectionUVs.Add(Vector2.right);
                            intersectionUVs.Add(Vector2.up);
                        }

                        vertexOffset += 3;
                    }
                }
            }

            if (intersectionVertices.Count == 0)
            {
                return null;
            }

            // 转换为相对于拍照相机的本地坐标
            Vector3[] localVertices = new Vector3[intersectionVertices.Count];
            for (int i = 0; i < intersectionVertices.Count; i++)
            {
                localVertices[i] = photoCamera.transform.InverseTransformPoint(intersectionVertices[i]);
            }

            TerrainIntersectionData intersectionData = new TerrainIntersectionData
            {
                intersectionTriangles = intersectionTriangles.ToArray(),
                intersectionUVs = intersectionUVs.ToArray(),
                localVertices = localVertices,
                terrainMaterial = terrainMaterial
            };

            return intersectionData;
        }
        
        // 检查三角形是否与锥形视锥体相交
        private bool IsTriangleIntersectingFrustum(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            // 如果三角形的任意一个顶点在锥体内，或者三角形中心在锥体内，则认为相交
            bool v0InFrustum = IsInFrustum(v0);
            bool v1InFrustum = IsInFrustum(v1);
            bool v2InFrustum = IsInFrustum(v2);
            
            // 如果任意顶点在锥体内，则相交
            if (v0InFrustum || v1InFrustum || v2InFrustum) return true;
            
            // 检查三角形中心是否在锥体内
            Vector3 triangleCenter = (v0 + v1 + v2) / 3f;
            if (IsInFrustum(triangleCenter)) return true;
            
            return false;
        }

        // 检查点是否在锥形视锥体内
        private bool IsInFrustum(Vector3 point)
        {
            Vector3 localPoint = photoCamera.transform.InverseTransformPoint(point);

            // 检查是否在锥体的Z范围内
            if (localPoint.z < 0 || localPoint.z > currentFrustumHeight)
            {
                return false;
            }

            // 计算在当前深度下的锥体切面宽高
            float normalizedDepth = localPoint.z / currentFrustumHeight;
            float currentWidth = frustumBottomWidth * normalizedDepth;
            float currentHeight = frustumBottomHeight * normalizedDepth;

            // 检查是否在锥体的XY范围内
            bool inXRange = Mathf.Abs(localPoint.x) <= currentWidth / 2f;
            bool inYRange = Mathf.Abs(localPoint.y) <= currentHeight / 2f;

            return inXRange && inYRange;
        }
    }
}
