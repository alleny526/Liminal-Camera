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
    public GameObject[] poiPrefabs;
    public int poiCount;

    [Space]
    public GameObject[] largePropPrefabs;
    public int largePropCount;

    [Space]
    public GameObject[] smallPropPrefabs;
    public int smallPropCount;
    
    [Header("Prop间距设置")]
    public float poiSafeDistance = 5.0f;
    public float largePropSafeDistance = 4.0f;
    public float smallPropSafeDistance = 2.0f;
    public float doorSafeDistance = 5.0f;
    // public float extraSafeDistance = 0.5f; // 不加这个可能会重叠
}
