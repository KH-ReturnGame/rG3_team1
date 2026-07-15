using UnityEngine;

// 튜토리얼 도움말 트리거. 트리거 콜라이더 안에 플레이어가 들어오면 화면에 도움말 팝업이 뜨고,
// 벗어나면 사라진다. 어떤 도움말이 뜰지는 인스펙터의 Topic 으로 선택(텍스트는 내장).
// 튜토리얼 씬의 원하는 구역에 빈 오브젝트 + Collider2D(트리거)로 배치.
[RequireComponent(typeof(Collider2D))]
public class HelpTrigger : MonoBehaviour
{
    public enum HelpTopic { TreasureChest, ChargeJump, Parry, Combat, Hotkeys, Custom }   // 보물상자 / S+Space / 패링 / 전투 / 단축키 / 직접입력

    [Header("어떤 도움말?")]
    public HelpTopic topic = HelpTopic.TreasureChest;

    [Header("Custom 일 때만 사용")]
    public string customTitle = "";
    [TextArea] public string customBody = "";
    public string customGifId = "";   // Custom 카드의 GIF 폴더 이름(Resources/Help/<id>/, 비우면 GIF 없음)

    public enum DisplayMode { Manual, Timed }   // (구) 카드 리워크로 미사용 — 직렬화 호환용 잔존

    [Header("옵션")]
    public DisplayMode displayMode = DisplayMode.Manual;   // (구) 미사용
    public float timedSeconds = 7f;                        // (구) 미사용
    public bool showOnce = false;   // true = 최초 1회만 뜸(이후 재진입해도 안 뜸)

    private bool consumed;

    void Reset() { var c = GetComponent<Collider2D>(); if (c != null) c.isTrigger = true; }
    void Awake() { var c = GetComponent<Collider2D>(); if (c != null) c.isTrigger = true; }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        if (showOnce && consumed) return;
        string t, b; GetText(out t, out b);
        if (HelpPopupUI.Instance != null)
            HelpPopupUI.Instance.Show(GifId(), t, b);   // 모달 카드(시간 정지 + GIF)
        consumed = true;
    }

    // 토픽 → GIF 폴더 이름(Resources/Help/<id>/) — 프레임 PNG를 넣으면 카드 하단에 재생됨
    private string GifId()
    {
        switch (topic)
        {
            case HelpTopic.TreasureChest: return "chest";
            case HelpTopic.ChargeJump:    return "charge_jump";
            case HelpTopic.Parry:         return "parry";
            case HelpTopic.Combat:        return "attack";
            case HelpTopic.Hotkeys:       return "hotkeys";
            default:                      return customGifId;
        }
    }

    public void GetText(out string title, out string body)
    {
        switch (topic)
        {
            case HelpTopic.TreasureChest:
                title = "보물상자";
                body = "이 아름다운 세계에는 누구의 손길도 받지 못한 상자가 존재합니다. 우리는 그것을 보물상자라고 부르죠.\n보물상자에선 포션, 재료, 장신구등 다양한 전리품을 얻을 수 있으면 때론 예상치 못한 보상을 얻을 수 있기도 합니다. 당신이 운이 좋다면 말이죠..";
                break;
            case HelpTopic.ChargeJump:
                title = "S + Space";
                body = "S키를 누른 후 점프키를 누르면 점프 차징 상태에 들어갈 수 있습니다.\n2단 점프를 한 것과 비슷한 점프력을 보여주며, 이를 통해 2단 점프로만은 가지 못하는 곳을 갈 수 있을지도 모릅니다.\n※ 통과형 발판 위에서는 S+Space가 '발판 아래로 하강'이 됩니다.";
                break;
            case HelpTopic.Parry:
                title = "패링";
                body = "우클릭을 하여 가드를 할 수 있습니다, 가드 상태에서는 받는 데미지가 감소하며, 동시에 움직임이 느려집니다.\n만약 당신이 공격을 받기 전, 아주 완벽한 타이밍에 가드를 했다면 *패링*을 할 수 있게 됩니다.\n\n*패링*을 성공했다면 적이 기절 상태에 빠지며, 동시에 Q스킬의 쿨타임이 초기화됩니다.";
                break;
            case HelpTopic.Combat:
                title = "전투 시스템";
                body = "좌클릭으로 검을 휘둘러 공격할 수 있습니다.\nQ를 누르면 넓게 베는 횡베기 스킬을 쓸 수 있습니다. 대시(Shift) 중에는 잠시 무적이 되어 공격을 피할 수 있습니다.";
                break;
            case HelpTopic.Hotkeys:
                title = "단축키";
                body = "이동:  A · D  (또는 ← · →)\n점프:  Space      대시:  Shift\n공격:  좌클릭      가드 · 패링:  우클릭      스킬:  Q\n검 뽑기 · 넣기:  R      상호작용 · 줍기:  F\n인벤토리:  C / B      퀘스트:  J      핸드북:  G      지도:  M      미니맵:  ,      보물 탐지:  Z\n아이템 사용:  배낭(B)에서 우클릭 → [사용/N번 슬롯 등록]      포션 단축키:  숫자키 (1~)";
                break;
            default:
                title = customTitle;
                body = customBody;
                break;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.6f);
        var col = GetComponent<Collider2D>();
        if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
