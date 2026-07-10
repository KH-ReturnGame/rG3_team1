using UnityEngine;

// StartScene 시작 메뉴 (OnGUI) — 타이틀 연출 포함.
//  배경: 어두운 그라데 + 지하 폐허 실루엣 + 아래에서 올라오는 잔불(ember) + 비네트.
//  로고: '레드' 붉은색 + '후드' 크림색 타이포 + RED HOOD 서브타이틀.
//  [메인] 새 게임 / 불러오기 / 종료 → 슬롯 페이지는 테마 패널.
// 저장/로드 로직은 전부 SaveSystem이 담당(이 스크립트는 '화면'만 담당).
public class StartMenu : MonoBehaviour
{
    public string startScene = "TutorialScene";   // 새 게임이 시작할 씬
    public string buildLabel = "경황제 데모 빌드";

    private enum Page { Main, NewGame, Load }
    private Page page = Page.Main;
    private int confirmSlot = -1;   // 덮어쓰기 확인 중인 슬롯

    private GUIStyle titleRed, titleCream, subStyle, menuStyle, headerStyle, slotStyle, slotSub, smallStyle, verStyle;

    // ── 배경 연출 상태 ──
    private struct Ember { public float x, y, size, speed, sway, phase; }
    private Ember[] embers;
    private float lastT;
    private Texture2D dot, vig;
    private float[] silhW, silhH;   // 폐허 실루엣(결정적 배치)

    // 플레이 중 재컴파일(도메인 리로드)에도 안전하게 — 비직렬화 배열은 OnGUI에서 지연 초기화
    private void EnsureInit()
    {
        if (embers != null && silhW != null) return;
        // 잔불 파티클 초기화(화면 비율 좌표 0~1)
        Random.State prev = Random.state;
        Random.InitState(20260704);
        embers = new Ember[42];
        for (int i = 0; i < embers.Length; i++)
            embers[i] = new Ember {
                x = Random.value, y = Random.value,
                size = Random.Range(2.5f, 6f),
                speed = Random.Range(0.018f, 0.05f),
                sway = Random.Range(0.6f, 1.6f),
                phase = Random.Range(0f, 6.28f)
            };
        // 폐허 실루엣: 폭/높이(화면 비율)
        int n = 11;
        silhW = new float[n]; silhH = new float[n];
        for (int i = 0; i < n; i++) { silhW[i] = Random.Range(0.05f, 0.12f); silhH[i] = Random.Range(0.06f, 0.22f); }
        Random.state = prev;
        lastT = Time.unscaledTime;
    }

    void OnGUI()
    {
        EnsureInit();
        EnsureStyles();
        float sw = Screen.width, sh = Screen.height;
        Vector2 m = Event.current.mousePosition;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;

        DrawBackground(sw, sh);

        // ── 로고 ──
        titleRed.fontSize = titleCream.fontSize = Mathf.RoundToInt(sh * 0.105f);
        subStyle.fontSize = Mathf.RoundToInt(sh * 0.022f);
        float ty = sh * 0.16f;
        string p1 = "Red", p2 = " Hood";
        Vector2 s1 = titleRed.CalcSize(new GUIContent(p1));
        Vector2 s2 = titleCream.CalcSize(new GUIContent(p2));
        float tx = (sw - s1.x - s2.x) * 0.5f;
        // 그림자 → 본문 (hover 색도 같이 고정 — 타이틀은 마우스에 반응하지 않음)
        var shadow = new Color(0f, 0f, 0f, 0.55f);
        SetCol(titleRed, shadow); SetCol(titleCream, shadow);
        GUI.Label(new Rect(tx + 3, ty + 4, s1.x, s1.y), p1, titleRed);
        GUI.Label(new Rect(tx + s1.x + 3, ty + 4, s2.x, s2.y), p2, titleCream);
        SetCol(titleRed, new Color(0.86f, 0.22f, 0.20f));
        SetCol(titleCream, new Color(0.93f, 0.90f, 0.85f));
        GUI.Label(new Rect(tx, ty, s1.x, s1.y), p1, titleRed);
        GUI.Label(new Rect(tx + s1.x, ty, s2.x, s2.y), p2, titleCream);
        // 서브타이틀 + 장식선
        GUI.Label(new Rect(0, ty + s1.y - sh * 0.012f, sw, sh * 0.03f), "P  r  o  j  e  c  t", subStyle);
        float lineY = ty + s1.y + sh * 0.028f;
        float lineW = Mathf.Min(sw * 0.34f, 480f);
        UITheme.Fill(new Rect((sw - lineW) * 0.5f, lineY, lineW, 2f), UITheme.A(UITheme.Accent, 0.7f));
        UITheme.Fill(new Rect(sw * 0.5f - 4f, lineY - 3f, 8f, 8f), UITheme.Accent);   // 중앙 다이아

        // ── 페이지 ──
        float y0 = sh * 0.47f;
        switch (page)
        {
            case Page.Main:    DrawMain(sw, sh, y0, m, click); break;
            case Page.NewGame: DrawSlots(sw, sh, m, click, true); break;
            case Page.Load:    DrawSlots(sw, sh, m, click, false); break;
        }

        // 빌드 라벨(우하단)
        verStyle.fontSize = Mathf.RoundToInt(sh * 0.017f);
        GUI.Label(new Rect(0, sh - sh * 0.045f, sw - 14f, sh * 0.03f), buildLabel, verStyle);
    }

    // ── 메인 메뉴(수직 리스트 + 호버 강조) ──
    private void DrawMain(float sw, float sh, float y, Vector2 m, bool click)
    {
        menuStyle.fontSize = Mathf.RoundToInt(sh * 0.034f);
        float rowH = sh * 0.072f, gap = rowH * 0.28f;
        string[] items = { "새 게임", "불러오기", "종료" };
        for (int i = 0; i < items.Length; i++)
        {
            Rect r = new Rect(sw * 0.5f - 180f, y + i * (rowH + gap), 360f, rowH);
            bool hv = r.Contains(m);
            if (hv)
            {
                UITheme.Glow(r, UITheme.Accent, 8f, 0.18f);
                UITheme.Fill(new Rect(r.x + 34f, r.y + rowH * 0.22f, 4f, rowH * 0.56f), UITheme.Accent);   // 좌측 오렌지 바
                SetCol(menuStyle, Color.white);
                GUI.Label(new Rect(r.x + 46f, r.y, 30f, rowH), "▸", menuStyle);
            }
            else SetCol(menuStyle, new Color(0.72f, 0.73f, 0.76f));
            GUI.Label(r, items[i], menuStyle);

            if (hv && click)
            {
                if (i == 0) { page = Page.NewGame; confirmSlot = -1; }
                else if (i == 1) page = Page.Load;
                else Application.Quit();
                Event.current.Use();
            }
        }

        // ── 난이도(클릭으로 전환) — "난이도 ‹ 쉬움 ›" 세 조각을 이어 그려 값만 색 강조 ──
        bool easy = Difficulty.Current == Difficulty.Mode.Easy;
        float dy = y + items.Length * (rowH + gap) + gap * 1.2f;
        Rect dr = new Rect(sw * 0.5f - 180f, dy, 360f, rowH * 0.6f);
        bool dhv = dr.Contains(m);

        smallStyle.fontSize = Mathf.RoundToInt(sh * 0.023f);
        var align0 = smallStyle.alignment; smallStyle.alignment = TextAnchor.MiddleLeft;
        Color dim = dhv ? Color.white : new Color(0.60f, 0.61f, 0.65f);
        Color val = easy ? new Color(0.85f, 0.88f, 0.80f) : new Color(0.90f, 0.32f, 0.28f);
        string s1 = "난이도   ‹ ", s2 = Difficulty.Label, s3 = " ›";
        float w1 = smallStyle.CalcSize(new GUIContent(s1)).x;
        float w2 = smallStyle.CalcSize(new GUIContent(s2)).x;
        float w3 = smallStyle.CalcSize(new GUIContent(s3)).x;
        float x0 = sw * 0.5f - (w1 + w2 + w3) * 0.5f;
        SetCol(smallStyle, dim);  GUI.Label(new Rect(x0, dr.y, w1 + 4f, dr.height), s1, smallStyle);
        SetCol(smallStyle, val);  GUI.Label(new Rect(x0 + w1, dr.y, w2 + 4f, dr.height), s2, smallStyle);
        SetCol(smallStyle, dim);  GUI.Label(new Rect(x0 + w1 + w2, dr.y, w3 + 4f, dr.height), s3, smallStyle);
        smallStyle.alignment = align0;

        // 설명 한 줄
        verStyle.fontSize = Mathf.RoundToInt(sh * 0.016f);
        var prevAlign = verStyle.alignment; verStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(sw * 0.5f - 300f, dr.yMax - 4f, 600f, sh * 0.03f),
            easy ? "위기의 순간, 예지(시간 감속)가 자동으로 발동합니다"
                 : "자동 예지 없음 — 모든 공격을 스스로 받아내야 합니다", verStyle);
        verStyle.alignment = prevAlign;

        if (dhv && click)
        {
            Difficulty.Current = easy ? Difficulty.Mode.Hard : Difficulty.Mode.Easy;
            Event.current.Use();
        }
    }

    // ── 슬롯 페이지(새 게임/불러오기 공용) — 타이틀과 같은 투명 톤, 구분은 헤어라인만 ──
    private void DrawSlots(float sw, float sh, Vector2 m, bool click, bool newGameMode)
    {
        float w = Mathf.Min(sw * 0.5f, 640f);
        float rowH = Mathf.Clamp(sh * 0.085f, 56f, 96f);
        float x = (sw - w) * 0.5f, y = sh * 0.43f;

        headerStyle.fontSize = Mathf.RoundToInt(sh * 0.024f);
        headerStyle.normal.textColor = new Color(0.60f, 0.61f, 0.65f);
        GUI.Label(new Rect(x, y, w, sh * 0.04f), newGameMode ? "새 게임 — 슬롯 선택" : "불러오기", headerStyle);
        y += sh * 0.055f;

        slotStyle.fontSize = Mathf.RoundToInt(sh * 0.024f);
        slotSub.fontSize = Mathf.RoundToInt(sh * 0.017f);
        smallStyle.fontSize = Mathf.RoundToInt(sh * 0.021f);

        Color hairline = UITheme.A(UITheme.Border, 0.28f);
        UITheme.Fill(new Rect(x, y, w, 1f), hairline);   // 목록 위 헤어라인

        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            var data = SaveSystem.Read(i);
            Rect r = new Rect(x, y, w, rowH);
            bool hv = r.Contains(m);
            bool empty = data == null;

            if (newGameMode && confirmSlot == i)
            {
                // 덮어쓰기 확인(행 그대로, 텍스트 버튼)
                UITheme.Fill(r, UITheme.A(UITheme.Danger, 0.08f));
                slotStyle.normal.textColor = UITheme.Text;
                GUI.Label(new Rect(r.x + 18f, r.y, r.width * 0.55f, rowH), "슬롯 " + (i + 1) + " — 덮어쓸까요?", slotStyle);
                if (TextBtn(new Rect(r.xMax - 170f, r.y, 70f, rowH), "예", UITheme.Danger, m, click)) SaveSystem.NewGame(i, startScene);
                if (TextBtn(new Rect(r.xMax - 92f, r.y, 80f, rowH), "아니오", new Color(0.62f, 0.63f, 0.67f), m, click)) confirmSlot = -1;
            }
            else
            {
                bool clickable = newGameMode || !empty;
                if (hv && clickable)
                {
                    UITheme.Fill(r, UITheme.A(Color.white, 0.045f));                          // 은은한 행 하이라이트
                    UITheme.Fill(new Rect(r.x, r.y + rowH * 0.22f, 3f, rowH * 0.56f), UITheme.Accent);   // 좌측 오렌지 바
                }

                if (empty)
                {
                    slotStyle.normal.textColor = newGameMode
                        ? (hv ? Color.white : new Color(0.72f, 0.73f, 0.76f))
                        : new Color(0.38f, 0.39f, 0.43f);
                    GUI.Label(new Rect(r.x + 18f, r.y, r.width - 36f, rowH), "슬롯 " + (i + 1) + "    " + (newGameMode ? "새 게임 시작" : "비어있음"), slotStyle);
                    if (newGameMode && hv && click) { SaveSystem.NewGame(i, startScene); Event.current.Use(); }
                }
                else
                {
                    slotStyle.normal.textColor = hv ? Color.white : UITheme.Text;
                    GUI.Label(new Rect(r.x + 18f, r.y + rowH * 0.10f, r.width - 120f, rowH * 0.5f), "슬롯 " + (i + 1) + "    " + data.sceneName, slotStyle);
                    GUI.Label(new Rect(r.x + 18f, r.y + rowH * 0.54f, r.width - 120f, rowH * 0.4f), data.lastPlayed + (newGameMode ? "  ·  선택하면 덮어쓰기" : ""), slotSub);

                    if (!newGameMode)
                    {
                        Rect del = new Rect(r.xMax - 76f, r.y, 64f, rowH);
                        bool delClicked = TextBtn(del, "삭제", UITheme.A(UITheme.Danger, 0.85f), m, click);
                        if (delClicked) { SaveSystem.Delete(i); Event.current.Use(); }
                        else if (hv && !del.Contains(m) && click) { SaveSystem.LoadGame(i); Event.current.Use(); }
                    }
                    else if (hv && click) { confirmSlot = i; Event.current.Use(); }
                }
            }

            y += rowH;
            UITheme.Fill(new Rect(x, y, w, 1f), hairline);   // 행 구분 헤어라인
        }

        // 뒤로(메인 메뉴와 같은 텍스트 스타일)
        if (TextBtn(new Rect(x, y + sh * 0.02f, 140f, rowH * 0.6f), "←  뒤로", new Color(0.62f, 0.63f, 0.67f), m, click))
        { page = Page.Main; confirmSlot = -1; }
    }

    // 투명 텍스트 버튼(호버 시 밝아짐). 클릭되면 true.
    private bool TextBtn(Rect r, string label, Color c, Vector2 m, bool click)
    {
        bool hv = r.Contains(m);
        smallStyle.normal.textColor = hv ? UITheme.Lighten(c, 0.25f) : c;
        GUI.Label(r, label, smallStyle);
        if (hv) UITheme.Fill(new Rect(r.x + r.width * 0.5f - 16f, r.yMax - r.height * 0.18f, 32f, 1.5f), UITheme.A(c, 0.8f));   // 밑줄
        return hv && click;
    }

    // ── 배경: 그라데 + 하단 화광 + 폐허 실루엣 + 잔불 + 비네트 ──
    private void DrawBackground(float sw, float sh)
    {
        // 그라데(위 짙은 어둠 → 아래 살짝 따뜻한 어둠)
        UITheme.FillV(new Rect(0, 0, sw, sh), new Color(0.045f, 0.045f, 0.06f), new Color(0.11f, 0.075f, 0.055f));
        // 하단 중앙 화광(지하의 불빛)
        UITheme.Glow(new Rect(sw * 0.15f, sh * 0.92f, sw * 0.7f, sh * 0.25f), new Color(0.95f, 0.45f, 0.12f), 60f, 0.10f);

        // 폐허 실루엣(하단, 화광보다 앞 = 역광)
        float bx = 0f;
        var silCol = new Color(0.028f, 0.028f, 0.038f, 1f);
        for (int i = 0; i < silhW.Length; i++)
        {
            float wpx = silhW[i] * sw, hpx = silhH[i] * sh;
            UITheme.Fill(new Rect(bx, sh - hpx, wpx, hpx), silCol);
            if (i % 3 == 0) UITheme.Fill(new Rect(bx + wpx * 0.5f, sh - hpx - sh * 0.05f, 2f, sh * 0.05f), silCol);   // 안테나
            bx += wpx * 0.92f;
            if (bx > sw) break;
        }

        // 잔불(Repaint에서만 이동)
        if (dot == null) BuildTextures();
        if (Event.current.type == EventType.Repaint)
        {
            float t = Time.unscaledTime;
            float dt = Mathf.Min(0.05f, t - lastT); lastT = t;
            for (int i = 0; i < embers.Length; i++)
            {
                embers[i].y -= embers[i].speed * dt;
                if (embers[i].y < -0.04f) { embers[i].y = 1.04f; embers[i].x = Random.value; }
            }
        }
        var prev = GUI.color;
        for (int i = 0; i < embers.Length; i++)
        {
            var e = embers[i];
            float wob = Mathf.Sin(Time.unscaledTime * e.sway + e.phase) * 0.008f;
            float flick = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.2f + e.phase * 3f);
            float fade = Mathf.Clamp01(e.y * 3f) * Mathf.Clamp01((1.05f - e.y) * 3f);
            GUI.color = new Color(1f, 0.62f, 0.22f, 0.5f * flick * fade);
            float s = e.size * (sh / 1080f + 0.4f);
            GUI.DrawTexture(new Rect((e.x + wob) * sw - s * 0.5f, e.y * sh - s * 0.5f, s, s), dot);
        }
        GUI.color = prev;

        // 비네트
        GUI.color = new Color(0f, 0f, 0f, 0.52f);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), vig);
        GUI.color = prev;
    }

    private void BuildTextures()
    {
        // 소프트 원형 점(잔불)
        const int D = 32;
        dot = new Texture2D(D, D, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var dp = new Color[D * D];
        for (int y = 0; y < D; y++)
            for (int x = 0; x < D; x++)
            {
                float dx = (x / (float)(D - 1) - 0.5f) * 2f, dy = (y / (float)(D - 1) - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                dp[y * D + x] = new Color(1f, 1f, 1f, a * a);
            }
        dot.SetPixels(dp); dot.Apply();

        // 방사형 비네트(중앙 투명 → 가장자리 검정) — 수동 smoothstep
        const int N = 128;
        vig = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var vp = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x / (float)(N - 1) - 0.5f) * 2f, dy = (y / (float)(N - 1) - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;
                float t = Mathf.Clamp01((r - 0.55f) / (1.0f - 0.55f));
                float a = t * t * (3f - 2f * t);
                vp[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        vig.SetPixels(vp); vig.Apply();
    }

    // normal·hover 색을 같이 지정 — IMGUI 기본 스킨의 hover 색 반응 차단
    private static void SetCol(GUIStyle st, Color c) { st.normal.textColor = c; st.hover.textColor = c; st.active.textColor = c; }

    private void EnsureStyles()
    {
        if (titleRed != null) return;
        titleRed   = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        titleCream = new GUIStyle(titleRed);
        subStyle   = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        SetCol(subStyle, new Color(0.55f, 0.50f, 0.46f));
        menuStyle  = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        headerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        headerStyle.normal.textColor = new Color(0.90f, 0.95f, 1f);
        slotStyle  = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
        slotSub    = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
        slotSub.normal.textColor = new Color(0.55f, 0.56f, 0.60f);
        smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        verStyle   = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
        verStyle.normal.textColor = new Color(0.42f, 0.40f, 0.38f);
    }
}
