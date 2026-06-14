using UnityEngine;

// 인벤토리 '최하단 줄'을 단축키로 바로 사용하는 시스템.
//  - 최하단 줄 = 인벤토리의 마지막 hotbarColumns칸.
//  - 그 줄의 왼쪽부터 hotkeySlots개가 숫자키 1,2,…에 매핑되어 소비 아이템을 사용.
//  - hotkeySlots는 상점 구매 등으로 늘릴 수 있음(AddHotkeySlot).
// 아래 OnGUI는 '표시'만 — 실제 사용 로직은 UseSlot()이라, 나중에 에셋 UI 버튼에서 UseSlot()을
// 호출하게 하면 그대로 교체된다(로직-표시 분리).
public class Hotbar : MonoBehaviour
{
    public static Hotbar Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }   // 중복 제거(HUD 하나만 유지)
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [Header("단축키")]
    public int hotbarColumns = 8;   // 최하단 줄 너비(인벤토리 columns와 맞추기)
    public int hotkeySlots = 2;     // 현재 활성 단축키 개수(상점으로 증가)

    [Header("표시(임시 — 에셋 들어오면 교체)")]
    public bool showBar = true;
    public float slotSizeRatio = 0.07f;   // 화면 높이 대비 칸 크기

    private GUIStyle keyStyle, countStyle, cdStyle;
    private Texture2D dim;

    private int BottomRowStart
    {
        get
        {
            int n = Inventory.Instance != null ? Inventory.Instance.slotCount : 24;
            return Mathf.Max(0, n - hotbarColumns);
        }
    }

    void Update()
    {
        if (Inventory.IsUIOpen) return;   // 인벤 열려있을 땐 마우스로 관리
        for (int i = 0; i < hotkeySlots; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) UseSlot(i);
    }

    // i번째 단축키(0-based) 사용. 에셋 UI 버튼에서도 이걸 호출하면 됨.
    public void UseSlot(int i)
    {
        if (Inventory.Instance == null || i < 0 || i >= hotkeySlots) return;
        int index = BottomRowStart + i;
        ItemData item = Inventory.Instance.ItemAt(index);
        if (item == null) return;
        // 포션(소비)은 종류별 쿨타임 체크
        if (item.kind == ItemData.ItemKind.Consumable && GameManager.Instance != null && !GameManager.Instance.IsPotionReady(item))
        {
            Toast.Show(item.itemName + " 쿨타임 " + Mathf.CeilToInt(GameManager.Instance.PotionCooldownLeft(item)) + "초", 1.5f);
            return;
        }
        if (item.Use())
        {
            Inventory.Instance.ConsumeAt(index, 1);
            if (item.kind == ItemData.ItemKind.Consumable && GameManager.Instance != null) GameManager.Instance.StartPotionCooldown(item);
        }
    }

    // 단축키 슬롯 늘리기(상점 구매 등). 최대 = 한 줄 너비.
    public void AddHotkeySlot(int n = 1)
        => hotkeySlots = Mathf.Clamp(hotkeySlots + n, 0, hotbarColumns);

    void OnGUI()
    {
        if (!showBar || Inventory.Instance == null || hotkeySlots <= 0) return;
        EnsureStyles();

        float size = Mathf.Clamp(Screen.height * slotSizeRatio, 44f, 96f);
        float pad = size * 0.12f;
        float totalW = hotkeySlots * (size + pad) - pad;
        float x = (Screen.width - totalW) * 0.5f;
        float y = Screen.height - size - pad * 2f;

        var slots = Inventory.Instance.slots;
        for (int i = 0; i < hotkeySlots; i++)
        {
            Rect r = new Rect(x + i * (size + pad), y, size, size);
            GUI.Box(r, GUIContent.none);

            int idx = BottomRowStart + i;
            if (idx >= 0 && idx < slots.Count && !slots[idx].IsEmpty)
            {
                var s = slots[idx];
                if (s.item.icon != null)
                    GUI.DrawTexture(new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 10), s.item.icon.texture, ScaleMode.ScaleToFit);
                else
                    GUI.Label(r, s.item.itemName);
                if (s.count > 1)
                    GUI.Label(new Rect(r.x, r.yMax - 22, r.width - 5, 20), s.count.ToString(), countStyle);

                // 포션 쿨타임 오버레이(남은 초)
                if (GameManager.Instance != null && s.item.kind == ItemData.ItemKind.Consumable)
                {
                    float cl = GameManager.Instance.PotionCooldownLeft(s.item);
                    if (cl > 0f)
                    {
                        var pc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(r, dim); GUI.color = pc;
                        GUI.Label(r, Mathf.CeilToInt(cl).ToString(), cdStyle);
                    }
                }
            }
            GUI.Label(new Rect(r.x + 4, r.y + 2, 22, 18), (i + 1).ToString(), keyStyle);   // 단축키 번호
        }
    }

    private void EnsureStyles()
    {
        if (keyStyle != null) return;
        keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        keyStyle.normal.textColor = Color.yellow;
        countStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
        countStyle.normal.textColor = Color.white;
        cdStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        cdStyle.normal.textColor = new Color(1f, 0.95f, 0.8f);
        dim = new Texture2D(1, 1); dim.SetPixel(0, 0, Color.white); dim.Apply();
    }
}
