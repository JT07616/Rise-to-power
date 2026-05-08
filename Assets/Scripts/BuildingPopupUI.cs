using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingPopupUI : MonoBehaviour
{
    public GameObject panel;
    public Button closeButton;

    public TMP_Text titleText;
    public TMP_Text descriptionText;

    void Start()
    {
        panel.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    public void Show(BuildingInfo building)
    {
        if (building == null)
        {
            return;
        }

        titleText.text = building.buildingName;
        descriptionText.text = building.description;

        panel.SetActive(true);
    }

    public void ClosePanel()
    {
        panel.SetActive(false);
    }
}
