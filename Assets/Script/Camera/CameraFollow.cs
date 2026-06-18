using UnityEngine;

// 2D 카메라: 플레이어를 '부드럽게' 따라가되(댐핑 + 진행방향 룩어헤드) 맵 경계를 벗어나지 않게 클램프.
// 경계 지정 방법(우선순위):
//   0) CameraZone(방마다 BoxCollider2D 트리거)을 배치하면, 플레이어가 들어간 구역으로 경계+줌이 부드럽게 전환
//      → 비일자 맵(여러 방이 복도로 연결)에서 권장. (useZones)
//   1) Bounds Area(Collider2D)를 넣으면 그 범위를 사용(일자 맵에 편함 — 맵 전체를 덮는 빈 BoxCollider2D 하나)
//   2) 없으면 Bounds Min/Max 수동 값
//   3) SetBounds(min,max) 런타임 호출(절차 생성 스테이지: ChunkManager가 생성 후 호출)
// CameraZone이 하나라도 있고 플레이어를 포함하면 그 구역이 1)~3)보다 우선.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;                 // 비우면 PlayerController 자동 탐색

    [Header("따라가기")]
    public float smoothTime = 0.18f;         // 클수록 느긋하게(부드럽게) 따라옴
    public Vector2 offset = new Vector2(0f, 1f);   // 살짝 위를 보도록

    [Header("룩어헤드(진행 방향 미리 보기 — 답답함 완화)")]
    public float lookAheadX = 2.2f;          // 좌우로 미리 당겨 보는 거리
    public float lookAheadSmooth = 0.4f;

    [Header("세로 프레이밍 (바닥 밑 빈 타일이 화면에 안 차게)")]
    [Range(0f, 0.5f)] public float playerAnchorY = 0.35f;   // 플레이어를 화면 세로 어디에 둘지 (0=바닥, 0.5=중앙). 낮을수록 위쪽을 더 보여줌
    public float verticalDeadZone = 1.2f;                  // 이 범위 안의 위아래 이동은 카메라 세로 고정(점프 떨림·불필요 스크롤 방지)

    [Header("맵 경계 (카메라가 벗어나지 않게)")]
    public bool useBounds = true;
    public Collider2D boundsArea;            // 있으면 이 콜라이더 범위를 경계로 사용
    public Vector2 boundsMin;                // boundsArea 없을 때 수동(왼쪽아래)
    public Vector2 boundsMax;                // 수동(오른쪽위)

    [Header("세로 줌 맞춤 (화면이 Bounds 높이를 넘지 않게 — 위/아래 빈 공간 제거)")]
    public bool fitZoomToBounds = true;
    public float maxOrthoSize = 0f;          // 기본(최대) 줌아웃 크기. 0이면 시작 시 카메라 현재 크기를 사용

    [Header("카메라 구역(CameraZone) — 비일자 맵: 방마다 카메라 전환")]
    public bool useZones = true;             // 씬에 CameraZone이 있으면 자동으로 구역별 경계 사용
    public float zoneTransition = 0.35f;     // 구역이 바뀔 때 새 경계로 옮겨가는 부드럽기(작을수록 빠름)

    private Camera cam;
    public static CameraFollow Instance;                 // Juice(셰이크) 접근용
    [System.NonSerialized] public Vector2 cutsceneOffset;   // 컷씬용 카메라 오프셋(IntroCutscene 등이 설정, 끝나면 0)
    private float shakeAmt, shakeTimer, shakeDur;
    private Vector3 vel;
    private float lookDir = 1f, curAhead, aheadVel, lastX;
    private bool hasBounds;
    private Vector2 bMin, bMax;                           // 현재(부드럽게 보간된) 경계 — 클램프/줌에 사용
    private Vector2 tMin, tMax;                           // 목표 경계(구역 전환 대상)
    private Vector2 minVel, maxVel;                       // 경계 SmoothDamp 속도
    private CameraZone activeZone;                        // 현재 활성 구역
    private bool snapBounds = true;                       // true면 보간 없이 즉시 적용(시작·SetBounds)

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        if (maxOrthoSize <= 0f && cam != null) maxOrthoSize = cam.orthographicSize;
    }

    void Start()
    {
        if (target == null)
        {
            var pc = PlayerController.Instance != null ? PlayerController.Instance : FindAnyObjectByType<PlayerController>();
            if (pc != null) target = pc.transform;
        }
        ResolveBounds();
        if (target != null) { lastX = target.position.x; SnapToTarget(); }
        snapBounds = true;   // 첫 LateUpdate에서 구역/경계를 즉시 적용(시작 시 팬/줌 안 튀게)
    }

    public void SetTarget(Transform t) { target = t; if (t != null) { lastX = t.position.x; SnapToTarget(); } }

    // 절차 생성 스테이지 등에서 생성 직후 호출
    public void SetBounds(Vector2 min, Vector2 max) { boundsArea = null; boundsMin = min; boundsMax = max; useBounds = true; snapBounds = true; ResolveBounds(); }
    public bool HasBounds => hasBounds;                                          // 낙사 판정용
    public float BoundsBottom => hasBounds ? bMin.y : float.NegativeInfinity;

    // 시작/SetBounds 시 1회: 레거시(단일) 경계를 목표로 잡고 현재 경계에 즉시 스냅 + 줌 맞춤
    private void ResolveBounds()
    {
        if (boundsArea != null) { tMin = boundsArea.bounds.min; tMax = boundsArea.bounds.max; hasBounds = true; }
        else if (boundsMax.x > boundsMin.x && boundsMax.y > boundsMin.y) { tMin = boundsMin; tMax = boundsMax; hasBounds = true; }
        else hasBounds = false;

        bMin = tMin; bMax = tMax;
        ApplyZoomFit();
    }

    // 화면 세로가 (현재)Bounds 높이보다 크면 그만큼 줌인 → 위/아래 빈(죽은) 공간 제거
    private void ApplyZoomFit()
    {
        if (hasBounds && fitZoomToBounds && cam != null && cam.orthographic)
        {
            if (maxOrthoSize <= 0f) maxOrthoSize = cam.orthographicSize;
            cam.orthographicSize = Mathf.Min(maxOrthoSize, (bMax.y - bMin.y) * 0.5f);
        }
    }

    // 매 프레임: 목표 경계 결정(구역 우선 → 레거시) 후 현재 경계를 부드럽게 보간 + 줌 갱신
    private void UpdateBounds()
    {
        bool fromZone = false;
        if (useZones && CameraZone.All.Count > 0 && target != null)
        {
            CameraZone z = PickZone(target.position);
            if (z != null) { activeZone = z; Bounds b = z.Area; tMin = b.min; tMax = b.max; hasBounds = true; fromZone = true; }
        }
        if (!fromZone)
        {
            if (boundsArea != null) { tMin = boundsArea.bounds.min; tMax = boundsArea.bounds.max; hasBounds = true; }
            else if (boundsMax.x > boundsMin.x && boundsMax.y > boundsMin.y) { tMin = boundsMin; tMax = boundsMax; hasBounds = true; }
            else hasBounds = false;
        }

        if (!hasBounds) return;

        if (snapBounds) { bMin = tMin; bMax = tMax; minVel = Vector2.zero; maxVel = Vector2.zero; snapBounds = false; }
        else
        {
            bMin = Vector2.SmoothDamp(bMin, tMin, ref minVel, zoneTransition);
            bMax = Vector2.SmoothDamp(bMax, tMax, ref maxVel, zoneTransition);
        }
        ApplyZoomFit();
    }

    // 플레이어를 포함하는 구역 선택. 현재 구역에 아직 있으면 유지(겹침 경계 깜빡임 방지),
    // 여러 구역이 겹치면 우선순위 높은 것 → 같으면 작은 구역 우선. 어디에도 없으면 직전 구역 유지(복도 틈).
    private CameraZone PickZone(Vector2 p)
    {
        if (activeZone != null && activeZone.isActiveAndEnabled && activeZone.Contains(p)) return activeZone;

        CameraZone best = null; int bestPrio = int.MinValue; float bestArea = float.MaxValue;
        var list = CameraZone.All;
        for (int i = 0; i < list.Count; i++)
        {
            CameraZone z = list[i];
            if (z == null || !z.isActiveAndEnabled || !z.Contains(p)) continue;
            Vector3 s = z.Area.size; float a = s.x * s.y;
            if (z.priority > bestPrio || (z.priority == bestPrio && a < bestArea))
            { bestPrio = z.priority; bestArea = a; best = z; }
        }
        return best != null ? best : activeZone;
    }

    private void SnapToTarget()
    {
        if (target == null) return;
        float halfH = (cam != null && cam.orthographic) ? cam.orthographicSize : 5f;
        float y = target.position.y + halfH * (1f - 2f * Mathf.Clamp01(playerAnchorY));
        transform.position = new Vector3(target.position.x + offset.x, y, transform.position.z);
    }

    void LateUpdate()
    {
        if (target == null)   // Start에서 못 찾았어도 매 프레임 재시도(플레이어가 늦게 생성/씬 전환 대비)
        {
            var pc = PlayerController.Instance != null ? PlayerController.Instance : FindAnyObjectByType<PlayerController>();
            if (pc == null) return;
            target = pc.transform; lastX = target.position.x; SnapToTarget();
        }

        UpdateBounds();   // 구역 결정 + 경계 보간 + 줌 갱신(클램프/halfH 계산 전에)

        // 룩어헤드: 이동 방향으로 카메라를 미리 당김(부드럽게)
        float dx = target.position.x - lastX;
        if (Mathf.Abs(dx) > 0.0005f) lookDir = Mathf.Sign(dx);
        lastX = target.position.x;
        curAhead = Mathf.SmoothDamp(curAhead, lookAheadX * lookDir, ref aheadVel, lookAheadSmooth);

        float halfH = (cam != null && cam.orthographic) ? cam.orthographicSize : 5f;

        // 가로: 진행 방향 룩어헤드
        float desiredX = target.position.x + offset.x + curAhead;

        // 세로: 플레이어를 화면 아래쪽(playerAnchorY)에 두고, 데드존 안의 위아래 이동은 무시(고정)
        float anchoredY = target.position.y + halfH * (1f - 2f * Mathf.Clamp01(playerAnchorY));
        float camY = transform.position.y;
        float dy = anchoredY - camY;
        float goalY = camY;
        if (dy > verticalDeadZone) goalY = anchoredY - verticalDeadZone;
        else if (dy < -verticalDeadZone) goalY = anchoredY + verticalDeadZone;

        Vector3 desired = new Vector3(desiredX, goalY, transform.position.z);
        desired += (Vector3)cutsceneOffset;   // 컷씬용 카메라 이동(부드럽게 SmoothDamp로 적용)
        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref vel, smoothTime);

        if (useBounds && hasBounds && cam != null && cam.orthographic)
        {
            float halfW = halfH * cam.aspect;
            pos.x = (bMax.x - bMin.x >= 2f * halfW) ? Mathf.Clamp(pos.x, bMin.x + halfW, bMax.x - halfW) : (bMin.x + bMax.x) * 0.5f;
            pos.y = (bMax.y - bMin.y >= 2f * halfH) ? Mathf.Clamp(pos.y, bMin.y + halfH, bMax.y - halfH) : (bMin.y + bMax.y) * 0.5f;
        }
        transform.position = pos;

        // 화면 흔들림(타격감) — 따라간 위치 위에 감쇠 오프셋
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float s = shakeAmt * Mathf.Clamp01(shakeTimer / Mathf.Max(0.01f, shakeDur));
            Vector2 off = Random.insideUnitCircle * s;
            transform.position += new Vector3(off.x, off.y, 0f);
        }
    }

    public void AddShake(float amt, float dur) { shakeAmt = Mathf.Max(shakeAmt, amt); shakeDur = Mathf.Max(0.01f, dur); shakeTimer = shakeDur; }

    void OnDrawGizmosSelected()
    {
        Vector2 mn = boundsArea != null ? (Vector2)boundsArea.bounds.min : boundsMin;
        Vector2 mx = boundsArea != null ? (Vector2)boundsArea.bounds.max : boundsMax;
        if (mx.x > mn.x && mx.y > mn.y)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube((mn + mx) * 0.5f, mx - mn);
        }
    }
}
