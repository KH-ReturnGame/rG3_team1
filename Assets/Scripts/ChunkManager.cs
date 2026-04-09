using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public GameObject[] chunks;
    public Transform startPoint;
    public Transform stageAssets;
    public GameObject chunkEnd;
    public int ChunkCount = 6;

    void Start()
    {
        Debug.Log("ChunkManager");
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

        GameObject endChunk = Instantiate(chunkEnd, Vector3.zero, Quaternion.identity);
        endChunk.transform.SetParent(stageAssets);

        Transform endStart = endChunk.transform.Find("ChunkStart");
        Vector3 endOffset = spawnPos - endStart.position;
        endChunk.transform.position += endOffset;
    }
}