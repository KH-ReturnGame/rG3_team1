// 온보딩 도움말 흐름(이벤트 기반). 새 게임 시작 시 Begin()으로 무장 →
//  · ★첫 보물상자 개봉 → [아이템 사용] 카드 1회 (TreasureChest.Interact가 호출)
//  · 첫 소모품 사용은 폴백(상자를 안 열고 포션을 먼저 쓴 경우) — 같은 카드, ShowOnce라 중복 없음
// armed는 NewGame에서만 켜지므로(불러오기 X) 기존 플레이어에겐 안 뜸.
public static class TutorialFlow
{
    private static bool armed, useTip;

    public static void Begin() { armed = true; useTip = false; }   // SaveSystem.NewGame에서 호출

    // [아이템 사용] 카드 — ShowOnce(세이브의 Seen 기록 기준)라 어느 트리거로든 1회만
    private static void ShowUseItemCard()
    {
        if (HelpPopupUI.Instance == null) return;
        useTip = true;
        HelpPopupUI.Instance.ShowOnce("use_item", "아이템 사용",
            "배낭의 아이템을 우클릭하면 메뉴가 열립니다.\n[사용]으로 바로 쓰거나, [N번 슬롯에 등록]하면 전투 중에도 숫자키로 즉시 사용할 수 있어요.\n회복 포션은 [1]번 슬롯에 등록해 두는 것을 추천합니다.");
    }

    // ★첫 보물상자 개봉 — 아이템 사용 안내(주 트리거)
    public static void OnChestOpened()
    {
        if (!armed || useTip) return;
        ShowUseItemCard();
    }

    // 첫 소모품 사용(회복 포션 등) — 폴백 트리거
    public static void OnPotionUsed()
    {
        if (!armed || useTip) return;
        ShowUseItemCard();
    }

    // (구) 폐지된 트리거들 — 호출부 호환용 no-op
    public static void OnBackpackOpened() { }
    public static void OnItemAcquired() { }
}
