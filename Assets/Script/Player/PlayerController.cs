using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // ───────────────────────── 컴포넌트 ─────────────────────────
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;
    private float defaultGravityScale;

    [Header("Movement")]
    public float moveSpeed = 5f;
    [Range(0f, 1f)] public float guardSpeedMultiplier = 0.2f; // 가드 중 이동 속도(원래의 20%)
    private float horizontalInput;
    private int facingDir = 1;            // 1 = 오른쪽, -1 = 왼쪽

    [Header("Jump")]
    public float jumpForce = 10f;
    public int maxJumps = 2;       // 2 = 2단 점프 (아이템으로 더 늘릴 수 있음)
    public string doubleJumpState = "FrontFlip";   // 첫 점프 제외, 공중 점프 시 재생할 애니
    private int currentJumps;

    [Header("Charge Jump (스페이스 길게 → 높은 점프)")]
    public float chargeJumpTime = 1.5f;            // 이 시간 이상 누르면 높은 점프
    public float tapJumpTime = 0.1f;               // 이 시간 안에 떼면 준비자세 없이 바로 일반 점프
    public float chargeJumpForce = 16f;            // 높은 점프 힘(일반 Jump Force보다 크게)
    public string chargeState = "Crouch";          // 차지 중 웅크리는 모션
    private bool isChargingJump;
    private float jumpHoldTimer;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    private bool isGrounded;

    [Header("Dash")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public int maxDashes = 1;
    private int currentDashes;
    private bool isDashing;
    private float dashTimer;
    private float dashDir;

    [Header("Guard & Parry")]
    public float parryWindow = 0.5f;
    private float parryTimer;
    private bool isGuarding;
    private bool isParrying;

    [Header("기력 소모/회복 (GameManager 스탯 사용)")]
    public float dashStaminaCost = 20f;
    public float guardStaminaCost = 15f;    // 가드/패링 시도(클릭 순간) 즉시 소모 — 남발 방지 패널티
    public float guardStaminaDrain = 30f;   // 가드 중 초당 소모
    public float staminaRegen = 15f;        // 가드/대시 중이 아닐 때 초당 회복
    public float parryStaminaRecover = 30f; // 패링 성공 시 회복

    [Header("Hit Reaction (피격 넉백/경직)")]
    public string hitState = "HitDamage";
    public float hitstunDuration = 0.3f;    // 피격 경직(입력 잠금) 시간
    public float knockbackForce = 4f;       // 피격 시 뒤로 밀리는 힘(수평만)
    public float hitInvincibleTime = 1f;    // 피격 후 무적 시간
    public float blinkInterval = 0.08f;     // 무적 중 점멸 간격
    [Range(0f, 1f)] public float blinkMinAlpha = 0.3f;   // 점멸 시 가장 흐려질 때의 투명도
    private float hitstunTimer;
    private float hitInvincibleTimer;

    [Header("Combat - Attack (좌클릭)")]
    public Transform attackPoint;
    public Vector2 attackBoxSize = new Vector2(1.2f, 1.2f);
    public float attackDamage = 10f;
    public LayerMask enemyLayer;

    [Header("Combo (A→B→C→D)")]
    public string[] comboStates = { "ComboAttackA", "ComboAttackB", "ComboAttackC", "ComboAttackD" };
    public float comboFinisherMultiplier = 2f;   // 마지막 타(D) 데미지 배수
    public float comboContinueBuffer = 0.35f;    // 스윙 끝난 뒤 다음 콤보로 이어갈 수 있는 여유 시간
    public float attackInputBuffer = 0.25f;      // 스윙 중 미리 누른 입력을 기억하는 시간
    private int comboStep;
    private float comboResetTimer;
    private float attackBufferTimer;

    [Header("Combat - Skill (Q / 횡베기)")]
    public float skillDamageMultiplier = 2f;
    public float skillRangeMultiplier = 2f;
    public float skillCooldown = 5f;
    private float skillCooldownTimer;

    [Header("Air / Plunge (공중 공격 / 낙하 공격)")]
    public string airAttackState = "AirSlash";
    public string airDownAttackState = "AirSlashDown";      // 공중 아래 베기(1타)
    public string plungeFallState = "AirSlashDown";         // 낙하(다이브) 자세
    public string plungeLandState = "GroundSlam";
    [Range(0f, 1f)] public float plungeLandStartTime = 0f;   // GroundSlam을 이 지점부터 재생(앞 준비동작 건너뛰기, 0~1)
    public float airAttackDamage = 10f;
    public float plungeDamage = 20f;
    public float plungeSpeed = 16f;                          // 낙하 공격 수직 하강 속도
    public Vector2 plungeAoeSize = new Vector2(3f, 1.5f);    // 착지 충격 범위(플레이어 중심)
    public float plungeAoeYOffset = -0.3f;
    public float plungeArmBuffer = 0.15f;                    // 아래 베기 후 낙하공격 입력을 받는 추가 여유
    public float airDownHoverTime = 0.25f;                   // 공중 아래 베기 시 체공 시간
    private bool isPlunging;
    private float plungeArmTimer;                            // >0이면 한 번 더 누를 때 낙하 공격
    private float hoverTimer;                                // >0이면 잠깐 체공(아래 베기 중)

    [Header("발도 / 납도 (Sheathe / Draw)")]
    public KeyCode sheatheKey = KeyCode.R;     // 검 뽑기/넣기 토글 키
    public bool startDrawn = true;             // 시작 시 검을 든 상태로?
    private bool isSwordDrawn;

    [Header("Animation 상태 이름 (컨트롤러의 상태명과 대소문자까지 정확히 일치)")]
    public string skillState = "SwordStandingSlash";
    public string dashState = "Dash";
    public string drawFlourishState = "SwordStandingSlash"; // 발도 흉내(검 뽑을 때)
    public string sheatheFlourishState = "Spin";            // 납도 흉내(검 넣을 때)

    [Header("이동/대기 애니 (납도 / 발도)")]
    public string idleState = "Idle";
    public string moveState = "Run";
    public string jumpRiseState = "JumpRise";
    public string jumpFallState = "JumpFall";
    public string swordIdleState = "SwordIdle";
    public string swordMoveState = "SwordWalk";          // 발도 상태 이동(요청대로 Walk)
    public string swordJumpRiseState = "SwordJumpRise";
    public string swordJumpFallState = "SwordJumpFall";
    public string guardState = "SwordGuard";
    public string parrySuccessState = "SwordStandingSlash";   // 패링 성공 시 반격 베기(리포스트). 인스펙터에서 교체 가능

    private string currentAnimState = "";
    private float animBusyTimer;   // >0이면 1회성 모션(공격/발도 등) 재생 중 → 이동 애니로 안 바뀌고 새 행동도 잠금
    private float animHoldTimer;    // >0이면 "애니만" 보호(입력은 막지 않음) — 2단 점프 플립 등
    private Dictionary<string, float> clipLengths;   // 상태(클립) 이름 → 실제 재생 길이(초)

    public static PlayerController Instance;          // 현재 씬의 플레이어(장비 적용용)
    private int baseMaxJumps;                         // 장신구 보너스 전 기본값
    private float baseStaminaRegen;

    [Header("낙사")]
    public float fallMargin = 6f;                     // 카메라 경계 바닥보다 이만큼 더 아래로 떨어지면 낙사
    public int fallDamage = 1;                        // 낙사 패널티(하트). HP 0되면 정상 사망 처리
    private Vector3 lastSafePos;

    void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        defaultGravityScale = rb.gravityScale;
        baseMaxJumps = maxJumps;
        baseStaminaRegen = staminaRegen;
        BuildClipLengthTable();
    }

    // 컨트롤러의 모든 클립 이름→길이를 미리 저장 (1회성 모션을 "딱 그 길이만큼만" 보호하기 위함)
    private void BuildClipLengthTable()
    {
        clipLengths = new Dictionary<string, float>();
        if (anim == null || anim.runtimeAnimatorController == null) return;
        foreach (AnimationClip c in anim.runtimeAnimatorController.animationClips)
            if (c != null) clipLengths[c.name] = c.length;
    }

    // 상태 이름으로 클립 길이를 얻는다. 없으면 fallback.
    private float ClipLength(string stateName, float fallback = 0.3f)
    {
        if (clipLengths != null && clipLengths.TryGetValue(stateName, out float len) && len > 0f)
            return len;
        return fallback;
    }

    void Start()
    {
        ApplyEquipment();
        lastSafePos = transform.position;
        currentJumps = maxJumps;
        currentDashes = maxDashes;
        isSwordDrawn = startDrawn;
        PlayStateForced(isSwordDrawn ? swordIdleState : idleState);
    }

    // 장신구 보너스 반영(점프 횟수·기력 회복). 장착 변경 시 Equipment가 호출.
    public void ApplyEquipment()
    {
        int jb = Equipment.Instance != null ? Equipment.Instance.MaxJumpBonus : 0;
        float rgb = Equipment.Instance != null ? Equipment.Instance.StaminaRegenBonus : 0f;
        maxJumps = baseMaxJumps + jb;
        staminaRegen = baseStaminaRegen + rgb;
        if (currentJumps > maxJumps) currentJumps = maxJumps;
    }

    // 낙사: 바닥에 서 있으면 안전지점 갱신, 카메라 경계 아래로 떨어지면 안전지점 복귀 + 패널티
    private void CheckFall()
    {
        if (isGrounded && rb != null && Mathf.Abs(rb.linearVelocity.y) < 0.6f) lastSafePos = transform.position;
        float killY = (CameraFollow.Instance != null && CameraFollow.Instance.HasBounds)
            ? CameraFollow.Instance.BoundsBottom - fallMargin : -50f;
        if (transform.position.y < killY)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            transform.position = lastSafePos;
            if (GameManager.Instance != null) GameManager.Instance.TakeDamage(fallDamage);
        }
    }

    void Update()
    {
        if (hitInvincibleTimer > 0)
        {
            hitInvincibleTimer -= Time.deltaTime;
            bool dim = ((int)(hitInvincibleTimer / blinkInterval) % 2) == 0;
            SetAlpha(dim ? blinkMinAlpha : 1f);                 // 반투명 ↔ 불투명 점멸
            if (hitInvincibleTimer <= 0) SetAlpha(1f);          // 무적 끝 → 완전 불투명
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0) EndDash();
            return;
        }

        if (hitstunTimer > 0)
        {
            hitstunTimer -= Time.deltaTime;
            return;   // 경직: 입력/행동 무시 (피격 모션·넉백 유지)
        }

        CheckInput();
        CheckGrounded();

        if (isPlunging && isGrounded) PlungeLand();   // 낙하 공격 착지

        CheckFall();   // 낙사 판정 + 안전지점 갱신

        if (isParrying)
        {
            parryTimer -= Time.deltaTime;
            if (parryTimer <= 0) isParrying = false;
        }

        if (skillCooldownTimer > 0) skillCooldownTimer -= Time.deltaTime;
        if (animBusyTimer > 0) animBusyTimer -= Time.deltaTime;
        if (animHoldTimer > 0) animHoldTimer -= Time.deltaTime;
        if (hoverTimer > 0) hoverTimer -= Time.deltaTime;
        if (attackBufferTimer > 0) attackBufferTimer -= Time.deltaTime;
        if (comboResetTimer > 0)
        {
            comboResetTimer -= Time.deltaTime;
            if (comboResetTimer <= 0) comboStep = 0;   // 시간 초과 → 콤보 처음으로
        }
        if (plungeArmTimer > 0) plungeArmTimer -= Time.deltaTime;
        if (isGrounded) plungeArmTimer = 0;            // 착지하면 낙하공격 입력창 닫힘

        // 기력: 가드 중 소모 / 그 외 회복 (대시 중에는 이 블록에 도달 안 함 — 위에서 return)
        if (GameManager.Instance != null)
        {
            if (isGuarding)
            {
                GameManager.Instance.ChangeStamina(-guardStaminaDrain * Time.deltaTime);
                if (GameManager.Instance.CurrentStamina <= 0f) EndGuard();   // 기력 고갈 시 가드 해제
            }
            else
            {
                GameManager.Instance.ChangeStamina(staminaRegen * Time.deltaTime);
            }
        }

        // 스윙 중 미리 눌러둔 공격: 스윙이 끝나는 즉시 다음 콤보로 연결
        if (attackBufferTimer > 0 && animBusyTimer <= 0 && isSwordDrawn && !isGuarding && !isDashing && isGrounded)
        {
            attackBufferTimer = 0;
            DoComboAttack();
        }

        UpdateAnimations();
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
            return;
        }
        if (hitstunTimer > 0) return;   // 경직 중엔 이동 제어 안 함(넉백 속도 유지)
        if (isPlunging)
        {
            rb.linearVelocity = new Vector2(0f, -plungeSpeed);   // 똑바로 빠르게 하강
            return;
        }
        if (hoverTimer > 0)
        {
            rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, 0f);   // 아래 베기 중 잠깐 체공
            return;
        }
        Move();
    }

    private void CheckInput()
    {
        if (isPlunging) return;   // 낙하 공격 중엔 입력 무시(착지까지 커밋)
        if (Inventory.IsUIOpen) { horizontalInput = 0f; return; }   // 인벤토리/메뉴 열려있으면 조작 잠금

        horizontalInput = Input.GetAxisRaw("Horizontal");

        // 발도/납도 토글
        if (Input.GetKeyDown(sheatheKey) && animBusyTimer <= 0)
            ToggleSheathe();

        // 가드/패링 (검을 들었을 때만)
        if (Input.GetMouseButtonDown(1) && isSwordDrawn && animBusyTimer <= 0 && !isChargingJump
            && (GameManager.Instance == null || GameManager.Instance.CurrentStamina >= guardStaminaCost))
            StartGuard();
        if (Input.GetMouseButtonUp(1)) EndGuard();

        // 좌클릭: 지상=콤보 / 공중=공중 공격 (아래키 같이 누르면 낙하 공격)
        if (Input.GetMouseButtonDown(0) && isSwordDrawn && !isGuarding && !isDashing && !isChargingJump)
        {
            if (isGrounded)
            {
                if (animBusyTimer <= 0) DoComboAttack();
                else attackBufferTimer = attackInputBuffer;   // 스윙 중이면 버퍼에 저장
            }
            else   // 공중
            {
                if (plungeArmTimer > 0)
                {
                    StartPlunge();   // 아래 베기 모션 중 한 번 더 → 낙하 공격
                }
                else if (animBusyTimer <= 0)
                {
                    bool down = Input.GetKey(KeyCode.S) || Input.GetAxisRaw("Vertical") < -0.1f;
                    if (down) AirDownAttack();   // S + 좌클릭 → 아래 베기(1타)
                    else AirAttack();
                }
            }
        }

        // 스킬 Q (검을 들었을 때만)
        if (Input.GetKeyDown(KeyCode.Q) && isSwordDrawn && !isGuarding && animBusyTimer <= 0 && skillCooldownTimer <= 0 && !isChargingJump)
            UseSkill();

        // 점프: 스페이스 탭=누르는 즉시 일반 점프(반응 즉각) / 아래(S)+스페이스=차지 높은 점프 / 공중=즉시 2단 점프
        if (Input.GetKeyDown(KeyCode.Space) && currentJumps > 0 && !isGuarding && !isChargingJump)
        {
            bool wantCharge = isGrounded &&
                (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetAxisRaw("Vertical") < -0.1f);
            if (wantCharge)
            {
                isChargingJump = true;   // 차지 높은 점프 시작(뗄 때 발사)
                jumpHoldTimer = 0f;
                animBusyTimer = 0;       // 공격 캔슬하고 웅크림
                comboStep = 0;
            }
            else
            {
                Jump(jumpForce);         // 일반/공중 점프 — 누르는 즉시 발사(핑 없음)
            }
        }

        if (isChargingJump)
        {
            if (!isGrounded)
            {
                isChargingJump = false;  // 차지 중 바닥에서 벗어나면 취소
            }
            else
            {
                if (Input.GetKey(KeyCode.Space)) jumpHoldTimer += Time.deltaTime;
                if (Input.GetKeyUp(KeyCode.Space))
                {
                    bool high = jumpHoldTimer >= chargeJumpTime;   // 충분히 충전해야만 높은 점프
                    isChargingJump = false;
                    Jump(high ? chargeJumpForce : jumpForce);      // 덜 눌렀으면 일반 점프
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && currentDashes > 0 && !isGuarding && animBusyTimer <= 0 && !isChargingJump
            && (GameManager.Instance == null || GameManager.Instance.CurrentStamina >= dashStaminaCost))
            StartDash();
    }

    private void Move()
    {
        if (isChargingJump && jumpHoldTimer >= tapJumpTime) { rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }  // 차지 중 제자리 고정

        float speed = isGuarding ? moveSpeed * guardSpeedMultiplier : moveSpeed;
        rb.linearVelocity = new Vector2(horizontalInput * speed, rb.linearVelocity.y);

        if (horizontalInput > 0) { sr.flipX = false; facingDir = 1; }
        else if (horizontalInput < 0) { sr.flipX = true; facingDir = -1; }
    }

    // ───────────────────────── 애니메이션 (코드 주도) ─────────────────────────
    // 컨트롤러의 모든 클립이 "상태"로 들어있으므로, 전이 그래프 대신 상태 이름을 직접 재생한다.

    private void UpdateAnimations()
    {
        if (anim == null) return;
        if (isDashing) { PlayState(dashState); return; }       // 대시 모션 유지
        if (isPlunging) { PlayState(plungeFallState); return; }
        if (animBusyTimer > 0 || animHoldTimer > 0) return;   // 1회성/플립 모션 보호
        if (isGuarding) { PlayState(guardState); return; }
        if (isChargingJump && jumpHoldTimer >= tapJumpTime) { PlayState(chargeState); return; }   // 준비자세는 0.1초 이상 홀드 때만

        string state;
        if (!isGrounded)
            state = rb.linearVelocity.y > 0.1f
                ? (isSwordDrawn ? swordJumpRiseState : jumpRiseState)
                : (isSwordDrawn ? swordJumpFallState : jumpFallState);
        else if (Mathf.Abs(horizontalInput) > 0.1f)
            state = isSwordDrawn ? swordMoveState : moveState;
        else
            state = isSwordDrawn ? swordIdleState : idleState;

        PlayState(state);
    }

    // 같은 상태면 다시 재생하지 않음(루프 모션이 끊겨 깜빡이는 것 방지)
    private void PlayState(string state)
    {
        if (state == currentAnimState) return;
        anim.Play(state, 0, 0f);
        currentAnimState = state;
    }

    // 공격/발도 같은 1회성 모션은 같은 상태라도 처음부터 다시 재생
    private void PlayStateForced(string state, float normalizedTime = 0f)
    {
        if (anim == null) return;
        anim.Play(state, 0, normalizedTime);
        currentAnimState = state;
        animHoldTimer = 0f;   // 강제 재생은 플립 같은 "애니만 보호" 상태를 덮어씀
    }

    // 스프라이트 투명도만 바꾸기(피격 무적 점멸용). RGB는 유지.
    private void SetAlpha(float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    private void ToggleSheathe()
    {
        isSwordDrawn = !isSwordDrawn;
        if (!isSwordDrawn) EndGuard();   // 검을 넣으면 가드 해제

        string flourish = isSwordDrawn ? drawFlourishState : sheatheFlourishState;
        if (!string.IsNullOrEmpty(flourish))
        {
            PlayStateForced(flourish);
            animBusyTimer = ClipLength(flourish);   // 흉내 모션 "딱 그 길이만큼만" 보호
        }
        // 흉내 클립 칸을 비워두면: 연출 없이 즉시 전환(UpdateAnimations가 바로 idle/run 재생)
    }

    // ───────────────────────── 전투 ─────────────────────────

    private void DoComboAttack()
    {
        if (comboStates == null || comboStates.Length == 0) return;
        comboStep = Mathf.Clamp(comboStep, 0, comboStates.Length - 1);

        string clip = comboStates[comboStep];
        PlayStateForced(clip);
        float len = ClipLength(clip);
        animBusyTimer = len;                 // 스윙 모션을 딱 그 길이만큼만 보호

        bool isFinisher = (comboStep == comboStates.Length - 1);
        float dmg = isFinisher ? attackDamage * comboFinisherMultiplier : attackDamage;
        PerformAttack(dmg, 1f);

        comboStep++;
        if (comboStep >= comboStates.Length) comboStep = 0;   // D 다음엔 다시 A
        comboResetTimer = len + comboContinueBuffer;          // 이 시간 안에 또 누르면 다음 콤보
    }

    private void UseSkill()
    {
        skillCooldownTimer = skillCooldown;
        animBusyTimer = ClipLength(skillState);
        comboStep = 0;   // 스킬 쓰면 콤보 끊김
        PlayStateForced(skillState);
        PerformAttack(attackDamage * skillDamageMultiplier, skillRangeMultiplier);
    }

    private void PerformAttack(float damage, float rangeMultiplier)
    {
        GetAttackBox(rangeMultiplier, out Vector2 center, out Vector2 size);
        PerformAreaDamage(center, size, damage);
    }

    // 지정한 사각형 범위 안의 적에게 데미지(낙하 공격 착지 충격 등에 사용)
    private void PerformAreaDamage(Vector2 center, Vector2 size, float damage)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, enemyLayer);
        bool anyHit = false;
        foreach (Collider2D hit in hits)
        {
            IDamageable target = hit.GetComponent<IDamageable>();
            if (target != null) { target.TakeDamage(damage * (GameManager.Instance != null ? GameManager.Instance.AttackMultiplier : 1f)); anyHit = true; }
        }
        if (anyHit) Juice.Hit();   // 적중 시 타격감(히트스톱 + 화면 흔들림)
    }

    // ── 공중 공격 ──
    private void AirAttack()
    {
        comboStep = 0;
        animBusyTimer = ClipLength(airAttackState);
        PlayStateForced(airAttackState);
        PerformAttack(airAttackDamage, 1f);
    }

    // ── 공중 아래 베기(1타). 이 모션 중 한 번 더 누르면 낙하 공격으로 전환 ──
    private void AirDownAttack()
    {
        comboStep = 0;
        float len = ClipLength(airDownAttackState);
        animBusyTimer = len;
        plungeArmTimer = len + plungeArmBuffer;   // 이 시간 안에 또 누르면 낙하 공격
        hoverTimer = airDownHoverTime;            // 아래 베기 중 잠깐 체공
        PlayStateForced(airDownAttackState);
        PerformAttack(airAttackDamage, 1f);
    }

    // ── 낙하 공격(아래 베기 중 한 번 더 입력) ──
    private void StartPlunge()
    {
        isPlunging = true;
        plungeArmTimer = 0;
        hoverTimer = 0;
        comboStep = 0;
        PlayStateForced(plungeFallState);
        // 하강 속도는 FixedUpdate, 착지 판정은 Update에서 처리
    }

    private void PlungeLand()
    {
        isPlunging = false;
        PlayStateForced(plungeLandState, plungeLandStartTime);   // 준비동작 건너뛰고 타격 프레임부터
        animBusyTimer = ClipLength(plungeLandState) * (1f - plungeLandStartTime);   // 남은 길이만큼만 보호
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);   // 착지 시 수평 정지

        Vector2 center = (Vector2)transform.position + new Vector2(0f, plungeAoeYOffset);
        PerformAreaDamage(center, plungeAoeSize, plungeDamage);     // 착지 충격(광역)
    }

    private void GetAttackBox(float rangeMultiplier, out Vector2 center, out Vector2 size)
    {
        size = new Vector2(attackBoxSize.x * rangeMultiplier, attackBoxSize.y);
        float forwardShift = (size.x - attackBoxSize.x) * 0.5f;
        center = GetAttackCenter() + new Vector2(facingDir * forwardShift, 0f);
    }

    private Vector2 GetAttackCenter()
    {
        if (attackPoint != null)
        {
            Vector3 local = attackPoint.localPosition;
            return (Vector2)transform.position + new Vector2(Mathf.Abs(local.x) * facingDir, local.y);
        }
        return (Vector2)transform.position + new Vector2(0.7f * facingDir, 0f);
    }

    // ───────────────────────── 점프 / 대시 / 바닥 ─────────────────────────

    private void Jump(float force)
    {
        bool isAirJump = (currentJumps < maxJumps);   // 처음(지상) 점프가 아니면 공중 점프(2단 이상)

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        rb.AddForce(Vector2.up * force, ForceMode2D.Impulse);
        currentJumps--;
        isGrounded = false;
        comboStep = 0;          // 점프하면 콤보 끊김
        hoverTimer = 0;         // 점프하면 체공 해제
        animBusyTimer = 0;      // 공격 모션 즉시 캔슬
        attackBufferTimer = 0;  // 버퍼된 공격도 취소

        if (isAirJump)
        {
            // 2단 이상 점프: 플립 애니(입력은 막지 않고 애니만 잠깐 보호)
            PlayStateForced(doubleJumpState);
            animHoldTimer = ClipLength(doubleJumpState);
        }
        // 첫 점프는 UpdateAnimations가 JumpRise로 자동 처리
    }

    private void StartDash()
    {
        if (GameManager.Instance != null) GameManager.Instance.TrySpendStamina(dashStaminaCost);
        isDashing = true;
        currentDashes--;
        dashTimer = dashDuration;
        dashDir = facingDir;
        rb.gravityScale = 0;
        comboStep = 0;   // 대시하면 콤보 끊김
        PlayStateForced(dashState);
    }

    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        // 대시 끝나면 UpdateAnimations가 idle/run으로 복귀
    }

    private void CheckGrounded()
    {
        if (groundCheck == null) return;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (isGrounded && rb.linearVelocity.y <= 0.1f)
        {
            currentJumps = maxJumps;
            currentDashes = maxDashes;
        }
    }

    // ───────────────────────── 가드 / 패링 / 피격 ─────────────────────────

    private void StartGuard()
    {
        if (GameManager.Instance != null) GameManager.Instance.TrySpendStamina(guardStaminaCost);  // 패링 시도 비용
        isGuarding = true;
        isParrying = true;
        parryTimer = parryWindow;
        comboStep = 0;   // 가드하면 콤보 끊김
        // 가드 자세(SwordGuard)는 UpdateAnimations가 유지
    }

    private void EndGuard()
    {
        isGuarding = false;
        isParrying = false;
    }

    public void TakeDamage(float damage, bool isMeleeAttacker, IParryable attacker = null, Vector2 source = default)
    {
        if (isDashing || hitInvincibleTimer > 0) return;   // 대시 무적 / 피격 후 무적

        if (isParrying)
        {
            if (isMeleeAttacker && attacker != null) attacker.ApplyGroggy();
            PlayStateForced(parrySuccessState);     // 패링 성공 → 반격 모션(인스펙터에서 교체 가능)
            animBusyTimer = ClipLength(parrySuccessState);
            if (GameManager.Instance != null) GameManager.Instance.ChangeStamina(parryStaminaRecover);  // 패링 성공 → 기력 회복
            skillCooldownTimer = 0f;   // 패링 성공 → Q스킬 즉시 초기화
            Juice.ParryHit();          // 강한 타격감(히트스톱 + 셰이크 + 플래시)
            return;
        }

        // damage는 "하트" 단위. 가드 중이면 절반(반올림).
        int hearts = isGuarding ? Mathf.RoundToInt(damage * 0.5f) : Mathf.RoundToInt(damage);
        if (hearts <= 0) return;   // 가드로 완전히 막힘 → 피해/반응 없음

        if (GameManager.Instance != null) GameManager.Instance.TakeDamage(hearts);

        Hurt(source);   // 넉백 + 경직 + 피격 모션
    }

    // 피격 반응: 진행 중 행동 취소 + 넉백 + 경직 + 피격 모션
    private void Hurt(Vector2 source)
    {
        comboStep = 0;
        isPlunging = false;
        hoverTimer = 0;
        animBusyTimer = 0;
        attackBufferTimer = 0;

        hitstunTimer = hitstunDuration;
        hitInvincibleTimer = hitInvincibleTime;   // 피격 후 무적 시작

        float kbDir = (source.x <= transform.position.x) ? 1f : -1f;   // 공격자 반대 방향으로 조금
        rb.linearVelocity = new Vector2(kbDir * knockbackForce, rb.linearVelocity.y);   // 수평만(공중으로 안 띄움)

        PlayStateForced(hitState);
    }

    // ───────────────────────── 기즈모 ─────────────────────────

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        GetAttackBox(1f, out Vector2 nCenter, out Vector2 nSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(nCenter, nSize);

        GetAttackBox(skillRangeMultiplier, out Vector2 sCenter, out Vector2 sSize);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(sCenter, sSize);

        // 낙하 공격 착지 충격 범위(보라색)
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube((Vector2)transform.position + new Vector2(0f, plungeAoeYOffset), plungeAoeSize);
    }
}
