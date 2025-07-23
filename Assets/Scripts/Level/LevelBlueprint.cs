using UnityEngine;

// 关卡蓝图配置，定义关卡类型和生成参数
[CreateAssetMenu(fileName = "New Level Blueprint", menuName = "Liminal Camera/Level Blueprint")]
public class LevelBlueprint : ScriptableObject
{
    [Header("关卡基本信息")]
    public LevelType levelType;
    public GameObject terrainPrefab;
    public GameObject doorPrefab;

    [Header("关卡Prop配置")]
    public GameObject poiPrefab;
    
    [Space]
    public GameObject[] largePropPrefabs;
    public int largePropCount;

    [Space]
    public GameObject[] smallPropPrefabs;
    public int smallPropCount;
}
