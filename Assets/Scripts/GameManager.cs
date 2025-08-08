using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameObject player;

    public Vector3 levelOffset = new Vector3(0, 0, 500f);

    public Canvas gameEndCanvas;

    private Transform currentLevelRoot;
    private Transform firstLevelRoot;
    private List<Transform> activeLevels = new List<Transform>();
    private List<GameObject> hiddenProps = new List<GameObject>();
    
    // 画框绘画系统状态
    private Frame pendingFrame = null;
    private bool waitingForPaintedLevel = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 第一关特殊化处理
        GameObject firstLevelObject = GameObject.Find("FirstLevel"); 
        if (firstLevelObject != null)
        {
            firstLevelRoot = firstLevelObject.transform;
            currentLevelRoot = firstLevelRoot;
        }
        player.GetComponent<PlayerInteraction>().playerInitLevelPosition = player.transform.position;
        
        if (gameEndCanvas != null)
        {
            gameEndCanvas.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // 检测R键重置关卡
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (IsAtFirstLevel())
            {
                return;
            }
            
            RestartLevel();
        }
    }

    public bool IsAtFirstLevel()
    {
        return firstLevelRoot != null && currentLevelRoot == firstLevelRoot;
    }

    // 画框激活时开始绘画流程
    public void SpawnNewLevel(Frame entryFrame)
    {
        pendingFrame = entryFrame;
        waitingForPaintedLevel = true;
        
        SetPlayerControlEnabled(false);
        
        Vector3 newLevelPosition = currentLevelRoot.position + levelOffset;
        LevelGenerator.Instance.SpawnLevel(newLevelPosition);
    }

    // 绘画完成后处理新关卡
    public void OnPaintedLevelGenerated(Transform newLevelRoot)
    {
        if (!waitingForPaintedLevel || pendingFrame == null) return;
        
        waitingForPaintedLevel = false;
        
        if (newLevelRoot != null)
        {
        activeLevels.Add(newLevelRoot);
            pendingFrame.SetTargetLevel(newLevelRoot);
        }
        
        pendingFrame = null;
        SetPlayerControlEnabled(true);
    }

    // 设置玩家控制是否启用
    public void SetPlayerControlEnabled(bool enabled)
    {
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.enabled = enabled;
        }

        PlayerInteraction playerInteraction = player.GetComponent<PlayerInteraction>();
        if (playerInteraction != null)
        {
            playerInteraction.enabled = enabled;
        }

        if (enabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // 玩家传送完成后，切换当前关卡并清理旧关卡
    public void OnPlayerTeleported(Transform newLevelRoot)
    {
        Transform oldLevelRoot = currentLevelRoot;

        currentLevelRoot = newLevelRoot;

        bool isReturningToFirstLevel = (newLevelRoot == firstLevelRoot);
        
        if (oldLevelRoot != null && oldLevelRoot != newLevelRoot && !isReturningToFirstLevel)
        {
            CleanupLevel(oldLevelRoot);
            hiddenProps.Clear();
        }

        if (LevelGenerator.Instance != null)
        {
            LevelGenerator.Instance.IncrementLevelEntryCount();
        }
    }

    // 清理指定关卡的所有内容
    private void CleanupLevel(Transform levelRoot)
    {
        if (levelRoot == null || levelRoot == currentLevelRoot || levelRoot == firstLevelRoot) return;

        activeLevels.Remove(levelRoot);
        
        Destroy(levelRoot.gameObject);
    }

    // 获取当前关卡中的玩家生成内容容器
    public Transform GetPlayerGeneratedContentContainer()
    {
        if (currentLevelRoot == null) return null;

        Transform container = currentLevelRoot.Find("PlayerGeneratedContent");
        if (container == null)
        {
            GameObject containerObj = new GameObject("PlayerGeneratedContent");
            containerObj.transform.SetParent(currentLevelRoot);
            containerObj.transform.localPosition = Vector3.zero;
            container = containerObj.transform;
        }

        return container;
    }

    // 重新开始当前关卡
    public void RestartLevel()
    {
        if (currentLevelRoot == null) return;
        
        if (IsAtFirstLevel())
        {
            return;
        }
        
        // 检查是否正在绘画中，如果是则阻止重置
        if (PaintingSystem.Instance != null && PaintingSystem.Instance.IsInPaintingMode())
        {
            return;
        }

        Transform playerContent = currentLevelRoot.Find("PlayerGeneratedContent");
        if (playerContent != null)
        {
            Destroy(playerContent.gameObject);
        }

        RestoreAllPropVisibility(currentLevelRoot);

        GameObject currentTerrain = GameObject.FindWithTag("Terrain");
        if (currentTerrain != null)
        {
            Destroy(currentTerrain);
        }

        GameObject newTerrain = LevelGenerator.Instance.RegenerateTerrain(currentLevelRoot);
        if (newTerrain != null)
        {
            newTerrain.name = "Terrain";
        }

        LevelGenerator.Instance.SetFrameGenerated(false);

        player.GetComponent<CharacterController>().enabled = false;
        player.transform.position = player.GetComponent<PlayerInteraction>().playerInitLevelPosition;
        Quaternion restartRotation = Quaternion.LookRotation(currentLevelRoot.position - player.transform.position);
        restartRotation.x = 0;
        restartRotation.z = 0;
        player.transform.rotation = restartRotation;
        player.GetComponent<CharacterController>().enabled = true;
    }

    // 恢复关卡中所有prop的可见性
    private void RestoreAllPropVisibility(Transform levelRoot)
    {
        if (levelRoot == null) return;

        foreach (GameObject propObject in hiddenProps)
        {
            propObject.SetActive(true);
        }

        hiddenProps.Clear();
    }

    public void AddHiddenProp(GameObject propObj)
    {
        if (propObj != null && !hiddenProps.Contains(propObj))
        {
            hiddenProps.Add(propObj);
        }
    }

    public void ShowGameEndUI()
    {
        if (gameEndCanvas != null)
        {
            gameEndCanvas.gameObject.SetActive(true);
            
            SetPlayerControlEnabled(false);
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}