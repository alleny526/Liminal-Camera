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

        // 保存照片
        public PhotoData SavePhoto()
        {
            photoCamera.cullingMask = capturableLayer | terrainLayer;
            RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
            photoCamera.targetTexture = rt;
            photoCamera.Render();
            Texture2D photoImage = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            photoImage.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            photoImage.Apply();
            RenderTexture.active = null;
            photoCamera.targetTexture = null;
            Object.Destroy(rt);

            List<CapturedPropData> propsInView = CapturePropsInView();
            TerrainIntersectionData terrainIntersection = CaptureTerrainIntersectionData();

            return new PhotoData(photoImage, propsInView, terrainIntersection, currentFrustumHeight, frustumBottomWidth, frustumBottomHeight);
        }

        // 捕获视锥体内的所有Props
        public List<CapturedPropData> CapturePropsInView()
        {
            List<CapturedPropData> propsInView = new List<CapturedPropData>();

            // 先捕获以photo camera为中心，以锥体高度为半径的球体内的所有可捕获Props
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
                if (terrainMesh.isReadable == false)
                {
                    terrainMesh = PhotoUtil.MakeReadableMeshCopy(terrainMesh);
                }
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
            return PhotoUtil.IsTriangleIntersectingFrustum(v0, v1, v2, photoCamera, currentFrustumHeight, frustumBottomWidth, frustumBottomHeight);
        }

        // 检查点是否在锥形视锥体内
        private bool IsInFrustum(Vector3 point)
        {
            return PhotoUtil.IsInFrustum(point, photoCamera, currentFrustumHeight, frustumBottomWidth, frustumBottomHeight);
        }
    }
}
