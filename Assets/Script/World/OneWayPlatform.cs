using UnityEngine;

// 원웨이(통과) 플랫폼: 아래/옆에서 점프로 뚫고 올라가 위에 착지할 수 있는 발판.
// 이 컴포넌트를 발판 오브젝트(콜라이더 있는 것)에 붙이면 PlatformEffector2D를 자동 설정한다.
// 플레이어는 아래키(S/↓)를 빠르게 두 번(더블탭) 눌러 발판 아래로 내려갈 수 있다(PlayerController가 처리).
//
// 세팅:
//   1) 발판 오브젝트(스프라이트 or 타일맵)에 이 컴포넌트 추가 → Collider2D + PlatformEffector2D 자동 구성
//   2) ★ 오브젝트 Layer를 Ground(플레이어 groundLayer와 동일)로 둘 것 → 착지/점프 리셋/내려가기가 정상 동작
//   (타일맵에 쓰려면: TilemapCollider2D + CompositeCollider2D(Used By Effector 체크) 위에 이 컴포넌트)
[RequireComponent(typeof(PlatformEffector2D))]
public class OneWayPlatform : MonoBehaviour
{
    [Tooltip("막는 면의 각도(도). 작을수록 위쪽만 막고 옆은 더 잘 통과. 보통 그대로(170)")]
    public float surfaceArc = 170f;

    void Reset() { Setup(); }
    void Awake() { Setup(); }

    void Setup()
    {
        // 부착된 모든 콜라이더를 이펙터가 쓰도록
        var cols = GetComponents<Collider2D>();
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null && !cols[i].isTrigger) cols[i].usedByEffector = true;

        var eff = GetComponent<PlatformEffector2D>();
        if (eff != null)
        {
            eff.useOneWay = true;
            eff.useOneWayGrouping = false;
            eff.surfaceArc = surfaceArc;
            eff.rotationalOffset = 0f;   // 위쪽이 막는 면
        }
    }
}
