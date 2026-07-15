using UnityEngine;

// 자동 예지(자동부팅·영구) — 모든 예지 발동의 단일 창구.
//  발동하면 '저스트 패링이 바로 가능한 순간' 시간이 잠깐 완전히 멈춘다(감속 아님 — 정지).
//   · 근접 공격: 타격은 예비동작이 끝나는 즉시 나가므로(Enemy.TickStrike 첫 프레임),
//     예비동작 95% 시점 = 타격 1~2프레임 전에 정지 → 정지 중 우클릭만 누르면 저스트 패링.
//   · 원거리 공격: 투사체가 플레이어 코앞(projectileNearDist 유닛 이내)까지 붙었을 때 정지 — 발사 순간이 아니라 '닿기 직전'.
//  발동 조건(둘 중 하나): ① 쉬움 난이도 + 체력 반 칸 이하(위기, 무료) ② 예지안 장신구 착용(난이도 무관, 쿨다운 소모)
//  정지 중에도 입력은 살아있고, 패링 창(parryTimer)은 scaled 시간이라 정지 동안 소모되지 않는다.
//  튜토리얼 첫 각성 레슨(CombatTutorial)과 겹치면 발동 시점의 SlowMoFx.Active 검사로 양보한다.
public class AutoPrecog : MonoBehaviour
{
    public static AutoPrecog Instance;

    [Header("자동 예지(시간 정지)")]
    public float freezeDuration = 0.5f;                            // 정지 시간(실시간) — 훅 멈췄다 훅 풀림
    [Range(0f, 0.98f)] public float windupLateFraction = 0.95f;    // 근접: 예비동작이 이만큼 지난 '타격 직전'에 정지
    public float projectileNearDist = 1.3f;                        // 원거리: 투사체가 플레이어와 이 거리(유닛) 이내로 붙었을 때 정지 — 속도 무관 '닿기 직전'
    public float refireCooldown = 2f;                              // 재발동 간격(실시간) — 보스 볼리·연타 스팸 방지

    private bool armed;
    private float nextReady;   // 재발동 가능 시각(unscaled)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) { var go = new GameObject("AutoPrecog"); Instance = go.AddComponent<AutoPrecog>(); DontDestroyOnLoad(go); }
    }

    void OnEnable() { Enemy.WindupStarted += OnWindup; }
    void OnDisable() { Enemy.WindupStarted -= OnWindup; }

    // 발동 공통 조건: (쉬움+반 칸 위기) 또는 (예지안 착용+쿨다운 완료). viaCharm=true면 장신구 쿨다운을 소모해야 함.
    private bool Eligible(out bool viaCharm)
    {
        viaCharm = false;
        if (SlowMoFx.Active || Time.unscaledTime < nextReady) return false;
        if (PlayerController.Instance == null) return false;
        bool crisis = Difficulty.AutoPrecog
            && GameManager.Instance != null && GameManager.Instance.CurrentHalf <= 2;   // 쉬움 + 체력 1칸 이하 위기(무료) — 몹 공격력 1칸 기준 '다음 타에 사망'
        bool charm = PrecogCharm.CharmReady;                                            // 예지안(난이도 무관, 쿨다운)
        if (!crisis && !charm) return false;
        viaCharm = !crisis;   // 위기 발동이 우선 — 겹치면 장신구 쿨다운은 아끼기
        return true;
    }

    // 근접 예비동작 — 원거리형 공격(원거리몹·보스 볼리)은 여기서 제외(투사체 접근 감시가 담당)
    private void OnWindup(Enemy e)
    {
        bool viaCharm;
        if (e == null || armed || e.RangedPrecog) return;
        if (!Eligible(out viaCharm)) return;
        var pc = PlayerController.Instance;
        if (e.TargetPlayer != pc.transform) return;                      // 플레이어를 노리는 공격만
        StartCoroutine(FireLate(e, pc));
    }

    // 예비동작 95%(타격 1~2프레임 전)까지 기다렸다가 정지 — 정지 중 우클릭 = 저스트 패링
    private System.Collections.IEnumerator FireLate(Enemy e, PlayerController pc)
    {
        armed = true;
        float wait = Mathf.Max(0f, e.attackWindup * windupLateFraction);
        float t = 0f;
        while (t < wait)
        {
            if (e == null || !e.IsAttacking) { armed = false; yield break; }   // 공격 취소(그로기·사망)
            t += Time.deltaTime;
            yield return null;
        }
        armed = false;
        if (e == null || !e.IsAttacking) yield break;
        bool viaCharm;
        if (!Eligible(out viaCharm)) yield break;   // 레슨이 선점했거나 그새 회복/쿨다운이면 양보
        Fire(pc, viaCharm);
    }

    // 원거리: 적 투사체가 '도달 직전'인지 매 프레임 감시(정지 중엔 Eligible이 걸러줌)
    void Update()
    {
        bool viaCharm;
        if (Projectile.All.Count == 0 || !Eligible(out viaCharm)) return;
        var pc = PlayerController.Instance;
        var col = pc.GetComponent<Collider2D>();
        Vector2 target = col != null ? (Vector2)col.bounds.center : (Vector2)pc.transform.position + Vector2.up * 0.6f;

        for (int i = 0; i < Projectile.All.Count; i++)
        {
            var p = Projectile.All[i];
            if (p == null || p.Reflected || p.precogSeen) continue;
            Vector2 to = target - (Vector2)p.transform.position;
            float dist = to.magnitude;
            if (dist < 0.01f) continue;
            if (Vector2.Dot(p.Velocity, to / dist) < 0.5f) continue;   // 멀어지거나 스치는 탄은 무시
            if (dist <= projectileNearDist)                             // 플레이어 바로 앞 = '닿기 직전'(속도 무관)
            {
                p.precogSeen = true;
                Fire(pc, viaCharm);
                break;
            }
        }
    }

    // 시간 정지 + 붉은 눈빛 — 정지 동안 패링을 준비할 기회. 장신구 경유면 쿨다운 소모.
    private void Fire(PlayerController pc, bool viaCharm)
    {
        nextReady = Time.unscaledTime + refireCooldown;
        if (viaCharm) PrecogCharm.Consume();
        SlowMoFx.Freeze(freezeDuration);
        PrecogCharm.PlayEyeFlash(pc);
    }
}
