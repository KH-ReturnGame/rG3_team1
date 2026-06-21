using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 1 — 지면 파동 (Ground Wave)
///
/// 바닥을 쳐서 보스 위치를 기준으로 좌우 대칭으로 충격파 히트박스가
/// 한 스텝씩 바깥쪽으로 퍼져나간다 (스토리보드 참고: 보스 중심 → 좌우로 확산).
/// 각 히트박스는 스폰 시 바닥에서 솟아올랐다가 다시 내려가는 모션으로 연출된다.
///
/// [페이즈 차이]
/// - 1페이즈: 1회 타격 → 1회 파동 확산
/// - 2페이즈: 여러 번 연타 → 파동 확산 N회 연속 발생, waveInterval 단축
///
/// [패링 불가] isParryable = false  (광역 지면 판정)
///
/// [사용법]
/// 1. waveHitboxPrefab에 BossHitbox 컴포넌트가 달린 프리팹 연결 (없으면 코드로 기본 생성).
///    프리팹의 BossHitbox.isParryable은 false로 설정.
/// 2. 별도의 "WavePoint" 오브젝트 배치는 더 이상 필요 없음 — 보스 위치 기준으로
///    waveStartDistance/waveStepDistance/waveStepCount 값으로 확산 범위를 조절.
/// </summary>
public class BossPattern1_GroundWave : BossPatternBase
{
    [Header("파동 확산 (보스 기준 좌우 대칭)")]
    [Tooltip("보스로부터 첫 파동 판정까지의 거리")]
    public float waveStartDistance = 1.2f;
    [Tooltip("스텝마다 추가로 멀어지는 거리")]
    public float waveStepDistance = 1.3f;
    [Tooltip("좌/우 각각 생성할 파동 스텝 수")]
    public int waveStepCount = 4;

    [Tooltip("각 지점에 스폰할 히트박스 프리팹 (BossHitbox 컴포넌트 필수)")]
    public GameObject waveHitboxPrefab;

    [Header("튜닝 변수")]
    [Tooltip("스텝 간 스폰 딜레이 (초) — 파동이 퍼지는 속도")]
    public float waveInterval = 0.15f;
    [Tooltip("히트박스 지속 시간 (초)")]
    public float hitboxLifetime = 0.4f;
    [Tooltip("스폰 전 선딜레이 (보스 타격 모션 시간)")]
    public float windupDuration = 0.6f;

    [Header("연출 — 솟아올랐다 내려가는 모션")]
    [Tooltip("파동이 솟아오르는 최대 높이")]
    public float waveRiseHeight = 0.6f;
    [Tooltip("솟아오름→내려감 전체 모션 시간 (초). hitboxLifetime보다 길면 자동으로 줄어듦.")]
    public float waveRiseDuration = 0.3f;

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

        // 마지막으로 스폰된 히트박스(들)가 화면에서 완전히 사라질 때까지 대기.
        // 이걸 안 기다리면 Execute()가 먼저 끝나버려서, 잔여 파동 이펙트가 떠 있는 동안
        // 다음 패턴이 곧바로 시작되는(겹쳐 보이는) 문제가 생김.
        yield return new WaitForSeconds(Mathf.Max(hitboxLifetime, waveRiseDuration));
    }

    // 보스 위치를 기준으로 좌우 대칭으로 한 스텝씩 바깥쪽으로 퍼져나간다.
    IEnumerator SpawnWave(float interval)
    {
        Vector3 origin = transform.position;

        for (int step = 0; step < waveStepCount; step++)
        {
            float dist = waveStartDistance + step * waveStepDistance;
            SpawnHitboxAt(origin + Vector3.right * dist);
            SpawnHitboxAt(origin + Vector3.left * dist);
            yield return new WaitForSeconds(interval);
        }
    }

    void SpawnHitboxAt(Vector3 position)
    {
        GameObject spawned;

        if (waveHitboxPrefab == null)
        {
            // 프리팹 없으면 코드로 기본 생성 (플레이스홀더)
            GameObject go = new GameObject("GroundWave_Hitbox");
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("Default");

            // 프리팹을 안 연결했을 때 판정만 생기고 화면에는 아무것도 안 보여서
            // "패턴이 작동 안 한다"처럼 보이는 문제 방지 — 눈에 보이는 임시 스프라이트 부착.
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetWaveSprite();
            sr.color  = new Color(1f, 0.85f, 0.2f, 0.85f);
            sr.transform.localScale = new Vector3(1.2f, 1.5f, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.2f, 1.5f);

            var hb = go.AddComponent<BossHitbox>();
            hb.isParryable  = false; // 패턴 1 — 패링 불가
            hb.damage       = damage;
            hb.disableOnHit = false; // 지속 판정
            hb.owner        = boss;

            spawned = go;
        }
        else
        {
            spawned = Instantiate(waveHitboxPrefab, position, Quaternion.identity);
            BossHitbox hitbox = spawned.GetComponent<BossHitbox>();
            if (hitbox != null)
            {
                hitbox.isParryable  = false;
                hitbox.damage       = damage;
                hitbox.disableOnHit = false;
                hitbox.owner        = boss;
            }
        }

        Destroy(spawned, hitboxLifetime);

        float riseDuration = Mathf.Min(waveRiseDuration, hitboxLifetime);
        StartCoroutine(RiseAndFallMotion(spawned, position, waveRiseHeight, riseDuration));
    }

    // 스폰 위치에서 위로 솟아올랐다가 다시 바닥으로 내려가는 연출.
    IEnumerator RiseAndFallMotion(GameObject target, Vector3 basePos, float height, float duration)
    {
        if (duration <= 0f || target == null) yield break;

        float t = 0f;
        while (t < duration && target != null)
        {
            float p = t / duration;
            float y = Mathf.Sin(p * Mathf.PI) * height; // 0 → height → 0
            target.transform.position = basePos + Vector3.up * y;
            t += Time.deltaTime;
            yield return null;
        }

        if (target != null) target.transform.position = basePos;
    }

    // ── 임시 시각 (플레이스홀더) ─────────────────────────────
    private static Sprite _waveSprite;
    static Sprite GetWaveSprite()
    {
        if (_waveSprite != null) return _waveSprite;
        const int res = 8;
        Texture2D tex = new Texture2D(res, res);
        Color[] pixels = new Color[res * res];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _waveSprite = Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
        return _waveSprite;
    }
}
