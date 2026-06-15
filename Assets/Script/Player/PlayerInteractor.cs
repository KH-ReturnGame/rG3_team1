using UnityEngine;

// 플레이어에 부착. F키로 가까운 상호작용 대상(IInteractable)을 실행.
// 가장 가까운 대상을 '테두리 강조 + 키 프롬프트 뱃지(대상 위/아래)'로 잘 보이게 표시한다.
public class PlayerInteractor : MonoBehaviour
{
    public KeyCode interactKey = KeyCode.F;
    public float interactRadius = 1.2f;

    [Header("강조 표시")]
    public Color highlightColor = new Color(1f, 0.85f, 0.3f);   // 테두리·뱃지 강조색(금색)
    public float borderThickness = 4f;                          // 대상 테두리 두께(px)

    private IInteractable nearest;
    private Collider2D nearestCol;
    private GUIStyle labelStyle, keyStyle;
    private static Texture2D _tex;

    void Update()
    {
        if (Inventory.IsUIOpen) { nearest = null; nearestCol = null; return; }   // UI 열려있으면 잠금
        nearest = FindNearest(out nearestCol);
        if (nearest != null && Input.GetKeyDown(interactKey))
            nearest.Interact();
    }

    private IInteractable FindNearest(out Collider2D col)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRadius);
        IInteractable best = null; Collider2D bestCol = null; float bestDist = Mathf.Infinity;
        foreach (var h in hits)
        {
            var it = h.GetComponent<IInteractable>();
            if (it == null) continue;
            if (string.IsNullOrEmpty(it.Prompt)) continue;   // 빈 프롬프트(이미 연 상자 등)는 제외
            float d = Vector2.Distance(transform.position, h.transform.position);
            if (d < bestDist) { bestDist = d; best = it; bestCol = h; }
        }
        col = bestCol;
        return best;
    }

    void OnGUI()
    {
        if (nearest == null || nearestCol == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        // 대상 콜라이더 범위를 화면 좌표로 투영
        Bounds b = nearestCol.bounds;
        Vector3 a = cam.WorldToScreenPoint(b.min);
        Vector3 c = cam.WorldToScreenPoint(b.max);
        if (a.z < 0f || c.z < 0f) return;   // 카메라 뒤면 표시 안 함

        float left = Mathf.Min(a.x, c.x), right = Mathf.Max(a.x, c.x);
        float topScr = Mathf.Max(a.y, c.y), botScr = Mathf.Min(a.y, c.y);
        Rect box = new Rect(left, Screen.height - topScr, right - left, topScr - botScr);   // GUI 좌표(위=0)

        EnsureStyles();

        // 1) 대상 테두리 강조(살짝 깜빡임)
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
        Color hl = highlightColor; hl.a = pulse;
        Rect pad = new Rect(box.x - 3f, box.y - 3f, box.width + 6f, box.height + 6f);
        DrawBorder(pad, borderThickness, hl);

        // 2) 키 프롬프트 뱃지 — "F: 이동" → 키캡 [F] + 라벨 "이동"
        string label = nearest.Prompt;
        string keyText = interactKey.ToString();
        if (label.StartsWith(keyText))
        {
            int ci = label.IndexOf(':');
            if (ci >= 0) label = label.Substring(ci + 1).Trim();
        }

        Vector2 lsz = labelStyle.CalcSize(new GUIContent(label));
        float keyW = 26f, padX = 9f, gap = 7f, bh = 30f;
        float bw = padX + keyW + gap + lsz.x + padX;
        float bx = box.center.x - bw * 0.5f;
        float by = pad.y - bh - 8f;                 // 기본: 대상 위
        if (by < 6f) by = pad.yMax + 8f;            // 위 공간 없으면 아래

        Rect badge = new Rect(bx, by, bw, bh);
        Fill(new Rect(badge.x + 2f, badge.y + 3f, badge.width, badge.height), new Color(0f, 0f, 0f, 0.35f));  // 그림자
        Fill(badge, new Color(0.07f, 0.07f, 0.09f, 0.92f));   // 어두운 배경
        DrawBorder(badge, 2f, highlightColor);                // 금색 테두리

        Rect keyCap = new Rect(badge.x + padX, badge.y + (bh - 22f) * 0.5f, keyW, 22f);
        Fill(keyCap, highlightColor);
        keyStyle.normal.textColor = new Color(0.12f, 0.09f, 0.04f);
        GUI.Label(keyCap, keyText, keyStyle);                 // 키캡 [F]

        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(keyCap.xMax + gap, badge.y, lsz.x + 6f, bh), label, labelStyle);
    }

    private void EnsureStyles()
    {
        if (labelStyle != null) return;
        labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 15, fontStyle = FontStyle.Bold };
        keyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold };
    }

    private static Texture2D Tex()
    {
        if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); }
        return _tex;
    }
    private static void Fill(Rect r, Color col) { Color o = GUI.color; GUI.color = col; GUI.DrawTexture(r, Tex()); GUI.color = o; }
    private static void DrawBorder(Rect r, float t, Color col)
    {
        Fill(new Rect(r.x, r.y, r.width, t), col);            // 위
        Fill(new Rect(r.x, r.yMax - t, r.width, t), col);     // 아래
        Fill(new Rect(r.x, r.y, t, r.height), col);           // 왼쪽
        Fill(new Rect(r.xMax - t, r.y, t, r.height), col);    // 오른쪽
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
