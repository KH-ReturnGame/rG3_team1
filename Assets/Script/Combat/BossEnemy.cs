using System.Collections;
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
    public float leapTime = 0.65f;        // (안전 타임아웃용)
    public float leapUpSpeed = 15f;       // 빠르게 뛰어오르는 상승 속도
    public float leapHangTime = 0.55f;    // 정점에서 공중 체공하는 시간(이때 착지 지점 경고)
    public float leapDownSpeed = 26f;     // 빠르게 내리꽂는 하강 속도
    public float slamWidth = 3.4f;        // 내려찍기/충격파 폭(±)
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
    private float moveDashTimer;          // 접근 대쉬(멀면 확 붙음)
    private float moveDashCd;
    private float backDashTimer;          // 패턴 마무리 백대쉬(너무 가까우면 거리 벌림)
    private bool backDashed;              // 백대쉬 직후 → 원거리 패턴 우선
    private SpriteRenderer redBox;        // 패링 불가 공격 범위 경고(깜박이는 빨간 박스)
    private Vector2 chargeDir;
    private float chargeStartX;
    private float landX, groundY, bossGravity;   // 도약 착지 지점(공격·방사 기준) + 원래 중력
    private SpriteRenderer redGlow;       // 붉은 강공격 텔레그래프(자식 글로우)
    private int wallMask;
    private Animator anim;                // Hero_Knight 애니메이터(상태별 재생)
    private float attackAnimLen = 0.55f;  // Attack 클립 길이(Start에서 조회) — 연타가 애니를 끊지 않게 간격 계산
    private float hitDelay = 0.28f;       // 휘두름 시작 → 타격(정점) 시점
    private bool comboHit;                // 이번 콤보 타의 타격을 냈는지(휘두름/마무리 2단계)

    void OnEnable() { Active = this; }
    void OnDisable() { if (Active == this) Active = null; }

    protected override void Start()
    {
        randomizeStats = false;   // 보스는 수치 고정
        base.Start();
        wallMask = LayerMask.GetMask("Ground");
        bossGravity = rb != null ? rb.gravityScale : 1f;
        anim = GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
            foreach (var c in anim.runtimeAnimatorController.animationClips)
                if (c.name == "Boss_Attack1") attackAnimLen = c.length;
        hitDelay = attackAnimLen * 0.5f;   // 타격 = 휘두름 애니의 중간(정점)
        BuildRedGlow();
        BuildRedBox();
    }

    // 상태·패턴 → 애니메이션(anim int: 0=Idle 1=Run 2=Attack 3=Jump 4=Fall 5=Hurt 6=Death)
    void LateUpdate()
    {
        if (anim == null) return;
        int a;
        if (state == State.Dead) a = 6;
        else if (state == State.Groggy) a = 5;
        else if (cur == Pattern.LeapSlam && state == State.Strike)
            a = step == 0 ? 3 : (step == 2 ? 4 : 0);          // 상승=Jump, 급강하=Fall, 그 외=Idle
        else if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.3f) a = 1;        // 이동
        else a = 0;                                                                // 대기(공격 동작은 attack 트리거로 재생)
        anim.SetInteger("anim", a);
    }

    // 공격 휘두름 애니(1회) — 각 타격 시작 시 호출해 타격 시점과 맞춘다
    private void TrigAttack() { if (anim != null) anim.SetTrigger("attack"); }

    // 패링 불가 공격 범위 경고 박스(반투명 빨강, 예비동작 동안 깜박임)
    private void BuildRedBox()
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var px = new Color[4]; for (int i = 0; i < 4; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);   // 1×1 유닛

        var go = new GameObject("RedBox");
        go.transform.SetParent(transform, false);
        redBox = go.AddComponent<SpriteRenderer>();
        redBox.sprite = sprite;
        redBox.sortingOrder = 3;   // 지형 위, 캐릭터 아래
        redBox.enabled = false;
    }

    // 월드 고정 경고 박스: lead초 동안 그 자리에 빨간 박스를 깜박이다 사라진다(내려찍기/충격파 예고).
    private void SpawnWarnBox(Vector2 worldPos, float width, float height, float lead)
    {
        if (redBox == null) return;
        StartCoroutine(WarnRoutine(worldPos, width, height, lead));
    }
    private IEnumerator WarnRoutine(Vector2 pos, float w, float h, float lead)
    {
        var go = new GameObject("BossWarn");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = redBox.sprite;
        sr.sortingOrder = 3;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(w, h, 1f);
        float t = 0f;
        while (t < lead)
        {
            float k = t / lead;
            sr.color = new Color(1f, 0.12f, 0.08f, 0.10f + 0.20f * (0.5f + 0.5f * Mathf.Sin(t * 30f)) * (0.35f + 0.65f * k));   // 임박할수록 진하게 깜박
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(go);
    }

    // 충격파 한 링 터뜨림: 착지 지점(landX) 좌우 dist·바닥(groundY)에 스파크 + 낮은 판정(점프로 회피)
    private void QuakeRingHit(int ring)
    {
        float dist = ring * 3.2f;
        for (int s = -1; s <= 1; s += 2)
        {
            Vector2 wp = new Vector2(landX + s * dist, groundY + 0.25f);
            ParryFx.Spark(wp, false);
            if (player != null && Mathf.Abs(player.position.x - wp.x) <= 1.6f
                && player.position.y - groundY <= 1.3f)   // 점프 중이면 회피
            {
                var pcw = player.GetComponent<PlayerController>();
                if (pcw != null) pcw.TakeDamage(attackDamage * 0.75f, true, this, wp);
            }
        }
        Juice.Shake(0.12f, 0.1f);
    }
    // 충격파 한 링 예고: 터지기 직전 착지 지점 좌우 바닥에 빨간 경고 박스
    private void QuakeRingWarn(int ring, float lead)
    {
        float dist = ring * 3.2f;
        for (int s = -1; s <= 1; s += 2)
            SpawnWarnBox(new Vector2(landX + s * dist, groundY + 0.12f), 3.0f, 1.2f, lead);
    }

    // 지정 지점(착지 지점 등) 기준 근접 판정 — 보스 위치가 아니라 공격 중심을 명시
    private void MeleeHitAt(float cx, float cy, float dmg, float rangeX, float rangeY)
    {
        if (player == null) return;
        if (Mathf.Abs(player.position.x - cx) > rangeX) return;
        if (Mathf.Abs(player.position.y - cy) > rangeY) return;
        var pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.TakeDamage(dmg, true, this, new Vector2(cx, cy));
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
        if (moveDashCd > 0f) moveDashCd -= Time.deltaTime;

        // 접근 대쉬 진행 중: 확 붙는다(유동성)
        if (moveDashTimer > 0f)
        {
            moveDashTimer -= Time.deltaTime;
            if (!LedgeAhead()) SetMove(dir * chargeSpeed * 0.9f); else { moveDashTimer = 0f; SetMove(0); }
            return;
        }

        if (attackCdTimer > 0f)   // 쿨다운: 사거리 언저리 유지
        {
            SetMove(d > attackRange * 0.9f && !LedgeAhead() ? dir * moveSpeed : 0f);
            return;
        }

        // 백대쉬 직후: 벌린 거리에서 원거리 패턴으로 응수
        if (backDashed) { backDashed = false; BeginPattern(PickRangedOnce()); return; }

        // 거리·페이즈 기반 패턴 선택(직전 패턴은 1회 리롤 — 같은 것 연속 방지)
        if (d > attackRange * 2.2f)
        {
            // 멀면 적극적으로 대쉬로 확 붙어 근접전(이동을 능동적으로 — 단조로운 걷기 접근 탈피)
            if (d > 4.5f && moveDashCd <= 0f && Random.value < 0.85f)
            { moveDashTimer = 0.3f; moveDashCd = 1.4f; AudioManager.Sfx("dash", 0.9f, 0.06f); return; }
            BeginPattern(PickRanged());
        }
        else if (d <= attackRange * 1.2f && DyToPlayer() <= attackHeight)
            BeginPattern(PickMelee());
        else if (LedgeAhead()) SetMove(0);      // ★낭떠러지 가드 — 구멍으로 걸어 나가지 않음(원거리 패턴으로 상대)
        else SetMove(dir * moveSpeed * 1.35f);  // 중거리도 빠르게 접근(이동 능동적)
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
        return r < 0.3f ? Pattern.Charge : Pattern.Volley;   // 돌진 비중↓ — 멀면 주로 볼리 견제(접근은 대쉬가 담당)
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
            if (r < 0.18f) return Pattern.RedSlash;
            if (r < 0.40f) return Pattern.LeapSlam;   // 내려찍기+충격파 통합 패턴(QuakeWave 흡수)
            return Pattern.Combo3;                     // 평타 비중↑(0.60)
        }
        return r < 0.88f ? Pattern.Combo3 : Pattern.Charge;   // P1 근접은 평타 위주(돌진은 가끔)
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
        if (p != Pattern.Roar) invuln = false;   // 다음 패턴이 시작되면 페이즈2 전환 무적 해제
        cur = p;
        lastPattern = p;
        step = 0;
        struck = false;
        heavyPaused = false;
        if (p == Pattern.Combo3)
        {
            comboMax = (phase2 && Random.value < 0.5f) ? 4 : 3;     // P2는 4연격 섞임
            comboHeavy = Random.value < 0.35f;                       // 마지막 타 강타 변형(멈칫 미끼)
            comboHit = false;
        }
        SetMove(0);
        state = State.Windup;
        stateTimer = WindupOf(p);
        if (p != Pattern.Roar) WindupStarted?.Invoke(this);   // 예지(쉬움)·튜토 훅
        if (p == Pattern.Roar)
        {
            phase2 = true;
            invuln = true;   // ★페이즈2 전환(포효) 동안 무적 — 다음 패턴이 시작될 때 풀린다
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
            case Pattern.Combo3:   return attackWindup;           // 0.45 권장
            case Pattern.Charge:   return 0.55f;                  // ★준비 단계 가속(스타일)
            case Pattern.Volley:   return 0.5f;
            case Pattern.LeapSlam: return 0.4f;
            case Pattern.RedSlash: return redWindup;               // 붉은 공격만 길게(읽기용)
            case Pattern.Roar:     return 1.4f;
            case Pattern.QuakeWave: return 0.6f;                   // 웅크렸다가 내려찍음
        }
        return 0.45f;
    }

    // ══════════ 예비동작 ══════════
    protected override void TickWindup()
    {
        SetMove(0);
        // 붉은 강공격 텔레그래프: 붉은 글로우 고동 + ★공격 범위 깜박이는 빨간 박스
        if (redGlow != null)
        {
            bool red = cur == Pattern.RedSlash;
            redGlow.enabled = red;
            if (red) redGlow.color = new Color(1f, 0.15f, 0.1f, 0.45f + 0.35f * Mathf.Sin(Time.time * 26f));
        }
        if (redBox != null)
        {
            bool red = cur == Pattern.RedSlash;
            redBox.enabled = red;
            if (red)
            {
                // 쇄도(≈2.5) + 베기 반경을 덮는 전방 박스
                float reach = 2.5f + attackRange + 1.2f;
                redBox.transform.localScale = new Vector3(reach / Mathf.Abs(transform.lossyScale.x), (attackHeight + 0.4f) * 2f / Mathf.Abs(transform.lossyScale.y), 1f);
                redBox.transform.localPosition = new Vector3(dir * reach * 0.5f / transform.lossyScale.x, 0f, 0f);
                redBox.color = new Color(1f, 0.1f, 0.08f, 0.14f + 0.14f * Mathf.Sin(Time.time * 22f));   // 깜박임
            }
        }
        if (cur == Pattern.Charge && stateTimer > 0.25f) SetMove(-dir * moveSpeed * 1.4f);   // 백스텝(돌진 조짐)

        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;

        // 예비동작 종료 → 패턴별 타격 개시
        state = State.Strike;
        switch (cur)
        {
            case Pattern.Combo3:   stateTimer = hitDelay; TrigAttack(); break;          // 첫 타 — 휘두름 시작 → 정점(hitDelay)에서 타격
            case Pattern.Charge:
                chargeDir = new Vector2(player != null && player.position.x >= transform.position.x ? 1f : -1f, 0f);
                dir = (int)chargeDir.x;
                chargeStartX = transform.position.x;
                stateTimer = 1.0f; struck = false; break;
            case Pattern.Volley:   stateTimer = 0.01f; break;
            case Pattern.LeapSlam:
                if (rb != null) rb.linearVelocity = new Vector2(0f, leapUpSpeed);   // ★빠르게 뛰어오름(수직)
                groundY = transform.position.y;   // 도약 시작 = 착지할 지면 레벨(경고·공격·방사의 바닥 기준)
                stateTimer = 4f;   // 안전 타임아웃
                step = 0;          // 0=상승 1=체공 2=급강하 3~5=방사 링
                break;
            case Pattern.RedSlash: stateTimer = 0.55f; struck = false; TrigAttack(); if (redBox != null) redBox.enabled = false; break;
            case Pattern.Roar:     ToRecover(0.4f); break;
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
                    if (!comboHit)   // ── 휘두름 정점 → 타격 ──
                    {
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
                        comboHit = true;
                        stateTimer = Mathf.Max(0.05f, attackAnimLen - hitDelay);   // ★애니 마무리(완주)까지 대기 — 끊김 없이
                    }
                    else   // ── 애니 마무리 끝 → 다음 타 ──
                    {
                        comboHit = false;
                        step++;
                        if (step >= comboMax) ToRecover(attackRecover);
                        else { stateTimer = hitDelay; TrigAttack(); }   // 다음 타: 휘두름 시작 → 정점에서 타격
                    }
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

            case Pattern.Volley:   // 약한 3연발 → 기 모으기 → 큰 것 한 방(전부 반사 기회)
                SetMove(0);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    if (step < 3)
                    {
                        FireProjectile(1f, 1f, projectileSpeed);
                        step++;
                        if (step == 3)
                        {
                            stateTimer = 0.6f;   // 기 모으기(노란 기운)
                            if (redGlow != null) { redGlow.enabled = true; redGlow.color = new Color(1f, 0.75f, 0.2f, 0.5f); }
                        }
                        else stateTimer = 0.26f;
                    }
                    else
                    {
                        if (redGlow != null) redGlow.enabled = false;
                        FireProjectile(2.2f, 1.9f, projectileSpeed * 1.25f);   // 큰 것 — 반사하면 그만큼 아프다
                        Juice.Shake(0.2f, 0.15f);
                        ToRecover(attackRecover);
                    }
                }
                break;

            case Pattern.LeapSlam: // ★빠른 상승 → 체공(착지 지점 경고) → 급강하 → 착지 지점 강타 → 방사 폭발 3링
                stateTimer -= Time.deltaTime;
                SetMove(0);
                if (step == 0)   // 빠르게 상승 → 정점에서 체공 전환
                {
                    if ((rb != null && rb.linearVelocity.y <= 0.5f) || stateTimer <= 2.6f)
                    {
                        landX = player != null ? player.position.x : transform.position.x;   // 착지 지점 = 지금 플레이어 위치
                        if (rb != null) { rb.gravityScale = 0f; rb.linearVelocity = Vector2.zero; }   // 공중 체공(중력 off)
                        SpawnWarnBox(new Vector2(landX, groundY + 0.12f), slamWidth * 2f, 1.3f, leapHangTime + 0.15f);   // ★바닥 기준 착지 경고
                        AudioManager.Sfx("boss_shot", 0.7f, 0.1f);
                        step = 1;
                        stateTimer = leapHangTime;
                    }
                }
                else if (step == 1)   // 공중 체공 — 착지 지점 위로 서서히 정렬
                {
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                    float nx = Mathf.MoveTowards(transform.position.x, landX, 7f * Time.deltaTime);
                    transform.position = new Vector3(nx, transform.position.y, transform.position.z);
                    if (stateTimer <= 0f)   // 체공 끝 → 급강하
                    {
                        if (rb != null) { rb.gravityScale = bossGravity; rb.linearVelocity = new Vector2(0f, -leapDownSpeed); }   // ★빠르게 내리꽂음
                        step = 2;
                        stateTimer = 1.5f;
                    }
                }
                else if (step == 2)   // 급강하 → 착지
                {
                    bool grounded = Physics2D.Raycast(transform.position, Vector2.down, 1.0f, wallMask).collider != null;
                    if (grounded || transform.position.y <= groundY + 0.05f || stateTimer <= 0f)
                    {
                        transform.position = new Vector3(landX, groundY, transform.position.z);   // 착지 지점 고정
                        if (rb != null) rb.linearVelocity = Vector2.zero;
                        Juice.Shake(0.55f, 0.35f);
                        AudioManager.Sfx("door_slam");
                        MeleeHitAt(landX, groundY, attackDamage, slamWidth, 1.5f);   // ★착지 지점 주변 강타
                        TrigAttack();
                        step = 3;
                        stateTimer = 0.3f;
                        QuakeRingWarn(1, 0.3f);   // 방사 첫 링 경고(착지 지점 기준)
                    }
                }
                else   // 방사 폭발(착지 지점 기준, 좌우 3링)
                {
                    if (stateTimer <= 0f)
                    {
                        int ring = step - 2;   // step3→링1, 4→링2, 5→링3
                        QuakeRingHit(ring);
                        if (ring >= 3) ToRecover(attackRecover * 1.2f);
                        else { step++; stateTimer = 0.3f; QuakeRingWarn(ring + 1, 0.3f); }
                    }
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
        if (redBox != null) redBox.enabled = false;
        // 백대쉬: 마무리 단계에 플레이어가 너무 가까우면 뒤로 미끄러지며 거리 벌림(스타일+원거리 패턴 셋업)
        if (backDashTimer > 0f)
        {
            backDashTimer -= Time.deltaTime;
            int back = -dir;
            Vector2 probe = (Vector2)transform.position + new Vector2(back * 1.1f, 0f);
            bool ledge = Physics2D.Raycast(probe, Vector2.down, 3.5f, wallMask).collider == null;
            SetMove(ledge ? 0f : back * chargeSpeed * 0.85f);
        }
        else SetMove(0);
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f) { attackCdTimer = attackCooldown; state = State.Chase; }
    }

    // 마무리 진입: 준비/마무리를 짧게(스타일리쉬) + 근접 시 백대쉬 셋업
    private void ToRecover(float t)
    {
        state = State.Recover;
        stateTimer = t * 0.75f;   // ★마무리 단계 가속 — 보스다운 절도
        SetMove(0);
        if (rb != null) rb.gravityScale = bossGravity;   // 도약 체공 중 종료 시 중력 복구(공중 고착 방지)
        if (player != null && DistToPlayer() < 1.8f && Random.value < 0.45f)
        { backDashTimer = 0.26f; backDashed = true; AudioManager.Sfx("dash", 0.8f, 0.06f); }
    }

    // 근접 판정(베이스 DoStrikeHit의 커스텀 버전 — 범위·불가 여부 지정)
    private void MeleeHit(float dmg, bool unblockable, float rangeX, float rangeY)
    {
        if (player == null) return;
        if (Mathf.Abs(player.position.x - transform.position.x) > rangeX) return;
        if (Mathf.Abs(player.position.y - transform.position.y) > rangeY) return;
        var pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.TakeDamage(dmg, true, this, transform.position, false, unblockable);
    }

    private void FireProjectile(float dmgMult, float sizeMult, float speed)
    {
        if (projectilePrefab == null || player == null) return;
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        var go = Instantiate(projectilePrefab, origin, Quaternion.identity);
        go.transform.localScale *= sizeMult;
        var proj = go.GetComponent<Projectile>();
        if (proj != null) proj.Init((Vector2)(player.position - origin), speed, attackDamage * 0.5f * dmgMult, transform);
        AudioManager.Sfx("boss_shot", 0.9f, 0.06f);
        TrigAttack();   // 던지는 휘두름 애니
    }

    // ══════════ 그로기(저스트 패링 누적) ══════════
    public override void ApplyGroggy()
    {
        if (state == State.Dead) return;
        if (rb != null) rb.gravityScale = bossGravity;   // 도약 체공 중 패링당하면 중력 복구
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
