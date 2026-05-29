using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int currentStage = 0;

    //public string[,] grid;

    void Start()
    {
        // grid = new string[4,4]
        // {
        //     { "a", "b", "c", "d" },
        //     { "e", "f", "g", "h" },
        //     { "i", "j", "k", "l" }
        //     { "m", "n", "o", "p" }
        // };
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}