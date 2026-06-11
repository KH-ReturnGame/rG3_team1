using UnityEngine;

public class DummyMonster : MonoBehaviour, IDamageable, IParryable
{
    [Header("Stats")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("플레이어 공격 (패링 테스트용)")]
    public bool attacksPlayer = true;     // 끄면 순수 샌드백(내 공격만 테스트할 때)
    public Transform player;
    public float attackInterval = 3f;
    public float attackDamage = 2f;       // 플레이어에게 주는 피해 = 하트 칸 수
    public float attackRange = 2.5f;
    private float attackTimer;

    [Header("그로기")]
    public float groggyDuration = 2f;
    private bool isGroggy;
    private float groggyTimer;

    [Header("피격 연출 (색 번쩍임)")]
    public Color hitColor = Color.red;                    // 내 공격이 맞는 순간
    public Color groggyColor = Color.cyan;               // 그로기 상태
    public Color attackColor = new Color(1f, 0.45f, 0f); // 허수아비가 나를 칠 때(주황)
    public float flashDuration = 0.08f;
    private float hitFlashTimer;
    private float attackFlashTimer;

    [Header("디버그 표시")]
    public bool showHealthLabel = true;   // 게임 화면에 HP 텍스트 표시(MainCamera 필요)
    public float labelHeight = 1.2f;

    private SpriteRenderer sr;
    private Color baseColor;
    private GUIStyle labelStyle;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        currentHealth = maxHealth;
        attackTimer = attackInterval;
        if (sr != null) baseColor = sr.color;   // 시작 색을 평상시 색으로 기억
    }

    void Update()
    {
        if (isGroggy)
        {
            groggyTimer -= Time.deltaTime;
            if (groggyTimer <= 0) isGroggy = false;
        }

        if (hitFlashTimer > 0) hitFlashTimer -= Time.deltaTime;
        if (attackFlashTimer > 0) attackFlashTimer -= Time.deltaTime;

        // 패링 테스트용: 일정 간격으로 플레이어 공격 (그로기 중엔 못 침)
        if (attacksPlayer && player != null && !isGroggy)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                attackTimer = attackInterval;
                TryAttackPlayer();
            }
        }

        UpdateColor();
    }

    // 플레이어에게 맞았을 때 (IDamageable)
    public void TakeDamage(float damage)
    {
        if (isGroggy) damage *= 1.5f;   // 그로기 중 치명타

        currentHealth -= damage;
        hitFlashTimer = flashDuration;  // 번쩍 → "맞았다!"가 눈에 보임

        Debug.Log($"[허수아비] {damage:0.#} 피격! 남은 HP {Mathf.Max(0, currentHealth):0}/{maxHealth:0}" +
                  (isGroggy ? " (그로기 치명타)" : ""));

        if (currentHealth <= 0) ResetDummy();
    }

    // 죽어도 사라지지 않고 그 자리에서 풀피로 리셋 → 연속 타격 테스트가 편함
    private void ResetDummy()
    {
        Debug.Log("[허수아비] 격파! 풀피로 리셋합니다.");
        currentHealth = maxHealth;
        isGroggy = false;
    }

    // 패링 당했을 때 (PlayerController가 호출)
    public void ApplyGroggy()
    {
        isGroggy = true;
        groggyTimer = groggyDuration;
        Debug.Log("[허수아비] 패링 당해 그로기!");
    }

    private void TryAttackPlayer()
    {
        if (Vector2.Distance(transform.position, player.position) > attackRange) return;

        attackFlashTimer = flashDuration * 2f;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.TakeDamage(attackDamage, true, this, transform.position);
    }

    private void UpdateColor()
    {
        if (sr == null) return;
        if (hitFlashTimer > 0)         sr.color = hitColor;
        else if (attackFlashTimer > 0) sr.color = attackColor;
        else if (isGroggy)             sr.color = groggyColor;
        else                           sr.color = baseColor;
    }

    // 게임 화면(Game 뷰)에 머리 위 HP 표시 — 별도 세팅 없이 타격이 들어가는지 바로 확인용
    private void OnGUI()
    {
        if (!showHealthLabel || Camera.main == null) return;

        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * labelHeight);
        if (sp.z < 0) return; // 카메라 뒤면 그리지 않음

        if (labelStyle == null)
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        labelStyle.normal.textColor = isGroggy ? Color.cyan : Color.white;

        string txt = $"HP {Mathf.Max(0, currentHealth):0}/{maxHealth:0}" + (isGroggy ? "  (그로기)" : "");
        GUI.Label(new Rect(sp.x - 60, Screen.height - sp.y - 24, 120, 22), txt, labelStyle);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
