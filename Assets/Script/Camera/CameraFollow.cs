using UnityEngine;

// 2D 카메라: 플레이어를 '부드럽게' 따라가되(댐핑 + 진행방향 룩어헤드) 맵 경계를 벗어나지 않게 클램프.
// 경계 지정 방법(우선순위):
//   1) Bounds Area(Collider2D)를 넣으면 그 범위를 사용(가장 편함 — 맵 전체를 덮는 빈 BoxCollider2D 하나 두면 됨)
//   2) 없으면 Bounds Min/Max 수동 값
//   3) SetBounds(min,max) 런타임 호출(절차 생성 스테이지: ChunkManager가 생성 후 호출)
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

    private Camera cam;
    private Vector3 vel;
    private float lookDir = 1f, curAhead, aheadVel, lastX;
    private bool hasBounds;
    private Vector2 bMin, bMax;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (maxOrthoSize <= 0f && cam != null) maxOrthoSize = cam.orthographicSize;
    }

    void Start()
    {
        if (target == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) target = pc.transform;
        }
        if (target != null) { lastX = target.position.x; SnapToTarget(); }
        ResolveBounds();
    }

    public void SetTarget(Transform t) { target = t; if (t != null) { lastX = t.position.x; SnapToTarget(); } }

    // 절차 생성 스테이지 등에서 생성 직후 호출
    public void SetBounds(Vector2 min, Vector2 max) { boundsArea = null; boundsMin = min; boundsMax = max; useBounds = true; ResolveBounds(); }

    private void ResolveBounds()
    {
        if (boundsArea != null) { bMin = boundsArea.bounds.min; bMax = boundsArea.bounds.max; hasBounds = true; }
        else if (boundsMax.x > boundsMin.x && boundsMax.y > boundsMin.y) { bMin = boundsMin; bMax = boundsMax; hasBounds = true; }
        else hasBounds = false;

        // 화면 세로가 Bounds 높이보다 크면 그만큼 줌인 → 위/아래 빈(죽은) 공간 제거
        if (hasBounds && fitZoomToBounds && cam != null && cam.orthographic)
        {
            if (maxOrthoSize <= 0f) maxOrthoSize = cam.orthographicSize;
            cam.orthographicSize = Mathf.Min(maxOrthoSize, (bMax.y - bMin.y) * 0.5f);
        }
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
        if (target == null) return;

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
        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref vel, smoothTime);

        if (useBounds && hasBounds && cam != null && cam.orthographic)
        {
            float halfW = halfH * cam.aspect;
            pos.x = (bMax.x - bMin.x >= 2f * halfW) ? Mathf.Clamp(pos.x, bMin.x + halfW, bMax.x - halfW) : (bMin.x + bMax.x) * 0.5f;
            pos.y = (bMax.y - bMin.y >= 2f * halfH) ? Mathf.Clamp(pos.y, bMin.y + halfH, bMax.y - halfH) : (bMin.y + bMax.y) * 0.5f;
        }
        transform.position = pos;
    }

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
