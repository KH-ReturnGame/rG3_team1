using UnityEngine;
using UnityEngine.SceneManagement;

// 튜토리얼 전용 전투 안내. TutorialScene에서만 동작하는 자동부팅 싱글톤.
//  1) 몬스터 발견(근접) → 공격(좌클릭·Q) 도움말 1회.
//  2) 적의 첫 예비동작(피격 직전, Enemy.WindupStarted) → 시간이 느려지며 패링 안내 표시.
//     이때 [우클릭]이 감지되면 슬로우모션·도움말이 꺼지고 패링 성공 연출("팅")이 발동
//     (적 그로기 + 반격 모션 + Q쿨 초기화). 미입력 시 시간만 복구되고 공격은 그대로 들어감.
//  설정: 씬 이름이 다르면 TutorialSceneName 만 바꾸면 됨.
public class CombatTutorial : MonoBehaviour
{
    public const string TutorialSceneName = "TutorialScene";

    [Header("발견 도움말")]
    public float sightRange = 7.5f;     // 이 거리 안에 살아있는 적이 들어오면 공격 안내(1회)
    public bool combatHelpManual = false;  // false: 시간 지나면 자동으로 사라짐(요청) / true: ESC·X로 직접 닫기
    public float combatHelpSeconds = 9f;   // 자동일 때 표시 시간

    [Header("적 첫 공격 유예")]
    public float enemyFirstAttackDelay = 3f;   // 튜토리얼 적이 플레이어를 발견한 뒤 첫 공격까지 더 기다림(읽을 시간)

    [Header("튜토리얼 밸런스")]
    public float enemyDamageCap = 1f;     // 튜토 적 공격력 상한(하트) — 한 칸씩(요청으로 0.5→1 버프)
    // (구) 각성 회복은 폐지 — 회복은 '저스트 패링 성공 시 한 칸'(PlayerController.justParryHealHalves)으로 일원화

    [Header("패링 레슨")]
    [Range(0.02f, 0.5f)] public float slowScale = 0.05f;  // (구) 감속 배율 — 레슨도 완전 정지로 통일돼 미사용
    public float reactSeconds = 3.2f;                     // 우클릭 대기(실시간) — 넘기면 그대로 피격

    [Header("예지 타이밍")]
    [Range(0f, 0.95f)] public float windupLateFraction = 0.85f;   // (근접) 예비동작의 이 비율이 지난 '직전'(맞기 아주 임박)에 발동
    public float projectileNearDist = 1.6f;   // (원거리) 투사체가 플레이어와 이 거리 안에 오면 발동 — '닿기 직전'

    private static CombatTutorial _inst;

    private bool inScene;
    private PlayerController player;

    private bool parryLessonDone;

    // 패링 레슨 진행 상태
    private bool lessonActive;
    private Enemy lessonEnemy;            // 근접 레슨 대상(공격이 끝나면 레슨 종료)
    private Projectile lessonProjectile;  // 원거리 레슨 대상(사라지면 레슨 종료)
    private bool lessonRanged;            // 이번 레슨이 원거리(투사체)인가
    private float lessonStartReal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_inst != null) return;
        var go = new GameObject("CombatTutorial");
        _inst = go.AddComponent<CombatTutorial>();
        DontDestroyOnLoad(go);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Enemy.WindupStarted += OnEnemyWindup;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Enemy.WindupStarted -= OnEnemyWindup;
    }

    void Start() => Refresh(SceneManager.GetActiveScene());   // 부팅 시점 현재 씬 반영

    private void OnSceneLoaded(Scene s, LoadSceneMode m) => Refresh(s);

    private void Refresh(Scene s)
    {
        EndLesson();   // 진행 중 레슨이 있으면 시간/도움말 정리(씬 전환 안전)
        if (guardCardPending != null) { StopCoroutine(guardCardPending); guardCardPending = null; }   // 지연 카드 취소
        inScene = s.name == TutorialSceneName;
        player = null;
        parryLessonDone = false;
        guardParryCardShown = false;
        if (inScene) ApplyEnemyAttackGrace();   // 튜토리얼 적들의 첫 공격을 늦춤
    }

    // 튜토리얼 씬의 모든 적에게 '첫 공격 유예' + '공격력 상한(반칸)' 적용.
    //  상한 덕에 체력이 반칸 단위로 완만하게 깎여, 하한(0.5칸)에 닿기 전에 전투가 끝나는 그림을 만든다.
    private void ApplyEnemyAttackGrace()
    {
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            if (e != null)
            {
                e.firstAttackDelay = Mathf.Max(e.firstAttackDelay, enemyFirstAttackDelay);
                e.attackDamage = Mathf.Min(e.attackDamage, enemyDamageCap);
            }
    }

    private PlayerController Player()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        return player;
    }

    void Update()
    {
        if (!inScene) return;

        // 패링 레슨 진행 중: 우클릭 성공 / 시간초과·대상소멸 처리(언스케일 시간 기준)
        if (lessonActive)
        {
            if (Input.GetMouseButtonDown(1)) { ResolveParrySuccess(); return; }
            bool timedOut = Time.unscaledTime - lessonStartReal > reactSeconds;
            bool subjectGone = lessonRanged ? (lessonProjectile == null)
                                            : (lessonEnemy == null || !lessonEnemy.IsAttacking);
            if (subjectGone || timedOut) EndLesson(teachCard: true);
            return;
        }

        // 첫 각성(원거리): 적 투사체가 코앞에 온 순간 — 시간 완전 정지 + 우클릭(튕겨내기) 유도
        if (!parryLessonDone && !SlowMoFx.Active && pendingPrecog == null) TryRangedLesson();

        // (구) 몬스터 발견 → 공격 도움말은 폐지 — 독백 끝 [이동/공격] 카드(TutorialSequence)가 대체
    }

    // 원거리 첫 각성: 살아있는 적 투사체가 플레이어에 근접(projectileNearDist)하면 레슨 시작.
    //  AutoPrecog(1.3)보다 살짝 먼 거리(1.6)에서 먼저 잡아 레슨이 우선권을 갖는다.
    private void TryRangedLesson()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentHalf > 1) return;   // 반 칸 위기에서만
        var p = Player();
        if (p == null) return;
        var col = p.GetComponent<Collider2D>();
        Vector2 target = col != null ? (Vector2)col.bounds.center : (Vector2)p.transform.position + Vector2.up * 0.6f;
        for (int i = 0; i < Projectile.All.Count; i++)
        {
            var pr = Projectile.All[i];
            if (pr == null || pr.Reflected) continue;
            Vector2 to = target - (Vector2)pr.transform.position;
            float dist = to.magnitude;
            if (dist < 0.01f || dist > projectileNearDist) continue;
            if (Vector2.Dot(pr.Velocity, to / dist) < 0.5f) continue;   // 멀어지는/빗나가는 탄 무시
            BeginLesson(null, pr);
            return;
        }
    }

    // 딸피(0.5칸) 상태의 각성 레슨이 끝나면 → [1p 가드 / 2p 패링] 카드(1회).
    //  ★패링 순간 바로 띄우지 않고, 연출("팅"·그로기·반사)이 다 끝난 뒤(guardCardDelay)에 표시.
    public float guardCardDelay = 2f;      // 레슨 종결 후 카드까지 지연(실시간)
    private bool guardParryCardShown;
    private Coroutine guardCardPending;

    private void QueueGuardParryCard()
    {
        if (guardParryCardShown || guardCardPending != null) return;
        guardCardPending = StartCoroutine(GuardCardLater());
    }

    private System.Collections.IEnumerator GuardCardLater()
    {
        float t = 0f;
        while (t < guardCardDelay) { t += Time.unscaledDeltaTime; yield return null; }
        guardCardPending = null;
        if (inScene && !GameOverUI.Showing) ShowGuardParryCard();   // 씬을 떠났거나 게임오버 중이면 표시하지 않음
    }

    private void ShowGuardParryCard()
    {
        if (guardParryCardShown || HelpPopupUI.Instance == null) return;
        guardParryCardShown = true;
        HelpPopupUI.Instance.ShowPages(true,
            new HelpPopupUI.HelpPage("guard", "가드",
                "[우클릭]을 누르고 있으면 가드 자세를 취합니다.\n가드 중에는 받는 피해가 줄어들지만, 움직임이 느려집니다.\n위험할 때는 우선 가드부터 — 살아남는 것이 먼저입니다."),
            new HelpPopupUI.HelpPage("parry", "패링",
                "적의 공격이 닿기 '직전' 완벽한 타이밍의 [우클릭] 가드는 *패링*이 됩니다.\n" +
                "*저스트 패링*에 성공하면 적이 *그로기*에 빠지고, [Q] 스킬 쿨타임이 초기화되며, 체력을 반 칸 회복합니다.\n" +
                "그로기 상태의 적은 치명타를 받습니다 — 반격의 순간을 노리세요!"));
    }

    // 적이 예비동작에 진입 → '첫 각성 레슨' 판정(★튜토리얼 씬 한정, 스토리 연출이라 난이도 무관).
    //  체력이 반 칸(위기)일 때 근접 공격 '직전'에 1회 — 각성 연출 + 패링 레슨(우클릭 어시스트).
    //  이후의 반복 자동 예지는 난이도 시스템(AutoPrecog, 쉬움 전용)이 전 씬에서 담당한다.
    private Coroutine pendingPrecog;

    private void OnEnemyWindup(Enemy e)
    {
        if (!inScene || lessonActive || SlowMoFx.Active || pendingPrecog != null) return;
        if (parryLessonDone) return;                               // 레슨은 1회 — 이후는 AutoPrecog 몫
        if (e == null || e.RangedPrecog) return;                   // ★근접 공격만 레슨 대상 — 원거리는 '투사체 근접 정지'(AutoPrecog) 규칙을 따름(발사 순간 감속 방지)
        var p = Player();
        if (p == null || e.TargetPlayer != p.transform) return;    // 플레이어를 노리는 공격만
        if (GameManager.Instance == null || GameManager.Instance.CurrentHalf > 1) return;   // 반 칸 위기에서만

        pendingPrecog = StartCoroutine(TriggerPrecogLate(e, p));
    }

    // 예비동작 후반까지 기다렸다가(스케일 시간 = 적 윈드업과 같은 시계) 발동
    private System.Collections.IEnumerator TriggerPrecogLate(Enemy e, PlayerController p)
    {
        float wait = Mathf.Max(0f, e.attackWindup * windupLateFraction);
        float t = 0f;
        while (t < wait)
        {
            if (e == null || !e.IsAttacking) { pendingPrecog = null; yield break; }   // 공격 취소(그로기·사망)
            t += Time.deltaTime;
            yield return null;
        }
        pendingPrecog = null;
        if (e == null || !e.IsAttacking || SlowMoFx.Active || lessonActive || parryLessonDone) yield break;
        if (GameManager.Instance == null || GameManager.Instance.CurrentHalf > 1) yield break;   // 재확인(그새 회복했으면 취소)
        BeginLesson(e, null);
    }

    // 첫 각성 레슨 시작 — 근접(e)이면 예비동작을 감속으로 붙잡고, 원거리(pr)면 투사체를 코앞에 '정지'로 세워둔다.
    private void BeginLesson(Enemy e, Projectile pr)
    {
        var p = Player();
        PrecogCharm.PlayEyeFlash(p);   // 붉은 눈빛 — 잠재 기프트

        lessonActive = true;
        lessonEnemy = e;
        lessonProjectile = pr;
        lessonRanged = pr != null;
        lessonStartReal = Time.unscaledTime;

        if (lessonRanged)
        {
            SlowMoFx.FreezeHeld();   // 완전 정지 — 투사체가 코앞에 멈춘 채 우클릭을 기다린다
            Toast.Show("몸 속 깊은 곳에서 무언가 깨어난다 — 세상이 멈춘다", 3f);
            if (HelpPopupUI.Instance != null)
                HelpPopupUI.Instance.ShowSticky("각성 — 튕겨내기!",
                    "죽음의 위기에 잠재된 기프트가 깨어나 시간이 멈췄습니다!\n지금 [우클릭]으로 가드하면 날아오는 탄환을 *튕겨내* 적에게 되돌려줍니다.");
        }
        else
        {
            SlowMoFx.FreezeHeld();   // ★근접도 완전 정지 — 예지 발동은 언제나 '시간 멈춤' 연출(플래시+청회색 색조)로 통일
            Toast.Show("시간 정지됨", 3f);
            if (HelpPopupUI.Instance != null)
                HelpPopupUI.Instance.ShowSticky("각성 — 패링!",
                    "죽음의 위기에 잠재된 기프트가 깨어나 시간이 멈췄습니다!\n지금 [우클릭]으로 가드하면 *패링*이 발동해 적을 기절시키고 반격할 수 있습니다.");
        }
    }

    private void ResolveParrySuccess()
    {
        SlowMoFx.End();                                            // ★ Juice보다 먼저 복구해야 히트스톱이 동작
        if (HelpPopupUI.Instance != null) HelpPopupUI.Instance.ForceHide();
        if (!lessonRanged)
        {
            var p = Player();
            if (p != null) p.TutorialParrySuccess(lessonEnemy as IParryable);   // 그로기 + 반격 + "팅"
        }
        // 원거리: 방금 누른 우클릭으로 가드가 이미 시작됨 — 시간이 풀리면 코앞의 투사체가
        // 저스트 패링 창 안에서 자동으로 '반사'된다(TryDeflectProjectile). 별도 연출 불필요.
        lessonActive = false;
        lessonEnemy = null;
        lessonProjectile = null;
        lessonRanged = false;
        parryLessonDone = true;

        // 첫 패링이 '아예 끝난 뒤'에 [가드/패링] 정리 카드(연출 안 끊게 지연 표시)
        QueueGuardParryCard();
    }

    // 시간초과 / 공격종료 / 씬전환 → 시간만 복구하고 도움말 닫음(공격은 그대로 진행).
    private void EndLesson(bool teachCard = false)
    {
        if (pendingPrecog != null) { StopCoroutine(pendingPrecog); pendingPrecog = null; }   // 대기 중 예지 취소(씬 전환 안전)
        if (lessonActive)
        {
            SlowMoFx.End();
            if (HelpPopupUI.Instance != null) HelpPopupUI.Instance.ForceHide();
            parryLessonDone = true;   // 한 번 발동했으면(성공/실패 무관) 소진
            if (teachCard) QueueGuardParryCard();   // 패링 못 해서 맞았어도 가드/패링은 가르친다(피격 연출 끝난 뒤)
        }
        lessonActive = false;
        lessonEnemy = null;
        lessonProjectile = null;
        lessonRanged = false;
    }

    // ── 첫 예지 중 '우클릭' 시각 암시 ── 스티키 배너 텍스트에 더해, 화면에 깜박이는 마우스 우클릭 아이콘을 띄운다.
    private static Texture2D white;
    private GUIStyle hintSt;
    void OnGUI()
    {
        if (!lessonActive) return;
        if (hintSt == null) hintSt = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        float sw = Screen.width, sh = Screen.height;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
        GUI.depth = -1400;

        // 마우스 아이콘(절차) — 오른쪽 버튼이 빨갛게 깜박여 '우클릭'을 가리킨다
        float mw = 54f, mh = 78f;
        float mx = sw * 0.5f - mw * 0.5f, my = sh * 0.60f;
        DrawMouse(mx, my, mw, mh, pulse);

        // "우클릭!" 텍스트(깜박임)
        hintSt.normal.textColor = new Color(1f, 0.85f, 0.4f, 0.55f + 0.45f * pulse);
        GUI.Label(new Rect(0, my + mh + 6f, sw, 34f), "우클릭!", hintSt);
    }

    private void DrawMouse(float x, float y, float w, float h, float pulse)
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        float bh = h * 0.44f;   // 버튼부 높이
        Fill(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), new Color(0f, 0f, 0f, 0.5f));                 // 외곽 그림자
        Fill(new Rect(x, y, w, h), new Color(0.86f, 0.87f, 0.9f, 0.96f));                            // 몸체
        Fill(new Rect(x, y, w * 0.5f, bh), new Color(0.52f, 0.53f, 0.58f, 0.92f));                   // 왼쪽 버튼(비활성)
        Fill(new Rect(x + w * 0.5f, y, w * 0.5f, bh), new Color(0.92f, 0.26f, 0.2f, 0.45f + 0.55f * pulse));   // ★오른쪽 버튼(빨강 깜박)
        Fill(new Rect(x + w * 0.5f - 1f, y, 2f, bh), new Color(0.3f, 0.3f, 0.35f, 0.9f));            // 중앙 분할선
        Fill(new Rect(x + w * 0.5f - 3.5f, y + h * 0.12f, 7f, h * 0.16f), new Color(0.4f, 0.4f, 0.45f, 0.95f));  // 스크롤 휠
    }

    private static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
}
