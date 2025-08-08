using UnityEngine;

// 画框类，用于关卡间传送，通过绘画系统激活
public class Frame : Prop
{
    [Header("画框设置")]
    public Renderer portalRenderer;
    public Collider portalCollider;

    [Header("交互提示")]
    public GameObject interactionPrompt;

    [HideInInspector]
    public Transform targetLevel;

    [HideInInspector]
    public Vector3 virtualCameraPosition;

    private Camera portalCamera;
    private RenderTexture renderTexture;
    private bool isActivated = false;
    private bool isPlayerNearby = false;

    [HideInInspector]
    public bool canEnter = false;

    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    void Start()
    {
        // 设置画框的传送门相机
        GameObject portalCamObj = new GameObject("PortalCamera");
        portalCamObj.tag = "PortalCamera";
        portalCamera = portalCamObj.AddComponent<Camera>();
        portalCamera.enabled = false;
        portalCamera.transform.SetParent(transform);

        renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        portalCamera.targetTexture = renderTexture;

        if (portalRenderer != null)
        {
            portalRenderer.gameObject.SetActive(false);
            portalRenderer.material.mainTexture = renderTexture;
            portalRenderer.receiveShadows = false;
            portalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        if (portalCollider != null)
        {
            PortalTeleporter teleporter = portalCollider.gameObject.AddComponent<PortalTeleporter>();
            teleporter.Initialize(this);
        }
    }

    void Update()
    {
        // 检测E键交互
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E) && !isActivated)
        {
            Interact(GameObject.FindGameObjectWithTag("Player"));
        }
    }

    void LateUpdate()
    {
        if (!isActivated || portalCamera == null || targetLevel == null) return;

        // 检查玩家是否在画框的正面
        Vector3 viewDirection = mainCamera.transform.position - transform.position;
        float dotProduct = Vector3.Dot(transform.forward, viewDirection);

        if (isActivated && portalRenderer != null)
        {
            portalRenderer.gameObject.SetActive(true);
        }

        int curDotSign = dotProduct > 0 ? 1 : -1;

        if (curDotSign == -1 && portalRenderer.transform.localEulerAngles.y != 0.0f)
        {
            portalRenderer.transform.localEulerAngles = new Vector3(0.0f, 0.0f, 0.0f);
        }
        
        // 计算玩家相机相对于当前画框的位置和旋转
        Vector3 relativePosition = transform.InverseTransformPoint(mainCamera.transform.position);
        Quaternion relativeRotation = Quaternion.Inverse(transform.rotation) * mainCamera.transform.rotation;

        // 在目标关卡中应用相同的相对变换
        Vector3 portalCamPosition = virtualCameraPosition + Vector3.forward * relativePosition.z + Vector3.right * relativePosition.x + Vector3.up * relativePosition.y + Vector3.up * 2.0f;
        Quaternion portalCamRotation = relativeRotation;
        
        portalCamera.transform.SetPositionAndRotation(portalCamPosition, portalCamRotation);

        portalCamera.fieldOfView = mainCamera.fieldOfView;
        portalCamera.aspect = 1.0f; // 画框的宽高比
        portalCamera.nearClipPlane = 0.1f;

        portalCamera.Render();
    }

    // 画框的交互逻辑，开启关卡生成流程
    public override void Interact(GameObject interactor)
    {
        if (isActivated) return;

        // 隐藏交互提示
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        // 通过GameManager触发关卡生成
        GameManager.Instance.SpawnNewLevel(this);
    }

    // 设置传送门的目标关卡
    public void SetTargetLevel(Transform level)
    {
        targetLevel = level;
        
        if (level != null)
        {
            virtualCameraPosition = LevelGenerator.Instance.GetRandomPositionInLevel(level);
            isActivated = true;
            canEnter = true;
            canCapture = false;
        }
    }

    // 传送玩家到目标关卡
    public void Teleport(Transform player)
    {
        if (!isActivated || targetLevel == null) return;

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

    // 玩家进入交互范围
    public void OnTriggerEnter(Collider other)
    {
        if (!isActivated && other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            if (interactionPrompt != null)
                interactionPrompt.SetActive(true);
        }
    }

    // 玩家离开交互范围
    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);
        }
    }
}