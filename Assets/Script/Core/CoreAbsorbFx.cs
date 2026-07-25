using System.Collections;
using UnityEngine;

// [코어] 흡수 연출(자동부팅). CoreSystem.OnAbsorbed를 구독 — 보스 처치 순간 후드가 코어를 빨아들인다.
//  캐논: 첫 코어 = 각성의 계기. 슬로우모 + 코어 색 섬광 + 배너 + 획득 대사(선택).
public class CoreAbsorbFx : MonoBehaviour
{
    public static CoreAbsorbFx Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("CoreAbsorbFx");
        Instance = go.AddComponent<CoreAbsorbFx>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() { CoreSystem.OnAbsorbed += Play; }
    void OnDisable() { CoreSystem.OnAbsorbed -= Play; }

    private void Play(CoreData core)
    {
        if (core == null) return;
        StartCoroutine(Sequence(core));
    }

    private IEnumerator Sequence(CoreData core)
    {
        yield return new WaitForSeconds(0.8f);   // 보스 사망 연출이 먼저 흐르게

        SlowMoFx.BeginTimed(0.12f, 1.1f);
        Juice.Shake(0.35f, 0.4f);
        Juice.Flash(new Color(core.tint.r, core.tint.g, core.tint.b, 0.5f), 0.5f);
        AudioManager.Sfx("skill_ready");

        yield return new WaitForSecondsRealtime(0.5f);

        AcquireBanner.Show(
            "[코어] " + core.coreName,
            core.GradeLabel() + " 코어 — " + SlotHint(core),
            core.icon != null ? core.icon.texture : null,
            "기프트 흡수");
    }

    private string SlotHint(CoreData core)
    {
        switch (core.SlotCount)
        {
            case 3:  return "[Q] [E] [R] 해금";
            case 2:  return "[Q] [E] 해금";
            default: return "[Q] 해금";
        }
    }
}
