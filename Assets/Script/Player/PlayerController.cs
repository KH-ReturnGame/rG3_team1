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
    public int maxJumps = 1;
    private int currentJumps;
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

    private string currentAnimState = "";
    private float animBusyTimer;   // >0이면 1회성 모션(공격/발도 등) 재생 중 → 이동 애니로 안 바뀌고 새 행동도 잠금
    private Dictionary<string, float> clipLengths;   // 상태(클립) 이름 → 실제 재생 길이(초)

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        defaultGravityScale = rb.gravityScale;
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
        currentJumps = maxJumps;
        currentDashes = maxDashes;
        isSwordDrawn = startDrawn;
        PlayStateForced(isSwordDrawn ? swordIdleState : idleState);
    }

    void Update()
    {
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0) EndDash();
            return;
        }

        CheckInput();
        CheckGrounded();

        if (isParrying)
        {
            parryTimer -= Time.deltaTime;
            if (parryTimer <= 0) isParrying = false;
        }

        if (skillCooldownTimer > 0) skillCooldownTimer -= Time.deltaTime;
        if (animBusyTimer > 0) animBusyTimer -= Time.deltaTime;
        if (attackBufferTimer > 0) attackBufferTimer -= Time.deltaTime;
        if (comboResetTimer > 0)
        {
            comboResetTimer -= Time.deltaTime;
            if (comboResetTimer <= 0) comboStep = 0;   // 시간 초과 → 콤보 처음으로
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
        Move();
    }

    private void CheckInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // 발도/납도 토글
        if (Input.GetKeyDown(sheatheKey) && animBusyTimer <= 0)
            ToggleSheathe();

        // 가드/패링 (검을 들었을 때만)
        if (Input.GetMouseButtonDown(1) && isSwordDrawn && animBusyTimer <= 0) StartGuard();
        if (Input.GetMouseButtonUp(1)) EndGuard();

        // 좌클릭 콤보 (검 들고 + 지상에서만; 공중 공격은 3단계)
        if (Input.GetMouseButtonDown(0) && isSwordDrawn && !isGuarding && !isDashing && isGrounded)
        {
            if (animBusyTimer <= 0) DoComboAttack();
            else attackBufferTimer = attackInputBuffer;   // 스윙 중이면 버퍼에 저장
        }

        // 스킬 Q (검을 들었을 때만)
        if (Input.GetKeyDown(KeyCode.Q) && isSwordDrawn && !isGuarding && animBusyTimer <= 0 && skillCooldownTimer <= 0)
            UseSkill();

        if (Input.GetKeyDown(KeyCode.Space) && currentJumps > 0 && !isGuarding && animBusyTimer <= 0)
            Jump();

        if (Input.GetKeyDown(KeyCode.LeftShift) && currentDashes > 0 && !isGuarding && animBusyTimer <= 0)
            StartDash();
    }

    private void Move()
    {
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
        if (animBusyTimer > 0) return;          // 공격/발도 등 1회성 모션 보호
        if (isGuarding) { PlayState(guardState); return; }

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
    private void PlayStateForced(string state)
    {
        if (anim == null) return;
        anim.Play(state, 0, 0f);
        currentAnimState = state;
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
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, enemyLayer);
        foreach (Collider2D hit in hits)
        {
            DummyMonster monster = hit.GetComponent<DummyMonster>();
            if (monster != null) monster.TakePlayerDamage(damage);
        }
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

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        currentJumps--;
        isGrounded = false;
        comboStep = 0;   // 점프하면 콤보 끊김
        // 점프/낙하 애니메이션은 UpdateAnimations가 공중 상태(y속도)를 보고 자동 처리
    }

    private void StartDash()
    {
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

    public void TakeDamage(float damage, bool isMeleeAttacker, DummyMonster attacker = null)
    {
        if (isParrying)
        {
            if (isMeleeAttacker && attacker != null) attacker.ApplyGroggy();
            PlayStateForced("SwordGuardImpact");   // 패링 성공 연출
            animBusyTimer = ClipLength("SwordGuardImpact");
            return;
        }
        else if (isGuarding) damage *= 0.5f;

        // TODO: 체력 시스템 연결 시 여기서 실제 체력 차감 + 피격(넉백) 연출 (4단계)
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
    }
}
