using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StartMenuButton : MonoBehaviour
{
    public Button button;
    
    void Awake()
    {
        button.onClick.AddListener(OnButtonClick);
    }

    public void OnButtonClick()
    {
        SceneManager.LoadScene("TutorialScene");
    }

    void OnDestroy()
    {
        // Remove listener to prevent memory leaks
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
        }
    }
}
