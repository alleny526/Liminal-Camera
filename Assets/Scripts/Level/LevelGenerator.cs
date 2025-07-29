using UnityEngine;
using System.Collections.Generic;

// 关卡生成器，负责根据蓝图生成关卡内容
public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator Instance;

    [Header("关卡蓝图")]
    public List<LevelBlueprint> levelBlueprints;
    
    [Header("避让检查次数设置")]
    public int maxAttempts = 50;
    public int doorMaxAttempts = 100;

    private int levelEntryCount = 0; // 记录总关卡进入次数，用作关卡蓝图索引
    private int parkEntryCount = 0; // 记录公园进入次数，后续和公园生成随机性相关
    private int forestEntryCount = 0;
    
    private GameObject cachedLayerRoot;
    private GameObject cachedTerrain;

    // 已生成Prop的信息（位置、影响半径等）
    [System.Serializable]
    private class SpawnedPropInfo
    {
        public Vector3 position;
        public float radius;
        public GameObject gameObject;

        public SpawnedPropInfo(Vector3 pos, float rad, GameObject obj)
        {
            position = pos;
            radius = rad;
            gameObject = obj;
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 先预留Scene跳转不摧毁实例
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 生成关卡
    public Transform SpawnLevel(Vector3 pos)
    {
        if (levelBlueprints == null || levelBlueprints.Count == 0)
        {
            return null;
        }

        // 循环使用关卡蓝图 -- 根据关卡进入次数，关卡布置的随机规则也会有区别
        // 可能后期考虑随机化？
        // TODO: 目前是同scene下创建不同关卡（利用levelRoot） -- 后期是否考虑新建scene？
        int curIndex = levelEntryCount % levelBlueprints.Count;
        LevelBlueprint blueprint = levelBlueprints[curIndex];

        GameObject levelRootObj = new GameObject("LevelRoot_" + blueprint.levelType + "_" + pos);
        cachedLayerRoot = levelRootObj;
        Transform levelRoot = levelRootObj.transform;
        levelRoot.position = pos;

        // 创建一个专门用于存放玩家生成内容的子对象
        // TODO: 在多处判空时会在各处新建，看是否能整合
        GameObject playerGeneratedContainer = new GameObject("PlayerGeneratedContent");
        playerGeneratedContainer.transform.SetParent(levelRoot);
        playerGeneratedContainer.transform.localPosition = Vector3.zero;

        // 增加关卡进入次数
        levelEntryCount++;

        // 根据蓝图的关卡类型生成关卡
        switch (blueprint.levelType)
        {
            case LevelType.Forest:
                forestEntryCount++;
                GenerateForest(blueprint, levelRoot);
                break;
            case LevelType.Park:
                parkEntryCount++; // 记录公园进入次数
                GeneratePark(blueprint, levelRoot);
                break;
            default:
                break;
        }
        return levelRoot;
    }

    // 生成公园关卡
    // 每个关卡都有自己的特殊生成逻辑，但同时会使用随机生成方法
    private void GeneratePark(LevelBlueprint blueprint, Transform levelRoot)
    {
        // 生成地形
        float terrainRadius = GenerateTerrain(blueprint, levelRoot);

        // 用于跟踪已生成Prop的信息，防止重叠
        List<SpawnedPropInfo> spawnedProps = new List<SpawnedPropInfo>();

        // 生成POI
        // 位置固定为关卡中心
        if (blueprint.poiPrefabs != null)
        {
            GameObject poiInstance = Instantiate(blueprint.poiPrefabs[0], levelRoot.position, Quaternion.identity, levelRoot);

            float poiRadius = GetPropRadius(poiInstance);
            spawnedProps.Add(new SpawnedPropInfo(levelRoot.position, poiRadius, poiInstance));
        }

        // 生成规律固定的石头线条
        if (blueprint.largePropPrefabs.Length > 0)
        {
            int lineCount = 2 + parkEntryCount; // 石头线条数量和公园进入次数挂钩 -- 简单的次数使用案例
            for (int i = 1; i <= lineCount; i++)
            {
                float angle = 360f / lineCount * i;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                for (int j = 1; j < 4; j++)
                {
                    float distance = terrainRadius * (j / 4f) + Random.Range(-2f, 2f);
                    Vector3 rockPos = levelRoot.position + direction * distance;
                    rockPos.y = levelRoot.position.y;

                    GameObject rockInstance = Instantiate(blueprint.largePropPrefabs[0], rockPos, Quaternion.Euler(0, Random.Range(0, 360), 0), levelRoot);
                    float rockRadius = GetPropRadius(rockInstance);
                    spawnedProps.Add(new SpawnedPropInfo(rockPos, rockRadius, rockInstance));
                }
            }
        }

        // 根据blueprint的largePropCount生成额外的随机大型Prop
        // 0.8f就是个magic number，稍微缩小生成范围防止模型突出到地图外
        int successfulLargeProps = GenerateRandomProps(blueprint.largePropPrefabs, blueprint.largePropCount,
            levelRoot, terrainRadius * 0.8f, spawnedProps, blueprint.largePropSafeDistance);

        // 同上，生成小型Prop
        int successfulSmallProps = GenerateRandomProps(blueprint.smallPropPrefabs, blueprint.smallPropCount,
            levelRoot, terrainRadius, spawnedProps, blueprint.smallPropSafeDistance);

        // 在随机位置生成门，确保不与其他Prop重叠
        bool doorPlaced = GenerateDoor(blueprint, levelRoot, terrainRadius * 0.9f, spawnedProps);
    }

    // 生成森林关卡
    private void GenerateForest(LevelBlueprint blueprint, Transform levelRoot)
    {
        // 生成地形
        float terrainRadius = GenerateTerrain(blueprint, levelRoot);

        // 用于跟踪已生成Prop的信息，防止重叠
        List<SpawnedPropInfo> spawnedProps = new List<SpawnedPropInfo>();

        GenerateRandomProps(blueprint.poiPrefabs, blueprint.poiCount, levelRoot, terrainRadius * 0.8f, spawnedProps, blueprint.poiSafeDistance);
        GenerateRandomProps(blueprint.largePropPrefabs, blueprint.largePropCount, levelRoot, terrainRadius * 0.8f, spawnedProps, blueprint.largePropSafeDistance);
        GenerateRandomProps(blueprint.smallPropPrefabs, blueprint.smallPropCount, levelRoot, terrainRadius, spawnedProps, blueprint.smallPropSafeDistance);
    }

    // 生成地形
    private float GenerateTerrain(LevelBlueprint blueprint, Transform levelRoot)
    {
        GameObject terrainInstance = Instantiate(blueprint.terrainPrefab, levelRoot.position, Quaternion.identity, levelRoot);
        cachedTerrain = Instantiate(terrainInstance, levelRoot);
        cachedTerrain.SetActive(false);
        float terrainRadius = terrainInstance.GetComponentInChildren<Renderer>().bounds.extents.x;
        return terrainRadius;
    }

    // 随机生成指定数量指定类型Prop
    private int GenerateRandomProps(GameObject[] propPrefabs, int propCount, Transform levelRoot,
        float spawnRadius, List<SpawnedPropInfo> spawnedProps, float safeDistance)
    {
        if (propPrefabs == null || propPrefabs.Length == 0 || propCount <= 0)
        {
            return 0;
        }

        int successfulPropsCount = 0;
        for (int i = 0; i < propCount; i++)
        {
            // 预先选择一个Prop预制体来计算其大小
            int propIndex = Random.Range(0, propPrefabs.Length);
            GameObject selectedPrefab = propPrefabs[propIndex];

            // TODO: 每个都实例化会不会有性能问题？考虑循环前每个prop先有个临时实例，最后一并销毁。
            GameObject tempInstance = Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity);
            float propRadius = GetPropRadius(tempInstance);
            DestroyImmediate(tempInstance);

            // 尝试找到一个安全的位置
            Vector3 propPos = FindSafePosition(levelRoot.position, spawnRadius, spawnedProps, safeDistance, propRadius);

            if (propPos != Vector3.zero)
            {
                GameObject propInstance = Instantiate(selectedPrefab, propPos, Quaternion.Euler(0, Random.Range(0, 360), 0), levelRoot);

                // 更新跟踪列表
                spawnedProps.Add(new SpawnedPropInfo(propPos, propRadius, propInstance));
                successfulPropsCount++;
            }
        }

        return successfulPropsCount;
    }

    // 生成门
    private bool GenerateDoor(LevelBlueprint blueprint, Transform levelRoot, float spawnRadius, List<SpawnedPropInfo> spawnedProps)
    {
        if (blueprint.doorPrefab == null)
        {
            return false;
        }

        // 找一个安全的位置
        Vector3 doorPos = FindSafePosition(levelRoot.position, spawnRadius, spawnedProps, blueprint.doorSafeDistance, 1.0f);

        // 确定位置和旋转后，生成门
        // 门总是朝向关卡中心
        if (doorPos != Vector3.zero)
        {
            Vector3 lookDirection = -(levelRoot.position - doorPos).normalized;
            Quaternion doorRotation = Quaternion.LookRotation(lookDirection);

            GameObject doorInstance = Instantiate(blueprint.doorPrefab, doorPos, doorRotation, levelRoot);
            spawnedProps.Add(new SpawnedPropInfo(doorPos, 1.0f, doorInstance));
            return true;
        }
        return false;
    }

    // 查找安全位置
    // 在指定范围内随机寻找一个不与其他Prop重叠的位置
    // 如果找不到合适位置，则返回Vector3.zero
    private Vector3 FindSafePosition(Vector3 centerPos, float spawnRadius, List<SpawnedPropInfo> spawnedProps, float safeDistance, float propRadius)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomPos2D = Random.insideUnitCircle * spawnRadius;
            Vector3 candidatePos = new Vector3(centerPos.x + randomPos2D.x, centerPos.y, centerPos.z + randomPos2D.y);

            // 针对凹凸地形，利用射线判断位置y值
            RaycastHit hit;
            Vector3 rayStart = candidatePos + Vector3.up * 5f;
            if (Physics.Raycast(rayStart, Vector3.down, out hit, 15f))
            {
                candidatePos.y = hit.point.y;
                if (hit.collider != null && hit.collider.GetComponentInParent<Prop>() != null)
                {
                    continue;
                }
            }

            if (!IsPositionOverlapping(candidatePos, propRadius, spawnedProps, safeDistance))
            {
                if (!IsPositionSafeWithColliders(candidatePos, propRadius))
                {
                    continue;
                }

                return candidatePos;
            }
        }
        return Vector3.zero;
    }
    
    // 获取Prop的半径（用于碰撞检测）
    private float GetPropRadius(GameObject obj)
    {
        if (obj == null) return 0f;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 1f;

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            // 合并所有渲染器的包围盒
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 size = combinedBounds.size;
        return Mathf.Max(size.x, size.z) * 0.5f;
    }

    // 检查新位置是否与已生成Prop重叠
    // 此方法仅用位置+半径确定
    private bool IsPositionOverlapping(Vector3 newPosition, float newPropRadius, List<SpawnedPropInfo> spawnedProps, float safeDistance)
    {
        foreach (SpawnedPropInfo existingObj in spawnedProps)
        {
            float distance = Vector3.Distance(newPosition, existingObj.position);
            float requiredDistance = newPropRadius + existingObj.radius + safeDistance + 0.5f;
            if (distance < requiredDistance)
            {
                return true;
            }
        }
        return false;
    }

    // 根据Collider判断是否重叠
    // Radius也许不可靠，但重叠似乎也符合游戏整体调性
    private bool IsPositionSafeWithColliders(Vector3 newPosition, float newPropRadius)
    {
        Collider[] overlapping = Physics.OverlapSphere(newPosition, newPropRadius + 0.5f);

        foreach (Collider col in overlapping)
        {
            // if (col.isTrigger) continue;
            // if (col.gameObject.layer == LayerMask.NameToLayer("Terrain")) continue;
            if (col.gameObject.tag != "Prop") continue;

            return false;
        }

        return true;
    }

    // 获取指定关卡中的随机安全位置（用于传送）
    public Vector3 GetRandomPositionInLevel(Transform levelRoot, float playerRadius = 0.5f)
    {
        if (levelRoot == null) return Vector3.zero;

        Renderer terrainRenderer = levelRoot.GetComponentInChildren<Renderer>();
        if (terrainRenderer == null)
        {
            return levelRoot.position + new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f));
        }

        float terrainRadius = terrainRenderer.bounds.extents.x;
        
        List<SpawnedPropInfo> spawnedProps = new List<SpawnedPropInfo>();
        Collider[] allColliders = levelRoot.GetComponentsInChildren<Collider>();
        
        // 这里是取新关卡的所有生成Prop集合
        foreach (Collider col in allColliders)
        {
            // if (col.isTrigger) continue;
            // if (col.gameObject.layer == LayerMask.NameToLayer("Terrain")) continue;
            if (col.gameObject.tag != "Prop") continue;

            float objRadius = GetPropRadius(col.gameObject);
            spawnedProps.Add(new SpawnedPropInfo(col.transform.position, objRadius, col.gameObject));
        }

        Vector3 safePosition = FindSafePosition(levelRoot.position, terrainRadius * 0.8f, spawnedProps, 2.0f, playerRadius);
        
        if (safePosition == Vector3.zero)
        {
            safePosition = levelRoot.position;
        }

        return safePosition;
    }

    // 重新生成地形（基于缓存的数据）
    public GameObject RegenerateTerrain(Transform parent)
    {
        if (cachedTerrain == null)
        {
            return null;
        }

        GameObject newTerrain = Instantiate(cachedTerrain, parent);
        newTerrain.SetActive(true);
        
        return newTerrain;
    }
}
