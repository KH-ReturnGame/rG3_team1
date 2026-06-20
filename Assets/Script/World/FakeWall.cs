using UnityEngine;

// 숨겨진 통로 벽: 겉보기엔 벽이지만 통과할 수 있다. 플레이어가 안에 들어오면 반투명해져 내부가 보인다.
//  배치: 벽 스프라이트 오브젝트 + 트리거 Collider2D(통과 가능)에 이 컴포넌트.
//   - 솔리드 충돌이 아니라 트리거라 플레이어가 그대로 걸어 들어감.
//   - 자식 스프라이트도 함께 페이드(여러 칸 벽 가능).
[RequireComponent(typeof(Collider2D))]
public class FakeWall : MonoBehaviour
{
    [Range(0f, 1f)] public float enteredAlpha = 0.3f;   // 들어왔을 때 투명도
    public float fadeSpeed = 4f;

    private SpriteRenderer[] srs;
    private float target = 1f, cur = 1f;

    void Awake()
    {
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;   // 통과 가능하게
        srs = GetComponentsInChildren<SpriteRenderer>();
    }

    void OnTriggerEnter2D(Collider2D other) { if (other.GetComponentInParent<PlayerController>() != null) target = enteredAlpha; }
    void OnTriggerExit2D(Collider2D other) { if (other.GetComponentInParent<PlayerController>() != null) target = 1f; }

    void Update()
    {
        if (Mathf.Abs(cur - target) < 0.005f) return;
        cur = Mathf.MoveTowards(cur, target, fadeSpeed * Time.deltaTime);
        if (srs == null) return;
        foreach (var sr in srs)
            if (sr != null) { Color col = sr.color; col.a = cur; sr.color = col; }
    }
}
