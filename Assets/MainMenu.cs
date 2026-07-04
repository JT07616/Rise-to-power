using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource uiAudioSource;
    public AudioClip buttonClickSound;

    public void PlayGame()
    {
        PlayButtonClick();
        StartCoroutine(LoadGame());
    }

    private IEnumerator LoadGame()
    {
        yield return new WaitForSeconds(0.15f);
        SceneManager.LoadSceneAsync(1);
    }

    public void QuitGame()
    {
        PlayButtonClick();
        Debug.Log("Game exited");
        Application.Quit();
    }

    private void PlayButtonClick()
    {
        if (uiAudioSource != null && buttonClickSound != null)
        {
            uiAudioSource.PlayOneShot(buttonClickSound);
        }
    }
}