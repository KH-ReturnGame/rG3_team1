using UnityEngine;

// 점프대(트램펄린): 위에서 착지하면 플레이어를 위로 발사. 솔리드 콜라이더로 배치(밟을 수 있게).
[RequireComponent(typeof(Collider2D))]
public class BouncePad : MonoBehaviour
{
    [Tooltip("튕겨 올리는 세기(클수록 높이)")]
    public float bounceForce = 18f;

    void OnCollisionEnter2D(Collision2D col) { TryBounce(col.collider); }
    void OnCollisionStay2D(Collision2D col) { TryBounce(col.collider); }   // 떨어지며 닿는 프레임을 놓치지 않게

    private void TryBounce(Collider2D other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        // 위에서 내려올 때만(옆/아래 충돌 제외)
        if (pc.transform.position.y > transform.position.y + 0.05f)
            pc.Launch(bounceForce);
    }
}
