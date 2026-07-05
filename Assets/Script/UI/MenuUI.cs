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

    private GUIStyle titleStyle, itemStyle, statusStyle;

    void Update()
    {
        if (statusTimer > 0f) { statusTimer -= Time.unscaledDeltaTime; if (statusTimer <= 0f) status = null; }

        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene == "StartScene") { if (Paused) Resume(); return; }

        if ((Inventory.IsUIOpen || HelpPopupUI.ManualOpen) && !Paused) lastUIOpenTime = Time.unscaledTime;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Paused) Resume();
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

        // 제목
        titleStyle.fontSize = Mathf.RoundToInt(sh * 0.052f);
        GUI.Label(new Rect(0, sh * 0.20f, sw, sh * 0.08f), "일시정지", titleStyle);
        float lineW = Mathf.Min(sw * 0.24f, 340f);
        UITheme.Fill(new Rect((sw - lineW) * 0.5f, sh * 0.285f, lineW, 2f), UITheme.A(UITheme.Accent, 0.7f));

        // 메뉴(타이틀 화면과 같은 텍스트 리스트 + 호버)
        itemStyle.fontSize = Mathf.RoundToInt(sh * 0.030f);
        float rowH = sh * 0.064f, gap = rowH * 0.26f;
        float y = sh * 0.36f;
        string[] items = { "계속하기", "레포트 기록", "타이틀로", "게임 종료" };
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
                    case 1: SaveReport(); break;
                    case 2:   // 타이틀로 — 진행 저장 후 이동
                        SaveSystem.SaveCurrent();
                        Resume();
                        SceneFader.FadeToScene("StartScene");
                        break;
                    case 3:   // 게임 종료 — 진행 저장 후 종료
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
    }
}
