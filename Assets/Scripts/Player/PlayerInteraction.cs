using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using LiminalCamera.Photo;

// 玩家交互系统，处理拍照、放置照片和Prop交互
public class PlayerInteraction : MonoBehaviour
{
    [Header("相机引用")]
    public Camera mainCamera;
    public Camera photoCamera;

    [Header("交互设置")]
    public float interactionDistance = 3f;
    public LayerMask interactionLayer;

    [Header("拍摄参数")]
    public LayerMask capturableLayer;
    public LayerMask terrainLayer;
    public float frustumHeight = 50f;
    public float frustumBottomWidth = 16f;
    public float frustumBottomHeight = 9f;
    
    [Header("缩放设置")]
    public float minFOV = 30f;
    public float maxFOV = 80f;
    public float minFrustumHeight = 30f;
    public float maxFrustumHeight = 80f;
    public float zoomSensitivity = 10f;
    public float minPlacementDistance = 5f;
    public float maxPlacementDistance = 100f;

    [Header("UI元素")]
    public GameObject aimUI;
    public RawImage heldPhotoUI;
    public RawImage placementUI;
    public Image screenFadeUI;

    // 内部可变状态
    private PhotoData heldPhoto = null;
    private bool isPlacing = false;
    private float currentFOV;
    private float currentFrustumHeight;
    private float placementDistance = 10f;

    // 功能模块
    private PhotoCapturer photoCapturer;
    private PhotoPlacer photoPlacer;
    private PhotoUtil photoUtil;

    void Start()
    {
        // UI初始化
        aimUI.SetActive(false);
        heldPhotoUI.gameObject.SetActive(false);
        placementUI.gameObject.SetActive(false);
        if (screenFadeUI != null) screenFadeUI.color = new Color(0, 0, 0, 0);
        
        // 状态初始化
        if (photoCamera != null)
        {
            currentFOV = photoCamera.fieldOfView;
            currentFrustumHeight = frustumHeight;
            placementDistance = (minPlacementDistance + maxPlacementDistance) / 2f;
        }

        // 功能模块初始化
        photoCapturer = new PhotoCapturer(photoCamera, capturableLayer, terrainLayer, currentFrustumHeight, frustumBottomWidth, frustumBottomHeight);
        photoPlacer = new PhotoPlacer(mainCamera, placementDistance);
        photoUtil = new PhotoUtil(mainCamera, photoCamera, capturableLayer, terrainLayer, screenFadeUI);
    }

    void Update()
    {
        HandlePhotoInput();
        HandleInteractionInput();
    }

    // 处理玩家与可交互Prop的交互输入 -- 目前只有门
    // 由于门asset的交互是E键，所以就用E键交互
    // 但是门asset自带脚本是用的trigger collider，后续看看能否统一
    private void HandleInteractionInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            RaycastHit hit;
            if (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit, interactionDistance, interactionLayer))
            {
                Prop interactable = hit.collider.GetComponentInParent<Prop>();
                if (interactable != null)
                {
                    interactable.Interact(gameObject);
                }
            }
        }
    }

    // 处理拍摄阶段的缩放
    // 调整相机FOV和锥体高度
    private void HandleCaptureZoom(float scrollDelta)
    {
        // 更新FOV (滚轮向上减小FOV实现放大效果)
        currentFOV -= scrollDelta * zoomSensitivity;
        currentFOV = Mathf.Clamp(currentFOV, minFOV, maxFOV);
        photoCamera.fieldOfView = currentFOV;
        
        // 根据FOV计算锥体高度 (FOV越小，锥体高度越大)
        float normalizedFOV = (currentFOV - minFOV) / (maxFOV - minFOV);
        currentFrustumHeight = Mathf.Lerp(maxFrustumHeight, minFrustumHeight, normalizedFOV);
        Debug.Log("Current Frustum Height: " + currentFrustumHeight);
        
        if (photoCapturer != null)
            photoCapturer.UpdateFrustumParameters(currentFrustumHeight);
    }

    // 处理放置阶段的缩放
    // 调整放置距离和UI缩放
    private void HandlePlacementZoom(float scrollDelta)
    {
        // 更新放置距离 (滚轮向上减小距离)
        placementDistance -= scrollDelta * 2f;
        placementDistance = Mathf.Clamp(placementDistance, minPlacementDistance, maxPlacementDistance);
        
        if (placementUI != null)
        {
            // 根据放置距离计算UI缩放 (距离越近，UI越大)
            float normalizedDistance = (placementDistance - minPlacementDistance) / (maxPlacementDistance - minPlacementDistance);
            float uiScale = Mathf.Lerp(2.5f, 0.8f, normalizedDistance);
            placementUI.transform.localScale = Vector3.one * uiScale;
        }
        
        if (photoPlacer != null)
            photoPlacer.UpdatePlacementDistance(placementDistance);
    }

    private void HandlePhotoInput()
    {
        photoCamera.transform.rotation = mainCamera.transform.rotation;

        // 处理鼠标中键滚轮缩放
        if (Input.mouseScrollDelta.y != 0)
        {
            if (aimUI.activeSelf) // 拍摄阶段
            {
                HandleCaptureZoom(Input.mouseScrollDelta.y);
            }
            else if (isPlacing) // 放置阶段
            {
                HandlePlacementZoom(Input.mouseScrollDelta.y);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (heldPhoto == null)
            {
                aimUI.SetActive(true);
                photoCamera.gameObject.SetActive(true);
                photoCamera.enabled = true;
                mainCamera.enabled = false;
                photoCamera.fieldOfView = currentFOV;
            }
            else
            {
                isPlacing = true;
                placementUI.gameObject.SetActive(true);
                heldPhotoUI.gameObject.SetActive(false);
            }
        }

        if (Input.GetMouseButtonUp(1))
        {
            photoCamera.gameObject.SetActive(false);
            photoCamera.enabled = false;
            mainCamera.enabled = true;
            mainCamera.gameObject.SetActive(true);

            if (heldPhoto == null)
            {
                aimUI.SetActive(false);
            }
            else
            {
                isPlacing = false;
                placementUI.gameObject.SetActive(false);
                heldPhotoUI.gameObject.SetActive(true);
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (aimUI.activeSelf)
            {
                StartCoroutine(TakePhotoSequence());
            }
            else if (isPlacing)
            {
                PlacePhoto();
            }
        }
    }

    private IEnumerator TakePhotoSequence()
    {
        aimUI.SetActive(false);
        if (screenFadeUI != null) yield return StartCoroutine(photoUtil.FadeScreen(true, 0.2f));

        // 捕获Props
        List<CapturedPropData> propsInView = photoCapturer.CapturePropsInView();

        // 捕获地形
        TerrainIntersectionData terrainIntersection = photoCapturer.CaptureTerrainIntersectionData();

        // 保存照片
        heldPhoto = photoUtil.SavePhoto(propsInView, terrainIntersection);
        
        heldPhotoUI.texture = heldPhoto.photoImage;
        placementUI.texture = heldPhoto.photoImage;
        heldPhotoUI.gameObject.SetActive(true);

        photoCamera.gameObject.SetActive(false);
        photoCamera.enabled = false;
        mainCamera.enabled = true;
        mainCamera.gameObject.SetActive(true);

        if (screenFadeUI != null) yield return StartCoroutine(photoUtil.FadeScreen(false, 0.3f));
    }

    private void PlacePhoto()
    {
        isPlacing = false;
        placementUI.gameObject.SetActive(false);
        heldPhotoUI.gameObject.SetActive(false);

        if (GameManager.Instance != null)
        {
            Transform playerGeneratedContainer = GameManager.Instance.GetPlayerGeneratedContentContainer();
            if (playerGeneratedContainer != null)
            {
                photoPlacer.SetParentTransform(playerGeneratedContainer);
            }
        }

        // 放置照片
        photoPlacer.PlacePhoto(heldPhoto);
        
        heldPhoto = null;
    }
}
