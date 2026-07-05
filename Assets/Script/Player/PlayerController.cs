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
    public float maxFallSpeed = 22f;   // 종단 낙하 속도 제한(고속 낙하로 땅에 박히는 것 방지)
    public int maxJumps = 2;   // 기본 더블 점프. 장신구 +1, 상점(영구) +1 로 더 늘릴 수 있음
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

    [Header("Wall (벽 슬라이드 / 벽 점프)")]
    public bool wallMoveEnabled = false;       // 벽 슬라이드/점프 기능 on/off (기본 OFF — 필요하면 인스펙터에서 체크)
    public LayerMask wallLayer;                // 비우면 groundLayer 사용(보통 지형=벽)
    // 튜닝값은 const로 — 인스턴스 직렬화에 안 휘둘리고 무조건 적용됨(바꾸려면 여기 숫자만 수정)
    private const float wallSlideSpeed = 2.5f; // 벽에 붙어 미끄러질 때 최대 낙하 속도
    private const float wallJumpForceX = 11.5f; // 벽 차고 나가는 수평 힘
    private const float wallJumpForceY = 11f;   // 벽 점프 수직 힘
    private const float wallJumpLockTime = 0.24f; // 벽점프 직후 수평 입력 무시(길수록 더 멀리 날아감)
    public string wallSlideState = "WallSlide";
    public string wallJumpState = "WallJump";
    private bool isTouchingWall, isWallSliding;
    private int wallDir;                        // +1 = 오른쪽 벽 / -1 = 왼쪽 벽
    private float wallJumpLockTimer;
    private float wallJumpVelX;                 // 벽점프 수평 속도(잠금 동안 매 물리프레임 재적용)

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

    [Header("쿨타임 (스태미나 대신 — 꼼수 방지)")]
    public float dashCooldown = 0.7f;        // 대시 후 재대시까지(무한 대시 방지)
    public float guardCooldown = 0.6f;       // 가드 해제 후 재가드까지(가드 연타 패링 방지)
    private float dashCooldownTimer;
    private float guardCooldownTimer;
    private bool guardParried;                // 이번 가드에서 패링 성공? → 해제해도 쿨타임 안 걸림(즉시 재가드 가능)

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

    // ── 컷씬 제어(IntroCutscene 등이 사용) ──
    [System.NonSerialized] public bool cutsceneActive;   // true면 입력·자동애니 잠금(중력 낙하는 유지)
    public bool Grounded => isGrounded;                  // 외부에서 착지 판정 읽기
    public void PlayAnim(string state) => PlayStateForced(state);   // 특정 애니 강제 재생
    public void ZeroVelocity() { if (rb != null) rb.linearVelocity = Vector2.zero; }

    // 점프대(트램펄린) 등이 호출 — 위로 발사 + 공중 점프 리필.
    public void Launch(float upSpeed)
    {
        if (rb == null) return;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, upSpeed);
        currentJumps = maxJumps;
        isGrounded = false;
        hoverTimer = 0; isPlunging = false; animBusyTimer = 0;
    }

    // 컷씬용 걷기: 지정 방향으로 이동 + 방향 전환 + 걷기 애니(매 프레임 호출). cutsceneActive 중 외부 컷씬이 구동.
    public void CutsceneWalk(int dir)
    {
        facingDir = dir < 0 ? -1 : 1;
        if (sr != null) sr.flipX = facingDir < 0;
        if (rb != null) rb.linearVelocity = new Vector2(facingDir * moveSpeed, rb.linearVelocity.y);
        string ws = isSwordDrawn ? swordMoveState : moveState;
        if (ws != currentAnimState) PlayStateForced(ws);   // 매 프레임 재시작 방지(루트모션 고정 회피)
    }
    // 컷씬용 이동(속도·애니 지정): 마을 진입처럼 천천히 'Walk'로 걷게 할 때.
    public void CutsceneMove(int dir, float speed, string animState)
    {
        if (anim != null) anim.applyRootMotion = false;   // 루트모션이 위치를 고정해 속도 이동을 막으므로 컷씬 걷기 동안 끔
        facingDir = dir < 0 ? -1 : 1;
        if (sr != null) sr.flipX = facingDir < 0;
        if (rb != null) rb.linearVelocity = new Vector2(facingDir * speed, rb.linearVelocity.y);
        if (!string.IsNullOrEmpty(animState) && animState != currentAnimState) PlayStateForced(animState);
    }

    // 컷씬용 정지: 수평 정지 + 대기 자세. 루트모션 원복.
    public void CutsceneStop()
    {
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        PlayStateForced(isSwordDrawn ? swordIdleState : idleState);
        if (anim != null) anim.applyRootMotion = true;
    }

    [Header("낙사")]
    public float fallMargin = 6f;                     // 카메라 경계 바닥보다 이만큼 더 아래로 떨어지면 낙사
    public int fallDamage = 1;                        // 낙사 패널티(하트). HP 0되면 정상 사망 처리
    private Vector3 lastSafePos;

    [Header("원웨이 플랫폼 (S+Space로 하강)")]
    public float dropThroughTime = 0.35f;            // 통과하는 동안 발판과 충돌을 끄는 시간
    private Collider2D bodyCollider;                  // 플레이어 몸 콜라이더(통과 처리용)

    void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        defaultGravityScale = rb.gravityScale;
        baseMaxJumps = maxJumps;
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>())   // 트리거 아닌 첫 몸 콜라이더(통과 처리용)
            if (c != null && !c.isTrigger) { bodyCollider = c; break; }
        BuildClipLengthTable();

        // 인스펙터에서 enemyLayer를 비워둔 채(=0, Nothing) 저장된 씬/프리팹이 있으면
        // 공격 판정(OverlapBoxAll)이 항상 빈 결과를 반환해 "공격이 안 맞는" 것처럼 보임.
        // "Enemy" 레이어로 자동 보정해 Boss 등 IDamageable 대상에 공격이 항상 닿도록 함.
        if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
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
        int sb = GameManager.Instance != null ? GameManager.Instance.bonusJumps : 0;   // 상점 영구 점프 업그레이드
        maxJumps = baseMaxJumps + sb + jb;
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

        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;     // 대시/가드 쿨타임은 항상 진행
        if (guardCooldownTimer > 0f) guardCooldownTimer -= Time.deltaTime;

        if (cutsceneActive) { CheckGrounded(); return; }   // 컷씬 중: 입력·자동애니 잠금(중력 낙하만 유지)

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

        CheckGrounded();
        CheckWall();          // 입력(점프)보다 먼저 벽 상태 갱신 → 누른 즉시 벽 점프 반응
        CheckInput();
        isWallSliding = isTouchingWall && rb.linearVelocity.y < 0.2f
            && Mathf.Abs(horizontalInput) > 0.1f && (int)Mathf.Sign(horizontalInput) == wallDir;
        if (wallJumpLockTimer > 0f) wallJumpLockTimer -= Time.deltaTime;

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
        if (!cutsceneActive)
        {
            if (wallJumpLockTimer > 0f)   // 벽점프 잠금 동안: 충돌/디페네트레이션이 지워도 수평 속도 재적용 → 확실히 밀려남
                rb.linearVelocity = new Vector2(wallJumpVelX, rb.linearVelocity.y);
            else
                Move();
        }

        if (isWallSliding && !cutsceneActive && rb.linearVelocity.y < -wallSlideSpeed)   // 벽 슬라이드: 천천히 미끄러짐
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideSpeed);

        // 종단 속도 제한 — 너무 빠르게 낙하해 지형을 뚫고 박히는 것 방지
        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    // UI(일시정지/인벤/대사창 등)를 '닫는 클릭'이 같은 프레임에 공격으로 새는 것 방지 —
    // UI가 닫힌 뒤 짧은 유예 동안 마우스 공격/가드 입력만 무시(이동·키보드는 즉시 허용).
    private float uiCloseGraceTimer;
    private const float UiCloseGrace = 0.15f;

    private void CheckInput()
    {
        if (isPlunging) return;   // 낙하 공격 중엔 입력 무시(착지까지 커밋)
        if (Inventory.IsUIOpen) { horizontalInput = 0f; uiCloseGraceTimer = UiCloseGrace; return; }   // 인벤토리/메뉴 열려있으면 조작 잠금

        if (uiCloseGraceTimer > 0f) uiCloseGraceTimer -= Time.unscaledDeltaTime;
        bool mouseOk = uiCloseGraceTimer <= 0f;   // UI 닫은 직후엔 마우스 입력 무효

        horizontalInput = Input.GetAxisRaw("Horizontal");

        // 발도/납도 토글
        if (Input.GetKeyDown(sheatheKey) && animBusyTimer <= 0)
            ToggleSheathe();

        // 가드/패링 (검을 들었을 때만)
        if (mouseOk && Input.GetMouseButtonDown(1) && isSwordDrawn && animBusyTimer <= 0 && !isChargingJump && guardCooldownTimer <= 0f)
            StartGuard();
        if (Input.GetMouseButtonUp(1)) EndGuard();

        // 좌클릭: 지상=콤보 / 공중=공중 공격 (아래키 같이 누르면 낙하 공격)
        if (mouseOk && Input.GetMouseButtonDown(0) && isSwordDrawn && !isGuarding && !isDashing && !isChargingJump)
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

        // 점프: 스페이스 탭=누르는 즉시 일반 점프 / 공중=즉시 2단 점프
        // 아래(S)+스페이스: '원웨이 플랫폼 위'면 아래로 하강, 일반 지형이면 차지 높은 점프 (더블탭 하강은 폐지)
        if (Input.GetKeyDown(KeyCode.Space) && !isGuarding && !isChargingJump)
        {
            bool holdDown = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetAxisRaw("Vertical") < -0.1f;

            if (isGrounded && holdDown && TryDropThroughPlatform())
            {
                // 플랫폼 아래로 하강 — 점프 소모 없음(DropThroughRoutine이 처리)
            }
            else if (!isGrounded && isTouchingWall)
            {
                WallJump();              // 벽에 붙어 있으면 벽 점프(공중 점프 횟수 소모 X)
            }
            else if (currentJumps > 0)
            {
                if (isGrounded && holdDown)
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

        if (Input.GetKeyDown(KeyCode.LeftShift) && currentDashes > 0 && dashCooldownTimer <= 0f && !isGuarding && animBusyTimer <= 0 && !isChargingJump)
            StartDash();
    }

    private void Move()
    {
        if (isChargingJump && jumpHoldTimer >= tapJumpTime) { rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }  // 차지 중 제자리 고정
        if (wallJumpLockTimer > 0f) return;   // 벽점프 직후엔 수평 입력 무시(벽에서 밀려나는 속도 유지)

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
        if (isWallSliding) { PlayState(wallSlideState); return; }   // 벽 슬라이드 자세

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

    // 컷씬/튜토리얼용 강제 발도 — 이미 뽑았으면 무시. (아픔을 참고 검을 드는 연출 등)
    public void CutsceneDrawSword()
    {
        if (isSwordDrawn) return;
        ToggleSheathe();
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

    // 공중에서 양옆에 벽이 있는지 검사(지면에선 검사 안 함 → 바닥 모서리 오탐 방지)
    private void CheckWall()
    {
        isTouchingWall = false; wallDir = 0;
        if (!wallMoveEnabled || bodyCollider == null || isGrounded) return;   // 기능 OFF면 벽 미감지 → 슬라이드/점프 모두 비활성
        LayerMask lm = wallLayer.value != 0 ? wallLayer : groundLayer;
        Bounds b = bodyCollider.bounds;
        float d = 0.22f;   // 감지 여유(벽에 딱 안 붙어도 잡히도록)
        Vector2 size = new Vector2(d, b.size.y * 0.7f);
        if (Physics2D.OverlapBox(new Vector2(b.max.x + d * 0.5f, b.center.y), size, 0f, lm)) { isTouchingWall = true; wallDir = 1; }
        else if (Physics2D.OverlapBox(new Vector2(b.min.x - d * 0.5f, b.center.y), size, 0f, lm)) { isTouchingWall = true; wallDir = -1; }
    }

    // 벽 점프: 벽 반대 방향으로 차고 나간다. 공중 점프 횟수는 리필.
    private void WallJump()
    {
        wallJumpVelX = -wallDir * wallJumpForceX;
        rb.linearVelocity = new Vector2(wallJumpVelX, wallJumpForceY);
        facingDir = -wallDir;
        if (sr != null) sr.flipX = facingDir < 0;
        wallJumpLockTimer = wallJumpLockTime;
        currentJumps = maxJumps;   // 벽점프 후 공중 점프 다시 가능
        isWallSliding = false; isTouchingWall = false;
        comboStep = 0; hoverTimer = 0; animBusyTimer = 0; attackBufferTimer = 0;
        PlayStateForced(wallJumpState);
    }

    private void StartDash()
    {
        dashCooldownTimer = dashCooldown;   // 무한 대시 방지
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

    // 아래키+점프: 발밑이 원웨이 플랫폼이면 잠깐 충돌을 꺼서 아래로 내려간다. 통과 처리했으면 true.
    private bool TryDropThroughPlatform()
    {
        if (groundCheck == null || bodyCollider == null) return false;
        Collider2D[] unders = Physics2D.OverlapCircleAll(groundCheck.position, groundCheckRadius + 0.15f, groundLayer);   // 판정 여유(가장자리에서도 안정적으로)
        bool dropped = false;
        foreach (Collider2D c in unders)
        {
            if (c == null) continue;
            PlatformEffector2D eff = c.GetComponent<PlatformEffector2D>();
            if (eff != null && eff.useOneWay)
            {
                StartCoroutine(DropThroughRoutine(c));
                dropped = true;
            }
        }
        return dropped;
    }

    private System.Collections.IEnumerator DropThroughRoutine(Collider2D platform)
    {
        Physics2D.IgnoreCollision(bodyCollider, platform, true);
        isGrounded = false;
        currentJumps = maxJumps;   // 통과 후 공중에서도 점프 가능하게
        yield return new WaitForSeconds(dropThroughTime);
        if (platform != null && bodyCollider != null) Physics2D.IgnoreCollision(bodyCollider, platform, false);
    }

    // ───────────────────────── 가드 / 패링 / 피격 ─────────────────────────

    private void StartGuard()
    {
        isGuarding = true;
        isParrying = true;
        guardParried = false;
        parryTimer = parryWindow;
        comboStep = 0;   // 가드하면 콤보 끊김
        // 가드 자세(SwordGuard)는 UpdateAnimations가 유지
    }

    private void EndGuard()
    {
        if (isGuarding && !guardParried) guardCooldownTimer = guardCooldown;   // 가드 해제 후 쿨타임(연타 패링 방지). 단, 패링 성공한 가드는 쿨타임 없음
        isGuarding = false;
        isParrying = false;
    }

    // 튜토리얼 패링 레슨: 슬로우모션 중 우클릭이 감지되면 CombatTutorial이 호출.
    // 실제 TakeDamage 패링 분기와 동일한 보상(그로기·반격 모션·Q쿨 초기화·타격감)을 강제 발동한다.
    // ※ 호출 전 Time.timeScale=1 복구 필요(Juice 히트스톱은 timeScale<0.9면 무시됨).
    public void TutorialParrySuccess(IParryable attacker)
    {
        if (attacker != null) attacker.ApplyGroggy();
        PlayStateForced(parrySuccessState);
        animBusyTimer = ClipLength(parrySuccessState);
        skillCooldownTimer = 0f;          // 패링 성공 → Q스킬 즉시 초기화
        isGuarding = false;
        isParrying = false;
        guardCooldownTimer = guardCooldown;   // 같은 프레임의 우클릭이 일반 가드로 중복 처리되는 것 방지
        Juice.ParryHit();                 // "팅" — 히트스톱 + 셰이크 + 플래시
    }

    public void TakeDamage(float damage, bool isMeleeAttacker, IParryable attacker = null, Vector2 source = default, bool nonLethal = false)
    {
        if (isDashing || hitInvincibleTimer > 0) return;   // 대시 무적 / 피격 후 무적

        if (isParrying)
        {
            if (isMeleeAttacker && attacker != null) attacker.ApplyGroggy();
            PlayStateForced(parrySuccessState);     // 패링 성공 → 반격 모션(인스펙터에서 교체 가능)
            animBusyTimer = ClipLength(parrySuccessState);
            skillCooldownTimer = 0f;   // 패링 성공 → Q스킬 즉시 초기화
            guardParried = true;       // 패링 성공 → 가드 쿨타임 초기화(해제해도 안 걸림 → 즉시 재가드)
            guardCooldownTimer = 0f;
            Juice.ParryHit();          // 강한 타격감(히트스톱 + 셰이크 + 플래시)
            return;
        }

        // damage는 "하트" 단위. 가드(패링 X) 중이면 50% 경감하되 반칸 단위로 반영.
        // 가드는 '경감'만 — 피해가 있으면 최소 반칸은 들어간다(완전 무효화는 패링만). 0데미지 버그 방지.
        float eff = isGuarding ? damage * 0.5f : damage;
        int halves = Mathf.Max(1, Mathf.RoundToInt(eff * 2f));   // 반칸 단위(예: 가드로 1칸→0.5칸→반칸)

        if (GameManager.Instance != null)
        {
            if (nonLethal)   // 훈련용(허수아비 등): 최소 반칸은 남겨 죽지 않게 — 딸피 튜토리얼용
            {
                int survivable = Mathf.Max(0, GameManager.Instance.CurrentHalf - 1);
                halves = Mathf.Min(halves, survivable);
            }
            if (halves > 0) GameManager.Instance.TakeDamageHalves(halves);
        }

        Hurt(source);   // 넉백 + 경직 + 피격 모션(피해가 0이어도 맞은 반응은 준다)
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
