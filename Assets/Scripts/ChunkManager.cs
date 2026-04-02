using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public GameObject[] chunks;
    public Transform startPoint;
    public Transform stageAssets;
    public int ChunkCount = 6;

    void Start()
    {
        GenerateLevel();
    }

    void GenerateLevel()
    {
    Vector3 spawnPos = startPoint.position;
    int lastIndex = -1;

    for (int i = 0; i < ChunkCount; i++)
        {
            int randomIndex;

            do {
            randomIndex = Random.Range(0, chunks.Length);
            } while (randomIndex == lastIndex && chunks.Length > 1);

            lastIndex = randomIndex;

            GameObject chunk = Instantiate(chunks[randomIndex], Vector3.zero, Quaternion.identity);
            chunk.transform.SetParent(stageAssets);

            Transform chunkStart = chunk.transform.Find("ChunkStart");
            Vector3 offset = spawnPos - chunkStart.position;
            chunk.transform.position += offset;

            Transform exit = chunk.transform.Find("ChunkExit");
            spawnPos = exit.position;
        }
    }
}