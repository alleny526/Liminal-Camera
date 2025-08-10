using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Prop放置信息的数据结构
[System.Serializable]
public class PropPlacementData
{
    public PropType propType;
    public Vector2 normalizedPosition;
    public float density;
}

// 线条和Prop的映射关系
[System.Serializable]
public class LinePropsMapping
{
    public int lineIndex; // 主要靠lineIndex来区分线条
    public List<GameObject> props = new List<GameObject>();
    public int maxPlaceableProps = 0; // 用于编辑模式

    public LinePropsMapping(int index) { lineIndex = index; }

}

// 关卡生成器，负责根据蓝图生成关卡内容
public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator Instance;

    [Header("关卡蓝图")]
    public List<LevelBlueprint> levelBlueprints;
    [Header("绘画系统")]
    public PaintingSystem paintingSystem;
    [Header("避让检查次数设置")]
    public int maxAttempts = 50;

    private int levelEntryCount = 0; // 记录总关卡进入次数，用作关卡蓝图索引
    private bool waitingForFramePainting = false;
    private LevelBlueprint pendingBlueprint;
    private Transform levelRoot;
    private List<LinePropsMapping> linePropsMappings = new List<LinePropsMapping>();
    private List<SpawnedPropInfo> allSpawnedPropsInfo = new List<SpawnedPropInfo>();
    private List<GameObject> generatedTerrainPieces = new List<GameObject>();
    private bool frameGenerated = false;
    private int placementCount = 0;

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
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 生成关卡
    public void SpawnLevel(Vector3 pos)
    {
        if (levelBlueprints == null || levelBlueprints.Count == 0) return;

        // 循环使用关卡蓝图
        int curIndex = levelEntryCount % levelBlueprints.Count;
        LevelBlueprint blueprint = levelBlueprints[curIndex];
        pendingBlueprint = blueprint;

        GameObject levelRootObj = new GameObject("Level_" + blueprint.levelType + "_" + pos);
        levelRoot = levelRootObj.transform;
        levelRoot.position = pos;

        if (blueprint.skyboxMaterial != null)
        {
            RenderSettings.skybox = blueprint.skyboxMaterial;
        }

        // 创建一个专门用于存放玩家生成内容的子对象
        GameObject playerGeneratedContainer = new GameObject("PlayerGeneratedContent");
        playerGeneratedContainer.transform.SetParent(levelRoot);
        playerGeneratedContainer.transform.localPosition = Vector3.zero;

        // 生成用于预览的地形
        GameObject terrainInstance = Instantiate(blueprint.terrainPrefab, levelRoot.position, Quaternion.identity, levelRoot);
        terrainInstance.name = "Terrain";

        // 由于重置关卡bug，使用LevelInfo存储关卡对应的缓存地形
        GameObject cachedTerrain = Instantiate(blueprint.terrainPrefab, levelRoot);
        cachedTerrain.SetActive(false);
        cachedTerrain.name = "CachedTerrain";
        LevelInfo levelInfo = levelRootObj.AddComponent<LevelInfo>();
        levelInfo.Initialize(blueprint, cachedTerrain);

        frameGenerated = false;
        placementCount = 0;
        generatedTerrainPieces.Clear();
        linePropsMappings.Clear();
        allSpawnedPropsInfo.Clear();

        if (paintingSystem != null)
        {
            waitingForFramePainting = true;
            paintingSystem.StartPainting(blueprint, levelRoot);
        }
    }

    // 绘画完成回调
    public void OnPaintingCompleted()
    {
        if (waitingForFramePainting && levelRoot != null)
        {
            waitingForFramePainting = false;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPaintedLevelGenerated(levelRoot);
            }
        }
    }

    // 根据新画线条生成prop
    public void UpdateLevelFromPaintLine(Transform levelRoot, LevelBlueprint blueprint, PaintingSystem.PaintLine paintLine)
    {
        if (levelRoot == null || blueprint == null) return;

        Renderer terrainRenderer = levelRoot.GetComponentInChildren<Renderer>();
        float terrainRadius = terrainRenderer != null ? terrainRenderer.bounds.extents.x : 25.0f;

        LinePropsMapping mapping = new LinePropsMapping(linePropsMappings.Count);
        // 确认线条生成prop的种类、位置和密度
        List<PropPlacementData> placements = ConvertPaintLineToPlacement(paintLine, -1);

        // 根据种类生成prop
        GeneratePropsByType(placements, mapping, levelRoot, blueprint, terrainRadius);

        // 初始化该线条的最大可放置prop数量
        mapping.maxPlaceableProps = (int)(mapping.props.Count / paintLine.saturation);
        linePropsMappings.Add(mapping);
    }

    // 根据种类生成prop
    private void GeneratePropsByType(List<PropPlacementData> placements, LinePropsMapping mapping, Transform levelRoot, LevelBlueprint blueprint, float terrainRadius)
    {
        var grouped = placements.GroupBy(p => p.propType);
        foreach (var group in grouped)
        {
            GameObject[] prefabs = null;
            float safeDistance = 0f;

            switch (group.Key)
            {
                case PropType.POI:
                    prefabs = blueprint.poiPrefabs;
                    safeDistance = blueprint.poiSafeDistance;
                    break;
                case PropType.LargeProp:
                    prefabs = blueprint.largePropPrefabs;
                    safeDistance = blueprint.largePropSafeDistance;
                    break;
                case PropType.SmallProp:
                    prefabs = blueprint.smallPropPrefabs;
                    safeDistance = blueprint.smallPropSafeDistance;
                    break;
            }

            if (prefabs != null)
                GeneratePropsFromPlacements(prefabs, group.ToList(), levelRoot, terrainRadius, allSpawnedPropsInfo, safeDistance, mapping);
        }
    }

    // 撤销线条时，摧毁线条对应prop、删除mapping并将后续mapping的lineIndex减1 -- 0
    // 编辑线条时，摧毁线条对应prop并清除mapping内信息                        -- 1
    public void RemoveOrClearPropsForLine(int lineIndex, int sign)
    {
        LinePropsMapping mapping = linePropsMappings.FirstOrDefault(m => m.lineIndex == lineIndex);
        if (mapping != null)
        {
            foreach (GameObject prop in mapping.props)
            {
                if (prop != null)
                {
                    allSpawnedPropsInfo.RemoveAll(info => info.gameObject == prop);
                    DestroyImmediate(prop);
                }
            }

            if (sign == 0)
            {
                linePropsMappings.Remove(mapping);
            }
            else
            {
                mapping.props.Clear();
            }
        }
    }

    // 编辑线条时，摧毁线条对应prop并重新生成
    public void UpdatePropsForLine(int lineIndex, PaintingSystem.PaintLine updatedLine)
    {
        LinePropsMapping mapping = linePropsMappings.FirstOrDefault(m => m.lineIndex == lineIndex);
        if (mapping != null)
        {
            RemoveOrClearPropsForLine(lineIndex, 1);

            if (levelRoot != null && pendingBlueprint != null)
            {
                Renderer terrainRenderer = levelRoot.GetComponentInChildren<Renderer>();
                float terrainRadius = terrainRenderer != null ? terrainRenderer.bounds.extents.x : 25.0f;

                int targetCount = Mathf.RoundToInt(mapping.maxPlaceableProps * updatedLine.saturation);
                targetCount = Mathf.Max(1, targetCount); // 线条饱和度最小0.1f，至少1个prop

                // 根据更新后的饱和度调整最大prop生成数量
                List<PropPlacementData> placements = ConvertPaintLineToPlacement(updatedLine, targetCount);
                GeneratePropsByType(placements, mapping, levelRoot, pendingBlueprint, terrainRadius);
            }
        }
    }

    // 根据单一线条确认prop放置信息（种类、位置和密度）
    private List<PropPlacementData> ConvertPaintLineToPlacement(PaintingSystem.PaintLine paintLine, int maxCount = -1)
    {
        List<PropPlacementData> placements = new List<PropPlacementData>();

        if (paintLine.points.Count < 2) return placements;

        int finalCount;
        if (maxCount > 0)
        {
            finalCount = maxCount;
        }
        else
        {
            float totalLength = 0f;
            // 根据线条长度计算prop生成数量 -- 如果用点数，会因为鼠标移动太快而变化太大
            for (int i = 1; i < paintLine.points.Count; i++)
                totalLength += Vector2.Distance(paintLine.points[i - 1], paintLine.points[i]);
            finalCount = Mathf.Max(1, Mathf.RoundToInt(totalLength * 0.1f)); // 至少生成1个prop
        }

        for (int i = 0; i < finalCount; i++)
        {
            // 根据prop生成顺位/总prop个数（标化位置）结合线条点位计算位置
            float t = finalCount > 1 ? (float)i / (finalCount - 1) : 0.5f;
            Vector2 pointOnLine = GetPointAlongLine(paintLine.points, t);

            Vector2 normalizedPos = new Vector2(
                pointOnLine.x / PaintingSystem.Instance.canvasSize,
                pointOnLine.y / PaintingSystem.Instance.canvasSize
            );
            if (pendingBlueprint.allowRandomness)
                normalizedPos += Random.insideUnitCircle * 0.02f;

            normalizedPos = new Vector2(Mathf.Clamp01(normalizedPos.x), Mathf.Clamp01(normalizedPos.y));

            placements.Add(new PropPlacementData
            {
                propType = paintLine.propType,
                normalizedPosition = normalizedPos,
                density = paintLine.saturation
            });
        }

        return placements;
    }

    // 根据prop及线条上点的顺位确定位置
    private Vector2 GetPointAlongLine(List<Vector2> points, float norm)
    {
        if (points.Count < 2) return points[0];
        if (norm <= 0f) return points[0];
        if (norm >= 1f) return points[points.Count - 1];

        float totalLength = 0f;
        List<float> segmentLengths = new List<float>();

        for (int i = 1; i < points.Count; i++)
        {
            float segmentLength = Vector2.Distance(points[i - 1], points[i]);
            segmentLengths.Add(segmentLength);
            totalLength += segmentLength;
        }

        float targetDistance = totalLength * norm;
        float currentDistance = 0f;

        // 段落式增加距离来找到目标距离的近似对应点位
        for (int i = 0; i < segmentLengths.Count; i++)
        {
            if (currentDistance + segmentLengths[i] >= targetDistance)
            {
                float t = segmentLengths[i] > 0 ? (targetDistance - currentDistance) / segmentLengths[i] : 0f;
                return Vector2.Lerp(points[i], points[i + 1], t);
            }
            currentDistance += segmentLengths[i];
        }

        return points[points.Count - 1];
    }

    // 根据放置信息生成prop
    private void GeneratePropsFromPlacements(GameObject[] propPrefabs, List<PropPlacementData> placements, Transform levelRoot, float terrainRadius, List<SpawnedPropInfo> spawnedProps, float safeDistance, LinePropsMapping mapping)
    {
        if (propPrefabs == null || propPrefabs.Length == 0 || placements.Count == 0) return;

        Renderer[] renderers = levelRoot.GetComponentsInChildren<Renderer>();

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            // 合并所有渲染器的包围盒
            combinedBounds.Encapsulate(renderers[i].bounds);
        }
        Vector3 terrainCenter = combinedBounds.center;

        foreach (var placement in placements)
        {
            Vector2 normalizedPos = placement.normalizedPosition;
            Vector3 worldPos = terrainCenter + new Vector3(
                (normalizedPos.x - 0.5f) * 2f * terrainRadius,
                0,
                (normalizedPos.y - 0.5f) * 2f * terrainRadius);

            GameObject selectedPrefab = propPrefabs[Random.Range(0, propPrefabs.Length)];
            float propRadius = GetPropRadius(selectedPrefab);

            RaycastHit hit;
            if (Physics.Raycast(worldPos + Vector3.up * 10f, Vector3.down, out hit, 25f))
            {
                if (hit.collider == null) continue;
                if (hit.collider.GetComponentInParent<Prop>() != null) continue; // 不能放在prop上
                worldPos.y = hit.point.y;
            }

            // 生成逻辑改为确认的位置，而非寻找随机位置
            // 所以这里判断重叠即可
            if (!IsPositionOverlapping(worldPos, propRadius, spawnedProps, safeDistance) && IsPositionSafeWithColliders(worldPos, propRadius))
            {
                Quaternion rotation = pendingBlueprint.allowRandomness ? Quaternion.Euler(0, Random.Range(0, 360), 0) : Quaternion.identity;
                GameObject propInstance = Instantiate(selectedPrefab, worldPos, rotation, levelRoot);
                // 更新跟踪列表
                SpawnedPropInfo propInfo = new SpawnedPropInfo(worldPos, propRadius, propInstance);
                spawnedProps.Add(propInfo);
                mapping.props.Add(propInstance);
            }
        }
    }

    // 简易画框生成逻辑
    public void OnPhotoPlaced(List<LiminalCamera.Photo.CapturedPropData> capturedProps, GameObject terrainPiece)
    {
        placementCount++;
        if (frameGenerated) return;

        if (terrainPiece != null && !generatedTerrainPieces.Contains(terrainPiece))
            generatedTerrainPieces.Add(terrainPiece);

        // 捕捉到POI/5个以上prop/放置3次以上照片
        bool shouldGenerate = capturedProps.Any(prop => prop.prefab.GetComponent<Prop>() is PointOfInterest) || capturedProps.Count >= 5 || placementCount >= 3;

        if (shouldGenerate && generatedTerrainPieces.Count > 0)
            GenerateFrameOnTerrain();
    }

    // 生成画框
    private void GenerateFrameOnTerrain()
    {
        if (frameGenerated || pendingBlueprint?.framePrefab == null) return;

        GameObject targetTerrain = generatedTerrainPieces.LastOrDefault();
        if (targetTerrain == null) return;

        Renderer terrainRenderer = targetTerrain.GetComponent<Renderer>();
        if (terrainRenderer == null) return;

        Bounds terrainBounds = terrainRenderer.bounds;
        float terrainRadius = Mathf.Min(terrainBounds.size.x, terrainBounds.size.z) * 0.4f;
        Vector3 framePos = FindSafePosition(terrainBounds.center, terrainRadius, allSpawnedPropsInfo, pendingBlueprint.frameSafeDistance, 1.0f, targetTerrain);

        if (framePos != Vector3.zero)
        {
            Vector3 lookDirection = (framePos - terrainBounds.center).normalized;
            Quaternion frameRotation = Quaternion.LookRotation(lookDirection);
            GameObject frameInstance = Instantiate(pendingBlueprint.framePrefab, framePos + Vector3.up * 2.5f, frameRotation, GameManager.Instance.GetPlayerGeneratedContentContainer());
                        
            if (levelEntryCount == levelBlueprints.Count)
            {
                Frame frameComponent = frameInstance.GetComponent<Frame>();
                if (frameComponent != null)
                {
                    GameManager.Instance.firstLevel.SetActive(true);
                    frameComponent.isReturnToFirstLevel = true;
                }
            }
            
            frameGenerated = true;
        }
    }

    // 查找安全位置
    private Vector3 FindSafePosition(Vector3 centerPos, float spawnRadius, List<SpawnedPropInfo> spawnedProps, float safeDistance, float propRadius, GameObject specificTerrain = null)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomPos2D = Random.insideUnitCircle * spawnRadius;
            Vector3 candidatePos = new Vector3(centerPos.x + randomPos2D.x, centerPos.y, centerPos.z + randomPos2D.y);

            // 针对凹凸地形，利用射线判断位置y值
            RaycastHit hit;
            if (Physics.Raycast(candidatePos + Vector3.up * 5f, Vector3.down, out hit, 15f))
            {
                candidatePos.y = hit.point.y;

                // 生成画框用
                if (specificTerrain != null)
                {
                    if (hit.collider.gameObject == specificTerrain && !IsPositionOverlapping(candidatePos, propRadius, spawnedProps, safeDistance))
                        return candidatePos;
                }
                // 寻找传送位置用
                else
                {
                    if (hit.collider.GetComponentInParent<Prop>() != null) continue;

                    if (!IsPositionOverlapping(candidatePos, propRadius, spawnedProps, safeDistance))
                    {
                        if (!IsPositionSafeWithColliders(candidatePos, propRadius))
                        {
                            continue;
                        }

                        return candidatePos;
                    }
                }
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
    public GameObject RegenerateTerrain(Transform levelRoot)
    {
        if (levelRoot == null) return null;

        LevelInfo levelInfo = levelRoot.GetComponent<LevelInfo>();
        if (levelInfo == null || levelInfo.cachedTerrainPrefab == null) return null;

        GameObject newTerrain = Instantiate(levelInfo.cachedTerrainPrefab, levelRoot);
        newTerrain.SetActive(true);
        newTerrain.name = "Terrain";
        
        return newTerrain;
    }

    public void SetFrameGenerated(bool generated)
    {
        frameGenerated = generated;
    }

    public void IncrementLevelEntryCount()
    {
        levelEntryCount++;
    }
}