using UnityEngine;

// 좌상단 "메뉴" 버튼 → "레포트 기록"(현재 진행 저장) 등. HUD에 같이 붙어 모든 씬에서 표시.
// 저장은 SaveSystem이 담당(인벤토리·스탯을 현재 슬롯에 기록).
public class MenuUI : MonoBehaviour
{
    private bool open;
    private string status;
    private float statusTimer;
    private GUIStyle btnStyle, statusStyle;

    void Update()
    {
        if (statusTimer > 0f) { statusTimer -= Time.unscaledDeltaTime; if (statusTimer <= 0f) status = null; }
    }

    void OnGUI()
    {
        EnsureStyles();
        UIScale.Apply();   // 해상도 독립 스케일
        const float bw = 84f, bh = 30f, m = 10f;
        if (GUI.Button(new Rect(m, m, bw, bh), "메뉴", btnStyle)) open = !open;

        if (!string.IsNullOrEmpty(status))
            GUI.Label(new Rect(m + bw + 8f, m + 5f, 340f, 22f), status, statusStyle);

        if (open)
        {
            const float pw = 180f, ph = 88f;
            Rect panel = new Rect(m, m + bh + 4f, pw, ph);
            GUI.Box(panel, GUIContent.none);
            if (GUI.Button(new Rect(panel.x + 10f, panel.y + 10f, pw - 20f, 32f), "레포트 기록"))
            { SaveReport(); open = false; }
            if (GUI.Button(new Rect(panel.x + 10f, panel.y + 48f, pw - 20f, 30f), "닫기"))
                open = false;
        }
    }

    private void SaveReport()
    {
        if (GameManager.Instance == null) { Flash("저장 실패 (GameManager 없음)"); return; }
        if (SaveSystem.CurrentSlot < 0) SaveSystem.CurrentSlot = 0;   // 활성 슬롯 없으면 1번 슬롯에 기록
        SaveSystem.SaveCurrent();
        Flash("레포트 기록됨 — 슬롯 " + (SaveSystem.CurrentSlot + 1));
    }

    private void Flash(string msg) { status = msg; statusTimer = 2.5f; }

    private void EnsureStyles()
    {
        if (btnStyle != null) return;
        btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
        statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        statusStyle.normal.textColor = new Color(1f, 0.88f, 0.45f);
    }
}
