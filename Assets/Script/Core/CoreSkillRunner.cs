using System.Collections;
using UnityEngine;

// 코어 스킬 실행기(플레이어에 런타임 부착). CoreSkill.kind에 따라 실제 동작을 수행한다.
//  PlayerController는 '무엇을 쓸지'만 정하고, '어떻게 동작하는지'는 여기로 분리 — 스킬이 늘어도 컨트롤러가 비대해지지 않는다.
//  판정/이동은 PlayerController의 공개 훅(AreaDamageAt / ForceMove / SetInvincible)을 사용.
public class CoreSkillRunner : MonoBehaviour
{
    public static CoreSkillRunner Instance { get; private set; }

    private PlayerController pc;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // 플레이어가 있는 씬에서만 붙는다(씬 전환 시 새 플레이어에 다시 부착)
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Attach();
        Attach();
    }

    private static void Attach()
    {
        var p = PlayerController.Instance != null ? PlayerController.Instance : Object.FindAnyObjectByType<PlayerController>();
        if (p == null) return;
        var r = p.GetComponent<CoreSkillRunner>();
        if (r == null) r = p.gameObject.AddComponent<CoreSkillRunner>();
        Instance = r;
        r.pc = p;
    }

    void Awake() { Instance = this; pc = GetComponent<PlayerController>(); }

    // 스킬 실행 — 성공하면 true. (쿨타임/해금 판정은 CoreSystem이 이미 끝낸 상태)
    public bool Run(CoreSkill sk, float power, float rangeMul)
    {
        if (sk == null || pc == null) return false;
        switch (sk.kind)
        {
            case CoreSkill.Kind.Dash:       StartCoroutine(DoDash(sk, power)); break;
            case CoreSkill.Kind.Projectile: DoProjectile(sk, power); break;
            case CoreSkill.Kind.Shockwave:  StartCoroutine(DoShockwave(sk, power)); break;
            case CoreSkill.Kind.Buff:       DoBuff(sk); break;
            default:                        DoSlash(sk, power, rangeMul); break;
        }
        return true;
    }

    // ── 베기: 전방 광역(기본) ──
    private void DoSlash(CoreSkill sk, float power, float rangeMul)
    {
        AudioManager.Sfx("swing", 0.95f, 0.07f);
        pc.AreaDamageAhead(power, rangeMul);
    }

    // ── 돌진: 앞으로 미끄러지며 관통 피해 + 무적 ──
    private IEnumerator DoDash(CoreSkill sk, float power)
    {
        AudioManager.Sfx("dash", 1f, 0.05f);
        int dir = pc.FacingDir;
        float t = 0f, dur = Mathf.Max(0.05f, sk.dashTime);
        float speed = sk.dashDistance / dur;
        var hitOnce = new System.Collections.Generic.HashSet<Collider2D>();
        pc.SetInvincible(dur + 0.05f);
        while (t < dur)
        {
            t += Time.deltaTime;
            pc.ForceMoveX(dir * speed);
            // 지나가는 길의 적을 1회씩 타격
            var box = pc.AttackBoxAhead(1.2f);
            foreach (var h in Physics2D.OverlapBoxAll(box.center, box.size, 0f, pc.enemyLayer))
            {
                if (h == null || hitOnce.Contains(h)) continue;
                var d = h.GetComponent<IDamageable>();
                if (d != null) { hitOnce.Add(h); d.TakeDamage(power * pc.DamageScale); Juice.Hit(); }
            }
            yield return null;
        }
        pc.ForceMoveX(0f);
    }

    // ── 투사체: 전방으로 발사(여러 발이면 부채꼴) ──
    private void DoProjectile(CoreSkill sk, float power)
    {
        AudioManager.Sfx("boss_shot", 0.9f, 0.06f);
        int n = Mathf.Max(1, sk.projectileCount);
        int dir = pc.FacingDir;
        Vector3 origin = pc.transform.position + Vector3.up * 0.6f + Vector3.right * dir * 0.5f;
        for (int i = 0; i < n; i++)
        {
            float ang = n == 1 ? 0f : Mathf.Lerp(-sk.spreadDegrees * 0.5f, sk.spreadDegrees * 0.5f, i / (float)(n - 1));
            Vector2 v = Quaternion.Euler(0, 0, ang) * new Vector2(dir, 0f);
            SpawnBolt(origin, v, sk, power);
        }
    }

    // 플레이어 투사체(프리팹 없으면 절차 생성) — 적에게만 피해
    private void SpawnBolt(Vector3 origin, Vector2 dir, CoreSkill sk, float power)
    {
        GameObject go;
        if (sk.projectilePrefab != null) go = Instantiate(sk.projectilePrefab, origin, Quaternion.identity);
        else
        {
            go = new GameObject("CoreBolt");
            go.transform.position = origin;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BoltSprite();
            sr.color = CoreSystem.Instance != null && CoreSystem.Instance.Main != null ? CoreSystem.Instance.Main.tint : Color.cyan;
            sr.sortingOrder = 10;
            go.transform.localScale = Vector3.one * 0.5f;
        }
        var b = go.GetComponent<CoreBolt>();
        if (b == null) b = go.AddComponent<CoreBolt>();
        b.Init(dir.normalized, sk.projectileSpeed, power * pc.DamageScale, pc.enemyLayer);
    }

    private static Sprite _bolt;
    private static Sprite BoltSprite()
    {
        if (_bolt != null) return _bolt;
        int S = 16;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float nx = (x - S * 0.5f) / (S * 0.5f), ny = (y - S * 0.5f) / (S * 0.5f);
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                px[y * S + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - r));
            }
        tex.SetPixels(px); tex.Apply();
        _bolt = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return _bolt;
    }

    // ── 충격파: 발밑에서 좌우로 단계적으로 퍼짐(점프로 회피 가능한 낮은 판정) ──
    private IEnumerator DoShockwave(CoreSkill sk, float power)
    {
        AudioManager.Sfx("door_slam", 0.9f, 0.05f);
        Juice.Shake(0.3f, 0.22f);
        float groundY = pc.transform.position.y;
        for (int step = 1; step <= Mathf.Max(1, sk.waveSteps); step++)
        {
            float dist = step * sk.waveStepDistance;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 p = new Vector2(pc.transform.position.x + s * dist, groundY + 0.25f);
                ParryFx.Spark(p, false);
                foreach (var h in Physics2D.OverlapBoxAll(p, new Vector2(2.2f, 1.4f), 0f, pc.enemyLayer))
                {
                    var d = h != null ? h.GetComponent<IDamageable>() : null;
                    if (d != null) { d.TakeDamage(power * pc.DamageScale); Juice.Hit(); }
                }
            }
            yield return new WaitForSeconds(sk.waveStepInterval);
        }
    }

    // ── 강화: 일정 시간 공격력 상승(기존 버프 시스템 재사용) ──
    private void DoBuff(CoreSkill sk)
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.ApplyAttackBuff(sk.buffAttackMult, sk.buffDuration);
        AudioManager.Sfx("skill_ready");
        Juice.Flash(new Color(1f, 0.85f, 0.4f, 0.35f), 0.3f);
        var core = CoreSystem.Instance != null ? CoreSystem.Instance.Main : null;
        Toast.Show((core != null ? core.coreName + " — " : "") + "공격력 상승 (" + sk.buffDuration.ToString("0") + "초)", 2f);
    }
}

// 플레이어가 쏘는 코어 투사체(적에게만 피해). 벽에 닿거나 수명이 끝나면 소멸.
public class CoreBolt : MonoBehaviour
{
    private Vector2 dir;
    private float speed, damage, life = 3f;
    private LayerMask enemyMask;
    private int wallMask;

    public void Init(Vector2 d, float s, float dmg, LayerMask enemies)
    {
        dir = d; speed = s; damage = dmg; enemyMask = enemies;
        wallMask = LayerMask.GetMask("Ground");
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        transform.position += (Vector3)(dir * step);
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        if (Physics2D.Raycast(transform.position, dir, step + 0.1f, wallMask).collider != null) { Destroy(gameObject); return; }
        foreach (var h in Physics2D.OverlapCircleAll(transform.position, 0.35f, enemyMask))
        {
            var d = h != null ? h.GetComponent<IDamageable>() : null;
            if (d != null) { d.TakeDamage(damage); Juice.Hit(); Destroy(gameObject); return; }
        }
    }
}
