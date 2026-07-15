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
//      [나]     이 줄은 '주인공'의 대사 — 왼쪽(주인공) 초상화·이름표가 밝아지고 상대는 흐려짐. 연출 태그와 조합 가능("[나][놀람]...")
//  · 양쪽 초상화(앤디와 레일리의 관 풍): 주인공=왼쪽, 상대=오른쪽(마주보게 좌우반전). 말하지 않는 쪽은 초상화·이름표를 어둡게.
//    주인공 초상화는 Resources/Portraits/후드.png (PlayerName과 같은 파일명) — 파일이 없으면 왼쪽엔 이름표만([나] 대사가 있을 때만) 표시.
public class DialogueUI : MonoBehaviour
{
    public static string PlayerName = "후드";   // 주인공 이름표(왼쪽) + 초상화 자동 로드 파일명
    public static DialogueUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }
    public static float ClosedAt = -99f;   // 마지막으로 대화가 닫힌 시각(unscaled) — 종료 입력이 상호작용에 재사용되는 것 방지

    private string speaker;
    private Sprite portrait;
    private string[] lines;
    private Action onComplete;
    private string[] choices;      // 마지막 줄 후 표시할 선택지(null이면 일반 대화)
    private Action<int> onChoice;  // 선택 콜백(고른 인덱스) — 선택지 대화의 종료 콜백을 겸함
    private bool choosing;         // 선택지 표시 중(하나를 고를 때까지 대화가 닫히지 않음)
    private int choiceSel;         // 현재 하이라이트된 선택지
    private float chooseTime;      // 선택지가 열린 시각(직전 입력이 바로 확정되는 것 방지)
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
    private bool lineIsPlayer;    // 현재 줄이 주인공([나]) 대사인가 — 밝기/이름표 전환용
    private bool hasPlayerLines;  // 이 대화에 [나] 줄이 하나라도 있나 — 왼쪽(주인공) 자리 표시 여부

    // 현재 줄의 태그를 파싱하고 타자기를 리셋. 태그는 순서 무관하게 여러 개 조합 가능("[나][놀람]...")
    private void StartLine()
    {
        string raw = lines[index];
        curFx = LineFx.None;
        lineIsPlayer = false;
        bool again = true;
        while (again && raw != null)
        {
            again = true;
            if (raw.StartsWith("[나]"))          { lineIsPlayer = true;      raw = raw.Substring("[나]".Length); }
            else if (raw.StartsWith("[놀람]"))   { curFx = LineFx.Surprise;  raw = raw.Substring("[놀람]".Length); }
            else if (raw.StartsWith("[흔들림]")) { curFx = LineFx.Shake;     raw = raw.Substring("[흔들림]".Length); }
            else if (raw.StartsWith("[떨림]"))   { curFx = LineFx.Tremble;   raw = raw.Substring("[떨림]".Length); }
            else again = false;
        }
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
    private GUIStyle nameStyle, textStyle, hintStyle, skipStyle, choiceStyle;

    void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("DialogueUI").AddComponent<DialogueUI>(); }

    public static void Show(string speaker, Sprite portrait, string[] lines, Action onComplete = null)
    {
        if (Instance == null || lines == null || lines.Length == 0) { if (onComplete != null) onComplete(); return; }
        Instance.Begin(speaker, portrait, lines, onComplete);
    }

    // 선택지 대화: 마지막 줄이 다 보이면 선택지가 떠서 하나를 고를 때까지 닫히지 않는다.
    //  onChoice(고른 인덱스)가 종료 콜백을 겸한다 — 콜백 안에서 새 Show(후속 대사)를 바로 열어도 안전.
    //  lines를 비우면 대사 없이 선택지만 바로 뜬다.
    public static void Show(string speaker, Sprite portrait, string[] lines, string[] choices, Action<int> onChoice)
    {
        if (choices == null || choices.Length == 0) { Show(speaker, portrait, lines, null); return; }
        if (Instance == null) { if (onChoice != null) onChoice(0); return; }
        if (lines == null || lines.Length == 0) lines = new string[] { "" };
        Instance.Begin(speaker, portrait, lines, null);
        Instance.choices = choices;
        Instance.onChoice = onChoice;
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
        choices = null; onChoice = null; choosing = false;   // 선택지 상태 초기화(선택지 대화는 Show가 Begin 뒤에 지정)
        hasPlayerLines = false;
        foreach (string l in ln) if (l != null && l.Contains("[나]")) { hasPlayerLines = true; break; }   // 연출 태그가 앞에 와도 인식
        index = 0; openTime = Time.unscaledTime;
        StartLine();
        IsOpen = true; Inventory.DialogueOpen = true;
    }

    void Update()
    {
        if (!IsOpen) return;
        int len = curText.Length;
        if (shown < len) shown += CharsPerSec * Time.unscaledDeltaTime;

        // [Ctrl] 길게 → 대화 전체 스킵(선택지가 있으면 닫는 대신 마지막 줄+선택지로 점프 — 선택은 건너뛸 수 없음)
        if (!choosing && Input.GetKey(skipKey))
        {
            skipTimer += Time.unscaledDeltaTime;
            if (skipTimer >= skipHold)
            {
                skipTimer = 0f;
                if (choices != null)
                {
                    if (index != lines.Length - 1) { index = lines.Length - 1; StartLine(); }
                    shown = curText.Length;
                }
                else { Close(); return; }
            }
        }
        else skipTimer = 0f;

        // 마지막 줄이 다 보였고 선택지가 있으면 → 선택 모드 진입
        if (!choosing && choices != null && index == lines.Length - 1 && shown >= curText.Length)
        { choosing = true; choiceSel = 0; chooseTime = Time.unscaledTime; }

        // 선택 모드: W·S/화살표로 이동, Space/F/Enter로 확정(마우스는 OnGUI에서 호버·클릭 처리)
        if (choosing)
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            { choiceSel = (choiceSel + choices.Length - 1) % choices.Length; AudioManager.Sfx("ui_move"); }
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            { choiceSel = (choiceSel + 1) % choices.Length; AudioManager.Sfx("ui_move"); }
            bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F)
                || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (confirm && Time.unscaledTime - chooseTime > 0.25f) ConfirmChoice(choiceSel);
            return;
        }

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
        ClosedAt = Time.unscaledTime;   // 이 직후 같은 F/클릭이 상호작용에 재사용되지 않게(대화 재시작 방지)
        Action cb = onComplete; onComplete = null; lines = null;
        if (cb != null) cb();
    }

    // 선택 확정: 대화를 닫고 onChoice(인덱스) 실행 — 콜백 안에서 후속 대사 Show를 바로 열어도 안전.
    private void ConfirmChoice(int i)
    {
        Action<int> cb = onChoice;
        choices = null; onChoice = null; choosing = false;
        IsOpen = false; Inventory.DialogueOpen = false;
        ClosedAt = Time.unscaledTime;
        onComplete = null; lines = null;
        AudioManager.Sfx("ui_move");
        if (cb != null) cb(i);
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

        // 양쪽 초상화(앤디와 레일리의 관 풍) — 주인공=왼쪽, 상대=오른쪽(마주보게 반전). 말하는 쪽만 밝게.
        // 박스보다 먼저 그려 박스가 앞에 오게.
        Sprite playerPor = AutoPortrait(PlayerName);
        bool showPlayerSide = playerPor != null || hasPlayerLines;   // 주인공 자리: 초상화가 있거나 [나] 대사가 있을 때만
        DrawSidePortrait(playerPor, boxBase, W, H, false, lineIsPlayer);
        DrawSidePortrait(portrait, boxBase, W, H, true, !lineIsPlayer);

        // 대사 박스 — 고급 프레임(먹색 그라데 + 금테 + 안쪽 이중 헤어라인 + 코너 브래킷)
        Fill(new Rect(box.x + 4, box.y + 5, box.width, box.height), new Color(0, 0, 0, 0.35f));   // 그림자
        UITheme.FillV(box, UITheme.A(UITheme.PanelTop, 0.97f), UITheme.A(UITheme.PanelBot, 0.985f));
        Border(box, 1.5f, UITheme.A(UITheme.Accent, 0.9f));
        Border(new Rect(box.x + 5f, box.y + 5f, box.width - 10f, box.height - 10f), 1f, UITheme.A(UITheme.Accent, 0.20f));
        UITheme.Corners(box, 16f, 3f);

        // 이름표 — 주인공(좌상단) + 상대(우상단). 말하는 쪽=금테, 아닌 쪽=흐리게.
        if (showPlayerSide) DrawNameTag(box, PlayerName, false, lineIsPlayer);
        DrawNameTag(box, speaker, true, !lineIsPlayer);
        UITheme.Divider(box.x + 22f, box.y + 54f, box.width - 44f, 0.4f);   // 이름 아래 장식 구분선

        // 본문(타자기)
        string full = curText;
        int n = Mathf.Clamp(Mathf.FloorToInt(shown), 0, full.Length);
        textStyle.normal.textColor = UITheme.Text;
        GUI.Label(new Rect(box.x + 28f, box.y + 64f, box.width - 56f, box.height - 78f), full.Substring(0, n), textStyle);

        // 진행 표시 ▼ (줄 다 보이면 깜빡 — 선택지가 떠 있을 땐 숨김)
        if (!choosing && n >= full.Length && Mathf.Sin(Time.unscaledTime * 5f) > 0f)
        {
            hintStyle.normal.textColor = UITheme.Lighten(UITheme.Accent, 0.2f);
            GUI.Label(new Rect(box.xMax - 42f, box.yMax - 34f, 24f, 24f), "▼", hintStyle);
        }

        // 선택지 목록(대사박스 오른쪽 위)
        if (choosing && choices != null) DrawChoices(box);

        // 스킵 힌트(좌하단 — 우상단은 상대 이름표 자리) + 홀드 진행 게이지 — 전용 작은 스타일
        if (skipStyle == null)
        {
            skipStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            skipStyle.hover.textColor = skipStyle.normal.textColor;   // 호버 색 반응 차단
        }
        skipStyle.normal.textColor = skipStyle.hover.textColor = new Color(0.60f, 0.61f, 0.65f);
        GUI.Label(new Rect(box.x + 22f, box.yMax - 32f, 220f, 22f), "[Ctrl] 꾹 눌러 건너뛰기", skipStyle);
        if (skipTimer > 0.02f)
        {
            Rect sg = new Rect(box.x + 22f, box.yMax - 10f, 170f, 4f);
            UITheme.Fill(sg, UITheme.A(UITheme.Border, 0.4f));
            UITheme.Fill(new Rect(sg.x, sg.y, sg.width * Mathf.Clamp01(skipTimer / skipHold), sg.height), UITheme.Accent);
        }
    }

    // 선택지 목록 — 대사박스 오른쪽 위에 세로로 쌓임. 마우스 호버=이동·클릭=확정(키보드는 Update에서).
    private void DrawChoices(Rect box)
    {
        float cw = Mathf.Min(box.width * 0.46f, 460f), rh = 46f, gap = 8f;
        float cx = box.xMax - cw;
        float cy = box.y - choices.Length * (rh + gap) - 6f;
        Vector2 m = Event.current.mousePosition;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;

        for (int i = 0; i < choices.Length; i++)
        {
            Rect r = new Rect(cx, cy + i * (rh + gap), cw, rh);
            if (r.Contains(m)) choiceSel = i;   // 호버 = 하이라이트 이동
            bool sel = choiceSel == i;

            Fill(new Rect(r.x + 3f, r.y + 4f, r.width, r.height), new Color(0f, 0f, 0f, 0.30f));   // 그림자
            UITheme.FillV(r, UITheme.A(UITheme.PanelTop, sel ? 0.99f : 0.90f), UITheme.A(UITheme.PanelBot, 0.985f));
            Border(r, 1.5f, sel ? UITheme.A(UITheme.Accent, 0.95f) : UITheme.A(UITheme.Border, 0.55f));

            Color tc = sel ? Color.white : new Color(0.72f, 0.73f, 0.76f);
            choiceStyle.normal.textColor = choiceStyle.hover.textColor = tc;
            if (sel)
            {
                UITheme.Fill(new Rect(r.x + 10f, r.y + rh * 0.24f, 4f, rh * 0.52f), UITheme.Accent);   // 좌측 오렌지 바
                GUI.Label(new Rect(r.x + 20f, r.y, 24f, rh), "▸", choiceStyle);
            }
            GUI.Label(new Rect(r.x + 44f, r.y, r.width - 56f, rh), choices[i], choiceStyle);

            if (click && r.Contains(m) && Time.unscaledTime - chooseTime > 0.2f)
            { Event.current.Use(); ConfirmChoice(i); return; }
        }
    }

    // 한쪽 큰 초상화를 '틀 없이' 그린다(세로 비율 유지·바닥 정렬·컷아웃 드롭섀도).
    //  rightSide=true면 오른쪽 배치 + 좌우반전(서로 마주보게). active=false면 어둡게(말하지 않는 쪽).
    private void DrawSidePortrait(Sprite s, Rect box, float W, float H, bool rightSide, bool active)
    {
        if (s == null || s.texture == null) return;
        Rect tr = s.rect; Texture t = s.texture;
        float aspect = tr.width / Mathf.Max(1f, tr.height);

        float maxH = H * 0.55f;              // 초상화 높이(화면 대비) — 줄이면 더 작아짐
        float maxW = W * 0.30f;              // 한쪽 영역 폭 제한
        float ph = maxH, pw = ph * aspect;
        if (pw > maxW) { pw = maxW; ph = pw / aspect; }

        float px = rightSide ? W * 0.95f - pw : W * 0.05f;
        float pTop = box.yMax - ph;           // 초상화 밑 = 대사박스 밑에 맞춤
        Vector2 po = active ? FxOffset(true) : Vector2.zero;   // 연출([놀람] 등)은 말하는 쪽에만
        Rect pr = new Rect(px + po.x, pTop + po.y, pw, ph);
        // 오른쪽은 U좌표를 뒤집어 좌우반전(왼쪽의 주인공과 마주보게)
        Rect tc = rightSide
            ? new Rect((tr.x + tr.width) / t.width, tr.y / t.height, -tr.width / t.width, tr.height / t.height)
            : new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height);

        Color o = GUI.color;
        // 컷아웃 드롭섀도(실루엣)
        GUI.color = new Color(0f, 0f, 0f, 0.32f);
        GUI.DrawTextureWithTexCoords(new Rect(pr.x + 8f, pr.y + 10f, pr.width, pr.height), t, tc);
        // 실제 초상화 — 말하지 않는 쪽은 어둡게
        GUI.color = active ? Color.white : new Color(0.38f, 0.38f, 0.44f, 0.95f);
        GUI.DrawTextureWithTexCoords(pr, t, tc);
        GUI.color = o;
    }

    // 이름표 뱃지 — active=금테+먹색 글자(말하는 쪽) / 비활성=회색 흐림
    private void DrawNameTag(Rect box, string name, bool rightSide, bool active)
    {
        if (string.IsNullOrEmpty(name)) return;
        Vector2 nsz = nameStyle.CalcSize(new GUIContent(name));
        float w = nsz.x + 30f;
        float x = rightSide ? box.xMax - 22f - w : box.x + 22f;
        Rect tag = new Rect(x, box.y + 14f, w, 34f);
        if (active)
        {
            Fill(tag, UITheme.A(UITheme.Accent, 0.95f));
            nameStyle.normal.textColor = new Color(0.06f, 0.09f, 0.09f);
        }
        else
        {
            Fill(tag, UITheme.A(UITheme.Border, 0.35f));
            nameStyle.normal.textColor = new Color(0.52f, 0.53f, 0.58f);
        }
        GUI.Label(tag, name, nameStyle);
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (textStyle != null) return;
        nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, wordWrap = true, alignment = TextAnchor.UpperLeft };
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        choiceStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
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
