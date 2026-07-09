using UnityEngine;

// 위기 자동 예지(쉬움 난이도 전용, 자동부팅·영구).
//  체력이 반 칸일 때 적의 공격이 명중하기 '직전'(예비동작 후반) 아주 짧은 슬로우가 훅 켜졌다 훅 꺼진다.
//  장신구 예지안(PrecogCharm, 0.85·1.7s)보다 더 늦게(0.92) 더 짧게(0.65s) — 난이도 보조 장치.
//  튜토리얼 첫 각성 레슨(CombatTutorial)과 겹치면 발동 시점의 SlowMoFx.Active 검사로 양보한다.
public class AutoPrecog : MonoBehaviour
{
    public static AutoPrecog Instance;

    [Header("쉬움 난이도 — 위기 자동 예지(짧고 굵게)")]
    [Range(0.02f, 0.5f)] public float slowScale = 0.25f;
    public float slowDuration = 0.65f;                             // 실시간 지속 — 훅 켜졌다 훅 꺼짐
    [Range(0f, 0.98f)] public float windupLateFraction = 0.92f;    // 예비동작이 이만큼 지난 '진짜 직전'에 발동

    private bool armed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) { var go = new GameObject("AutoPrecog"); Instance = go.AddComponent<AutoPrecog>(); DontDestroyOnLoad(go); }
    }

    void OnEnable() { Enemy.WindupStarted += OnWindup; }
    void OnDisable() { Enemy.WindupStarted -= OnWindup; }

    private void OnWindup(Enemy e)
    {
        if (!Difficulty.AutoPrecog) return;                                            // 어려움: 자동 예지 없음
        if (e == null || armed || SlowMoFx.Active) return;
        if (GameManager.Instance == null || GameManager.Instance.CurrentHalf > 1) return;   // 반 칸 위기에서만
        var pc = PlayerController.Instance;
        if (pc == null || e.TargetPlayer != pc.transform) return;                      // 플레이어를 노리는 공격만
        StartCoroutine(FireLate(e, pc));
    }

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
        if (e == null || !e.IsAttacking || SlowMoFx.Active) yield break;       // 레슨·예지안이 선점했으면 양보
        if (GameManager.Instance == null || GameManager.Instance.CurrentHalf > 1) yield break;

        SlowMoFx.BeginTimed(slowScale, slowDuration);
        PrecogCharm.PlayEyeFlash(pc);   // 붉은 눈빛 — 잠재 기프트

        // 조기 해제: 패링 성공(그로기)·공격 종료 시 즉시 시간 복구
        while (SlowMoFx.Active)
        {
            if (e == null || !e.IsAttacking) { SlowMoFx.End(); yield break; }
            yield return null;
        }
    }
}
