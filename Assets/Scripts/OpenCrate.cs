using UnityEngine;

public class OpenCrate : MonoBehaviour, IInteractable   // 1. add IInteractable
{
    public Transform player;

    [Header("Crate Rewards")]
    public int CrateMultiplier;
    public int goldMin = 1;
    public int goldMax = 5;
    public Sprite goldSprite;
    public int goldCoinMin = 2;
    public int goldCoinMax = 3;
    public LootDrop[] loot;
    public float dropSize = 0.5f;
    public float dropScatter = 0.3f;

    private SpriteRenderer sr;

    // 2. IInteractable members
    public string Prompt => "F: Open Crate";

    public void Interact()
    {
        GrantRewards();
        gameObject.SetActive(false);
    }

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void GrantRewards()
    {
        int goldAmount = Random.Range(goldMin, goldMax + 1) * CrateMultiplier;
        if (goldAmount > 0) DropGoldCoins(goldAmount);

        Debug.Log("Gained " + goldAmount + " gold");

        if (loot == null || loot.Length == 0) return;
        foreach (var d in loot)
        {
            if (d == null || d.item == null || Random.value > d.chance) continue;
            int n = Random.Range(d.minCount, d.maxCount + 1);
            if (n <= 0) continue;
            Vector3 pos = transform.position + (Vector3)(Random.insideUnitCircle * dropScatter) + Vector3.up * 0.2f;
            ItemPickup.SpawnWorld(d.item, n, pos, dropSize);

            Debug.Log("Earned: " + n + " of " + d.item);
        }
    }

    private void DropGoldCoins(int amount)
    {
        int coins = Mathf.Clamp(Random.Range(goldCoinMin, goldCoinMax + 1), 1, amount);
        int per = amount / coins, rem = amount % coins;
        for (int i = 0; i < coins; i++)
            GoldCoin.Spawn(transform.position + Vector3.up * 0.3f, per + (i < rem ? 1 : 0), goldSprite, player);
    }
}