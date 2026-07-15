// 온보딩 도움말 흐름(이벤트 기반). 새 게임 시작 시 Begin()으로 무장 →
//  · ★첫 소모품(회복 포션 등) 사용 → [아이템 사용] 카드 1회 (GameManager.StartPotionCooldown이 호출)
//  · (구) 보물상자 개봉/아이템 획득/배낭 오픈 트리거는 전부 폐지 — 호출부 호환용 no-op만 남김
// armed는 NewGame에서만 켜지므로(불러오기 X) 기존 플레이어에겐 안 뜸.
public static class TutorialFlow
{
    private static bool armed, useTip;

    public static void Begin() { armed = true; useTip = false; }   // SaveSystem.NewGame에서 호출

    // 첫 소모품 사용(회복 포션 등) — 아이템 사용 안내
    public static void OnPotionUsed()
    {
        if (!armed || useTip || HelpPopupUI.Instance == null) return;
        useTip = true;
        HelpPopupUI.Instance.Show("use_item", "아이템 사용",
            "배낭의 아이템을 우클릭하면 메뉴가 열립니다.\n[사용]으로 바로 쓰거나, [N번 슬롯에 등록]하면 전투 중에도 숫자키로 즉시 사용할 수 있어요.\n회복 포션은 [1]번 슬롯에 등록해 두는 것을 추천합니다.");
    }

    // (구) 폐지된 트리거들 — 호출부 호환용 no-op
    public static void OnChestOpened() { }
    public static void OnBackpackOpened() { }
    public static void OnItemAcquired() { }
}
