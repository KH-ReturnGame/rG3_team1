using UnityEngine;

// ESC 일시정지 메뉴 (구 좌상단 '메뉴' 버튼을 대체 — 클래스명 유지로 기존 HUD 배선 그대로).
//  ESC → 시간 정지 + 어두운 오버레이 + 메뉴: 계속하기 / 레포트 기록(저장) / 타이틀로 / 게임 종료.
//  · 다른 UI가 열려 있으면 ESC는 '그 UI 닫기'가 우선 — 닫은 직후 0.15초는 일시정지가 안 열림(이중 반응 방지).
//  · 컷씬(레터박스)·결과창(이미 시간정지) 중에는 열리지 않음.
//  · 스타일은 타이틀 화면과 동일한 투명 톤(오버레이 + 텍스트 메뉴 + 호버 오렌지 바).
public class MenuUI : MonoBehaviour
{
    public static bool Paused { get; private set; }

    private float prevTimeScale = 1f;   // 일시정지 직전 timeScale(슬로우모션 복원용)
    private float lastUIOpenTime;       // 다른 UI가 마지막으로 열려 있던 시각
    private string status;              // "기록 완료" 등 피드백
    private float statusTimer;
    private int page;                   // 0=메인 / 1=설정
    private int draggingSlider = -1;    // 드래그 중인 슬라이더 id(-1=없음)

    private GUIStyle titleStyle, itemStyle, statusStyle, setLabelSt, setValSt;

    void Update()
    {
        if (statusTimer > 0f) { statusTimer -= Time.unscaledDeltaTime; if (statusTimer <= 0f) status = null; }

        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene == "StartScene") { if (Paused) Resume(); return; }

        if ((Inventory.IsUIOpen || HelpPopupUI.ManualOpen) && !Paused) lastUIOpenTime = Time.unscaledTime;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Paused) { if (page == 1) page = 0; else Resume(); }   // 설정 페이지에선 ESC = 뒤로
            else if (!Inventory.IsUIOpen
                  && !HelpPopupUI.ManualOpen                        // ESC는 도움말 닫기 우선
                  && Time.unscaledTime - lastUIOpenTime > 0.15f   // 방금 다른 UI/도움말을 닫은 ESC와 분리
                  && !Letterbox.Covering                            // 컷씬 중 금지
                  && Time.timeScale > 0.01f)                        // 결과창 등 이미 정지 상태면 금지
                Pause();
        }
    }

    private void Pause()
    {
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Paused = true;
        page = 0;
        draggingSlider = -1;
        Inventory.PauseOpen = true;
    }

    private void Resume()
    {
        Time.timeScale = prevTimeScale > 0.01f ? prevTimeScale : 1f;
        Paused = false;
        Inventory.PauseOpen = false;
    }

    void OnGUI()
    {
        if (!Paused) return;
        EnsureStyles();
        float sw = Screen.width, sh = Screen.height;
        Vector2 m = Event.current.mousePosition;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;

        // 어두운 오버레이(게임 화면이 비쳐 보이게)
        UITheme.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.66f));

        // 제목 + 장식(◆ 다이아 + 양끝 다이아 구분선 — 금테 문법)
        titleStyle.fontSize = Mathf.RoundToInt(sh * 0.052f);
        GUI.Label(new Rect(0, sh * 0.20f, sw, sh * 0.08f), "일시정지", titleStyle);
        float lineW = Mathf.Min(sw * 0.24f, 340f);
        UITheme.Divider((sw - lineW) * 0.5f, sh * 0.285f, lineW, 0.85f);
        UITheme.Diamond(new Vector2(sw * 0.5f, sh * 0.285f + 1f), 9f, UITheme.Accent);   // 중앙 큰 다이아

        float rowH = sh * 0.064f, gap = rowH * 0.26f;
        float y = sh * 0.36f;

        if (page == 1) { DrawSettings(sw, sh, y, rowH, gap, m, click); return; }

        // 메뉴(타이틀 화면과 같은 텍스트 리스트 + 호버)
        itemStyle.fontSize = Mathf.RoundToInt(sh * 0.030f);
        string[] items = { "계속하기", "설정", "레포트 기록", "타이틀로", "게임 종료" };
        for (int i = 0; i < items.Length; i++)
        {
            Rect r = new Rect(sw * 0.5f - 170f, y + i * (rowH + gap), 340f, rowH);
            bool hv = r.Contains(m);
            if (hv)
            {
                UITheme.Glow(r, UITheme.Accent, 7f, 0.16f);
                UITheme.Fill(new Rect(r.x + 30f, r.y + rowH * 0.22f, 4f, rowH * 0.56f), UITheme.Accent);
                itemStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(r.x + 42f, r.y, 28f, rowH), "▸", itemStyle);
            }
            else itemStyle.normal.textColor = new Color(0.74f, 0.75f, 0.78f);
            GUI.Label(r, items[i], itemStyle);

            if (hv && click)
            {
                switch (i)
                {
                    case 0: Resume(); break;
                    case 1: page = 1; break;   // 설정
                    case 2: SaveReport(); break;
                    case 3:   // 타이틀로 — 진행 저장 후 이동
                        SaveSystem.SaveCurrent();
                        Resume();
                        SceneFader.FadeToScene("StartScene");
                        break;
                    case 4:   // 게임 종료 — 진행 저장 후 종료
                        SaveSystem.SaveCurrent();
                        Application.Quit();
                        break;
                }
                Event.current.Use();
            }
        }

        // 저장 피드백
        if (!string.IsNullOrEmpty(status))
        {
            statusStyle.fontSize = Mathf.RoundToInt(sh * 0.021f);
            GUI.Label(new Rect(0, y + items.Length * (rowH + gap) + sh * 0.015f, sw, sh * 0.04f), status, statusStyle);
        }
    }

    // ── 설정 페이지: 볼륨 3종(즉시 적용·저장) + 난이도 ──
    private void DrawSettings(float sw, float sh, float y, float rowH, float gap, Vector2 m, bool click)
    {
        float w = Mathf.Min(sw * 0.42f, 560f);
        float x = (sw - w) * 0.5f;
        setLabelSt.fontSize = Mathf.RoundToInt(sh * 0.023f);
        setValSt.fontSize = Mathf.RoundToInt(sh * 0.020f);
        float rh = sh * 0.058f;

        // 드래그 종료 감지(효과음 미리듣기)
        int released = -1;
        if (Event.current.type == EventType.MouseUp && draggingSlider >= 0) { released = draggingSlider; draggingSlider = -1; }

        float vy = y;
        float v0 = AudioManager.MasterVolume, v1 = AudioManager.BgmVolume, v2 = AudioManager.SfxVolume;
        float n0 = VolumeRow(new Rect(x, vy, w, rh), "마스터 음량", v0, 0, m); vy += rh + gap;
        float n1 = VolumeRow(new Rect(x, vy, w, rh), "배경음",     v1, 1, m); vy += rh + gap;
        float n2 = VolumeRow(new Rect(x, vy, w, rh), "효과음",     v2, 2, m); vy += rh + gap;
        if (n0 != v0) AudioManager.MasterVolume = n0;   // 변경 시에만 저장(매 프레임 PlayerPrefs.Save 방지)
        if (n1 != v1) AudioManager.BgmVolume = n1;
        if (n2 != v2) AudioManager.SfxVolume = n2;
        if (released == 0 || released == 2) AudioManager.Sfx("parry_just");   // 손 떼면 미리듣기(BGM은 실시간이라 불필요)

        // 난이도(클릭 토글)
        Rect dr = new Rect(x, vy, w, rh);
        bool easy = Difficulty.Current == Difficulty.Mode.Easy;
        bool dhv = dr.Contains(m);
        setLabelSt.normal.textColor = new Color(0.80f, 0.81f, 0.78f);
        GUI.Label(new Rect(dr.x, dr.y, w * 0.4f, rh), "난이도", setLabelSt);
        setValSt.normal.textColor = dhv ? Color.white : (easy ? new Color(0.85f, 0.88f, 0.80f) : UITheme.Danger);
        var pa = setValSt.alignment; setValSt.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(dr.x, dr.y, w, rh), "‹  " + Difficulty.Label + "  ›", setValSt);
        setValSt.alignment = pa;
        if (dhv && click) { Difficulty.Current = easy ? Difficulty.Mode.Hard : Difficulty.Mode.Easy; Event.current.Use(); }
        vy += rh + gap;

        UITheme.Divider(x, vy, w, 0.4f);
        vy += gap;

        // 뒤로
        Rect back = new Rect(sw * 0.5f - 100f, vy, 200f, rowH * 0.8f);
        bool bhv = back.Contains(m);
        itemStyle.fontSize = Mathf.RoundToInt(sh * 0.026f);
        itemStyle.normal.textColor = bhv ? Color.white : new Color(0.74f, 0.75f, 0.78f);
        if (bhv) UITheme.Fill(new Rect(back.x + 24f, back.y + back.height * 0.22f, 4f, back.height * 0.56f), UITheme.Accent);
        GUI.Label(back, "←  뒤로", itemStyle);
        if (bhv && click) { page = 0; Event.current.Use(); }
    }

    // 볼륨 한 줄: 라벨 + 커스텀 슬라이더(먹색 트랙+민트 채움+금 다이아 핸들) + %
    private float VolumeRow(Rect r, string label, float value, int id, Vector2 m)
    {
        setLabelSt.normal.textColor = new Color(0.80f, 0.81f, 0.78f);
        GUI.Label(new Rect(r.x, r.y, r.width * 0.30f, r.height), label, setLabelSt);

        Rect track = new Rect(r.x + r.width * 0.32f, r.center.y - 4f, r.width * 0.52f, 8f);
        Rect hit = new Rect(track.x - 8f, r.y, track.width + 16f, r.height);   // 잡기 쉬운 히트 영역
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hit.Contains(m))
        { draggingSlider = id; Event.current.Use(); }
        if (draggingSlider == id) value = Mathf.Clamp01((m.x - track.x) / track.width);

        UITheme.Fill(track, new Color(0.05f, 0.085f, 0.085f, 0.95f));                                  // 트랙(먹색)
        if (value > 0f) UITheme.FillV(new Rect(track.x + 1f, track.y + 1f, (track.width - 2f) * value, track.height - 2f),
                                       UITheme.Good, new Color(0.13f, 0.42f, 0.35f));                  // 민트 채움
        UITheme.Border2(track, 1f, UITheme.A(UITheme.Accent, 0.7f));
        UITheme.Diamond(new Vector2(track.x + track.width * value, track.center.y), 11f,
                        draggingSlider == id ? UITheme.Warm : UITheme.Accent);                          // 금 다이아 핸들

        setValSt.normal.textColor = UITheme.A(UITheme.Gold, 0.9f);
        var pa = setValSt.alignment; setValSt.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(r.x, r.y, r.width, r.height), Mathf.RoundToInt(value * 100f) + " %", setValSt);
        setValSt.alignment = pa;
        return value;
    }

    private void SaveReport()
    {
        SaveSystem.SaveCurrent();
        status = "현재까지의 여정을 레포트에 기록했다.";
        statusTimer = 2.5f;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = new Color(0.93f, 0.90f, 0.85f);
        titleStyle.hover.textColor = titleStyle.normal.textColor;
        itemStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        statusStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        statusStyle.normal.textColor = UITheme.Good;
        setLabelSt = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
        setLabelSt.hover.textColor = setLabelSt.normal.textColor;
        setValSt = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
        setValSt.hover.textColor = setValSt.normal.textColor;
    }
}
