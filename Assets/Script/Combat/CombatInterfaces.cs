// 전투 공용 인터페이스
// - IDamageable : 플레이어 공격이 데미지를 줄 수 있는 대상(모든 적, 허수아비 등)
// - IParryable  : 패링당하면 그로기에 걸리는 대상(근접 공격하는 적)

public interface IDamageable
{
    void TakeDamage(float damage);
}

public interface IParryable
{
    void ApplyGroggy();
}
