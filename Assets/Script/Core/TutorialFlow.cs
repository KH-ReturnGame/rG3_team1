// 온보딩 도움말 흐름(이벤트 기반). 새 게임 시작 시 Begin()으로 무장 →
//  · 첫 아이템 획득 → "배낭을 B로 열어보세요" 팁
//  · 첫 배낭 오픈 → "우클릭 메뉴로 사용/버리기" 팁
// 각 1회만. armed는 NewGame에서만 켜지므로(불러오기 X) 기존 플레이어에겐 안 뜸.
public static class TutorialFlow
{
    private static bool armed, itemTip, backpackTip;

    // 온보딩 팁 표시 방식(여기서 직접 조정). ManualTips=true: ESC/X로 닫기 / false: TipSeconds초 뒤 자동.
    public static bool ManualTips = true;
    public static float TipSeconds = 7f;

    public static void Begin() { armed = true; itemTip = false; backpackTip = false; }   // SaveSystem.NewGame에서 호출

    private static void ShowTip(string t, string b)
    {
        if (ManualTips) HelpPopupUI.Instance.ShowManual(t, b);
        else HelpPopupUI.Instance.ShowTimed(t, b, TipSeconds);
    }

    public static void OnItemAcquired()
    {
        if (!armed || itemTip || HelpPopupUI.Instance == null) return;
        itemTip = true;
        ShowTip("아이템 획득",
            "아이템을 손에 넣었습니다!\n[B] 키를 눌러 배낭을 열어 무엇을 주웠는지 확인해 보세요.");
    }

    public static void OnBackpackOpened()
    {
        if (!armed || backpackTip || HelpPopupUI.Instance == null) return;
        backpackTip = true;
        ShowTip("배낭 · 아이템 사용",
            "배낭의 아이템을 우클릭하면 메뉴가 열립니다.\n[사용]으로 바로 쓰거나, [N번 슬롯에 등록]하면 전투 중에도 숫자키로 즉시 사용할 수 있어요.");
    }
}
