using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 1 — 지면 파동 (Ground Wave)
///
/// 바닥을 쳐서 사전에 배치된 지점들에 순차적으로 충격파 프리팹을 스폰.
///
/// [페이즈 차이]
/// - 1페이즈: 1회 타격 → 1회 파동
/// - 2페이즈: 여러 번 연타 → 파동 N회 연속 발생, waveInterval 단축
///
/// [패링 불가] isParryable = false  (광역 지면 판정)
///
/// [사용법]
/// 1. 보스 씬에 "WavePoint" 오브젝트들을 배치하고 wavePoints 배열에 등록.
/// 2. waveHitboxPrefab에 BossHitbox 컴포넌트가 달린 프리팹 연결.
///    프리팹의 BossHitbox.isParryable은 false로 설정.
/// </summary>
public class BossPattern1_GroundWave : BossPatternBase
{
    [Header("파동 지점")]
    [Tooltip("순차 스폰될 지점들 (씬에 미리 배치). 왼→오 또는 원하는 순서로 정렬.")]
    public Transform[] wavePoints;

    [Tooltip("각 지점에 스폰할 히트박스 프리팹 (BossHitbox 컴포넌트 필수)")]
    public GameObject waveHitboxPrefab;

    [Header("튜닝 변수")]
    [Tooltip("지점 간 스폰 딜레이 (초) — 파동 속도")]
    public float waveInterval = 0.15f;
    [Tooltip("히트박스 지속 시간 (초)")]
    public float hitboxLifetime = 0.4f;
    [Tooltip("스폰 전 선딜레이 (보스 타격 모션 시간)")]
    public float windupDuration = 0.6f;

    [Header("2페이즈 변수")]
    [Tooltip("2페이즈 연타 횟수")]
    public int phase2WaveCount = 3;
    [Tooltip("2페이즈 waveInterval 배율 (1보다 작으면 가속)")]
    public float phase2IntervalMultiplier = 0.6f;
    [Tooltip("2페이즈 연타 간 대기 시간 (초)")]
    public float phase2WaveDelay = 0.5f;

    [Header("데미지")]
    public float damage = 12f;

    public override IEnumerator Execute(bool isPhase2)
    {
        if (wavePoints == null || wavePoints.Length == 0)
        {
            Debug.LogWarning("[Pattern1] wavePoints가 비어 있습니다.");
            yield break;
        }

        FacePlayer();

        // 선딜레이 (타격 모션)
        yield return new WaitForSeconds(windupDuration);

        if (isPhase2)
        {
            float interval = waveInterval * phase2IntervalMultiplier;
            for (int wave = 0; wave < phase2WaveCount; wave++)
            {
                yield return StartCoroutine(SpawnWave(interval));
                if (wave < phase2WaveCount - 1)
                    yield return new WaitForSeconds(phase2WaveDelay);
            }
        }
        else
        {
            yield return StartCoroutine(SpawnWave(waveInterval));
        }
    }

    IEnumerator SpawnWave(float interval)
    {
        foreach (var point in wavePoints)
        {
            if (point == null) continue;
            SpawnHitboxAt(point.position);
            yield return new WaitForSeconds(interval);
        }
    }

    void SpawnHitboxAt(Vector3 position)
    {
        if (waveHitboxPrefab == null)
        {
            // 프리팹 없으면 코드로 기본 생성 (플레이스홀더)
            GameObject go = new GameObject("GroundWave_Hitbox");
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Default");

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.2f, 1.5f);

            var hb = go.AddComponent<BossHitbox>();
            hb.isParryable  = false; // 패턴 1 — 패링 불가
            hb.damage       = damage;
            hb.disableOnHit = false; // 지속 판정
            hb.owner        = boss;

            Destroy(go, hitboxLifetime);
            return;
        }

        GameObject spawned = Instantiate(waveHitboxPrefab, position, Quaternion.identity);
        BossHitbox hitbox = spawned.GetComponent<BossHitbox>();
        if (hitbox != null)
        {
            hitbox.isParryable  = false;
            hitbox.damage       = damage;
            hitbox.disableOnHit = false;
            hitbox.owner        = boss;
        }
        Destroy(spawned, hitboxLifetime);
    }
}
