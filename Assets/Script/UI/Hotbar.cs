using UnityEngine;

// 단축키(핫바) — 인벤토리에서 '우클릭 → N번 슬롯에 등록'한 아이템을 숫자키로 즉시 사용.
//  · 인벤 배치와 분리: 등록된 ItemData '참조'만 들고, 사용 시 인벤 어디에 있든 1개 소모(그리드 안전).
//  · 인벤 닫힌 상태에서 핫바 아이콘 우클릭 → [슬롯에서 제거].
public class Hotbar : MonoBehaviour
{
    public static Hotbar Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }   // 중복 제거(HUD 하나만)
        Instance = this;
    }

    [Header("단축키")]
    public int hotbarColumns = 8;   // 최대 단축키 수
    public int baseHotkeySlots = 3; // 기본 단축키 개수
    public int hotkeySlots = 3;     // 현재 활성 단축키 개수

    [Header("표시(임시 — 에셋 들어오면 교체)")]
    public bool showBar = true;
    public float slotSizeRatio = 0.07f;   // 화면 높이 대비 칸 크기

    private readonly ItemData[] registered = new ItemData[16];   // 슬롯별 등록 아이템(참조)
    private int ctxSlot = -1;             // 우클릭 제거 메뉴가 열린 슬롯
    private Rect ctxRect;

    private GUIStyle keyStyle, countStyle, cdStyle, ctxStyle;
    private Texture2D dim;

    // ── 등록/해제 (InventoryUI 우클릭 메뉴에서 호출) ──
    public void Register(ItemData item, int slot)
    {
        if (item == null || slot < 0 || slot >= registered.Length) return;
        for (int k = 0; k < registered.Length; k++) if (registered[k] == item) registered[k] = null;   // 중복 등록 제거
        registered[slot] = item;
    }
    public void UnregisterSlot(int slot) { if (slot >= 0 && slot < registered.Length) registered[slot] = null; }
    public ItemData GetRegistered(int slot) => (slot >= 0 && slot < registered.Length) ? registered[slot] : null;

    void Update()
    {
        if (Inventory.IsUIOpen) { ctxSlot = -1; return; }   // 인벤 열려있으면 핫바 우클릭 메뉴 닫기
        for (int i = 0; i < hotkeySlots; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) UseSlot(i);
    }

    // i번째 단축키(0-based) 사용.
    public void UseSlot(int i)
    {
        if (Inventory.Instance == null || i < 0 || i >= hotkeySlots) return;
        ItemData item = GetRegistered(i);
        if (item == null) return;
        if (Inventory.Instance.CountOf(item) <= 0) return;   // 인벤에 없으면 무시
        if (item.kind == ItemData.ItemKind.Consumable && GameManager.Instance != null && !GameManager.Instance.IsPotionReady(item))
        {
            Toast.Show(item.itemName + " 쿨타임 " + Mathf.CeilToInt(GameManager.Instance.PotionCooldownLeft(item)) + "초", 1.5f);
            return;
        }
        if (item.Use())
        {
            Inventory.Instance.Remove(item, 1);
            if (item.kind == ItemData.ItemKind.Consumable && GameManager.Instance != null) GameManager.Instance.StartPotionCooldown(item);
        }
    }

    // 단축키 슬롯 수 조정(구 모듈 호환)
    public void AddHotkeySlot(int n = 1) => hotkeySlots = Mathf.Clamp(hotkeySlots + n, 0, hotbarColumns);
    public void ApplyQuickdraw(int level) => hotkeySlots = Mathf.Clamp(baseHotkeySlots + level, 0, hotbarColumns);

    void OnGUI()
    {
        if (!showBar || Inventory.Instance == null || hotkeySlots <= 0) return;
        EnsureStyles();
        UIScale.Apply();

        float size = Mathf.Clamp(UIScale.H * slotSizeRatio, 44f, 96f);
        float pad = size * 0.12f;
        float totalW = hotkeySlots * (size + pad) - pad;
        float x = (UIScale.W - totalW) * 0.5f;
        float y = UIScale.H - size - pad * 2f;

        Vector2 mouse = Event.current.mousePosition;
        bool invClosed = !Inventory.IsUIOpen;

        // 우클릭 제거 메뉴: 밖 클릭 시 닫기
        if (ctxSlot >= 0 && Event.current.type == EventType.MouseDown && !ctxRect.Contains(mouse))
        { ctxSlot = -1; Event.current.Use(); }

        for (int i = 0; i < hotkeySlots; i++)
        {
            Rect r = new Rect(x + i * (size + pad), y, size, size);
            ItemData item = GetRegistered(i);
            bool hover = invClosed && r.Contains(mouse);

            UITheme.DrawSlot(r, UITheme.Warm, hover, 2f);

            if (item != null)
            {
                int cnt = Inventory.Instance.CountOf(item);
                var prev = GUI.color;
                if (cnt <= 0) GUI.color = new Color(1f, 1f, 1f, 0.30f);   // 인벤에 없으면 흐리게
                if (item.icon != null)
                    GUI.DrawTexture(new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 12), item.icon.texture, ScaleMode.ScaleToFit);
                else GUI.Label(r, item.itemName, ctxStyle);
                GUI.color = prev;

                if (cnt > 1) GUI.Label(new Rect(r.x, r.yMax - 24, r.width - 6, 20), cnt.ToString(), countStyle);

                // 포션 쿨타임 오버레이(남은 초)
                if (GameManager.Instance != null && item.kind == ItemData.ItemKind.Consumable)
                {
                    float cl = GameManager.Instance.PotionCooldownLeft(item);
                    if (cl > 0f)
                    {
                        var pc = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(r, dim); GUI.color = pc;
                        GUI.Label(r, Mathf.CeilToInt(cl).ToString(), cdStyle);
                    }
                }

                // 우클릭 → 제거 메뉴 열기(인벤 닫혀있을 때)
                if (invClosed && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(mouse))
                { ctxSlot = i; ctxRect = new Rect(r.x, r.y - 36f, Mathf.Max(150f, size + 40f), 32f); Event.current.Use(); }
            }

            // 단축키 번호(앰버 배지)
            Rect badge = new Rect(r.x + 4, r.y + 4, 20, 20);
            UITheme.Fill(badge, UITheme.A(UITheme.Warm, 0.9f));
            GUI.Label(badge, (i + 1).ToString(), keyStyle);
        }

        // 제거 메뉴
        if (ctxSlot >= 0)
        {
            UITheme.Shadow(ctxRect, 8f, 0.4f);
            UITheme.FillV(ctxRect, UITheme.PanelTop, UITheme.PanelBot);
            UITheme.Border2(ctxRect, 2f, UITheme.Accent);
            if (ctxRect.Contains(mouse)) UITheme.Fill(ctxRect, UITheme.A(UITheme.Accent, 0.16f));
            GUI.Label(ctxRect, "슬롯에서 제거", ctxStyle);
            if (GUI.Button(ctxRect, GUIContent.none, GUIStyle.none)) { UnregisterSlot(ctxSlot); ctxSlot = -1; }
        }
    }

    private void EnsureStyles()
    {
        if (keyStyle != null) return;
        keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        keyStyle.normal.textColor = new Color(0.10f, 0.06f, 0.02f);   // 앰버 배지 위 짙은 글자
        countStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
        countStyle.normal.textColor = Color.white;
        cdStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        cdStyle.normal.textColor = new Color(1f, 0.95f, 0.8f);
        ctxStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        ctxStyle.normal.textColor = UITheme.Text;
        dim = new Texture2D(1, 1); dim.SetPixel(0, 0, Color.white); dim.Apply();
    }
}
