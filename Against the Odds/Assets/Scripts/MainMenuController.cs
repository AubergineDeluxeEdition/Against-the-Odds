using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    private UIDocument _uiDocument;

    private void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        var root = _uiDocument.rootVisualElement;

        Button btnNewGame = root.Q<Button>("btn-new-game");
        Button btnQuit = root.Q<Button>("btn-quit");

        if (btnNewGame != null)
        {
            btnNewGame.clicked += StartNewGame;
        }

        if (btnQuit != null)
        {
            btnQuit.clicked += QuitGame;
        }
    }

    private void StartNewGame()
    {
        SceneManager.LoadScene("2_DeckSelection");
    }

    private void QuitGame()
    {
        Application.Quit();
    }
}