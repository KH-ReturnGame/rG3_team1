// 온보딩 도움말 흐름(이벤트 기반). 새 게임 시작 시 Begin()으로 무장 →
//  · 첫 아이템 획득 → "배낭을 B로 열어보세요" 팁
//  · 첫 배낭 오픈 → "핫바에 등록하면 숫자키로 사용" 팁
// 각 1회만. armed는 NewGame에서만 켜지므로(불러오기 X) 기존 플레이어에겐 안 뜸.
public static class TutorialFlow
{
    private static bool armed, itemTip, backpackTip;

    public static void Begin() { armed = true; itemTip = false; backpackTip = false; }   // SaveSystem.NewGame에서 호출

    public static void OnItemAcquired()
    {
        if (!armed || itemTip || HelpPopupUI.Instance == null) return;
        itemTip = true;
        HelpPopupUI.Instance.ShowManual("아이템 획득",
            "아이템을 손에 넣었습니다!\n[B] 키를 눌러 배낭을 열어 무엇을 주웠는지 확인해 보세요.");
    }

    public static void OnBackpackOpened()
    {
        if (!armed || backpackTip || HelpPopupUI.Instance == null) return;
        backpackTip = true;
        HelpPopupUI.Instance.ShowManual("배낭 · 핫바 등록",
            "배낭의 아이템을 번호(1, 2 …)가 적힌 핫바 칸으로 옮기면, 그 숫자키로 언제든 빠르게 사용할 수 있습니다.\n포션을 핫바에 등록해 두면 전투 중에도 바로 쓸 수 있어요.");
    }
}
