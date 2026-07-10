using UnityEngine;
using UnityEngine.Tilemaps;

// 숨겨진 통로 벽: 겉보기엔 벽이지만 통과할 수 있다. 플레이어가 안에 들어오면 반투명해져 내부가 보인다.
//  배치: 벽 스프라이트(또는 타일맵) 오브젝트 + Collider2D(영역)에 이 컴포넌트.
//   - 판정은 물리 이벤트가 아니라 '플레이어 좌표가 영역 안인지'를 매 프레임 검사 —
//     레이어 충돌 매트릭스/트리거 설정과 무관하게 항상 동작한다.
//   - 자식의 SpriteRenderer + TilemapRenderer 모두 페이드(타일맵 벽 지원).
//   - 가림 순서: 기본으로 sortingOrder를 강제해 안의 보물상자(5)·드랍(10)보다 항상 앞에 그려짐.
[RequireComponent(typeof(Collider2D))]
public class FakeWall : MonoBehaviour
{
    [Range(0f, 1f)] public float enteredAlpha = 0.3f;   // 들어왔을 때 투명도
    public float fadeSpeed = 4f;

    [Header("가림 순서(안의 상자·아이템을 덮도록)")]
    public bool forceSortingOrder = true;
    public int sortingOrder = 30;                        // 상자 5 / 드랍 10 보다 위

    private SpriteRenderer[] srs;
    private Tilemap[] tilemaps;
    private Collider2D area;
    private float target = 1f, cur = 1f;

    void Awake()
    {
        area = GetComponent<Collider2D>();
        if (area != null) area.isTrigger = true;   // 통과 가능하게(물리로 막지 않음)

        srs = GetComponentsInChildren<SpriteRenderer>(true);
        tilemaps = GetComponentsInChildren<Tilemap>(true);

        // 타일은 기본으로 색이 잠겨(LockColor) 있어 Tilemap.color가 안 먹는다 → 잠금 해제
        foreach (var tm in tilemaps)
        {
            var b = tm.cellBounds;
            for (int y = b.yMin; y < b.yMax; y++)
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    var p = new Vector3Int(x, y, 0);
                    if (tm.HasTile(p)) tm.SetTileFlags(p, TileFlags.None);
                }
        }

        // 안의 내용물(상자·아이템)을 확실히 가리도록 정렬 순서 강제
        if (forceSortingOrder)
        {
            foreach (var sr in srs) if (sr != null) sr.sortingOrder = sortingOrder;
            foreach (var tm in tilemaps)
            {
                var tr = tm.GetComponent<TilemapRenderer>();
                if (tr != null) tr.sortingOrder = sortingOrder;
            }
        }
    }

    private static bool helpShown;   // 비밀 통로 카드(세션 1회)

    void Update()
    {
        // 플레이어 좌표 기반 판정(레이어/트리거 설정 무관)
        var pc = PlayerController.Instance;
        bool inside = pc != null && area != null && area.OverlapPoint(pc.transform.position);
        target = inside ? enteredAlpha : 1f;

        // 처음으로 가짜 벽을 발견한 순간: 비밀 통로 카드
        if (inside && !helpShown && HelpPopupUI.Instance != null)
        {
            helpShown = true;
            HelpPopupUI.Instance.Show("fake_wall", "비밀 통로",
                "눈에 보이는 것이 전부가 아닙니다 — 이 벽은 *통과할 수 있는 가짜 벽*이었습니다.\n수상해 보이는 벽은 직접 걸어 들어가 보세요. 숨겨진 보물이 기다릴지도 모릅니다.");
        }

        if (Mathf.Abs(cur - target) < 0.005f) return;
        cur = Mathf.MoveTowards(cur, target, fadeSpeed * Time.deltaTime);

        if (srs != null)
            foreach (var sr in srs)
                if (sr != null) { Color col = sr.color; col.a = cur; sr.color = col; }
        if (tilemaps != null)
            foreach (var tm in tilemaps)
                if (tm != null) { Color col = tm.color; col.a = cur; tm.color = col; }
    }
}
