// 온보딩 도움말 흐름(이벤트 기반). 새 게임 시작 시 Begin()으로 무장 →
//  · 첫 아이템 획득(보물상자 포션 등) → 인벤토리 카드
//  · 인벤토리 카드가 나온 '다음' 첫 배낭 오픈 → 아이템 사용 카드
// 각 1회만. armed는 NewGame에서만 켜지므로(불러오기 X) 기존 플레이어에겐 안 뜸.
public static class TutorialFlow
{
    private static bool armed, itemTip, backpackTip;

    public static void Begin() { armed = true; itemTip = false; backpackTip = false; }   // SaveSystem.NewGame에서 호출

    public static void OnItemAcquired()
    {
        if (!armed || itemTip || HelpPopupUI.Instance == null) return;
        itemTip = true;
        HelpPopupUI.Instance.Show("loot", "인벤토리",
            "아이템을 손에 넣었습니다!\n[B] 또는 [C] 키로 배낭을 열어 무엇을 주웠는지 확인해 보세요.\n장비·장신구·재료·소모품 모두 이곳에 모입니다.");
    }

    public static void OnBackpackOpened()
    {
        if (!armed || backpackTip || !itemTip || HelpPopupUI.Instance == null) return;   // 인벤토리 카드가 먼저
        backpackTip = true;
        HelpPopupUI.Instance.Show("use_item", "아이템 사용",
            "배낭의 아이템을 우클릭하면 메뉴가 열립니다.\n[사용]으로 바로 쓰거나, [N번 슬롯에 등록]하면 전투 중에도 숫자키로 즉시 사용할 수 있어요.\n회복 포션은 [1]번 슬롯에 등록해 두는 것을 추천합니다.");
    }
}
