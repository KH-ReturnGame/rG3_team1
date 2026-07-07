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

    [Header("패링 레슨")]
    [Range(0.02f, 0.5f)] public float slowScale = 0.12f;  // 슬로우모션 배율
    public float reactSeconds = 3.2f;                     // 우클릭 대기(실시간) — 넘기면 그대로 피격

    [Header("예지 타이밍")]
    [Range(0f, 0.95f)] public float windupLateFraction = 0.85f;   // 예비동작의 이 비율이 지난 '직전'(맞기 아주 임박)에 발동

    private static CombatTutorial _inst;

    private bool inScene;
    private PlayerController player;

    private bool combatHelpShown;
    private bool parryLessonDone;

    // 패링 레슨 진행 상태
    private bool lessonActive;
    private Enemy lessonEnemy;
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
        inScene = s.name == TutorialSceneName;
        player = null;
        combatHelpShown = false;
        parryLessonDone = false;
        if (inScene) ApplyEnemyAttackGrace();   // 튜토리얼 적들의 첫 공격을 늦춤
    }

    // 튜토리얼 씬의 모든 적에게 '첫 공격 유예'를 적용(발견 후 첫 스윙까지 시간 확보).
    private void ApplyEnemyAttackGrace()
    {
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            if (e != null) e.firstAttackDelay = Mathf.Max(e.firstAttackDelay, enemyFirstAttackDelay);
    }

    private PlayerController Player()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        return player;
    }

    void Update()
    {
        if (!inScene) return;

        // 패링 레슨 진행 중: 우클릭 성공 / 시간초과·공격종료 처리(언스케일 시간 기준)
        if (lessonActive)
        {
            if (Input.GetMouseButtonDown(1)) { ResolveParrySuccess(); return; }
            bool timedOut = Time.unscaledTime - lessonStartReal > reactSeconds;
            if (lessonEnemy == null || !lessonEnemy.IsAttacking || timedOut) EndLesson();
            return;
        }

        // 몬스터 발견 → 공격 도움말 1회
        if (!combatHelpShown) TryShowCombatHelp();
    }

    private void TryShowCombatHelp()
    {
        var p = Player();
        if (p == null || HelpPopupUI.Instance == null) return;
        if (NearestLiveEnemy(p.transform.position, sightRange) == null) return;

        combatHelpShown = true;
        const string ct = "전투 — 공격";
        const string cb = "[좌클릭]으로 검을 휘둘러 적을 공격합니다. 연속으로 누르면 콤보가 이어지고, 마지막 일격이 가장 강력합니다.\n" +
            "[Q]를 누르면 넓게 베는 횡베기 스킬을 사용합니다(쿨타임 있음).\n" +
            "적의 공격은 [우클릭] 가드로 막거나 타이밍 맞춰 패링할 수 있습니다.";
        if (combatHelpManual) HelpPopupUI.Instance.ShowManual(ct, cb);
        else HelpPopupUI.Instance.ShowTimed(ct, cb, combatHelpSeconds);
    }

    private Enemy NearestLiveEnemy(Vector3 from, float maxDist)
    {
        Enemy best = null;
        float bd = maxDist;
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (e == null) continue;
            float d = Vector2.Distance(from, e.transform.position);
            if (d <= bd) { bd = d; best = e; }
        }
        return best;
    }

    // 적이 예비동작에 진입 → 예지 판정(★튜토리얼 씬 한정).
    //  규칙: 체력이 반 칸(위기)일 때 피격 '직전'이면 항상 예지(잠재 기프트)가 발동한다.
    //   · 근접·원거리·돌진 전부 대상(원거리는 발사 직전에 느려져 피할 시간을 준다)
    //   · 예비동작이 windupLateFraction만큼 지난 '막판'에 발동 — 진짜 맞기 직전의 긴박함
    //   · 첫 발동 = 각성 연출 + 패링 레슨(근접만, 우클릭 어시스트) / 이후 = 슬로우모션+눈빛만
    private Coroutine pendingPrecog;

    private void OnEnemyWindup(Enemy e)
    {
        if (!inScene || lessonActive || SlowMoFx.Active || pendingPrecog != null) return;
        if (e == null) return;                                     // 원거리 포함 — 종류 안 가림
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
        if (e == null || !e.IsAttacking || SlowMoFx.Active || lessonActive) yield break;
        if (GameManager.Instance == null || GameManager.Instance.CurrentHalf > 1) yield break;   // 재확인(그새 회복했으면 취소)

        PrecogCharm.PlayEyeFlash(p);   // 붉은 눈빛 — 잠재 기프트

        if (!parryLessonDone && e.IsParryableMelee)
        {
            // 첫 각성: 레슨(우클릭 어시스트) — 근접 공격에서만
            lessonActive = true;
            lessonEnemy = e;
            lessonStartReal = Time.unscaledTime;
            SlowMoFx.BeginHeld(slowScale);   // 시간감속 + 줌인 + 집중 연출
            Toast.Show("몸 속 깊은 곳에서 무언가 깨어난다 — 세상이 느려진다", 3f);
            if (HelpPopupUI.Instance != null)
                HelpPopupUI.Instance.ShowSticky("각성 — 패링!",
                    "죽음의 위기에 잠재된 기프트가 깨어났습니다. 적의 공격이 느리게 보입니다!\n지금 [우클릭]으로 가드하면 *패링*이 발동해 적을 기절시키고 반격할 수 있습니다.");
        }
        else
        {
            // 이후(또는 원거리): 예지만 발동 — 피하거나 스스로 패링
            SlowMoFx.BeginTimed(slowScale + 0.08f, 1.6f);
        }
    }

    private void ResolveParrySuccess()
    {
        SlowMoFx.End();                                            // ★ Juice보다 먼저 복구해야 히트스톱이 동작
        if (HelpPopupUI.Instance != null) HelpPopupUI.Instance.ForceHide();
        var p = Player();
        if (p != null) p.TutorialParrySuccess(lessonEnemy as IParryable);   // 그로기 + 반격 + "팅"
        lessonActive = false;
        lessonEnemy = null;
        parryLessonDone = true;
    }

    // 시간초과 / 공격종료 / 씬전환 → 시간만 복구하고 도움말 닫음(공격은 그대로 진행).
    private void EndLesson()
    {
        if (pendingPrecog != null) { StopCoroutine(pendingPrecog); pendingPrecog = null; }   // 대기 중 예지 취소(씬 전환 안전)
        if (lessonActive)
        {
            SlowMoFx.End();
            if (HelpPopupUI.Instance != null) HelpPopupUI.Instance.ForceHide();
            parryLessonDone = true;   // 한 번 발동했으면(성공/실패 무관) 소진
        }
        lessonActive = false;
        lessonEnemy = null;
    }
}
