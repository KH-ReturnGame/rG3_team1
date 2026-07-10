using UnityEngine;

// 채집물 스폰 포인트. 씬 곳곳에 배치해두면, 플레이어가 씬에 진입할 때(Start)
// spawnChance(기본 50%) 확률로 possible 중 하나의 채집물을 이 위치에 생성한다.
// 어떤 채집물이 나올지는 인스펙터의 possible 배열에서 설정(여러 개면 무작위 택1).
// 생성된 채집물은 ItemPickup이라 F로 줍고, 채집 퀘스트(ReportGather)에도 집계된다.
public class GatheringSpawn : MonoBehaviour
{
    [Range(0f, 1f)] public float spawnChance = 0.5f;   // 씬 진입 시 생성 확률(행운 스탯이 약간 더해짐)
    public ItemData[] possible;                        // 이 중 하나가 생성됨(여러 개면 무작위)
    public int minCount = 1;
    public int maxCount = 1;
    public float worldSize = 0.6f;                     // 생성될 채집물 크기
    public bool floatInAir = true;                     // true=공중에 둥둥(눈에 잘 띔) / false=중력 받아 바닥에

    void Start()
    {
        if (possible == null || possible.Length == 0) return;

        float chance = spawnChance;
        if (GameManager.Instance != null) chance += GameManager.Instance.statLuck * 0.03f;   // 행운 → 채집물 조우 확률↑
        if (Random.value > Mathf.Clamp01(chance)) return;                                     // 실패 → 생성 안 함

        ItemData item = possible[Random.Range(0, possible.Length)];
        if (item == null) return;
        int n = Random.Range(minCount, Mathf.Max(minCount, maxCount) + 1);
        if (n <= 0) return;

        ItemPickup.SpawnWorld(item, n, transform.position, worldSize, floatInAir);
    }

    // 씬에서 스폰 포인트가 보이도록(에디터 전용 표시)
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.55f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.35f, transform.position + Vector3.up * 0.75f);
    }
}
