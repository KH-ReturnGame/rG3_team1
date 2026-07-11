using UnityEngine;

// 첫 보스 "굶주린 흡수체" — 기프트를 흡수하다 폭주한 옛 전사(Enemy 상속, 패턴 상태머신).
//  페이즈1: ①3연격 콤보(연속 패링 시험) ②백스텝→돌진 ③투사체 볼리(반사 기회)
//  페이즈2(체력 60%↓, 포효 전환): ④도약 내려찍기(충격파—점프 회피) ⑤★붉은 강공격(패링 불가—대시 회피 강제) + 전체 고속화
//  그로기: 저스트 패링을 staggerNeed(3)회 누적하면 그로기(치명타 창). 그 전엔 짧은 휘청만.
//  연동: questKillId="boss_first"(메인퀘 완료), WindupStarted(예지 자동), BossHealthBar(Active 참조).
public class BossEnemy : Enemy
{
    private enum Pattern { None, Combo3, Charge, Volley, LeapSlam, RedSlash, Roar, QuakeWave }

    [Header("보스 — 굶주린 흡수체")]
    public string bossName = "굶주린 흡수체";
    [Range(0.2f, 0.9f)] public float phase2Frac = 0.6f;   // 이 체력 비율 밑에서 페이즈2
    public int staggerNeed = 3;                            // 저스트 패링 누적 → 그로기
    public GameObject projectilePrefab;                    // 볼리용(EnemyProjectile)
    public float projectileSpeed = 10f;

    [Header("패턴 수치")]
    public float comboGap = 0.38f;        // 3연격 타격 간격(연속 패링 리듬)
    public float chargeSpeed = 13f;       // 돌진 속도
    public float leapTime = 0.65f;        // 도약 체공 시간
    public float slamWidth = 3.4f;        // 내려찍기 충격파 폭(±)
    public float redWindup = 0.95f;       // 붉은 강공격 예비동작(길게 — 읽고 피할 시간)
    public float redDamage = 2f;          // 붉은 강공격 피해(하트)

    public static BossEnemy Active;       // 체력바 UI 참조
    public float HealthFrac => Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
    public bool Phase2 => phase2;
    public bool Encountered => encountered;   // 체력바는 전투 시작 후에만 표시

    private Pattern cur = Pattern.None;
    private Pattern lastPattern = Pattern.None;   // 같은 패턴 연속 방지
    private int step;                     // 패턴 내부 진행(콤보 타수/볼리 발수/파동 링)
    private bool phase2;
    private bool encountered;             // 첫 조우(어그로) — 퀘스트 진행 + 보스 BGM
    private int stagger;                  // 저스트 패링 누적
    private int comboMax;                 // 이번 콤보 타수(3~4)
    private bool comboHeavy;              // 마지막 타 = 멈칫 후 강타(패링 타이밍 미끼)
    private bool heavyPaused;             // 강타 멈칫 소비 여부
    private Vector2 chargeDir;
    private float chargeStartX;
    private SpriteRenderer redGlow;       // 붉은 강공격 텔레그래프(자식 글로우)
    private int wallMask;

    void OnEnable() { Active = this; }
    void OnDisable() { if (Active == this) Active = null; }

    protected override void Start()
    {
        randomizeStats = false;   // 보스는 수치 고정
        base.Start();
        wallMask = LayerMask.GetMask("Ground");
        BuildRedGlow();
    }

    // ══════════ 패턴 선택(추격) ══════════
    protected override void TickChase()
    {
        if (player == null) { SetMove(0); return; }

        // 첫 조우: 메인퀘 진행(심층을 향해 완료 → 첫 번째 위협 수주) + 보스 BGM
        if (!encountered)
        {
            encountered = true;
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.CompleteForce("mq_descend");
                QuestManager.Instance.AcceptById("mq_boss");
            }
            AudioManager.Bgm("boss", 0.8f);
            Juice.Shake(0.35f, 0.5f);
            AudioManager.Sfx("boss_roar");
        }

        // 페이즈2 전환: 포효(전 패턴 중단 아님 — 추격 상태에서만 진입)
        if (!phase2 && currentHealth <= maxHealth * phase2Frac) { BeginPattern(Pattern.Roar); return; }

        dir = player.position.x >= transform.position.x ? 1 : -1;
        float d = DistToPlayer();

        if (attackCdTimer > 0f)   // 쿨다운: 사거리 언저리 유지
        {
            SetMove(d > attackRange * 0.9f ? dir * moveSpeed : 0f);
            return;
        }

        // 거리·페이즈 기반 패턴 선택(직전 패턴은 1회 리롤 — 같은 것 연속 방지)
        if (d > attackRange * 2.2f)
            BeginPattern(PickRanged());
        else if (d <= attackRange * 1.2f && DyToPlayer() <= attackHeight)
            BeginPattern(PickMelee());
        else if (LedgeAhead()) SetMove(0);      // ★낭떠러지 가드 — 구멍으로 걸어 나가지 않음(원거리 패턴으로 상대)
        else SetMove(dir * moveSpeed);          // 접근
    }

    // 진행 방향 앞 발밑에 지면이 없으면 true(추락 방지)
    private bool LedgeAhead()
    {
        Vector2 probe = (Vector2)transform.position + new Vector2(dir * 1.1f, 0f);
        return Physics2D.Raycast(probe, Vector2.down, 3.5f, wallMask).collider == null;
    }

    private Pattern PickRanged()
    {
        Pattern p = Roll(PickRangedOnce());
        return p;
    }
    private Pattern PickRangedOnce()
    {
        float r = Random.value;
        if (phase2 && r < 0.30f) return Pattern.QuakeWave;   // P2: 지면 파동 추가
        return r < 0.6f ? Pattern.Charge : Pattern.Volley;
    }

    private Pattern PickMelee()
    {
        return Roll(PickMeleeOnce());
    }
    private Pattern PickMeleeOnce()
    {
        float r = Random.value;
        if (phase2)
        {
            if (r < 0.24f) return Pattern.RedSlash;
            if (r < 0.44f) return Pattern.LeapSlam;
            if (r < 0.58f) return Pattern.QuakeWave;
            return Pattern.Combo3;
        }
        return r < 0.75f ? Pattern.Combo3 : Pattern.Charge;   // P1 근접에도 가끔 돌진(거리 흔들기)
    }

    // 직전과 같은 패턴이면 한 번 다시 굴림(그래도 같으면 허용 — 무한루프 방지)
    private Pattern Roll(Pattern p)
    {
        if (p != lastPattern) return p;
        return DistToPlayer() > attackRange * 2.2f ? PickRangedOnce() : PickMeleeOnce();
    }

    private void BeginPattern(Pattern p)
    {
        if (AttackHold) return;   // 대사·컷씬 중 봉인(베이스 규칙 준수)
        cur = p;
        lastPattern = p;
        step = 0;
        struck = false;
        heavyPaused = false;
        if (p == Pattern.Combo3)
        {
            comboMax = (phase2 && Random.value < 0.5f) ? 4 : 3;     // P2는 4연격 섞임
            comboHeavy = Random.value < 0.35f;                       // 마지막 타 강타 변형(멈칫 미끼)
        }
        SetMove(0);
        state = State.Windup;
        stateTimer = WindupOf(p);
        if (p != Pattern.Roar) WindupStarted?.Invoke(this);   // 예지(쉬움)·튜토 훅
        if (p == Pattern.Roar)
        {
            phase2 = true;
            attackCooldown *= 0.72f;   // 고속화
            moveSpeed *= 1.25f;
            Juice.Shake(0.5f, 0.6f);
            AudioManager.Sfx("boss_roar");
        }
    }

    private float WindupOf(Pattern p)
    {
        switch (p)
        {
            case Pattern.Combo3:   return attackWindup;          // 0.55 권장
            case Pattern.Charge:   return 0.7f;
            case Pattern.Volley:   return 0.6f;
            case Pattern.LeapSlam: return 0.5f;
            case Pattern.RedSlash: return redWindup;
            case Pattern.Roar:     return 1.4f;
            case Pattern.QuakeWave: return 0.7f;   // 웅크렸다가 내려찍음
        }
        return 0.5f;
    }

    // ══════════ 예비동작 ══════════
    protected override void TickWindup()
    {
        SetMove(0);
        // 붉은 강공격 텔레그래프: 붉은 글로우 고동
        if (redGlow != null)
        {
            bool red = cur == Pattern.RedSlash;
            redGlow.enabled = red;
            if (red) redGlow.color = new Color(1f, 0.15f, 0.1f, 0.45f + 0.35f * Mathf.Sin(Time.time * 26f));
        }
        if (cur == Pattern.Charge && stateTimer > 0.25f) SetMove(-dir * moveSpeed * 1.4f);   // 백스텝(돌진 조짐)

        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;

        // 예비동작 종료 → 패턴별 타격 개시
        state = State.Strike;
        switch (cur)
        {
            case Pattern.Combo3:   stateTimer = 0.01f; break;                            // 첫 타 즉시
            case Pattern.Charge:
                chargeDir = new Vector2(player != null && player.position.x >= transform.position.x ? 1f : -1f, 0f);
                dir = (int)chargeDir.x;
                chargeStartX = transform.position.x;
                stateTimer = 1.0f; struck = false; break;
            case Pattern.Volley:   stateTimer = 0.01f; break;
            case Pattern.LeapSlam:
                if (player != null && rb != null)
                {
                    float vx = (player.position.x - transform.position.x) / leapTime;
                    rb.linearVelocity = new Vector2(vx, 5.5f + leapTime * 9.81f * 0.5f);   // 포물선으로 플레이어 머리 위까지
                }
                stateTimer = leapTime + 1.2f;   // 안전 타임아웃
                break;
            case Pattern.RedSlash: stateTimer = 0.55f; struck = false; break;
            case Pattern.Roar:     ToRecover(0.4f); break;
            case Pattern.QuakeWave:   // 내려찍기 즉시 + 파동 시작
                Juice.Shake(0.35f, 0.25f);
                AudioManager.Sfx("door_slam");
                stateTimer = 0.22f; step = 0;
                break;
        }
    }

    // ══════════ 타격 ══════════
    protected override void TickStrike()
    {
        switch (cur)
        {
            case Pattern.Combo3:   // 연격(3~4타, 간격 흔들림) — 마지막 타는 가끔 '멈칫 후 강타'(패링 미끼)
                SetMove(0);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    bool last = step == comboMax - 1;
                    if (last && comboHeavy && !heavyPaused)
                    {
                        heavyPaused = true;             // 멈칫 — 여기서 패링을 지르면 늦거나 빠름
                        stateTimer = 0.5f;
                        if (redGlow != null) { redGlow.enabled = true; redGlow.color = new Color(1f, 0.8f, 0.2f, 0.4f); }   // 노란 기 — 강타 예고
                        break;
                    }
                    if (redGlow != null && cur == Pattern.Combo3) redGlow.enabled = false;
                    float dmg = (last && comboHeavy) ? attackDamage * 1.6f : attackDamage;
                    float rx = (last && comboHeavy) ? attackRange + 1.2f : attackRange + 0.5f;
                    if (last && comboHeavy) { Juice.Shake(0.25f, 0.18f); }
                    MeleeHit(dmg, false, rx, attackHeight);
                    if (state != State.Strike) return;   // 패링 휘청/그로기로 끊김
                    step++;
                    if (step >= comboMax) ToRecover(attackRecover);
                    else stateTimer = comboGap * Random.Range(0.85f, 1.2f);   // 리듬 흔들기
                }
                break;

            case Pattern.Charge:   // 직선 돌진(접촉 1회 피해, 패링 가능)
                SetMove(chargeDir.x * chargeSpeed);
                if (!struck && player != null && DistToPlayer() <= 1.2f && DyToPlayer() <= attackHeight)
                {
                    struck = true;
                    MeleeHit(attackDamage, false, 1.4f, attackHeight);
                    if (state != State.Strike) return;
                }
                if (Physics2D.Raycast(transform.position, chargeDir, chargeSpeed * Time.deltaTime + 0.6f, wallMask).collider != null
                    || Mathf.Abs(transform.position.x - chargeStartX) > 13f)
                { SetMove(0); ToRecover(attackRecover * 1.3f); return; }   // 벽/한계 → 살짝 긴 후딜(반격 창)
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    // P2 연계: 돌진이 플레이어 근처에서 끝나면 절반 확률로 즉시 연격으로 이어감(호흡 끊기)
                    if (phase2 && player != null && DistToPlayer() <= attackRange * 1.3f && Random.value < 0.5f)
                    { BeginPattern(Pattern.Combo3); return; }
                    ToRecover(attackRecover);
                }
                break;

            case Pattern.QuakeWave:   // 지면 파동 — 좌우로 3단 확산(점프 회피, 링마다 스파크)
                SetMove(0);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    step++;
                    float dist = step * 3.2f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector2 wp = (Vector2)transform.position + new Vector2(s * dist, 0.25f);
                        ParryFx.Spark(wp, false);   // 파동 이펙트(백은빛 스파크 재사용)
                        if (player != null
                            && Mathf.Abs(player.position.x - wp.x) <= 1.5f
                            && player.position.y - transform.position.y <= 1.3f)   // 점프 중이면 회피
                        {
                            var pcw = player.GetComponent<PlayerController>();
                            if (pcw != null) pcw.TakeDamage(attackDamage * 0.75f, true, this, wp);
                        }
                    }
                    Juice.Shake(0.12f, 0.1f);
                    if (step >= 3) ToRecover(attackRecover * 1.1f);
                    else stateTimer = 0.22f;
                }
                break;

            case Pattern.Volley:   // 투사체 3연발(반사 기회)
                SetMove(0);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    FireProjectile();
                    step++;
                    if (step >= 3) ToRecover(attackRecover);
                    else stateTimer = 0.28f;
                }
                break;

            case Pattern.LeapSlam: // 도약 → 착지 충격파(점프로 회피)
                stateTimer -= Time.deltaTime;
                bool falling = rb != null && rb.linearVelocity.y <= 0.05f;
                bool grounded = Physics2D.Raycast(transform.position, Vector2.down, 1.0f, wallMask).collider != null;
                if ((falling && grounded) || stateTimer <= 0f)
                {
                    SetMove(0);
                    Juice.Shake(0.4f, 0.3f);
                    AudioManager.Sfx("door_slam");
                    MeleeHit(attackDamage, false, slamWidth, 1.2f);   // 넓고 낮은 충격파 — 점프하면 회피
                    ToRecover(attackRecover * 1.2f);
                }
                break;

            case Pattern.RedSlash: // ★붉은 강공격 — 돌진 베기, 패링·가드 불가(대시 무적으로만 회피)
                if (redGlow != null) redGlow.color = new Color(1f, 0.1f, 0.05f, 0.75f);
                if (stateTimer > 0.33f) SetMove(dir * chargeSpeed * 0.85f);   // 짧은 쇄도
                else
                {
                    SetMove(0);
                    if (!struck)
                    {
                        struck = true;
                        MeleeHit(redDamage, true, attackRange + 1.2f, attackHeight + 0.4f);   // unblockable
                    }
                }
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f) { if (redGlow != null) redGlow.enabled = false; ToRecover(attackRecover * 1.5f); }   // 후딜 김(회피 보상)
                break;

            default: ToRecover(attackRecover); break;
        }
    }

    protected override void TickRecover()
    {
        if (redGlow != null) redGlow.enabled = false;
        base.TickRecover();
    }

    private void ToRecover(float t) { state = State.Recover; stateTimer = t; SetMove(0); }

    // 근접 판정(베이스 DoStrikeHit의 커스텀 버전 — 범위·불가 여부 지정)
    private void MeleeHit(float dmg, bool unblockable, float rangeX, float rangeY)
    {
        if (player == null) return;
        if (Mathf.Abs(player.position.x - transform.position.x) > rangeX) return;
        if (Mathf.Abs(player.position.y - transform.position.y) > rangeY) return;
        var pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.TakeDamage(dmg, true, this, transform.position, false, unblockable);
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null || player == null) return;
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        var go = Instantiate(projectilePrefab, origin, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj != null) proj.Init((Vector2)(player.position - origin), projectileSpeed, attackDamage * 0.5f, transform);
        AudioManager.Sfx("boss_shot", 0.9f, 0.06f);
    }

    // ══════════ 그로기(저스트 패링 누적) ══════════
    public override void ApplyGroggy()
    {
        if (state == State.Dead) return;
        stagger++;
        if (stagger >= staggerNeed)
        {
            stagger = 0;
            DamagePopup.Show(transform.position + Vector3.up * 1.6f, "그로기!", new Color(1f, 0.85f, 0.45f));
            base.ApplyGroggy();   // 진짜 그로기(치명타 창)
        }
        else
        {
            // 휘청: 진행 중 공격 취소 + 짧은 경직 (누적 표시)
            DamagePopup.Show(transform.position + Vector3.up * 1.6f, stagger + "/" + staggerNeed, new Color(1f, 0.85f, 0.45f));
            if (redGlow != null) redGlow.enabled = false;
            ToRecover(0.55f);
        }
    }

    // ══════════ 사망 연출 ══════════
    protected override void Die()
    {
        if (redGlow != null) redGlow.enabled = false;
        SlowMoFx.BeginTimed(0.18f, 1.3f);                       // 마지막 일격 슬로우
        Juice.Flash(new Color(1f, 0.92f, 0.75f, 0.5f), 0.45f);  // 크림빛 섬광
        Juice.Shake(0.6f, 0.5f);
        AudioManager.Sfx("boss_die");
        AudioManager.Bgm("stage", 2f);                          // 보스 BGM → 탐사 곡으로 복귀
        base.Die();   // 전리품 + questKillId("boss_first") 보고 → 메인퀘 완료
    }

    // 붉은 강공격 텔레그래프용 글로우(절차 생성 — 아트 오면 교체)
    private void BuildRedGlow()
    {
        const int N = 48;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r); a *= a;
                px[y * N + x] = new Color(1f, 0.25f, 0.15f, a);
            }
        tex.SetPixels(px); tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N * 0.28f);   // 크게

        var go = new GameObject("RedGlow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        redGlow = go.AddComponent<SpriteRenderer>();
        redGlow.sprite = sprite;
        redGlow.sortingOrder = 25;
        redGlow.enabled = false;
        var sh = Shader.Find("Legacy Shaders/Particles/Additive");
        if (sh != null) redGlow.material = new Material(sh);
    }
}
