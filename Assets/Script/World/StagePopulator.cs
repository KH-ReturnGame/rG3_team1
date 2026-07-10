using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 절차생성 스테이지 채우기. ChunkManager가 지형을 만든 뒤(다음 프레임), 생성된 바닥을 따라
// 적/채집물/보물상자를 확률적으로 흩뿌린다. 청크 프리팹을 건드리지 않고 모든 스테이지에 적용.
// 각 스테이지 씬에 빈 오브젝트로 두고 인스펙터에서 프리팹/확률 설정.
public class StagePopulator : MonoBehaviour
{
    [Header("스캔 (지형 자동 산출, 비우면 Ground 콜라이더 범위)")]
    public float step = 5f;          // 샘플 간격(작을수록 촘촘)
    public float safeStartPad = 12f; // 시작점(왼쪽)에서 이만큼은 비워둠(스폰 직후 안전)

    [Header("적")]
    public GameObject[] enemyPrefabs;          // 근접/원거리/공중 등
    [Range(0f, 1f)] public float enemyChance = 0.45f;

    [Header("채집물")]
    public ItemData[] gatherItems;             // 이 중 무작위
    [Range(0f, 1f)] public float gatherChance = 0.30f;

    [Header("보물상자")]
    public GameObject chestPrefab;
    [Range(0f, 1f)] public float chestChance = 0.12f;
    public int chestGoldMin = 30, chestGoldMax = 120;

    private IEnumerator Start()
    {
        yield return null;                       // ChunkManager.Start(생성) 다음 프레임
        yield return new WaitForSeconds(0.15f);  // 콜라이더 안정화
        Populate();
    }

    private void Populate()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) { Debug.LogWarning("[StagePopulator] Ground 레이어 없음"); return; }
        int groundMask = 1 << groundLayer;

        // 생성된 Ground 콜라이더 전체 범위 산출
        Bounds area = default; bool has = false;
        foreach (var c in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (c == null || c.gameObject.layer != groundLayer) continue;
            if (!has) { area = c.bounds; has = true; } else area.Encapsulate(c.bounds);
        }
        if (!has) { Debug.LogWarning("[StagePopulator] Ground 콜라이더 없음 — 배치 생략"); return; }

        float minX = area.min.x + safeStartPad, maxX = area.max.x - 3f, top = area.max.y + 5f;
        float rayLen = (top - area.min.y) + 10f;

        var parent = new GameObject("StageSpawned").transform;
        bool prevQ = Physics2D.queriesHitTriggers; Physics2D.queriesHitTriggers = false;
        int en = 0, ga = 0, ch = 0;

        for (float x = minX; x <= maxX; x += step)
        {
            var hit = Physics2D.Raycast(new Vector2(x + Random.Range(-1.5f, 1.5f), top), Vector2.down, rayLen, groundMask);
            if (hit.collider == null) continue;
            Vector3 g = hit.point;
            float r = Random.value;

            if (r < chestChance && chestPrefab != null)
            {
                var c = Instantiate(chestPrefab, g + Vector3.up * 0.55f, Quaternion.identity, parent);
                var tc = c.GetComponent<TreasureChest>();
                if (tc != null) { tc.lootGold = Random.Range(chestGoldMin, chestGoldMax + 1); tc.chestId = "stage_" + x.ToString("0") + "_" + g.y.ToString("0"); }
                ch++;
            }
            else if (r < chestChance + gatherChance && gatherItems != null && gatherItems.Length > 0)
            {
                var item = gatherItems[Random.Range(0, gatherItems.Length)];
                if (item != null) { ItemPickup.SpawnWorld(item, Random.Range(1, 3), g + Vector3.up * 0.9f, 0.6f, true); ga++; }
            }
            else if (r < chestChance + gatherChance + enemyChance && enemyPrefabs != null && enemyPrefabs.Length > 0)
            {
                var ep = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
                if (ep != null)
                {
                    bool flying = ep.GetComponent<FlyingEnemy>() != null;
                    Instantiate(ep, g + Vector3.up * (flying ? 4f : 1f), Quaternion.identity, parent);
                    en++;
                }
            }
        }

        Physics2D.queriesHitTriggers = prevQ;
        Debug.Log("[StagePopulator] 배치 완료 — 적 " + en + " / 채집물 " + ga + " / 상자 " + ch);
    }
}
