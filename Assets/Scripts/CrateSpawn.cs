using UnityEngine;

public class CrateSpawn : MonoBehaviour
{
    [Range(0f, 1f)]
    public float spawnChance = 0.5f;   // 0 = never, 1 = always
    public GameObject cratePrefab;

    void Start()
    {
        if (Random.value < spawnChance)
            Instantiate(cratePrefab, transform.position, transform.rotation);
    }
}