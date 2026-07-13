using UnityEngine;

// 낙하 이동 존: 플레이어가 이 트리거로 떨어지면 지정한 씬으로 이동한다(F 상호작용 불필요).
//  마을(StartingArea) 오른쪽 끝에서 떨어지면 지하(Metroidvania)로 '추락 진입'하는 연출용.
//  · 진입 즉시 플레이어를 멈추고 물리를 꺼서, 페이드(0.4초) 중 낙사 판정(killY 복귀·피해)이 끼어들지 않게 한다.
//  · 빈 오브젝트 + BoxCollider2D(IsTrigger)를 낙하 경로를 넓게 덮도록 배치.
[RequireComponent(typeof(Collider2D))]
public class FallExit : MonoBehaviour
{
    public string targetScene = "Metroidvania";
    private bool used;   // 페이드 중 중복 발동 방지

    void Reset() { var c = GetComponent<Collider2D>(); if (c != null) c.isTrigger = true; }
    void Awake() { var c = GetComponent<Collider2D>(); if (c != null) c.isTrigger = true; }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        if (GameFlow.Instance == null || !GameFlow.Instance.CanTransition) return;
        used = true;

        // 페이드 동안 계속 떨어져 killY(낙사 복귀+피해)에 닿는 것 방지 — 씬 로드로 파괴되므로 복원 불필요
        var rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
        pc.enabled = false;   // 조작 잠금

        GameFlow.Instance.GoToScene(targetScene);
    }
}
