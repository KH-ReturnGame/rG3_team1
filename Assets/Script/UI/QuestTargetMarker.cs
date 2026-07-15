using System.Collections.Generic;
using UnityEngine;

// 마을(허브) 퀘스트 대상 머리 위 화살표(자동부팅·영구, OnGUI).
//  · '낯선 지하 마을'(guide_village) 진행 중: 아직 방문 안 한 대상(엔지니어/상인/제작대/게시판) 위에 금색 ▼(둥실둥실)
//  · 마을 둘러보기 완료 후: 우물(지하 입구 SceneDoor) 위에 ▼ — 첫 보스를 잡을 때까지
//  0.5초마다 대상을 다시 스캔하므로 방문한 곳의 화살표는 곧바로 사라진다.
public class QuestTargetMarker : MonoBehaviour
{
    public static QuestTargetMarker Instance;

    public float bobAmp = 6f;        // 둥실 진폭(px)
    public float bobSpeed = 4f;
    public float yOffset = 1.15f;    // 대상 위 여백(월드) — 콜라이더가 있으면 그 위 기준

    private readonly List<Transform> targets = new List<Transform>();
    private float nextScan;
    private static Texture2D arrowTex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) { var go = new GameObject("QuestTargetMarker"); Instance = go.AddComponent<QuestTargetMarker>(); DontDestroyOnLoad(go); }
    }

    private static bool InHub => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "StartingArea";

    void Update()
    {
        if (!InHub || Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.5f;
        targets.Clear();
        var qm = QuestManager.Instance;
        if (qm == null) return;

        Quest guide = null;
        foreach (var q in qm.accepted) if (q.id == "guide_village") { guide = q; break; }
        if (guide != null)
        {
            if (!Visited(guide, "engineer")) AddFirst<EngineerStation>();
            if (!Visited(guide, "shop")) AddNearestShop();
            if (!Visited(guide, "craft")) AddFirst<CraftStation>();
            if (!Visited(guide, "board")) AddFirst<QuestBoard>();
        }
        else if (qm.completed.Contains("guide_village") && !qm.completed.Contains("mq_boss"))
        {
            // 마을 둘러보기 끝 → 우물(지하 입구)로 안내
            foreach (var d in FindObjectsByType<SceneDoor>(FindObjectsSortMode.None))
                if (d != null && d.targetScene == "Metroidvania") { targets.Add(d.transform); break; }
        }
    }

    private static bool Visited(Quest q, string id) => q.visited != null && q.visited.Contains(id);

    private void AddFirst<T>() where T : Component
    {
        var c = FindFirstObjectByType<T>();
        if (c != null) targets.Add(c.transform);
    }

    // 상점은 여러 명(재료/포션/탐험가) — 아무나 방문해도 인정되므로 플레이어와 가장 가까운 한 명만 표시
    private void AddNearestShop()
    {
        var pc = PlayerController.Instance;
        ShopStation best = null; float bd = float.MaxValue;
        foreach (var s in FindObjectsByType<ShopStation>(FindObjectsSortMode.None))
        {
            if (s == null) continue;
            float d = pc != null ? Vector2.Distance(s.transform.position, pc.transform.position) : 0f;
            if (best == null || d < bd) { best = s; bd = d; }
        }
        if (best != null) targets.Add(best.transform);
    }

    void OnGUI()
    {
        if (!InHub || targets.Count == 0 || Letterbox.Covering) return;
        var cam = Camera.main;
        if (cam == null) return;
        EnsureTex();
        float bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmp;

        var prev = GUI.color;
        foreach (var t in targets)
        {
            if (t == null) continue;
            Vector3 wp = t.position + Vector3.up * yOffset;
            var col = t.GetComponent<Collider2D>();
            if (col != null) wp.y = col.bounds.max.y + 0.45f;
            Vector3 sp = cam.WorldToScreenPoint(wp);
            if (sp.z <= 0f) continue;
            float x = sp.x, y = Screen.height - sp.y + bob;
            const float w = 30f, h = 24f;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);                    // 그림자
            GUI.DrawTexture(new Rect(x - w * 0.5f + 2f, y + 3f, w, h), arrowTex);
            GUI.color = new Color(1f, 0.80f, 0.25f, 0.95f);              // 금색(퀘스트 톤)
            GUI.DrawTexture(new Rect(x - w * 0.5f, y, w, h), arrowTex);
        }
        GUI.color = prev;
    }

    // 절차 생성: 아래를 향한 삼각형(▼) 텍스처
    private static void EnsureTex()
    {
        if (arrowTex != null) return;
        const int W = 32, H = 24;
        arrowTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            // GUI 기준 y=0이 위 — 텍스처는 아래가 뾰족해야 하므로 진행할수록 폭이 줄어든다(SetPixels는 아래부터라 뒤집어서)
            float k = 1f - (H - 1 - y) / (float)(H - 1);   // 텍스처 아랫줄(y=0)=1 → 윗줄=0
            float halfW = (W * 0.5f - 1f) * k;
            for (int x = 0; x < W; x++)
            {
                float dx = Mathf.Abs(x - (W - 1) * 0.5f);
                float a = Mathf.Clamp01(halfW - dx + 0.5f);   // 가장자리 살짝 안티앨리어싱
                px[y * W + x] = new Color(1f, 1f, 1f, a);
            }
        }
        arrowTex.SetPixels(px);
        arrowTex.Apply();
    }
}
