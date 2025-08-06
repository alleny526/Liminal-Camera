using UnityEngine;

// 存储关卡对应信息
public class LevelInfo : MonoBehaviour
{
    [Header("关卡缓存数据")]
    public LevelBlueprint blueprint;
    public GameObject cachedTerrainPrefab;
    
    public void Initialize(LevelBlueprint levelBlueprint, GameObject terrainPrefab)
    {
        blueprint = levelBlueprint;
        cachedTerrainPrefab = terrainPrefab;
    }
}