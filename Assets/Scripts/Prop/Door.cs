using UnityEngine;

// 传送门门类，用于关卡间传送
public class Door : Prop
{
    [Header("传送门设置")]
    public Renderer portalScreen;

    public Collider portalTrigger;

    [HideInInspector]
    public Transform targetLevel;

    [HideInInspector]
    public Vector3 virtualCameraPosition;

    private Camera portalCamera;
    private RenderTexture renderTexture;
    private bool isOpen = false;

    [HideInInspector]
    public bool canEnter = false;

    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;

        if (portalScreen != null)
        {
            portalScreen.gameObject.SetActive(false);

            GameObject portalCamObj = new GameObject("PortalCamera");
            portalCamObj.tag = "PortalCamera";
            portalCamera = portalCamObj.AddComponent<Camera>();
            portalCamera.enabled = false;

            renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
            portalCamera.targetTexture = renderTexture;
            portalScreen.material.mainTexture = renderTexture;
            portalScreen.receiveShadows = false;
            portalScreen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        if (portalTrigger != null)
        {
            PortalTeleporter teleporter = portalTrigger.gameObject.AddComponent<PortalTeleporter>();
            teleporter.Initialize(this);
        }
    }

    void LateUpdate()
    {
        if (!isOpen || portalCamera == null || targetLevel == null) return;

        // 检查玩家是否在门的正面 -- 门prefab模型的正反在游戏内反转了
        Vector3 viewDirection = mainCamera.transform.position - transform.position;
        float dotProduct = Vector3.Dot(transform.forward, viewDirection);

        if (dotProduct > 0)
        {
            if (portalScreen.gameObject.activeSelf)
            {
                portalScreen.gameObject.SetActive(false);
            }
            return;
        }
        if (!portalScreen.gameObject.activeSelf)
        {
            portalScreen.gameObject.SetActive(true);
        }
        
        // 计算玩家相机相对于当前门的位置和旋转
        Vector3 relativePosition = transform.InverseTransformPoint(mainCamera.transform.position);
        Quaternion relativeRotation = Quaternion.Inverse(transform.rotation) * mainCamera.transform.rotation;

        // 在目标关卡中应用相同的相对变换
        Vector3 portalCamPosition = virtualCameraPosition + Vector3.forward * relativePosition.z + Vector3.right * relativePosition.x + Vector3.up * relativePosition.y;
        Quaternion portalCamRotation = relativeRotation;
        
        portalCamera.transform.SetPositionAndRotation(portalCamPosition, portalCamRotation);

        portalCamera.fieldOfView = mainCamera.fieldOfView;
        portalCamera.aspect = 1.0f / 2.3f; // 门的宽高比
        portalCamera.nearClipPlane = 0.1f;

        portalCamera.Render();
    }

    // 门的交互逻辑，开启传送门
    public override void Interact(GameObject interactor)
    {
        if (isOpen) return;

        isOpen = true;
        canEnter = true; // 若后续决定需要正面才能进门，则需要修改canEnter逻辑
        canCapture = false;
        
        GameManager.Instance.LinkNewLevel(this);
    }

    // 设置传送门的目标关卡
    public void SetTargetLevel(Transform level)
    {
        targetLevel = level;
        
        if (level != null)
        {
            virtualCameraPosition = LevelGenerator.Instance.GetRandomPositionInLevel(level);
        }
    }

    // 传送玩家到目标关卡
    public void Teleport(Transform player)
    {
        if (!isOpen || targetLevel == null) return;

        CharacterController playerController = player.GetComponent<CharacterController>();
        bool hadController = playerController != null;
        if (hadController)
        {
            playerController.enabled = false;
        }

        // 计算传送位置和旋转
        Vector3 relativePosition = transform.InverseTransformPoint(player.position);
        Quaternion relativeRotation = Quaternion.Inverse(transform.rotation) * player.rotation;
        relativeRotation.x = 0;
        relativeRotation.z = 0;

        Vector3 newPosition = virtualCameraPosition + (Vector3.forward * relativePosition.z + Vector3.right * relativePosition.x + Vector3.up * relativePosition.y);     
        Quaternion newRotation = relativeRotation;
        
        // 使用射线检测找到实际的地面位置
        RaycastHit hit;
        Vector3 rayStart = newPosition + Vector3.up * 5f;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 15f))
        {
            newPosition.y = hit.point.y;
            if (hadController)
            {
                newPosition.y += playerController.height * 0.5f + playerController.skinWidth;
            }
        }
        
        // 设置玩家位置和旋转
        player.position = newPosition;
        player.rotation = newRotation;
        player.GetComponent<PlayerInteraction>().playerInitLevelPosition = newPosition;
        
        if (hadController)
        {
            playerController.enabled = true;

            if (player.GetComponent<PlayerController>() != null)
            {
                player.GetComponent<PlayerController>().ResetVelocity();
            }
        }

        // 通知GameManager玩家已传送，清理旧关卡
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTeleported(targetLevel);
        }
    }
}