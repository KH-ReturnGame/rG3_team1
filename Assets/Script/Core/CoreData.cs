using UnityEngine;

// [코어] — 몬스터의 기프트 정수. 주인공만 추출 가능하며, 후드에 접촉해 '일시 사용'한다.
//  등급이 열어주는 스킬 슬롯 수가 다르다: 일반=Q / 정예=Q·E / 보스=Q·E·R
//  ★밸런스 철학: 낮은 등급일수록 '특화성'이 두각 — 보스 코어는 범용(3스킬, 평이한 위력),
//    일반 코어는 스킬 하나뿐이지만 그 분야에서는 최강(specialization 배수). 고등급 독점 방지.
[CreateAssetMenu(fileName = "New Core", menuName = "Inventory/Core")]
public class CoreData : ScriptableObject
{
    public enum Grade { Normal, Elite, Boss }
    public enum Slot { Q, E, R }

    [Header("정체")]
    public string id;                       // 세이브용 고유 id(비우면 에셋 이름)
    public string coreName = "코어";
    public string sourceName = "";          // 추출한 개체 이름(도감·연출용)
    public Sprite icon;
    [TextArea] public string description;
    public Grade grade = Grade.Normal;
    public Color tint = new Color(0.6f, 0.85f, 1f);   // 연출/UI 색

    [Header("스킬 (등급이 허용하는 만큼만 사용됨)")]
    public CoreSkill skillQ = new CoreSkill();
    public CoreSkill skillE = new CoreSkill();
    public CoreSkill skillR = new CoreSkill();

    [Header("특화 (낮은 등급일수록 크게 — 파워크리프 방지)")]
    [Tooltip("이 코어 스킬들의 위력 배수. 일반 코어는 높게(예 1.6), 보스 코어는 1.0 근처로 두면 '전문가 vs 만능' 대비가 생긴다.")]
    public float specialization = 1f;

    [Header("상시 효과 (장착 중 스탯 — 선택)")]
    public float attackBonus = 0f;
    public int maxHeartBonus = 0;

    public string Id => string.IsNullOrEmpty(id) ? name : id;

    // 등급이 여는 슬롯 수: 일반 1(Q) / 정예 2(Q,E) / 보스 3(Q,E,R)
    public int SlotCount => grade == Grade.Boss ? 3 : (grade == Grade.Elite ? 2 : 1);
    public bool Unlocks(Slot s) => (int)s < SlotCount;

    public CoreSkill SkillOf(Slot s)
    {
        switch (s) { case Slot.E: return skillE; case Slot.R: return skillR; default: return skillQ; }
    }

    public string GradeLabel()
    {
        switch (grade) { case Grade.Boss: return "보스"; case Grade.Elite: return "정예"; default: return "일반"; }
    }
    public Color GradeColor()
    {
        switch (grade)
        {
            case Grade.Boss:  return new Color(1f, 0.55f, 0.2f);    // 주황
            case Grade.Elite: return new Color(0.72f, 0.45f, 1f);   // 보라
            default:          return new Color(0.45f, 0.8f, 1f);    // 파랑
        }
    }
}

// 코어가 제공하는 스킬 하나(골격 — 실제 연출/판정은 다음 단계에서 CoreSkillRunner가 해석)
[System.Serializable]
public class CoreSkill
{
    public string skillName = "";
    [TextArea] public string desc = "";
    public float cooldown = 6f;
    public float damageMultiplier = 1.5f;   // 플레이어 공격력 대비
    public float rangeMultiplier = 1.6f;
    public string animState = "";           // 비우면 플레이어 기본 스킬 모션(skillState)
    public bool IsDefined => !string.IsNullOrEmpty(skillName);
}
