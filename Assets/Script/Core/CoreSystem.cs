using System.Collections.Generic;
using UnityEngine;

// 코어 흡수/장착 시스템(자동부팅·영구). 후드가 매개체 — 코어를 접촉시켜 '일시 사용'한다.
//  ★메인 코어 1개 = 스킬(Q/E/R) + 패시브 전부 적용
//  ★서브 코어 N개 = 패시브만 적용(스킬 없음) — 빌드 조합의 축
//  · 등급이 여는 스킬 슬롯: 일반=Q / 정예=Q·E / 보스=Q·E·R (메인일 때만 의미)
//  · 스킬 실제 발동은 PlayerController가 TryUse로 문의 — 이 클래스는 '무엇을 쓸 수 있나'만 판정(골격).
public class CoreSystem : MonoBehaviour
{
    public static CoreSystem Instance { get; private set; }

    public const int SubSlots = 2;   // 서브 코어 칸 수(밸런스 노브 — 늘리면 패시브 조합 폭 증가)

    private readonly List<CoreData> collected = new List<CoreData>();
    public IReadOnlyList<CoreData> Collected => collected;

    public CoreData Main { get; private set; }                     // 스킬+패시브
    private readonly List<CoreData> subs = new List<CoreData>();    // 패시브만
    public IReadOnlyList<CoreData> Subs => subs;

    // (구) 호환 별칭 — 기존 호출부가 Equipped를 참조하던 것
    public CoreData Equipped => Main;

    public static event System.Action OnChanged;      // UI 갱신용
    public static event System.Action<CoreData> OnAbsorbed;   // 흡수 연출용

    private readonly Dictionary<CoreData.Slot, float> cdEnd = new Dictionary<CoreData.Slot, float>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("CoreSystem").AddComponent<CoreSystem>(); }

    // ── 흡수(획득) ── 보스/정예 처치 시 후드가 빨아들임
    public void Absorb(CoreData core)
    {
        if (core == null || collected.Contains(core)) return;
        collected.Add(core);
        if (Main == null) EquipMain(core);          // 첫 코어는 자동으로 메인
        else if (subs.Count < SubSlots) EquipSub(core);   // 이후엔 빈 서브 칸에
        OnAbsorbed?.Invoke(core);
        OnChanged?.Invoke();
    }

    // ── 메인 코어(스킬+패시브) ──
    public void EquipMain(CoreData core)
    {
        if (core != null && !collected.Contains(core)) return;   // 안 가진 코어는 장착 불가
        subs.Remove(core);                                        // 서브에 있던 걸 메인으로 올리면 서브에서 빠짐
        Main = core;
        cdEnd.Clear();                                            // 교체 시 쿨타임 초기화
        ApplyPassives();
        OnChanged?.Invoke();
    }
    public void Equip(CoreData core) => EquipMain(core);   // (구) 호환

    // ── 서브 코어(패시브 전용) ──
    public bool EquipSub(CoreData core)
    {
        if (core == null || !collected.Contains(core)) return false;
        if (core == Main) return false;                 // 메인과 중복 불가
        if (subs.Contains(core)) return false;
        if (subs.Count >= SubSlots) return false;       // 칸 가득
        subs.Add(core);
        ApplyPassives();
        OnChanged?.Invoke();
        return true;
    }

    public void UnequipSub(CoreData core)
    {
        if (core != null && subs.Remove(core)) { ApplyPassives(); OnChanged?.Invoke(); }
    }

    public bool IsEquipped(CoreData core) => core != null && (core == Main || subs.Contains(core));
    public int FreeSubSlots => Mathf.Max(0, SubSlots - subs.Count);

    public bool Has(CoreData core) => core != null && collected.Contains(core);
    public bool Has(string id) => Get(id) != null;
    public CoreData Get(string id)
    {
        foreach (var c in collected) if (c != null && c.Id == id) return c;
        return null;
    }

    // ── 스킬 슬롯 판정 (★메인 코어만 스킬을 준다) ──
    public bool SlotUnlocked(CoreData.Slot s) => Main != null && Main.Unlocks(s) && Main.SkillOf(s).IsDefined;
    public CoreSkill SkillAt(CoreData.Slot s) => SlotUnlocked(s) ? Main.SkillOf(s) : null;

    public float CooldownLeft(CoreData.Slot s)
    {
        float end;
        return cdEnd.TryGetValue(s, out end) ? Mathf.Max(0f, end - Time.time) : 0f;
    }
    public bool IsReady(CoreData.Slot s) => SlotUnlocked(s) && CooldownLeft(s) <= 0f;

    // 스킬 사용 시도 — 쓸 수 있으면 쿨타임을 걸고 스킬 정보를 돌려준다(실제 판정/연출은 호출자).
    public CoreSkill TryUse(CoreData.Slot s)
    {
        if (!IsReady(s)) return null;
        var sk = Main.SkillOf(s);
        cdEnd[s] = Time.time + Mathf.Max(0.1f, sk.cooldown);
        return sk;
    }

    public void ResetCooldowns() { cdEnd.Clear(); }   // 저스트 패링 보상 등

    // 메인 코어의 위력 배수(특화) — 낮은 등급일수록 크게 설정해 '전문가' 역할
    public float Specialization => Main != null ? Mathf.Max(0.1f, Main.specialization) : 1f;

    // ── 패시브 합산 (메인 + 서브 전부) ── 장신구와 같은 경로(GameManager.SetEquipBonuses)로 흘려보낸다.
    public int PassiveHearts { get; private set; }
    public float PassiveAttack { get; private set; }

    private void ApplyPassives()
    {
        int heart = Main != null ? Main.maxHeartBonus : 0;
        float atk = Main != null ? Main.attackBonus : 0f;
        foreach (var c in subs) { if (c == null) continue; heart += c.maxHeartBonus; atk += c.attackBonus; }
        PassiveHearts = heart; PassiveAttack = atk;
        // 장신구 합산에 코어 몫을 더해 재계산(Equipment.Recompute가 코어 값을 읽어 합산)
        if (Equipment.Instance != null) Equipment.Instance.Recompute();
        else if (GameManager.Instance != null) GameManager.Instance.SetEquipBonuses(heart, atk);
    }

    // ── 세이브 연동 ──
    public List<string> SaveIds()
    {
        var ids = new List<string>();
        foreach (var c in collected) if (c != null) ids.Add(c.Id);
        return ids;
    }
    public string SaveEquippedId() => Main != null ? Main.Id : "";
    public List<string> SaveSubIds()
    {
        var ids = new List<string>();
        foreach (var c in subs) if (c != null) ids.Add(c.Id);
        return ids;
    }

    public void LoadIds(List<string> ids, string mainId, List<string> subIds = null)
    {
        collected.Clear(); subs.Clear(); Main = null; cdEnd.Clear();
        if (ids != null)
            foreach (var id in ids)
            {
                var c = CoreDatabase.Get(id);
                if (c != null && !collected.Contains(c)) collected.Add(c);
            }
        var eq = CoreDatabase.Get(mainId);
        if (eq != null && collected.Contains(eq)) Main = eq;
        else if (collected.Count > 0) Main = collected[0];
        if (subIds != null)
            foreach (var id in subIds)
            {
                var c = CoreDatabase.Get(id);
                if (c != null && c != Main && collected.Contains(c) && !subs.Contains(c) && subs.Count < SubSlots) subs.Add(c);
            }
        ApplyPassives();
        OnChanged?.Invoke();
    }
}

// Resources 안의 모든 CoreData를 id로 조회(아이템 DB와 같은 방식)
public static class CoreDatabase
{
    private static Dictionary<string, CoreData> map;

    private static void EnsureLoaded()
    {
        if (map != null) return;
        map = new Dictionary<string, CoreData>();
        foreach (var c in Resources.LoadAll<CoreData>(""))
            if (c != null && !map.ContainsKey(c.Id)) map[c.Id] = c;
    }

    public static CoreData Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureLoaded();
        CoreData v;
        return map.TryGetValue(id, out v) ? v : null;
    }

    public static List<CoreData> All()
    {
        EnsureLoaded();
        return new List<CoreData>(map.Values);
    }
}
