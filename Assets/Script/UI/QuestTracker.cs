using UnityEngine;

// 퀘스트 트래커 HUD(자동부팅·영구, OnGUI). 원신풍 — 왼쪽 화면 중앙쯤에 추적 퀘스트 이름+목표를 작게 표시.
//  · [V]로 길찾기 토글 → 목표 위치(길잡이=하강 포탈 / 채집=가까운 채집물 / 처치=가까운 적)로 방향 화살표+거리 표시(미니맵 없이도 동작).
//  · 미니맵이 있으면 그쪽에도 퀘스트 마커가 뜸(Minimap이 QuestTracker.PathActive / TryGetPathTarget 참조).
public class QuestTracker : MonoBehaviour
{
    public static QuestTracker Instance { get; private set; }
    public static bool PathActive { get; private set; }
    public KeyCode pathKey = KeyCode.V;

    private Texture2D white;
    private GUIStyle iconStyle, titleStyle, objStyle, hintStyle, arrowStyle, distStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) { var go = new GameObject("QuestTracker"); Instance = go.AddComponent<QuestTracker>(); DontDestroyOnLoad(go); } }

    void Update()
    {
        if (Input.GetKeyDown(pathKey) && !Inventory.IsUIOpen)
        {
            var q = QuestManager.Instance != null ? QuestManager.Instance.GetTracked() : null;
            if (q != null) PathActive = !PathActive;
        }
    }

    // 추적 퀘스트의 월드 목표 위치(현재 씬). 길잡이=하강 포탈, 채집=가까운 채집물, 처치=가까운 적.
    public static bool TryGetPathTarget(out Vector2 pos)
    {
        pos = Vector2.zero;
        var qm = QuestManager.Instance;
        var q = qm != null ? qm.GetTracked() : null;
        if (q == null) return false;
        Vector2 p = PlayerController.Instance != null ? (Vector2)PlayerController.Instance.transform.position : Vector2.zero;

        if (q.pathToDescend)
        {
            SceneDoor best = null;
            foreach (var d in Object.FindObjectsByType<SceneDoor>(FindObjectsSortMode.None))
            { if (d.action == SceneDoor.DoorAction.AdvanceRunStage) { best = d; break; } if (best == null) best = d; }
            if (best == null) return false;
            pos = best.transform.position; return true;
        }

        Transform near = null; float bd = float.MaxValue;
        if (q.goal == QuestGoal.Gather)
            foreach (var g in Object.FindObjectsByType<GatheringSpawn>(FindObjectsSortMode.None))
            { float dd = ((Vector2)g.transform.position - p).sqrMagnitude; if (dd < bd) { bd = dd; near = g.transform; } }
        else
            foreach (var e in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            { float dd = ((Vector2)e.transform.position - p).sqrMagnitude; if (dd < bd) { bd = dd; near = e.transform; } }
        if (near == null) return false;
        pos = near.position; return true;
    }

    private static readonly string[] Arrows = { "→", "↗", "↑", "↖", "←", "↙", "↓", "↘" };
    private static string ArrowFor(Vector2 d)
    {
        int idx = Mathf.RoundToInt(Mathf.Atan2(d.y, d.x) / (Mathf.PI / 4f));
        idx = ((idx % 8) + 8) % 8;
        return Arrows[idx];
    }

    void OnGUI()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "StartScene") return;
        if (Inventory.IsUIOpen) return;
        var qm = QuestManager.Instance;
        var q = qm != null ? qm.GetTracked() : null;
        if (q == null) { PathActive = false; return; }
        EnsureStyles();

        float pad = 10f, w = 300f;
        float x = 14f, y = Screen.height * 0.40f;

        // 배경 패널(살짝 어둡게, 왼쪽 시안 띠)
        float h = PathActive ? 116f : 70f;
        Fill(new Rect(x, y, w, h), new Color(0.05f, 0.08f, 0.12f, 0.62f));
        Fill(new Rect(x, y, 3f, h), new Color(0.30f, 0.82f, 0.96f, 0.95f));

        // 제목 (◆ + 이름)
        Color gold = new Color(1f, 0.86f, 0.45f);
        iconStyle.normal.textColor = gold;
        GUI.Label(new Rect(x + pad, y + 8f, 18f, 22f), "◆", iconStyle);
        titleStyle.normal.textColor = gold;
        GUI.Label(new Rect(x + pad + 20f, y + 7f, w - pad - 26f, 24f), q.title, titleStyle);

        // 목표
        objStyle.normal.textColor = new Color(0.86f, 0.92f, 1f);
        GUI.Label(new Rect(x + pad + 4f, y + 34f, w - pad - 10f, 22f), q.ObjectiveText(), objStyle);

        // [V] 길찾기 힌트
        hintStyle.normal.textColor = PathActive ? new Color(0.45f, 0.95f, 1f) : new Color(0.55f, 0.70f, 0.82f);
        GUI.Label(new Rect(x + pad + 4f, y + 52f, w - pad - 10f, 18f), PathActive ? "[V] 길찾기 끄기" : "[V] 버튼을 눌러서 길찾기", hintStyle);

        // 길찾기: 방향 화살표 + 거리
        if (PathActive)
        {
            Vector2 tp;
            var pc = PlayerController.Instance;
            if (pc != null && TryGetPathTarget(out tp))
            {
                Vector2 dir = tp - (Vector2)pc.transform.position;
                float dist = dir.magnitude;
                Rect ar = new Rect(x + pad, y + 74f, 38f, 38f);
                Fill(ar, new Color(0.30f, 0.82f, 0.96f, 0.18f));
                arrowStyle.normal.textColor = new Color(0.45f, 0.95f, 1f);
                GUI.Label(ar, ArrowFor(dir), arrowStyle);
                distStyle.normal.textColor = new Color(0.86f, 0.92f, 1f);
                GUI.Label(new Rect(ar.xMax + 8f, y + 74f, w - 60f, 38f), Mathf.RoundToInt(dist) + "m", distStyle);
            }
            else
            {
                distStyle.normal.textColor = new Color(0.6f, 0.7f, 0.8f);
                GUI.Label(new Rect(x + pad, y + 78f, w - pad, 24f), "이 구역엔 목표가 없습니다", distStyle);
            }
        }
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (titleStyle != null) return;
        iconStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        objStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false };
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
        arrowStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        distStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
    }

    private void Fill(Rect r, Color c) { Color o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
}
