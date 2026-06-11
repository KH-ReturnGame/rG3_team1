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
        //Debug.Log("ChunkManager");
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

        ApplyCameraBounds();   // 생성된 맵 크기에 맞춰 카메라 경계 설정
    }

    // 생성된 스테이지 전체 콜라이더 범위를 카메라 경계(CameraFollow)로 전달
    private void ApplyCameraBounds()
    {
        if (stageAssets == null) return;
        Collider2D[] cols = stageAssets.GetComponentsInChildren<Collider2D>();
        if (cols.Length == 0) return;
        Bounds tb = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++) tb.Encapsulate(cols[i].bounds);
        CameraFollow cf = FindAnyObjectByType<CameraFollow>();
        if (cf != null) cf.SetBounds(tb.min, tb.max);
    }
}