using UnityEngine;
using System.Collections;

/// <summary>
/// 패턴 5 — 회전 레이저 (Rotating Beam)
///
/// 1. 보스가 화면 왼쪽으로 이동
/// 2. 8박자 사운드 큐로 예고 (6번째 박에서 피치 하강 → "곧 발사" 컷오프 신호)
/// 3. 보스 위치에서 직선으로 사각형 세그먼트를 순차 배치 (모든 세그먼트 크기 동일)
/// 4. 전체 빔이 보스를 중심으로 360도 회전 — 회전하는 동안 각 세그먼트는 매 프레임
///    피벗 회전값으로 강제 동기화되어 반지름 방향에 대해 계속 직각을 유지함
///
/// [패링 불가 권장] isParryable = false (지속형 회전 판정. [CONFIRM] 확정 필요)
///
/// [CONFIRM] 2페이즈 변경 사항: 현재 회전 속도 증가 + 세그먼트 수 증가로 구현.
///
/// [사용법]
/// - beamPivot: 보스 자식 빈 오브젝트 (빔 회전 기준점). 없으면 코드에서 자동 생성.
/// - beamSegmentPrefab: BossHitbox + SpriteRenderer + BoxCollider2D 달린 사각형 프리팹.
///   없으면 코드로 기본 생성 (흰 사각형 플레이스홀더).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossPattern5_RotatingBeam : BossPatternBase
{
    [Header("이동")]
    [Tooltip("발사 전 보스가 이동할 화면 왼쪽 X 위치")]
    public float leftPositionX = -7f;
    [Tooltip("왼쪽으로 이동하는 속도")]
    public float moveSpeed = 5f;
    [Tooltip("위치 이동 타임아웃 (초)")]
    public float moveTimeout = 3f;

    [Header("사운드 큐 (8박자)")]
    [Tooltip("8박자 반복 클립 (루프 오디오 권장)")]
    public AudioClip beatAudioClip;
    [Tooltip("6번째 박에서 피치가 내려갈 AudioSource (별도 AudioSource 연결)")]
    public AudioSource beatAudioSource;
    [Tooltip("BPM — 박자 간격 = 60 / BPM")]
    public float bpm = 120f;
    [Tooltip("6번째 박 피치 변화량 (기본 1.0 대비. 예: 0.75 = 3/4음 하강)")]
    public float beat6PitchShift = 0.75f;

    [Header("빔 세그먼트")]
    [Tooltip("세그먼트 프리팹 (BossHitbox + BoxCollider2D + SpriteRenderer 필수). 없으면 자동 생성.")]
    public GameObject beamSegmentPrefab;
    [Tooltip("세그먼트 개수 (직선 길이)")]
    public int segmentCount = 8;
    [Tooltip("세그먼트 간 간격")]
    public float segmentSpacing = 0.8f;
    [Tooltip("세그먼트 크기 (모든 세그먼트 동일)")]
    public float segmentBaseSize = 0.4f;

    [Header("회전")]
    [Tooltip("회전 속도 (도/초). 음수면 반시계.")]
    public float rotationSpeed = 90f;
    [Tooltip("회전 방향 랜덤화 여부")]
    public bool randomizeDirection = false;
    [Tooltip("2페이즈 회전 속도 배율")]
    public float phase2SpeedMultiplier = 1.5f;
    [Tooltip("2페이즈 세그먼트 수 증가량")]
    public int phase2ExtraSegments = 4;

    [Header("데미지")]
    public float damage = 18f;
    [Tooltip("[CONFIRM] 패링 가능 여부 (현재 false 권장)")]
    public bool hitParryable = false;

    // ── 런타임 ───────────────────────────────────────────────────
    private GameObject     _pivot;
    private GameObject[]   _segments;
    private AudioSource    _audio;

    protected override void Awake()
    {
        base.Awake();
        _audio = GetComponent<AudioSource>();
    }

    public override IEnumerator Execute(bool isPhase2)
    {
        // 1. 왼쪽으로 이동
        yield return StartCoroutine(MoveToLeft());

        // 2. 8박자 예고 사운드
        yield return StartCoroutine(PlayBeatCue());

        // 3. 빔 생성
        int count = segmentCount + (isPhase2 ? phase2ExtraSegments : 0);
        BuildBeam(count);

        // 4. 360도 회전
        float speed = rotationSpeed * (isPhase2 ? phase2SpeedMultiplier : 1f);
        if (randomizeDirection && Random.value < 0.5f) speed = -speed;

        yield return StartCoroutine(RotateBeam(speed));

        // 5. 빔 제거
        DestroyBeam();
    }

    // ── 왼쪽 이동 ────────────────────────────────────────────
    IEnumerator MoveToLeft()
    {
        float elapsed = 0f;
        while (elapsed < moveTimeout)
        {
            elapsed += Time.deltaTime;
            float dx = leftPositionX - transform.position.x;
            if (Mathf.Abs(dx) < 0.1f) { rb.linearVelocity = Vector2.zero; yield break; }

            rb.linearVelocity = new Vector2(Mathf.Sign(dx) * moveSpeed, rb.linearVelocity.y);
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
    }

    // ── 8박자 사운드 큐 ──────────────────────────────────────
    IEnumerator PlayBeatCue()
    {
        float beatInterval = 60f / bpm;
        AudioSource src = beatAudioSource != null ? beatAudioSource : _audio;

        for (int beat = 1; beat <= 8; beat++)
        {
            if (src != null && beatAudioClip != null)
            {
                // 6번째 박: 피치 하강 (컷오프 신호)
                src.pitch = (beat == 6) ? beat6PitchShift : 1f;
                src.PlayOneShot(beatAudioClip);
            }
            yield return new WaitForSeconds(beatInterval);
        }

        if (src != null) src.pitch = 1f;
    }

    // ── 빔 생성 ──────────────────────────────────────────────
    void BuildBeam(int count)
    {
        // 회전 피벗 (보스 위치에 자식 오브젝트 생성)
        _pivot = new GameObject("BeamPivot");
        _pivot.transform.position  = transform.position;
        _pivot.transform.SetParent(transform);

        _segments = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            float dist = segmentSpacing * (i + 1);

            GameObject seg;
            if (beamSegmentPrefab != null)
            {
                seg = Instantiate(beamSegmentPrefab,
                                  _pivot.transform.position + Vector3.right * dist,
                                  Quaternion.identity, _pivot.transform);
            }
            else
            {
                seg = CreateDefaultSegment(_pivot.transform, dist, segmentBaseSize);
            }

            // BossHitbox 초기화
            BossHitbox hb = seg.GetComponent<BossHitbox>();
            if (hb == null) hb = seg.AddComponent<BossHitbox>();
            hb.Init(damage, hitParryable, hitParryable ? (IParryable)boss : null);
            hb.disableOnHit = false; // 지속 판정

            // 크기 — 모든 세그먼트 동일
            seg.transform.localScale = Vector3.one * segmentBaseSize;

            _segments[i] = seg;
        }
    }

    GameObject CreateDefaultSegment(Transform parent, float dist, float size)
    {
        var go = new GameObject("BeamSeg");
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.right * dist;
        go.layer = LayerMask.NameToLayer("Default");

        // 시각 (플레이스홀더: 흰 사각형)
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSquareSprite();
        sr.color  = new Color(1f, 0.4f, 0.1f, 0.85f); // 주황빛 레이저

        // 충돌체
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = Vector2.one;

        return go;
    }

    // ── 360도 회전 ────────────────────────────────────────────
    IEnumerator RotateBeam(float speed)
    {
        float totalAngle = 0f;
        float targetAngle = speed >= 0 ? 360f : -360f;

        while (Mathf.Abs(totalAngle) < 360f)
        {
            if (_pivot == null) yield break;

            float delta = speed * Time.deltaTime;
            _pivot.transform.Rotate(0f, 0f, delta);
            totalAngle += delta;

            // 피벗 위치를 보스 위치에 고정 (보스가 움직이는 경우 대비)
            _pivot.transform.position = transform.position;

            // 세그먼트가 계속 회전 방향(반지름)에 대해 직각을 유지하도록 매 프레임 강제로 맞춰줌.
            if (_segments != null)
            {
                for (int i = 0; i < _segments.Length; i++)
                {
                    if (_segments[i] != null)
                        _segments[i].transform.rotation = _pivot.transform.rotation;
                }
            }

            yield return null;
        }
    }

    // ── 빔 제거 ──────────────────────────────────────────────
    void DestroyBeam()
    {
        if (_pivot != null) Destroy(_pivot);
        _pivot    = null;
        _segments = null;
    }

    // ── 흰 사각형 스프라이트 헬퍼 ────────────────────────────
    private static Sprite _whiteSquare;
    static Sprite GetWhiteSquareSprite()
    {
        if (_whiteSquare != null) return _whiteSquare;
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _whiteSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4f);
        return _whiteSquare;
    }

    void OnDestroy()
    {
        DestroyBeam();
    }
}
