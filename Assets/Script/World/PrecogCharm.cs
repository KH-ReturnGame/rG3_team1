using UnityEngine;

// 예지안 장신구 효과(자동부팅·영구). 적이 공격을 시작하는 순간(Enemy.WindupStarted) 시간이 잠깐 느려져
// 반응(가드/패링/회피) 기회를 준다. 착용 중인 장신구 중 precogSlow가 있을 때만, 쿨다운(precogCooldown, 기본 20초)마다.
public class PrecogCharm : MonoBehaviour
{
    public static PrecogCharm Instance;
    public float slowScale = 0.22f;     // 감속 배율
    public float slowDuration = 1.7f;   // 실시간 지속(반응 창)

    public static float CooldownLeft { get; private set; }   // 남은 쿨다운(HUD용)
    public static bool Equipped { get; private set; }        // 예지안 착용 여부(HUD용)
    private float nextReadyTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) { var go = new GameObject("PrecogCharm"); Instance = go.AddComponent<PrecogCharm>(); DontDestroyOnLoad(go); } }

    void OnEnable() { Enemy.WindupStarted += OnWindup; }
    void OnDisable() { Enemy.WindupStarted -= OnWindup; }

    void Update()
    {
        CooldownLeft = Mathf.Max(0f, nextReadyTime - Time.unscaledTime);
        Equipped = EquippedPrecog() != null;
    }

    private ItemData EquippedPrecog()
    {
        var eq = Equipment.Instance;
        if (eq == null) return null;
        for (int i = 0; i < eq.slots.Length; i++)
            if (eq.slots[i] != null && eq.slots[i].precogSlow) return eq.slots[i];
        return null;
    }

    private void OnWindup(Enemy e)
    {
        if (e == null || SlowMoFx.Active) return;            // 이미 슬로우(패링 레슨 등)면 중복 방지
        if (Time.unscaledTime < nextReadyTime) return;        // 쿨다운
        var pc = PlayerController.Instance;
        if (pc == null || e.TargetPlayer != pc.transform) return;   // 플레이어를 노리는 공격만
        var charm = EquippedPrecog();
        if (charm == null) return;                            // 예지안 미착용

        SlowMoFx.BeginTimed(slowScale, slowDuration);
        nextReadyTime = Time.unscaledTime + Mathf.Max(1f, charm.precogCooldown);
        Toast.Show("예지안 — 시간이 느려진다", 1.2f);
    }
}
