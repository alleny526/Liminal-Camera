using UnityEngine;

// 关卡蓝图配置，定义关卡类型和生成参数
[CreateAssetMenu(fileName = "New Level Blueprint", menuName = "Liminal Camera/Level Blueprint")]
public class LevelBlueprint : ScriptableObject
{
    [Header("关卡基本信息")]
    public LevelType levelType;
    public GameObject terrainPrefab;
    public GameObject framePrefab;

    [Header("关卡Prop配置")]
    public GameObject[] poiPrefabs;
    public GameObject[] largePropPrefabs;
    public GameObject[] smallPropPrefabs;
    
    [Header("颜料设置")]
    public int maxPOIPaint = 100;
    public int maxLargePropPaint = 150;
    public int maxSmallPropPaint = 200;
    
    [Header("Prop间距设置")]
    public float poiSafeDistance = 5.0f;
    public float largePropSafeDistance = 4.0f;
    public float smallPropSafeDistance = 2.0f;
    public float frameSafeDistance = 5.0f;
}