using UnityEngine;

// 원웨이(통과) 플랫폼: 아래/옆에서 점프로 뚫고 올라가 위에 착지할 수 있는 발판.
// 이 컴포넌트를 발판 오브젝트(콜라이더 있는 것)에 붙이면 PlatformEffector2D를 자동 설정한다.
// 플레이어는 발판 위에서 아래(S/↓)+스페이스로 발판 아래로 내려갈 수 있다(PlayerController가 처리).
//
// 세팅:
//   A) 스프라이트 발판: BoxCollider2D 위에 이 컴포넌트 추가 → 콜라이더가 이펙터를 쓰도록 자동 구성
//   B) 타일맵 발판(별도 타일맵 레이어 권장): TilemapCollider2D 위에 이 컴포넌트 추가 →
//      CompositeCollider2D + Static Rigidbody2D를 자동 생성·구성(타일맵→Composite→Effector 정석 연결)
//   ★ 두 경우 모두 오브젝트 Layer를 Ground(플레이어 groundLayer와 동일)로 둘 것 → 착지/점프리셋/내려가기 정상 동작
[RequireComponent(typeof(PlatformEffector2D))]
public class OneWayPlatform : MonoBehaviour
{
    [Tooltip("막는 면의 각도(도). 작을수록 위쪽만 막고 옆은 더 잘 통과. 보통 그대로(170)")]
    public float surfaceArc = 170f;

    void Reset() { Setup(); }
    void Awake() { Setup(); }

    void Setup()
    {
        var eff = GetComponent<PlatformEffector2D>();

        // 타일맵(TilemapCollider2D)이면 Composite 경로가 필요 — 정석 연결 자동 구성
        var tilemapCol = GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>();
        var composite = GetComponent<CompositeCollider2D>();
        bool tilemapMode = tilemapCol != null;

        if (tilemapMode)
        {
            // Composite + Static Rigidbody2D 보장
            if (composite == null) composite = gameObject.AddComponent<CompositeCollider2D>();
            var rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;     // Composite는 Rigidbody 필요 — 안 움직이게 Static

            // 피더 콜라이더(타일맵 등)는 Composite로 보냄(이펙터는 Composite에만)
            var cols = GetComponents<Collider2D>();
            for (int i = 0; i < cols.Length; i++)
            {
                Collider2D c = cols[i];
                if (c == null || c == composite || c.isTrigger) continue;
                c.usedByComposite = true;
                c.usedByEffector = false;
            }
            composite.usedByEffector = true;
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;   // 이펙터엔 Polygons가 안정적
            composite.GenerateGeometry();
        }
        else
        {
            // 스프라이트 발판: 일반 콜라이더가 이펙터를 쓰도록
            var cols = GetComponents<Collider2D>();
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null && !cols[i].isTrigger) cols[i].usedByEffector = true;
        }

        if (eff != null)
        {
            eff.useOneWay = true;
            eff.useOneWayGrouping = false;
            eff.surfaceArc = surfaceArc;
            eff.rotationalOffset = 0f;   // 위쪽이 막는 면
        }
    }
}
