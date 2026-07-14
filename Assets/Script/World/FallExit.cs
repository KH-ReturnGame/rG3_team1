using UnityEngine;

// 낙사 → 씬 이동 핸들러: 플레이어가 낙사 판정(killY)에 걸린 순간 x좌표가 minX 이상이면,
// 기존 낙사 처리(안전지점 복귀 + 피해) 대신 암전(SceneFader)과 함께 지정한 씬으로 이동한다.
//  · 마을(StartingArea) 오른쪽 허공으로 떨어지면 지하(Metroidvania)로 '추락 진입'하는 연출용.
//  · 씬에 하나만 배치(콜라이더 불필요). minX '왼쪽'에서 떨어지면 기존 낙사(복귀+피해) 그대로.
//  · 이 컴포넌트가 없는 씬에서는 아무 일도 하지 않는다(PlayerController가 Instance 유무로 판단).
public class FallExit : MonoBehaviour
{
    public string targetScene = "Metroidvania";
    public float minX = 16f;    // 이 x좌표 이상에서 낙사하면 씬 이동(미만이면 일반 낙사 처리)
    private bool used;          // 암전 중 중복 발동 방지

    public static FallExit Instance { get; private set; }
    void OnEnable() { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    // PlayerController.CheckFall()의 낙사 판정에서 호출.
    // 씬 이동을 처리했으면 true를 반환 → 일반 낙사 처리(복귀+피해)를 건너뛴다.
    public static bool TryHandleFall(PlayerController pc)
    {
        FallExit fx = Instance;
        if (fx == null || pc == null || fx.used) return false;
        if (pc.transform.position.x < fx.minX) return false;
        if (GameFlow.Instance == null || !GameFlow.Instance.CanTransition) return false;   // 전환 잠금 중엔 일반 낙사로
        fx.used = true;

        // 암전(0.4초) 동안 낙사 판정이 다시 돌지 않게 플레이어를 멈춤 — 씬 로드 때 파괴되므로 복원 불필요
        Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
        pc.enabled = false;

        GameFlow.Instance.GoToScene(fx.targetScene);   // SceneFader 암전 후 로드
        return true;
    }
}
