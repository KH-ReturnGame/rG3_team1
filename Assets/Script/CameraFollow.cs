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

    [Header("맵 경계 (카메라가 벗어나지 않게)")]
    public bool useBounds = true;
    public Collider2D boundsArea;            // 있으면 이 콜라이더 범위를 경계로 사용
    public Vector2 boundsMin;                // boundsArea 없을 때 수동(왼쪽아래)
    public Vector2 boundsMax;                // 수동(오른쪽위)

    private Camera cam;
    private Vector3 vel;
    private float lookDir = 1f, curAhead, aheadVel, lastX;
    private bool hasBounds;
    private Vector2 bMin, bMax;

    void Awake() { cam = GetComponent<Camera>(); }

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
    }

    private void SnapToTarget()
    {
        if (target == null) return;
        Vector3 p = target.position + (Vector3)offset;
        p.z = transform.position.z;
        transform.position = p;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 룩어헤드: 이동 방향으로 카메라를 미리 당김(부드럽게)
        float dx = target.position.x - lastX;
        if (Mathf.Abs(dx) > 0.0005f) lookDir = Mathf.Sign(dx);
        lastX = target.position.x;
        curAhead = Mathf.SmoothDamp(curAhead, lookAheadX * lookDir, ref aheadVel, lookAheadSmooth);

        Vector3 desired = target.position + (Vector3)(offset + new Vector2(curAhead, 0f));
        desired.z = transform.position.z;
        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref vel, smoothTime);

        if (useBounds && hasBounds && cam != null && cam.orthographic)
        {
            float halfH = cam.orthographicSize;
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