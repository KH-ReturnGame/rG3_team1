using UnityEngine;

// 퀘스트 트래커 HUD(자동부팅·영구, OnGUI). 원신풍 — 왼쪽 화면 중앙쯤에 추적 퀘스트 이름+목표를 작게 표시.
//  · [V]로 길찾기 토글 → 목표 위치(길잡이=하강 포탈 / 채집=가까운 채집물 / 처치=가까운 적)로 방향 화살표+거리 표시(미니맵 없이도 동작).
//  · 미니맵이 있으면 그쪽에도 퀘스트 마커가 뜸(Minimap이 QuestTracker.PathActive / TryGetPathTarget 참조).
public class QuestTracker : MonoBehaviour
{
    public static QuestTracker Instance { get; private set; }
    public static bool PathActive { get; private set; }
    public KeyCode pathKey = KeyCode.V;

    private int pathIdx = -1;   // 길찾기 초점 트랙 인덱스(-1=꺼짐). [V]로 트랙을 순환하며 길찾기.

    private Texture2D white;
    private GUIStyle iconStyle, titleStyle, objStyle, hintStyle, arrowStyle, distStyle, tagStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) { var go = new GameObject("QuestTracker"); Instance = go.AddComponent<QuestTracker>(); DontDestroyOnLoad(go); } }

    // 현재 표시할 추적 퀘스트 목록: 스토리 트랙, 망토 트랙 순(둘 다 병행 표시). 메인이 없으면 사이드 하나.
    private static readonly System.Collections.Generic.List<Quest> _list = new System.Collections.Generic.List<Quest>();
    private System.Collections.Generic.List<Quest> ActiveList()
    {
        _list.Clear();
        var qm = QuestManager.Instance;
        if (qm == null) return _list;
        var story = qm.GetActiveMain(QuestTrack.Story); if (story != null) _list.Add(story);
        var hood  = qm.GetActiveMain(QuestTrack.Hood);  if (hood != null)  _list.Add(hood);
        if (_list.Count == 0) { var t = qm.GetTracked(); if (t != null) _list.Add(t); }   // 사이드 퀘스트만 있을 때
        return _list;
    }

    void Update()
    {
        if (Input.GetKeyDown(pathKey) && !Inventory.IsUIOpen)
        {
            var list = ActiveList();
            if (list.Count == 0) { PathActive = false; pathIdx = -1; return; }
            pathIdx++;                                    // [V]: 트랙 순환 → 마지막 다음엔 꺼짐
            if (pathIdx >= list.Count) pathIdx = -1;
            PathActive = pathIdx >= 0;
            if (PathActive && QuestManager.Instance != null) QuestManager.Instance.SetTracked(list[pathIdx]);
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

        // 보스 대상(메인 체인 후반): 현재 씬의 보스 위치
        if (q.pathToBoss && BossEnemy.Active != null)
        { pos = BossEnemy.Active.transform.position; return true; }

        // 대상 씬이 지정된 문(메인 체인): targetScene 정확 매치만(씬에 그 문이 없으면 화살표 미표시)
        if (!string.IsNullOrEmpty(q.pathDoorScene))
        {
            foreach (var d in Object.FindObjectsByType<SceneDoor>(FindObjectsSortMode.None))
                if (d.targetScene == q.pathDoorScene) { pos = d.transform.position; return true; }
            if (!q.pathToDescend) return false;   // pathToDescend 겸용 퀘스트는 아래 폴백 계속
        }

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
        if (Letterbox.Covering) return;   // 컷씬(레터박스) 중엔 HUD 숨김
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "StartScene") return;
        if (Inventory.IsUIOpen) return;
        var list = ActiveList();
        if (list.Count == 0) { PathActive = false; pathIdx = -1; return; }
        if (pathIdx >= list.Count) { pathIdx = -1; PathActive = false; }   // 트랙이 완료돼 줄면 초점 리셋
        EnsureStyles();

        float pad = 10f, w = 300f;
        float x = 14f, y = Screen.height * 0.34f;
        Color gold = new Color(1f, 0.86f, 0.45f);
        float cursor = y;

        for (int i = 0; i < list.Count; i++)
        {
            var q = list[i];
            bool focused = PathActive && pathIdx == i;
            bool hasPathRow = focused;
            float ph = 58f + (hasPathRow ? 40f : 0f);

            // 패널(초점=밝은 금테, 비초점=은은)
            Fill(new Rect(x, cursor, w, ph), UITheme.A(UITheme.BgSolid, focused ? 0.72f : 0.55f));
            Fill(new Rect(x, cursor, 3f, ph), UITheme.A(UITheme.Accent, focused ? 1f : 0.6f));

            // 트랙 태그 칩([스토리]/[망토]) — 메인 퀘스트만
            float titleX = x + pad;
            if (q.category == QuestCategory.Main)
            {
                bool hoodT = q.track == QuestTrack.Hood;
                string tag = hoodT ? "망토" : "스토리";
                Color tc = hoodT ? new Color(0.95f, 0.55f, 0.35f) : new Color(0.55f, 0.78f, 1f);
                float tw = tagStyle.CalcSize(new GUIContent(tag)).x + 12f;
                Rect tr = new Rect(x + pad, cursor + 8f, tw, 18f);
                Fill(tr, UITheme.A(tc, 0.20f));
                Fill(new Rect(tr.x, tr.y, 2f, tr.height), UITheme.A(tc, 0.9f));
                tagStyle.normal.textColor = tc;
                GUI.Label(new Rect(tr.x + 6f, tr.y - 1f, tw, 20f), tag, tagStyle);
                titleX = tr.xMax + 8f;
            }

            // 제목
            titleStyle.normal.textColor = focused ? gold : new Color(0.9f, 0.82f, 0.55f);
            GUI.Label(new Rect(titleX, cursor + 6f, x + w - titleX - 6f, 22f), q.title, titleStyle);

            // 목표
            objStyle.normal.textColor = new Color(0.86f, 0.92f, 1f);
            GUI.Label(new Rect(x + pad + 4f, cursor + 32f, w - pad - 10f, 22f), q.ObjectiveText(), objStyle);

            // 초점 트랙의 길찾기 행
            if (hasPathRow)
            {
                Vector2 tp; var pc = PlayerController.Instance;
                if (pc != null && TryGetPathTarget(out tp))
                {
                    Vector2 dir = tp - (Vector2)pc.transform.position;
                    Rect ar = new Rect(x + pad, cursor + 56f, 34f, 34f);
                    Fill(ar, UITheme.A(UITheme.Accent, 0.18f));
                    arrowStyle.normal.textColor = new Color(0.45f, 0.95f, 1f);
                    GUI.Label(ar, ArrowFor(dir), arrowStyle);
                    distStyle.normal.textColor = new Color(0.86f, 0.92f, 1f);
                    GUI.Label(new Rect(ar.xMax + 8f, cursor + 56f, w - 60f, 34f), Mathf.RoundToInt(dir.magnitude) + "m", distStyle);
                }
                else
                {
                    distStyle.normal.textColor = new Color(0.6f, 0.7f, 0.8f);
                    GUI.Label(new Rect(x + pad, cursor + 60f, w - pad, 24f), "이 구역엔 목표가 없습니다", distStyle);
                }
            }
            cursor += ph + 6f;
        }

        // [V] 안내(패널 묶음 아래 한 줄)
        hintStyle.normal.textColor = PathActive ? new Color(0.45f, 0.95f, 1f) : new Color(0.68f, 0.82f, 0.94f);
        string hint = list.Count > 1
            ? (PathActive ? "[V] 다음 목표 길찾기 / 끄기" : "[V] 길찾기 (트랙 전환)")
            : (PathActive ? "[V] 길찾기 끄기" : "[V] 버튼을 눌러서 길찾기");
        GUI.Label(new Rect(x + pad + 4f, cursor + 2f, w, 22f), hint, hintStyle);
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (titleStyle != null) return;
        iconStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        objStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false };
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        arrowStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        distStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
    }

    private void Fill(Rect r, Color c) { Color o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
}
