using UnityEngine;

// StartScene 시작 메뉴 (OnGUI).
//  [메인] 새 게임 / 불러오기 / 종료
//   - 새 게임  → 슬롯 선택(빈 슬롯=시작, 찬 슬롯=덮어쓰기 확인)
//   - 불러오기 → 존재하는 세이브만 표시
//
// 저장/로드 로직은 전부 SaveSystem이 담당(이 스크립트는 '화면'만 담당).
// 나중에 UI 에셋으로 교체할 땐: 에셋 버튼에서 SaveSystem.NewGame / LoadGame / Read / Exists / Delete 만
// 호출하면 되고, 이 StartMenu 컴포넌트는 제거하면 됨. (로직-UI 분리)
public class StartMenu : MonoBehaviour
{
    public string startScene = "TutorialScene";   // 새 게임이 시작할 씬

    [Header("크기 (화면 높이 비율)")]
    public float titleRatio = 0.055f;
    public float buttonRatio = 0.028f;
    public float slotRatio = 0.024f;
    [Range(0.3f, 0.95f)] public float panelWidthRatio = 0.62f;

    private enum Page { Main, NewGame, Load }
    private Page page = Page.Main;
    private int confirmSlot = -1;   // 덮어쓰기 확인 중인 슬롯

    private GUIStyle titleStyle, headerStyle, btnStyle, slotStyle, smallStyle;

    void OnGUI()
    {
        EnsureStyles();
        float sw = Screen.width, sh = Screen.height;

        // 화면 크기에 맞춰 글씨 크기 스케일(해상도 달라도 일정하게 큼)
        titleStyle.fontSize  = Mathf.RoundToInt(sh * titleRatio);
        headerStyle.fontSize = Mathf.RoundToInt(sh * 0.030f);
        btnStyle.fontSize    = Mathf.RoundToInt(sh * buttonRatio);
        slotStyle.fontSize   = Mathf.RoundToInt(sh * slotRatio);
        smallStyle.fontSize  = Mathf.RoundToInt(sh * 0.022f);

        float w = Mathf.Min(sw * panelWidthRatio, 860f);
        float h = Mathf.Clamp(sh * 0.10f, 64f, 120f);   // 버튼 높이
        float gap = h * 0.28f;
        float x = (sw - w) * 0.5f;
        float y = sh * 0.13f;

        GUI.Label(new Rect(x, y, w, sh * 0.12f), "게임 제목 (임시)", titleStyle);
        y += sh * 0.17f;

        switch (page)
        {
            case Page.Main:    DrawMain(x, y, w, h, gap); break;
            case Page.NewGame: DrawSlots(x, y, w, h, gap, true); break;
            case Page.Load:    DrawSlots(x, y, w, h, gap, false); break;
        }
    }

    private void DrawMain(float x, float y, float w, float h, float gap)
    {
        if (GUI.Button(new Rect(x, y, w, h), "새 게임", btnStyle)) { page = Page.NewGame; confirmSlot = -1; }
        y += h + gap;
        if (GUI.Button(new Rect(x, y, w, h), "불러오기", btnStyle)) { page = Page.Load; }
        y += h + gap;
        if (GUI.Button(new Rect(x, y, w, h), "종료", btnStyle)) Application.Quit();
    }

    private void DrawSlots(float x, float y, float w, float h, float gap, bool newGameMode)
    {
        GUI.Label(new Rect(x, y, w, h * 0.7f), newGameMode ? "새 게임 — 슬롯 선택" : "불러오기 — 세이브 선택", headerStyle);
        y += h * 0.8f;

        int shown = 0;
        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            var data = SaveSystem.Read(i);

            if (newGameMode)
            {
                if (confirmSlot == i)   // 찬 슬롯 덮어쓰기 확인
                {
                    GUI.Label(new Rect(x, y, w * 0.5f, h), $"슬롯 {i + 1} 덮어쓸까요?", slotStyle);
                    if (GUI.Button(new Rect(x + w * 0.52f, y, w * 0.22f, h), "예", btnStyle)) SaveSystem.NewGame(i, startScene);
                    if (GUI.Button(new Rect(x + w * 0.76f, y, w * 0.24f, h), "아니오", btnStyle)) confirmSlot = -1;
                }
                else if (data == null)
                {
                    if (GUI.Button(new Rect(x, y, w, h), $"슬롯 {i + 1}   (비어있음)   + 새 게임", slotStyle)) SaveSystem.NewGame(i, startScene);
                }
                else
                {
                    if (GUI.Button(new Rect(x, y, w, h), $"슬롯 {i + 1}   {data.sceneName} ({data.lastPlayed})   [덮어쓰기]", slotStyle)) confirmSlot = i;
                }
                y += h + gap;
                shown++;
            }
            else
            {
                if (data == null) continue;   // 불러오기: 빈 슬롯은 숨김
                if (GUI.Button(new Rect(x, y, w - h - 6, h), $"슬롯 {i + 1}   {data.sceneName} ({data.lastPlayed})", slotStyle)) SaveSystem.LoadGame(i);
                if (GUI.Button(new Rect(x + w - h, y, h, h), "삭제", smallStyle)) SaveSystem.Delete(i);
                y += h + gap;
                shown++;
            }
        }

        if (!newGameMode && shown == 0)
        {
            GUI.Label(new Rect(x, y, w, h), "저장된 게임이 없습니다.", slotStyle);
            y += h + gap;
        }

        if (GUI.Button(new Rect(x, y + gap, w, h * 0.85f), "← 뒤로", btnStyle)) { page = Page.Main; confirmSlot = -1; }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle  = new GUIStyle(GUI.skin.label)  { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        headerStyle = new GUIStyle(GUI.skin.label)  { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        btnStyle    = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        slotStyle   = new GUIStyle(GUI.skin.button);
        smallStyle  = new GUIStyle(GUI.skin.button);
    }
}
