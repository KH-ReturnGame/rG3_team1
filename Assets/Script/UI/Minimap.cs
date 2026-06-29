using System.Collections.Generic;
using UnityEngine;

// 미니맵(자동부팅·영구 싱글톤, OnGUI). 우상단에 플레이어 중심 레이더식 표시.
//  · 상자(금)·출구/문(시안)·적(빨강)·채집물(초록)·마을 시설(상인/게시판/제작대)을 블립으로 표시
//  · 범위 밖이라도 '중요'한 것(상자·출구)은 가장자리에 방향 표시
//  · [M]으로 끄고 켬. 시작 메뉴·전체 UI(인벤/상점 등) 열렸을 땐 숨김.
public class Minimap : MonoBehaviour
{
    public static Minimap Instance;

    public KeyCode toggleKey = KeyCode.M;
    public bool visible = true;

    [Header("크기 / 위치 / 범위")]
    public float boxSize = 170f;        // 미니맵 한 변(px)
    public float margin = 14f;
    public float worldRange = 28f;      // 표시 반경(월드 유닛) — 작을수록 확대
    public float refreshInterval = 0.4f;

    private struct Blip { public Vector2 pos; public Color color; public float size; public bool important; }
    private readonly List<Blip> blips = new List<Blip>();
    private float nextScan;
    private Transform player;
    private static Texture2D _tex;

    private static readonly Color cChest = new Color(1f, 0.82f, 0.35f);
    private static readonly Color cExit  = new Color(0.30f, 0.80f, 0.95f);
    private static readonly Color cEnemy = new Color(0.95f, 0.35f, 0.35f);
    private static readonly Color cGather= new Color(0.50f, 0.90f, 0.50f);
    private static readonly Color cShop  = new Color(0.90f, 0.66f, 0.32f);
    private static readonly Color cBoard = new Color(0.72f, 0.46f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("Minimap");
        Instance = go.AddComponent<Minimap>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + refreshInterval; Scan(); }
    }

    private List<Bounds> scanAreas;   // 이번 스캔에서 '발견된 구역'(블립 필터링용)

    private void Scan()
    {
        blips.Clear();
        if (player == null && PlayerController.Instance != null) player = PlayerController.Instance.transform;
        scanAreas = MapDiscovery.DiscoveredAreas();   // 발견 구역만 블립 표시(fog-of-war)

        foreach (var c in TreasureChest.All)
            if (c != null && !c.IsOpened) AddBlip(c.transform.position, cChest, 6f, true);

        foreach (var d in FindObjectsByType<SceneDoor>(FindObjectsSortMode.None))
            AddBlip(d.transform.position, cExit, 6f, true);

        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            AddBlip(e.transform.position, cEnemy, 4f, false);

        foreach (var g in FindObjectsByType<GatheringSpawn>(FindObjectsSortMode.None))
            AddBlip(g.transform.position, cGather, 4f, false);

        foreach (var s in FindObjectsByType<ShopStation>(FindObjectsSortMode.None))
            AddBlip(s.transform.position, cShop, 5f, false);

        foreach (var q in FindObjectsByType<QuestBoard>(FindObjectsSortMode.None))
            AddBlip(q.transform.position, cBoard, 5f, false);
    }

    private void AddBlip(Vector2 p, Color c, float s, bool imp)
    {
        if (!MapDiscovery.InAreas(scanAreas, p)) return;   // 미발견 구역의 대상은 숨김
        blips.Add(new Blip { pos = p, color = c, size = s, important = imp });
    }

    void OnGUI()
    {
        if (!visible) return;
        if (GameManager.Instance == null || !GameManager.Instance.HasMinimap) return;   // 미니맵 모듈 해금 필요
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "StartScene") return;
        if (Inventory.IsUIOpen) return;   // 전체 UI 열렸을 땐 숨김
        if (player == null) { if (PlayerController.Instance != null) player = PlayerController.Instance.transform; else return; }
        UIScale.Apply();   // 해상도 독립 스케일

        float bs = boxSize;
        Rect box = new Rect(UIScale.W - bs - margin, margin, bs, bs);
        Fill(box, new Color(0.05f, 0.07f, 0.11f, 0.82f));
        Border(box, 2f, new Color(0.30f, 0.80f, 0.95f, 0.9f));

        Vector2 center = new Vector2(box.x + bs * 0.5f, box.y + bs * 0.5f);
        float half = bs * 0.5f - 6f;
        float scale = half / Mathf.Max(1f, worldRange);
        Vector2 pp = player.position;

        foreach (var b in blips)
        {
            Vector2 rel = (b.pos - pp) * scale;            // 월드 → 미니맵(스케일)
            Vector2 sp = new Vector2(center.x + rel.x, center.y - rel.y);   // y 반전(월드 위 = 화면 위)
            bool outside = Mathf.Abs(rel.x) > half || Mathf.Abs(rel.y) > half;
            if (outside)
            {
                if (!b.important) continue;                // 중요치 않은 건 범위 밖이면 생략
                Vector2 dir = ((Vector2)(b.pos - pp)).normalized;   // 가장자리에 방향 표시
                sp = center + new Vector2(dir.x, -dir.y) * half;
            }
            float s = b.size;
            Fill(new Rect(sp.x - s * 0.5f, sp.y - s * 0.5f, s, s), b.color);
        }

        // 플레이어(중앙)
        Fill(new Rect(center.x - 3f, center.y - 3f, 6f, 6f), Color.white);
        Border(new Rect(center.x - 3f, center.y - 3f, 6f, 6f), 1f, new Color(0.30f, 0.80f, 0.95f));
    }

    private static Texture2D Tex() { if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); } return _tex; }
    private static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Tex()); GUI.color = o; }
    private static void Border(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }
}
