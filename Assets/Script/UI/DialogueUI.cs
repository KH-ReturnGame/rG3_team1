using System;
using UnityEngine;

// 대화창(자동부팅·영구, OnGUI). VN(비주얼노벨)풍 — 왼쪽에 큰 초상화(틀 없이 컷아웃) + 화면 하단 대사 박스(박스 안 좌상단 이름).
//  · 타자기 연출. Space/F/Enter/좌클릭으로 진행(타이핑 중이면 즉시 완성 → 다 보이면 다음 줄/종료).
//  · 열려 있는 동안 Inventory.DialogueOpen=true → 플레이어 이동·공격·상호작용 잠금.
//  · DialogueUI.Show(이름, 초상화, 줄[], 완료콜백) — 마지막 줄까지 끝나면 onComplete 실행(상점/엔지니어 열기 등).
//  · ★연출 태그: 대사 줄 '맨 앞'에 붙이면 그 줄 동안 연출 재생(표시 텍스트에선 제거됨). 인스펙터 대사에도 그대로 사용.
//      [놀람]   초상화가 폴짝 튀어오름(깜짝)
//      [흔들림] 초상화+대사박스 강한 흔들림 후 잦아듦(중상·충격)
//      [떨림]   줄이 떠 있는 내내 잘게 떨림(고통·슬픔·공포)
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    private string speaker;
    private Sprite portrait;
    private string[] lines;
    private Action onComplete;
    private int index;
    private float shown;          // 현재 줄에서 보여준 글자 수(실수 — 타자 진행)
    private float openTime;
    private const float CharsPerSec = 48f;

    // 대사 스킵: [Ctrl]을 길게 누르면 대화 전체를 건너뜀(onComplete는 정상 실행 — 컷씬 체인 안전)
    public KeyCode skipKey = KeyCode.LeftControl;
    public float skipHold = 0.6f;
    private float skipTimer;

    // ── 줄 연출 ──
    private enum LineFx { None, Surprise, Shake, Tremble }
    private LineFx curFx;
    private float fxStart;        // 줄 시작 시각(unscaled)
    private string curText = ""; // 태그 제거된 표시용 본문

    // 현재 줄의 태그를 파싱하고 타자기를 리셋
    private void StartLine()
    {
        string raw = lines[index];
        curFx = LineFx.None;
        if (raw.StartsWith("[놀람]"))       { curFx = LineFx.Surprise; raw = raw.Substring("[놀람]".Length); }
        else if (raw.StartsWith("[흔들림]")) { curFx = LineFx.Shake;    raw = raw.Substring("[흔들림]".Length); }
        else if (raw.StartsWith("[떨림]"))   { curFx = LineFx.Tremble;  raw = raw.Substring("[떨림]".Length); }
        curText = raw;
        shown = 0f;
        fxStart = Time.unscaledTime;
    }

    // 연출 오프셋(픽셀). forPortrait=false면 대사박스용(약하게).
    private Vector2 FxOffset(bool forPortrait)
    {
        float t = Time.unscaledTime - fxStart;
        switch (curFx)
        {
            case LineFx.Surprise:   // 폴짝 두 번 튀며 잦아듦 — 초상화만
            {
                const float dur = 0.55f;
                if (!forPortrait || t >= dur) return Vector2.zero;
                float k = t / dur;
                return new Vector2(0f, -30f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * (1f - k));
            }
            case LineFx.Shake:      // 강한 흔들림 후 감쇠 — 초상화+박스
            {
                const float dur = 0.65f;
                if (t >= dur) return Vector2.zero;
                float amp = (forPortrait ? 11f : 7f) * (1f - t / dur);
                return new Vector2(
                    (Mathf.PerlinNoise(t * 38f, 0.3f) - 0.5f) * 2f * amp,
                    (Mathf.PerlinNoise(0.7f, t * 41f) - 0.5f) * 2f * amp);
            }
            case LineFx.Tremble:    // 줄 내내 잔떨림 — 초상화 위주, 박스는 아주 약하게
            {
                float amp = forPortrait ? 2.8f : 1.2f;
                return new Vector2(
                    (Mathf.PerlinNoise(t * 22f, 0.5f) - 0.5f) * 2f * amp,
                    (Mathf.PerlinNoise(0.9f, t * 24f) - 0.5f) * 2f * amp);
            }
        }
        return Vector2.zero;
    }

    private Texture2D white;
    private GUIStyle nameStyle, textStyle, hintStyle, skipStyle;

    void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("DialogueUI").AddComponent<DialogueUI>(); }

    public static void Show(string speaker, Sprite portrait, string[] lines, Action onComplete = null)
    {
        if (Instance == null || lines == null || lines.Length == 0) { if (onComplete != null) onComplete(); return; }
        Instance.Begin(speaker, portrait, lines, onComplete);
    }

    // 초상화 자동 로드: Resources/Portraits/<화자 이름>.png 를 찾는다.
    //  → 초상화를 넣거나 바꾸려면 그 폴더에 '화자 이름과 같은 파일명'의 PNG(Sprite)만 두면 끝.
    //    (NpcDialogue의 portrait 필드로 개별 지정하면 그게 우선)
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> portraitCache
        = new System.Collections.Generic.Dictionary<string, Sprite>();

    private static Sprite AutoPortrait(string speaker)
    {
        if (string.IsNullOrEmpty(speaker)) return null;
        if (portraitCache.TryGetValue(speaker, out var s)) return s;
        s = Resources.Load("Portraits/" + speaker, typeof(Sprite)) as Sprite;
        portraitCache[speaker] = s;   // 없으면 null도 캐시(매 대사마다 디스크 조회 방지)
        return s;
    }

    private void Begin(string sp, Sprite por, string[] ln, Action done)
    {
        speaker = sp; portrait = por != null ? por : AutoPortrait(sp); lines = ln; onComplete = done;
        index = 0; openTime = Time.unscaledTime;
        StartLine();
        IsOpen = true; Inventory.DialogueOpen = true;
    }

    void Update()
    {
        if (!IsOpen) return;
        int len = curText.Length;
        if (shown < len) shown += CharsPerSec * Time.unscaledDeltaTime;

        // [Ctrl] 길게 → 대화 전체 스킵
        if (Input.GetKey(skipKey))
        {
            skipTimer += Time.unscaledDeltaTime;
            if (skipTimer >= skipHold) { skipTimer = 0f; Close(); return; }
        }
        else skipTimer = 0f;

        if (Time.unscaledTime - openTime < 0.18f) return;   // 연 직후 입력 무시(여는 F가 첫 줄을 바로 넘기지 않게)
        bool advance = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F)
            || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetMouseButtonDown(0);
        if (!advance) return;

        if (shown < len) { shown = len; return; }           // 타이핑 중 → 즉시 완성
        index++;
        if (index >= lines.Length) { Close(); return; }
        StartLine(); openTime = Time.unscaledTime;           // 다음 줄(태그 파싱) + 살짝 입력가드(연타 방지)
    }

    private void Close()
    {
        IsOpen = false; Inventory.DialogueOpen = false;
        Action cb = onComplete; onComplete = null; lines = null;
        if (cb != null) cb();
    }

    void OnGUI()
    {
        if (!IsOpen) return;
        EnsureStyles();
        GUI.depth = -1500;

        float W = Screen.width, H = Screen.height;

        // ── 하단 대사 박스(넓게) ──
        float bw = Mathf.Min(W * 0.88f, 1180f), bh = 190f;
        float bx = (W - bw) * 0.5f, by = H - bh - 28f;
        Rect boxBase = new Rect(bx, by, bw, bh);            // 연출 오프셋 없는 기준(초상화 정렬용)
        Vector2 bo = FxOffset(false);                        // [흔들림]/[떨림] — 박스
        Rect box = new Rect(bx + bo.x, by + bo.y, bw, bh);

        // 큰 초상화(왼쪽·틀 없이 컷아웃) — 박스보다 먼저 그려 박스가 앞에 오게
        DrawBigPortrait(boxBase, W, H);

        // 대사 박스 — 고급 프레임(먹색 그라데 + 금테 + 안쪽 이중 헤어라인 + 코너 브래킷)
        Fill(new Rect(box.x + 4, box.y + 5, box.width, box.height), new Color(0, 0, 0, 0.35f));   // 그림자
        UITheme.FillV(box, UITheme.A(UITheme.PanelTop, 0.97f), UITheme.A(UITheme.PanelBot, 0.985f));
        Border(box, 1.5f, UITheme.A(UITheme.Accent, 0.9f));
        Border(new Rect(box.x + 5f, box.y + 5f, box.width - 10f, box.height - 10f), 1f, UITheme.A(UITheme.Accent, 0.20f));
        UITheme.Corners(box, 16f, 3f);

        // 이름표 — 박스 안 좌상단(금테 뱃지 + 먹색 글자)
        Vector2 nsz = nameStyle.CalcSize(new GUIContent(speaker));
        Rect nameTag = new Rect(box.x + 22f, box.y + 14f, nsz.x + 30f, 34f);
        Fill(nameTag, UITheme.A(UITheme.Accent, 0.95f));
        nameStyle.normal.textColor = new Color(0.06f, 0.09f, 0.09f);
        GUI.Label(nameTag, speaker, nameStyle);
        UITheme.Divider(box.x + 22f, box.y + 54f, box.width - 44f, 0.4f);   // 이름 아래 장식 구분선

        // 본문(타자기)
        string full = curText;
        int n = Mathf.Clamp(Mathf.FloorToInt(shown), 0, full.Length);
        textStyle.normal.textColor = UITheme.Text;
        GUI.Label(new Rect(box.x + 28f, box.y + 64f, box.width - 56f, box.height - 78f), full.Substring(0, n), textStyle);

        // 진행 표시 ▼ (줄 다 보이면 깜빡)
        if (n >= full.Length && Mathf.Sin(Time.unscaledTime * 5f) > 0f)
        {
            hintStyle.normal.textColor = UITheme.Lighten(UITheme.Accent, 0.2f);
            GUI.Label(new Rect(box.xMax - 42f, box.yMax - 34f, 24f, 24f), "▼", hintStyle);
        }

        // 스킵 힌트(우상단) + 홀드 진행 게이지 — 전용 작은 스타일(이름표 스타일 재사용 금지: 21px 글자가 20px 상자에 깨졌음)
        if (skipStyle == null)
        {
            skipStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            skipStyle.hover.textColor = skipStyle.normal.textColor;   // 호버 색 반응 차단
        }
        skipStyle.normal.textColor = skipStyle.hover.textColor = new Color(0.60f, 0.61f, 0.65f);
        GUI.Label(new Rect(box.xMax - 240f, box.y + 8f, 220f, 22f), "[Ctrl] 꾹 눌러 건너뛰기", skipStyle);
        if (skipTimer > 0.02f)
        {
            Rect sg = new Rect(box.xMax - 190f, box.y + 32f, 170f, 4f);
            UITheme.Fill(sg, UITheme.A(UITheme.Border, 0.4f));
            UITheme.Fill(new Rect(sg.x, sg.y, sg.width * Mathf.Clamp01(skipTimer / skipHold), sg.height), UITheme.Accent);
        }
    }

    // 왼쪽에 큰 초상화를 '틀 없이' 그린다(세로 비율 유지·바닥 정렬·컷아웃 드롭섀도).
    private void DrawBigPortrait(Rect box, float W, float H)
    {
        if (portrait == null || portrait.texture == null) return;
        Rect tr = portrait.rect; Texture t = portrait.texture;
        float aspect = tr.width / Mathf.Max(1f, tr.height);

        float maxH = H * 0.55f;              // 초상화 높이(화면 대비) — 줄이면 더 작아짐
        float maxW = W * 0.30f;              // 왼쪽 영역 폭 제한
        float ph = maxH, pw = ph * aspect;
        if (pw > maxW) { pw = maxW; ph = pw / aspect; }

        float pLeft = W * 0.05f;
        float pTop = box.yMax - ph;           // 초상화 밑 = 대사박스 밑에 맞춤
        Vector2 po = FxOffset(true);          // [놀람]/[흔들림]/[떨림] — 초상화
        Rect pr = new Rect(pLeft + po.x, pTop + po.y, pw, ph);
        Rect tc = new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height);

        Color o = GUI.color;
        // 컷아웃 드롭섀도(실루엣)
        GUI.color = new Color(0f, 0f, 0f, 0.32f);
        GUI.DrawTextureWithTexCoords(new Rect(pr.x + 8f, pr.y + 10f, pr.width, pr.height), t, tc);
        // 실제 초상화
        GUI.color = Color.white;
        GUI.DrawTextureWithTexCoords(pr, t, tc);
        GUI.color = o;
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (textStyle != null) return;
        nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, wordWrap = true, alignment = TextAnchor.UpperLeft };
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    private void Fill(Rect r, Color c) { Color o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
    private void Border(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }
}
