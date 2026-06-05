using UnityEngine;
using System.Collections;

public class ParallaxSpawner : MonoBehaviour
{
    [Header("Background")]
    public GameObject BackgroundPrefab;
    public Transform BackgroundStart;
    public Transform BackgroundEnd;
    public float BackgroundSpeed = 0.5f;
    public float BackgroundSpawnTime = 10f;

    [Header("Middleground")]
    public GameObject MiddlePrefab;
    public Transform MiddleStart;
    public Transform MiddleEnd;
    public float MiddleSpeed = 1f;
    public float MiddleSpawnTime = 7f;

    [Header("Foreground")]
    public GameObject ForegroundPrefab;
    public Transform ForegroundStart;
    public Transform ForegroundEnd;
    public float ForegroundSpeed = 2f;
    public float ForegroundSpawnTime = 4f;

    void Start()
    {
        StartCoroutine(SpawnLayer(
            BackgroundPrefab,
            BackgroundStart,
            BackgroundEnd,
            BackgroundSpeed,
            BackgroundSpawnTime));

        StartCoroutine(SpawnLayer(
            MiddlePrefab,
            MiddleStart,
            MiddleEnd,
            MiddleSpeed,
            MiddleSpawnTime));

        StartCoroutine(SpawnLayer(
            ForegroundPrefab,
            ForegroundStart,
            ForegroundEnd,
            ForegroundSpeed,
            ForegroundSpawnTime));
    }

    IEnumerator SpawnLayer(
        GameObject prefab,
        Transform startPos,
        Transform endPos,
        float speed,
        float spawnTime)
    {
        while (true)
        {
            GameObject obj = Instantiate(
                prefab,
                startPos.position,
                Quaternion.identity);

            StartCoroutine(MoveObject(
                obj,
                endPos.position.x,
                speed));

            yield return new WaitForSeconds(spawnTime);
        }
    }

    IEnumerator MoveObject(
        GameObject obj,
        float endX,
        float speed)
    {
        while (obj != null)
        {
            obj.transform.position += Vector3.left * speed * Time.deltaTime;

            if (obj.transform.position.x <= endX)
            {
                Destroy(obj);
                yield break;
            }

            yield return null;
        }
    }
}