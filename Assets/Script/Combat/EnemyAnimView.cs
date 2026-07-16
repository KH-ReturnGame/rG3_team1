using UnityEngine;

// 일반 몬스터 애니메이션 뷰(선택 부착). Enemy의 공개 상태를 읽어 Animator 파라미터로 전달만 한다.
//  - "attack" (Bool): 예비동작~타격 중 true — 컨트롤러에서 Idle→Attack 전환에 사용(궁수 등).
//  파라미터가 없는 컨트롤러(슬라임/박쥐 단일 루프)에는 붙일 필요 없음.
[RequireComponent(typeof(Enemy))]
public class EnemyAnimView : MonoBehaviour
{
    private Enemy enemy;
    private Animator anim;
    private bool hasAttack;

    void Start()
    {
        enemy = GetComponent<Enemy>();
        anim = GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
            foreach (var p in anim.parameters)
                if (p.name == "attack" && p.type == AnimatorControllerParameterType.Bool) hasAttack = true;
    }

    void LateUpdate()
    {
        if (enemy == null || anim == null || !hasAttack) return;
        anim.SetBool("attack", enemy.IsAttacking);
    }
}
