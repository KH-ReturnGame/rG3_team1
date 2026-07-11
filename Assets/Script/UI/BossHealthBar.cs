using UnityEngine;

// 보스 체력바(자동부팅·영구). 씬에 BossEnemy가 활성일 때만 하단 중앙에 대형 게이지 표시.
//  금테+먹색 문법: 이름(크림) + 트랙 + 붉은 채움(부드럽게 따라옴) + 피격 칩 잔상 + 페이즈2 붉은 ◆.
public class BossHealthBar : MonoBehaviour
{
    public static BossHealthBar Instance;

    public float chipDelay = 0.35f;
    public float chipSpeed = 0.5f;    // 잔상 감소(비율/초)

    private float display = -1f, chip, chipWait;
    private GUIStyle nameSt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) { var go = new GameObject("BossHealthBar"); Instance = go.AddComponent<BossHealthBar>(); DontDestroyOnLoad(go); }
    }

    void Update()
    {
        var boss = BossEnemy.Active;
        if (boss == null || !boss.Encountered) { display = -1f; return; }   // 보스방(전투 시작) 전엔 숨김
        float cur = boss.HealthFrac;
        if (display < 0f) { display = cur; chip = cur; return; }

        float dt = Time.unscaledDeltaTime;
        if (cur < display - 0.0005f) { chip = Mathf.Max(chip, display); display = cur; chipWait = chipDelay; }
        else display = cur;
        if (chipWait > 0f) chipWait -= dt;
        else if (chip > display) chip = Mathf.Max(display, chip - chipSpeed * dt);
    }

    void OnGUI()
    {
        var boss = BossEnemy.Active;
        if (boss == null || !boss.Encountered || display < 0f) return;   // 전투 시작 후에만
        if (Letterbox.Covering) return;
        UIScale.Apply();
        EnsureStyles();
        float sw = UIScale.W, sh = UIScale.H;

        float w = Mathf.Min(sw * 0.56f, 860f), h = Mathf.Clamp(sh * 0.020f, 14f, 24f);
        float x = (sw - w) * 0.5f, y = sh - sh * 0.085f;

        // 이름 + 페이즈2 표식
        nameSt.fontSize = Mathf.RoundToInt(sh * 0.022f);
        nameSt.normal.textColor = new Color(0.95f, 0.90f, 0.78f);
        GUI.Label(new Rect(x, y - sh * 0.034f, w, sh * 0.03f), boss.bossName, nameSt);
        if (boss.Phase2)
        {
            float k = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
            UITheme.Diamond(new Vector2(x + w * 0.5f + nameSt.CalcSize(new GUIContent(boss.bossName)).x * 0.5f + 16f, y - sh * 0.019f), 9f, new Color(1f, 0.2f, 0.15f, k));
        }

        // 게이지: 트랙 + 칩 + 붉은 채움 + 금테 + 브래킷
        Rect bar = new Rect(x, y, w, h);
        UITheme.Fill(bar, new Color(0.05f, 0.07f, 0.075f, 0.95f));
        if (chip > display + 0.001f)
            UITheme.Fill(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * chip, bar.height - 2f), new Color(0.95f, 0.85f, 0.6f, 0.85f));
        if (display > 0f)
            UITheme.FillV(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * display, bar.height - 2f),
                          new Color(0.92f, 0.28f, 0.22f), new Color(0.55f, 0.08f, 0.10f));
        UITheme.Border2(bar, 1.2f, UITheme.A(UITheme.Accent, 0.85f));
        UITheme.Fill(new Rect(bar.x - 3f, bar.y - 3f, 2f, bar.height + 6f), UITheme.Accent);          // 브래킷 좌
        UITheme.Fill(new Rect(bar.xMax + 1f, bar.y - 3f, 2f, bar.height + 6f), UITheme.Accent);       // 브래킷 우
    }

    private void EnsureStyles()
    {
        if (nameSt != null) return;
        nameSt = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        nameSt.hover.textColor = nameSt.normal.textColor;
    }
}
