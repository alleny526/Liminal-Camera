using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Vector3 levelOffset = new Vector3(0, 0, 500f); // 临时设定的新关卡偏移量

    private Transform currentLevelRoot;
    private List<Transform> activeLevels = new List<Transform>();

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
}
