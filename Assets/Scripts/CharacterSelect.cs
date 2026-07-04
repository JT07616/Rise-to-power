using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class CharacterSelect : MonoBehaviour
{
    public TMP_InputField nameInput;
    public string sceneToLoad = "NewSampleScene";

    private Button maleButton;
    private Button femaleButton;

    private string selectedCharacter = "";

    public static string playerName;
    public static string playerCharacter;

    public Color selectedColor = Color.green;
    public Color normalColor = Color.white;

    [Header("Audio")]
    public AudioSource uiAudioSource;
    public AudioClip buttonClickSound;

    void Awake()
    {
        maleButton = GameObject.Find("MaleButton").GetComponent<Button>();
        femaleButton = GameObject.Find("FemaleButton").GetComponent<Button>();
    }

    public void SelectMale()
    {
        PlayButtonClick();

        selectedCharacter = "Male";

        maleButton.image.color = selectedColor;
        femaleButton.image.color = normalColor;

        Debug.Log("Male selected");
    }

    public void SelectFemale()
    {
        PlayButtonClick();

        selectedCharacter = "Female";

        femaleButton.image.color = selectedColor;
        maleButton.image.color = normalColor;

        Debug.Log("Female selected");
    }

    public void StartGame()
    {
        if (nameInput == null)
        {
            Debug.LogError("NameInput nije spojen!");
            return;
        }

        if (string.IsNullOrEmpty(nameInput.text))
        {
            Debug.LogWarning("Enter your name!");
            return;
        }

        if (string.IsNullOrEmpty(selectedCharacter))
        {
            Debug.LogWarning("Select Male or Female!");
            return;
        }

        playerName = nameInput.text;
        playerCharacter = selectedCharacter;

        PlayButtonClick();
        StartCoroutine(LoadGame());
    }

    private IEnumerator LoadGame()
    {
        yield return new WaitForSeconds(0.15f);
        SceneManager.LoadScene(sceneToLoad);
    }

    private void PlayButtonClick()
    {
        if (uiAudioSource != null && buttonClickSound != null)
        {
            uiAudioSource.PlayOneShot(buttonClickSound);
        }
    }
}