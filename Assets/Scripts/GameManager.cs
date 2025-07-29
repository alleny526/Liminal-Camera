using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameObject player;

    public Vector3 levelOffset = new Vector3(0, 0, 500f); // 临时设定的新关卡偏移量

    private Transform currentLevelRoot;
    private List<Transform> activeLevels = new List<Transform>();
    private List<GameObject> hiddenProps = new List<GameObject>();

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
        currentLevelRoot = LevelGenerator.Instance.SpawnLevel(Vector3.zero);
        player.transform.position = LevelGenerator.Instance.GetRandomPositionInLevel(currentLevelRoot);
        player.transform.rotation = Quaternion.LookRotation(currentLevelRoot.position - player.transform.position);
        player.GetComponent<PlayerInteraction>().playerInitLevelPosition = player.transform.position;
    }

    void Update()
    {
        // 检测R键重置关卡
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }
    }

    // 当门被打开时，创建并链接一个新关卡
    public void LinkNewLevel(Door entryDoor)
    {
        Vector3 newLevelPosition = currentLevelRoot.position + levelOffset;

        Transform newLevelRoot = LevelGenerator.Instance.SpawnLevel(newLevelPosition);
        if (newLevelRoot == null)
        {
            return;
        }

        activeLevels.Add(newLevelRoot);

        entryDoor.SetTargetLevel(newLevelRoot);
    }

    // 玩家传送完成后，切换当前关卡并清理旧关卡
    public void OnPlayerTeleported(Transform newLevelRoot)
    {
        Transform oldLevelRoot = currentLevelRoot;

        currentLevelRoot = newLevelRoot;

        if (oldLevelRoot != null && oldLevelRoot != newLevelRoot)
        {
            CleanupLevel(oldLevelRoot);
            hiddenProps.Clear();
        }
    }

    // 清理指定关卡的所有内容
    private void CleanupLevel(Transform levelRoot)
    {
        if (levelRoot == null) return;

        if (levelRoot == currentLevelRoot)
        {
            return;
        }

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

    public void RestartLevel()
    {
        if (currentLevelRoot == null)
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

        player.GetComponent<CharacterController>().enabled = false;
        player.transform.position = player.GetComponent<PlayerInteraction>().playerInitLevelPosition;
        player.transform.rotation = Quaternion.LookRotation(currentLevelRoot.position - player.transform.position);
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
}
