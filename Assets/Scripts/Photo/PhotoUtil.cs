using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Rendering;

// Photo工具类
// 负责提供照片相关的通用静态工具函数和UI效果
namespace LiminalCamera.Photo
{
    public static class PhotoUtil
    {
        // 检查点是否在锥形视锥体内
        public static bool IsInFrustum(Vector3 point, Camera referenceCamera, float frustumHeight, float frustumBottomWidth, float frustumBottomHeight)
        {
            Vector3 localPoint = referenceCamera.transform.InverseTransformPoint(point);

            // 检查是否在锥体的Z范围内
            if (localPoint.z < 0 || localPoint.z > frustumHeight)
            {
                return false;
            }

            // 计算在当前深度下的锥体切面宽高
            float normalizedDepth = localPoint.z / frustumHeight;
            float currentWidth = frustumBottomWidth * normalizedDepth;
            float currentHeight = frustumBottomHeight * normalizedDepth;

            // 检查是否在锥体的XY范围内
            bool inXRange = Mathf.Abs(localPoint.x) <= currentWidth / 2f;
            bool inYRange = Mathf.Abs(localPoint.y) <= currentHeight / 2f;

            return inXRange && inYRange;
        }

        // 检查三角形是否与锥形视锥体相交
        public static bool IsTriangleIntersectingFrustum(Vector3 v0, Vector3 v1, Vector3 v2, Camera referenceCamera, float frustumHeight, float frustumBottomWidth, float frustumBottomHeight)
        {
            // 如果三角形的任意一个顶点在锥体内，或者三角形中心在锥体内，则认为相交
            bool v0InFrustum = IsInFrustum(v0, referenceCamera, frustumHeight, frustumBottomWidth, frustumBottomHeight);
            bool v1InFrustum = IsInFrustum(v1, referenceCamera, frustumHeight, frustumBottomWidth, frustumBottomHeight);
            bool v2InFrustum = IsInFrustum(v2, referenceCamera, frustumHeight, frustumBottomWidth, frustumBottomHeight);

            // 如果任意顶点在锥体内，则相交
            if (v0InFrustum || v1InFrustum || v2InFrustum) return true;

            // 检查三角形中心是否在锥体内
            Vector3 triangleCenter = (v0 + v1 + v2) / 3f;
            if (IsInFrustum(triangleCenter, referenceCamera, frustumHeight, frustumBottomWidth, frustumBottomHeight)) return true;

            return false;
        }

        // 获取Mesh的可读副本
        // 参考自https://discussions.unity.com/t/reading-meshes-at-runtime-that-are-not-enabled-for-read-write/804189/8
        public static Mesh MakeReadableMeshCopy(Mesh nonReadableMesh)
        {
            Mesh meshCopy = new Mesh();
            meshCopy.indexFormat = nonReadableMesh.indexFormat;

            // Handle vertices
            GraphicsBuffer verticesBuffer = nonReadableMesh.GetVertexBuffer(0);
            int totalSize = verticesBuffer.stride * verticesBuffer.count;
            byte[] data = new byte[totalSize];
            verticesBuffer.GetData(data);
            meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, nonReadableMesh.GetVertexAttributes());
            meshCopy.SetVertexBufferData(data, 0, 0, totalSize);
            verticesBuffer.Release();

            // Handle triangles
            meshCopy.subMeshCount = nonReadableMesh.subMeshCount;
            GraphicsBuffer indexesBuffer = nonReadableMesh.GetIndexBuffer();
            int tot = indexesBuffer.stride * indexesBuffer.count;
            byte[] indexesData = new byte[tot];
            indexesBuffer.GetData(indexesData);
            meshCopy.SetIndexBufferParams(indexesBuffer.count, nonReadableMesh.indexFormat);
            meshCopy.SetIndexBufferData(indexesData, 0, 0, tot);
            indexesBuffer.Release();

            // Restore submesh structure
            uint currentIndexOffset = 0;
            for (int i = 0; i < meshCopy.subMeshCount; i++)
            {
                uint subMeshIndexCount = nonReadableMesh.GetIndexCount(i);
                meshCopy.SetSubMesh(i, new SubMeshDescriptor((int)currentIndexOffset, (int)subMeshIndexCount));
                currentIndexOffset += subMeshIndexCount;
            }

            // Recalculate normals and bounds
            meshCopy.RecalculateNormals();
            meshCopy.RecalculateBounds();

            return meshCopy;
        }

        // 屏幕淡入淡出效果
        // 临时效果，后续可能更改
        public static IEnumerator FadeScreen(Image screenFadeUI, bool fadeIn, float duration)
        {
            float timer = 0f;
            Color startColor = fadeIn ? new Color(0, 0, 0, 0) : Color.black;
            Color endColor = fadeIn ? Color.black : new Color(0, 0, 0, 0);
            while (timer < duration)
            {
                if (screenFadeUI != null) screenFadeUI.color = Color.Lerp(startColor, endColor, timer / duration);
                timer += Time.deltaTime;
                yield return null;
            }
            if (screenFadeUI != null) screenFadeUI.color = endColor;
        }
    }
}
