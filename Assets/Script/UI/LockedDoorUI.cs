using UnityEngine;
using UnityEngine.SceneManagement;

// 잠긴 문 해제 UI(자동부팅·영구) — 열쇠를 소지품에서 '직접' 집어 열쇠 구멍에 꽂는다.
//  왼쪽 = 소지품 그리드(InvGridGUI 재사용: 집기/놓기/스택/[R] 회전 동일 규칙)
//  오른쪽 = 열쇠 구멍(큰 1칸): 맞는 열쇠를 든 채 클릭하면 문이 열림. 틀리면 붉게 흔들림.
//  LockedDoor.Interact()가 Open(door)으로 연다. [ESC] 닫기.
public class LockedDoorUI : MonoBehaviour
{
    public static LockedDoorUI Instance;

    private LockedDoor door;
    private ItemData held; private int heldCount; private int heldRot;
    private float wrongAt = -99f;   // 틀린 열쇠 연출 시각
    private float openedAt;
    private GUIStyle titleStyle, tagStyle, textStyle, subStyle, hintStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) { var go = new GameObject("LockedDoorUI"); Instance = go.AddComponent<LockedDoorUI>(); DontDestroyOnLoad(go); }
    }

    void OnEnable() { SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }
    private void OnScene(Scene s, LoadSceneMode m) { door = null; held = null; heldCount = 0; Inventory.LockUIOpen = false; }   // 씬 전환 안전

    public static bool IsOpen => Instance != null && Instance.door != null;

    public static void Open(LockedDoor d)
    {
        if (Instance == null || d == null || d.requiredKey == null) return;
        Instance.door = d;
        Instance.held = null; Instance.heldCount = 0; Instance.heldRot = 0;
        Instance.openedAt = Time.unscaledTime;
        Inventory.LockUIOpen = true;
    }

    private void Close()
    {
        ReturnHeld();
        door = null;
        Inventory.LockUIOpen = false;
    }

    // 들고 있던 아이템을 소지품으로 되돌림(자리 없으면 발밑에 떨굼 — 유실 방지)
    private void ReturnHeld()
    {
        if (held == null) return;
        int leftover = Inventory.Instance != null ? Inventory.Instance.Add(held, heldCount) : heldCount;
        if (leftover > 0 && PlayerController.Instance != null)
            ItemPickup.SpawnWorld(held, leftover, PlayerController.Instance.transform.position);
        held = null; heldCount = 0; heldRot = 0;
    }

    void Update()
    {
        if (door == null) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        if (Input.GetKeyDown(KeyCode.R) && held != null) heldRot = (heldRot + 1) & 3;
    }

    void OnGUI()
    {
        if (door == null) return;
        EnsureStyles();
        UIScale.Apply();
        float sw = UIScale.W, sh = UIScale.H;
        Vector2 m = Event.current.mousePosition;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0
                     && Time.unscaledTime - openedAt > 0.15f;   // 여는 F 클릭 누수 방지

        // 배경 딤 + 패널
        UITheme.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.55f));
        float w = Mathf.Min(880f, sw * 0.72f), h = Mathf.Min(540f, sh * 0.68f);
        Rect panel = new Rect((sw - w) * 0.5f, (sh - h) * 0.5f, w, h);
        UITheme.DrawPanel(panel);

        // 헤더
        float pad = 24f, headH = 44f;
        UITheme.Fill(new Rect(panel.x, panel.y + 8f, 4f, headH - 8f), UITheme.Accent);
        tagStyle.normal.textColor = UITheme.Accent;
        GUI.Label(new Rect(panel.x + pad, panel.y + 8f, 90f, headH), "잠긴 문", tagStyle);
        titleStyle.normal.textColor = new Color(0.97f, 0.96f, 0.94f);
        GUI.Label(new Rect(panel.x + pad + 92f, panel.y + 6f, w - pad * 2f - 92f, headH), door.requiredKey.itemName + " 필요", titleStyle);
        UITheme.Fill(new Rect(panel.x + pad, panel.y + headH + 8f, w - pad * 2f, 1f), UITheme.A(UITheme.Border, 0.5f));

        // 왼쪽: 소지품 그리드
        Rect gridArea = new Rect(panel.x + pad, panel.y + headH + 18f, w * 0.60f, h - headH - 64f);
        InvGridGUI.Draw(gridArea, m, click, ref held, ref heldCount, ref heldRot);

        // 오른쪽: 열쇠 구멍(큰 1칸)
        float slotSize = Mathf.Min(150f, h * 0.30f);
        Rect keyRect = new Rect(gridArea.xMax + ((panel.xMax - pad) - gridArea.xMax - slotSize) * 0.5f,
                                panel.y + h * 0.30f, slotSize, slotSize);

        bool hoverSlot = keyRect.Contains(m);
        float wrongK = Mathf.Clamp01((Time.unscaledTime - wrongAt) / 0.45f);
        float shake = wrongK < 1f ? Mathf.Sin(Time.unscaledTime * 55f) * 5f * (1f - wrongK) : 0f;
        Rect kr = new Rect(keyRect.x + shake, keyRect.y, keyRect.width, keyRect.height);

        Color ringC = wrongK < 1f ? new Color(0.9f, 0.25f, 0.2f)
                    : (held != null && held == door.requiredKey ? new Color(0.35f, 0.85f, 0.40f) : UITheme.A(UITheme.Warm, hoverSlot ? 1f : 0.7f));
        UITheme.Glow(kr, ringC, 14f, held != null && held == door.requiredKey ? 0.30f : 0.14f);
        UITheme.DrawSlot(kr, ringC, hoverSlot, 2.5f);

        // 구멍 안: 요구 열쇠 실루엣(흐리게)
        if (door.requiredKey.icon != null)
        {
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.22f);
            GUI.DrawTexture(new Rect(kr.x + 18f, kr.y + 18f, kr.width - 36f, kr.height - 36f), door.requiredKey.icon.texture, ScaleMode.ScaleToFit);
            GUI.color = prev;
        }

        // 안내 텍스트
        subStyle.normal.textColor = new Color(0.62f, 0.63f, 0.67f);
        GUI.Label(new Rect(kr.x - 60f, kr.yMax + 10f, kr.width + 120f, 26f), "열쇠를 집어 여기에 꽂기", subStyle);
        textStyle.normal.textColor = new Color(0.85f, 0.86f, 0.89f);
        string need = door.requiredKey.itemName + (door.requiredCount > 1 ? "  ×" + door.requiredCount : "");
        GUI.Label(new Rect(kr.x - 60f, kr.yMax + 34f, kr.width + 120f, 24f), need, textStyle);

        // 열쇠 구멍 클릭 판정
        if (hoverSlot && click)
        {
            Event.current.Use();
            if (held == null) { /* 빈손 클릭 — 무시 */ }
            else if (held == door.requiredKey && heldCount >= door.requiredCount) UnlockWithHeld();
            else { wrongAt = Time.unscaledTime; Toast.Show("맞지 않는 열쇠다.", 1.5f); }
        }

        // 하단 힌트
        hintStyle.normal.textColor = new Color(0.55f, 0.56f, 0.60f);
        GUI.Label(new Rect(panel.x, panel.yMax - 32f, w, 24f), "[ESC] 닫기" + (held != null ? "   ·   [R] 회전" : ""), hintStyle);

        // 들고 있는 아이템 커서 표시
        if (held != null && held.icon != null)
        {
            float s = 52f;
            var mtx = GUI.matrix;
            if ((heldRot & 3) != 0) GUIUtility.RotateAroundPivot((heldRot & 3) * 90f, m);
            GUI.DrawTexture(new Rect(m.x - s * 0.5f, m.y - s * 0.5f, s, s), held.icon.texture, ScaleMode.ScaleToFit);
            GUI.matrix = mtx;
            if (heldCount > 1) GUI.Label(new Rect(m.x + 10f, m.y + 10f, 40f, 20f), heldCount.ToString(), textStyle);
        }

        // 패널 밖 클릭은 흡수(뒤 게임으로 안 새게)
        if (click && Event.current.type == EventType.MouseDown) Event.current.Use();
    }

    // 맞는 열쇠 삽입: 소모(consumeKey) 후 문 개방 → UI 닫기
    private void UnlockWithHeld()
    {
        var d = door;
        if (d.consumeKey)
        {
            heldCount -= d.requiredCount;
            if (heldCount <= 0) { held = null; heldCount = 0; heldRot = 0; }
        }
        ReturnHeld();   // 남은 수량(또는 비소모면 전부) 소지품으로
        door = null;
        Inventory.LockUIOpen = false;
        d.Unlock();
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        subStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
    }
}
