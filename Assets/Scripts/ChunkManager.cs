using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public GameObject[] chunks;   // stage prefabs
    public float chunkWidth = 60f; // width

    void Start()
    {
        GenerateLevel();
    }

    void GenerateLevel()
    {
        float xPosition = 0f;

        for (int i = 0; i < chunks.Length; i++)
        {
            Instantiate(chunks[i], new Vector3(xPosition, 0, 0), Quaternion.identity);
            xPosition += chunkWidth;
        }
    }
}