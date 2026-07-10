using System.Collections.Generic;
using UnityEngine;

// 움직이는 플랫폼: 시작 위치(A)와 pointB 사이를 왕복. 위에 탄 플레이어를 함께 옮긴다.
//  배치: 솔리드 콜라이더 + 스프라이트 오브젝트에 이 컴포넌트. pointB에 도착 지점(빈 오브젝트)을 드래그.
//  - 수직 이동은 물리(키네마틱 밀기)로 자연스럽게 태워지고, 수평 이동은 델타로 직접 태운다.
[RequireComponent(typeof(Collider2D))]
public class MovingPlatform : MonoBehaviour
{
    public Transform pointB;          // 반대쪽 지점(시작 위치 = A)
    public float speed = 2f;
    public float waitTime = 0.5f;     // 양 끝에서 멈추는 시간

    private Vector3 a, b;
    private bool toB = true;
    private float waitTimer;
    private Rigidbody2D rb;
    private readonly List<Transform> riders = new List<Transform>();

    void Awake()
    {
        a = transform.position;
        b = pointB != null ? pointB.position : a;
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void FixedUpdate()
    {
        if (pointB == null) return;
        Vector3 cur = transform.position;
        Vector3 np = cur;
        if (waitTimer > 0f) waitTimer -= Time.fixedDeltaTime;
        else
        {
            Vector3 target = toB ? b : a;
            np = Vector3.MoveTowards(cur, target, speed * Time.fixedDeltaTime);
            if ((np - target).sqrMagnitude < 0.0001f) { toB = !toB; waitTimer = waitTime; }
        }
        Vector3 delta = np - cur;
        rb.MovePosition(np);

        // 위에 탄 플레이어를 수평으로 함께 옮김(수직은 키네마틱 충돌이 처리)
        for (int i = riders.Count - 1; i >= 0; i--)
        {
            var r = riders[i];
            if (r == null) { riders.RemoveAt(i); continue; }
            if (r.position.y > transform.position.y) r.position += new Vector3(delta.x, 0f, 0f);
        }
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        var pc = c.collider.GetComponentInParent<PlayerController>();
        if (pc != null && !riders.Contains(pc.transform)) riders.Add(pc.transform);
    }

    void OnCollisionExit2D(Collision2D c)
    {
        var pc = c.collider.GetComponentInParent<PlayerController>();
        if (pc != null) riders.Remove(pc.transform);
    }

    void OnDrawGizmos()
    {
        if (pointB == null) return;
        Gizmos.color = new Color(0.3f, 0.8f, 0.95f, 0.7f);
        Gizmos.DrawLine(transform.position, pointB.position);
        Gizmos.DrawWireSphere(pointB.position, 0.2f);
    }
}
