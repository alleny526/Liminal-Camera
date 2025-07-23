using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// Photo工具类
// 目前负责保存照片信息和临时的UI淡入淡出效果
namespace LiminalCamera.Photo
{
    public class PhotoUtil
    {
        private Camera mainCamera;
        private Camera photoCamera;
        private LayerMask capturableLayer;
        private LayerMask terrainLayer;
        private Image screenFadeUI;

        public PhotoUtil(Camera mainCam, Camera photoCam, LayerMask cLayer, LayerMask tLayer, Image fade)
        {
            mainCamera = mainCam;
            photoCamera = photoCam;
            capturableLayer = cLayer;
            terrainLayer = tLayer;
            screenFadeUI = fade; // 临时渐变效果的UI组件
        }

        public PhotoData SavePhoto(List<CapturedPropData> propsInView, TerrainIntersectionData terrainIntersection)
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

            return new PhotoData(photoImage, propsInView, terrainIntersection);
        }

        // 屏幕淡入淡出效果
        // 临时效果，后续可能更改
        public IEnumerator FadeScreen(bool fadeIn, float duration)
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
