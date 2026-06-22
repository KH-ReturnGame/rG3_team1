using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 5 — 고드름 낙하 (Icicle Drop)
///
/// 천장에 왼쪽부터 순차적으로 고드름이 생성되고(예고), 잠시 후 역시 왼쪽부터 순차적으로
/// 떨어진다. 매번 그 중 하나는 랜덤으로 비어서(갭) 플레이어가 그 자리로 피할 수 있으며,
/// 갭이 정해지는 순간 사운드 힌트(gapCueClip)가 재생된다.
/// 이 "생성 → 낙하" 한 세트를 waveCount(기본 3)번 반복한다.
///
/// [클래스/파일명은 BossPattern5_RotatingBeam 그대로 유지]
/// 씬에 저장된 컴포넌트 참조가 스크립트 GUID 기반이라, 클래스 이름을 바꾸면 기존 보스
/// 오브젝트의 패턴5 컴포넌트가 "Missing Script"로 깨짐. 그래서 내부 로직만 완전히
/// 새로 짜고 이름은 그대로 둠.
///
/// [패링 불가] isParryable = false (환경 피해)
///
/// [사용법]
/// - iciclePrefab: BossHitbox 컴포넌트가 달린 프리팹. 없으면 코드로 기본 생성(플레이스홀더).
/// - groundLayer: 바닥 레이어. 비워두면 "Ground"로 자동 설정.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossPattern5_RotatingBeam : BossPatternBase
{
    [Header("배치 (천장, 왼쪽부터)")]
    [Tooltip("가장 왼쪽 고드름의 X 좌표")]
    public float startX = -8f;
    [Tooltip("고드름이 생성되는 천장 Y 좌표")]
    public float ceilingY = 5f;
    [Tooltip("고드름 간 가로 간격")]
    public float columnSpacing = 1.4f;
    [Tooltip("한 웨이브에 생성할 고드름 개수")]
    public int columnCount = 7;

    [Header("타이밍")]
    [Tooltip("고드름이 왼쪽부터 순차적으로 생성되는 간격 (초)")]
    public float spawnStagger = 0.08f;
    [Tooltip("전부 생성된 뒤, 낙하 시작까지 매달려 있는 예고 시간 (초)")]
    public float warningDuration = 0.6f;
    [Tooltip("고드름이 왼쪽부터 순차적으로 떨어지기 시작하는 간격 (초)")]
    public float dropStagger = 0.08f;
    [Tooltip("낙하 속도")]
    public float fallSpeed = 16f;
    [Tooltip("낙하 시작 후 바닥에 닿지 않아도 강제로 사라지는 최대 시간 (초, 안전장치)")]
    public float maxFallDuration = 3f;

    [Header("회피 구간 (갭)")]
    [Tooltip("매 웨이브마다 한 칸을 랜덤으로 비워서 플레이어가 피할 수 있게 할지 여부")]
    public bool enableGapColumn = true;
    [Tooltip("갭이 정해지는 순간 재생할 사운드 힌트")]
    public AudioClip gapCueClip;

    [Header("반복")]
    [Tooltip("생성→낙하 한 세트를 몇 번 반복할지")]
    public int waveCount = 3;
    [Tooltip("웨이브 사이 대기 시간 (초)")]
    public float waveDelay = 0.5f;
    [Tooltip("2페이즈에서 추가로 더 반복할 횟수")]
    public int phase2ExtraWaves = 1;
    [Tooltip("2페이즈에서 낙하 속도에 곱할 배율")]
    public float phase2FallSpeedMultiplier = 1.3f;

    [Header("고드름 모양 (플레이스홀더)")]
    [Tooltip("스폰할 고드름 프리팹 (BossHitbox 필요). 없으면 코드로 기본 생성.")]
    public GameObject iciclePrefab;
    [Tooltip("고드름 가로 크기")]
    public float icicleWidth = 0.5f;
    [Tooltip("고드름 세로 크기")]
    public float icicleHeight = 1.1f;

    [Header("판정")]
    [Tooltip("바닥/벽 레이어. 비워두면 \"Ground\"로 자동 설정.")]
    public LayerMask groundLayer;
    [Tooltip("바닥 감지 레이캐스트 거리")]
    public float groundCheckDist = 0.2f;

    [Header("데미지")]
    public float damage = 14f;

    private AudioSource _audio;

    protected override void Awake()
    {
        base.Awake();
        _audio = GetComponent<AudioSource>();
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Ground");
    }

    public override IEnumerator Execute(bool isPhase2)
    {
        FacePlayer();

        int waves = waveCount + (isPhase2 ? phase2ExtraWaves : 0);
        float fallSpd = fallSpeed * (isPhase2 ? phase2FallSpeedMultiplier : 1f);

        for (int w = 0; w < waves; w++)
        {
            yield return StartCoroutine(RunOneWave(fallSpd));

            if (w < waves - 1)
                yield return new WaitForSeconds(waveDelay);
        }
    }

    // 한 세트: 왼쪽부터 순차 생성(예고) → 잠깐 대기 → 왼쪽부터 순차 낙하 → 전부 사라질 때까지 대기
    IEnumerator RunOneWave(float fallSpd)
    {
        int count = Mathf.Max(1, columnCount);
        int gapIndex = enableGapColumn ? Random.Range(0, count) : -1;

        var icicles = new GameObject[count];

        // 1. 왼쪽부터 순차적으로 천장에 생성 (갭 자리는 생성하지 않음 — 그 자체가 시각적 신호)
        for (int i = 0; i < count; i++)
        {
            if (i != gapIndex)
            {
                float x = startX + i * columnSpacing;
                icicles[i] = SpawnIcicle(x, ceilingY);
            }
            yield return new WaitForSeconds(spawnStagger);
        }

        if (gapIndex >= 0) PlayGapCue();

        // 2. 예고 — 잠깐 매달려 있는 시간 (플레이어가 어디로 피할지 파악)
        yield return new WaitForSeconds(warningDuration);

        // 3. 왼쪽부터 순차적으로 낙하 시작
        for (int i = 0; i < count; i++)
        {
            if (icicles[i] != null)
                StartCoroutine(DropIcicle(icicles[i], fallSpd));

            yield return new WaitForSeconds(dropStagger);
        }

        // 이번 웨이브의 고드름이 모두 사라질 때까지 대기 — 다음 패턴이 곧바로 시작돼서
        // 잔여 고드름과 겹쳐 보이는 것을 방지.
        float waited = 0f;
        float safetyTimeout = maxFallDuration + dropStagger * count + 1f;
        while (waited < safetyTimeout && System.Array.Exists(icicles, ic => ic != null))
        {
            waited += Time.deltaTime;
            yield return null;
        }
    }

    GameObject SpawnIcicle(float x, float y)
    {
        GameObject go;
        if (iciclePrefab != null)
        {
            go = Instantiate(iciclePrefab, new Vector3(x, y, 0f), Quaternion.identity);
        }
        else
        {
            go = CreateDefaultIcicle();
            go.transform.position = new Vector3(x, y, 0f);
        }

        BossHitbox hb = go.GetComponent<BossHitbox>();
        if (hb == null) hb = go.AddComponent<BossHitbox>();
        hb.isParryable  = false; // 환경 피해 — 패링 불가
        hb.damage       = damage;
        hb.disableOnHit = false; // 떨어지는 동안 지속 판정
        hb.owner        = boss;

        return go;
    }

    GameObject CreateDefaultIcicle()
    {
        var go = new GameObject("Icicle");
        go.layer = LayerMask.NameToLayer("Default");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetIcicleSprite();
        sr.color  = new Color(0.75f, 0.92f, 1f, 0.95f); // 옅은 청백색
        go.transform.localScale = new Vector3(icicleWidth, icicleHeight, 1f);

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = Vector2.one;

        return go;
    }

    // 고드름을 아래로 낙하시키고, 바닥에 닿거나 최대 낙하 시간이 지나면 파괴.
    IEnumerator DropIcicle(GameObject icicle, float speed)
    {
        float elapsed = 0f;
        while (icicle != null && elapsed < maxFallDuration)
        {
            elapsed += Time.deltaTime;
            icicle.transform.position += Vector3.down * speed * Time.deltaTime;

            RaycastHit2D hit = Physics2D.Raycast(icicle.transform.position, Vector2.down, groundCheckDist, groundLayer);
            if (hit.collider != null) break;

            yield return null;
        }

        if (icicle != null) Destroy(icicle);
    }

    void PlayGapCue()
    {
        if (gapCueClip == null) return;
        if (_audio != null) _audio.PlayOneShot(gapCueClip);
        else AudioSource.PlayClipAtPoint(gapCueClip, transform.position, 1f);
    }

    // ── 고드름 플레이스홀더 스프라이트 (세로로 긴 사각형) ──────
    private static Sprite _icicleSprite;
    static Sprite GetIcicleSprite()
    {
        if (_icicleSprite != null) return _icicleSprite;
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _icicleSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4f);
        return _icicleSprite;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.6f);
        for (int i = 0; i < Mathf.Max(1, columnCount); i++)
        {
            Vector3 p = new Vector3(startX + i * columnSpacing, ceilingY, 0f);
            Gizmos.DrawWireCube(p, new Vector3(icicleWidth, icicleHeight, 0f));
        }
    }
}
