using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingPopupUI : MonoBehaviour
{
    public static bool IsAnyOpen { get; private set; }

    public GameObject panel;
    public Button closeButton;

    public TMP_Text titleText;
    public TMP_Text descriptionText;

    [Header("Audio")]
    public AudioSource uiAudioSource;
    public AudioClip popupOpenSound;
    public AudioClip buttonClickSound;

    private BuildingInfo currentBuilding;

    public bool IsOpen
    {
        get { return panel != null && panel.activeSelf; }
    }

    void Start()
    {
        panel.SetActive(false);
        IsAnyOpen = false;

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    void Update()
    {
        if (IsOpen && currentBuilding != null)
        {
            RefreshText();
        }
    }

    public void Show(BuildingInfo building)
    {
        if (building == null)
        {
            return;
        }

        titleText.text = building.buildingName;
        currentBuilding = building;
        RefreshText();

        panel.SetActive(true);
        IsAnyOpen = true;

        PlayPopupOpen();
    }

    public void ClosePanel()
    {
        PlayButtonClick();

        Debug.Log("CLOSE CLICKED");
        currentBuilding = null;
        panel.SetActive(false);
        IsAnyOpen = false;
    }

    void OnGUI()
    {
        if (panel == null || !panel.activeSelf || currentBuilding == null || !currentBuilding.hasProductionControls)
        {
            return;
        }

        if (GameEventManager.IsPauseMenuOpen)
        {
            return;
        }

        if (GameEventManager.IsPopupOpen)
        {
            return;
        }

        if (DeliveryOrderManager.IsPopupOpen)
        {
            return;
        }

        Rect panelRect = GetPanelScreenRect();
        float buttonHeight = 38f;
        float gap = 12f;
        int buttonCount = GetActionButtonCount();
        float buttonWidth = Mathf.Min(230f, (panelRect.width - 40f - gap * (buttonCount - 1)) / buttonCount);
        float totalWidth = buttonWidth * buttonCount + gap * (buttonCount - 1);
        float x = panelRect.x + (panelRect.width - totalWidth) / 2f;
        float y = panelRect.yMax - buttonHeight - 18f;

        if (currentBuilding.buildingRole == BuildingRole.CorruptOfficer)
        {
            if (!currentBuilding.IsUnlocked())
            {
                GUI.enabled = currentBuilding.CanPurchase();
                if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), currentBuilding.GetPurchaseButtonLabel()))
                {
                    PlayButtonClick();
                    currentBuilding.Purchase();
                    RefreshText();
                }

                GUI.enabled = true;
                return;
            }

            GUI.enabled = currentBuilding.CanBuyRiskProtection();
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), currentBuilding.GetRiskProtectionButtonLabel()))
            {
                PlayButtonClick();
                currentBuilding.BuyRiskProtection();
                RefreshText();
            }

            GUI.enabled = currentBuilding.CanBuyRaidBribe();
            float raidButtonX = x + buttonWidth + gap;
            if (GUI.Button(new Rect(raidButtonX, y, buttonWidth, buttonHeight), currentBuilding.GetRaidBribeButtonLabel()))
            {
                PlayButtonClick();
                currentBuilding.BuyRaidBribe();
                RefreshText();
            }

            GUI.enabled = true;
            return;
        }

        if (!currentBuilding.IsUnlocked())
        {
            GUI.enabled = currentBuilding.CanPurchase();
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), currentBuilding.GetPurchaseButtonLabel()))
            {
                PlayButtonClick();
                currentBuilding.Purchase();
                RefreshText();
            }

            GUI.enabled = true;
            return;
        }

        int buttonIndex = 0;
        if (currentBuilding.producesGoods)
        {
            GUI.enabled = currentBuilding.CanProduceGoods();
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), currentBuilding.GetProduceGoodsButtonLabel()))
            {
                PlayButtonClick();
                currentBuilding.StartGoodsProduction();
                RefreshText();
            }
            buttonIndex++;
        }

        if (currentBuilding.buildingRole == BuildingRole.Factory ||
            currentBuilding.buildingRole == BuildingRole.Warehouse)
        {
            GUI.enabled = currentBuilding.CanMoveGoods();
            float buttonX = x + buttonIndex * (buttonWidth + gap);
            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), currentBuilding.GetMoveGoodsButtonLabel()))
            {
                PlayButtonClick();
                currentBuilding.MoveGoodsToWarehouse();
                RefreshText();
            }
            buttonIndex++;
        }

        if (currentBuilding.showIncreaseAction)
        {
            GUI.enabled = currentBuilding.CanIncreaseProduction();
            float buttonX = x + buttonIndex * (buttonWidth + gap);
            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), currentBuilding.GetIncreaseButtonLabel()))
            {
                PlayButtonClick();
                currentBuilding.IncreaseProduction();
                RefreshText();
            }
            buttonIndex++;
        }

        if (currentBuilding.showDecreaseAction)
        {
            GUI.enabled = currentBuilding.CanDecreaseProduction();
            float buttonX = x + buttonIndex * (buttonWidth + gap);
            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), currentBuilding.GetDecreaseButtonLabel()))
            {
                PlayButtonClick();
                currentBuilding.DecreaseProduction();
                RefreshText();
            }
            buttonIndex++;
        }

        if (currentBuilding.buildingRole == BuildingRole.WorkerContact)
        {
            GUI.enabled = currentBuilding.CanBuyEmergencyAction();
            float buttonX = x + buttonIndex * (buttonWidth + gap);
            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), currentBuilding.GetEmergencyActionButtonLabel()))
            {
                PlayButtonClick();
                currentBuilding.BuyEmergencyAction();
                RefreshText();
            }
            buttonIndex++;
        }

        if (currentBuilding.hasUpgrade)
        {
            GUI.enabled = currentBuilding.CanUpgrade();
            float buttonX = x + buttonIndex * (buttonWidth + gap);
            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), currentBuilding.GetUpgradeButtonLabel()))
            {
                PlayButtonClick();
                currentBuilding.Upgrade();
                RefreshText();
            }
        }

        GUI.enabled = true;
    }

    private int GetActionButtonCount()
    {
        if (currentBuilding != null && currentBuilding.buildingRole == BuildingRole.CorruptOfficer)
        {
            return currentBuilding.IsUnlocked() ? 2 : 1;
        }

        if (currentBuilding == null || !currentBuilding.IsUnlocked())
        {
            return 1;
        }

        int count = 0;
        if (currentBuilding.producesGoods)
        {
            count++;
        }

        if (currentBuilding.buildingRole == BuildingRole.Factory ||
            currentBuilding.buildingRole == BuildingRole.Warehouse)
        {
            count++;
        }

        if (currentBuilding.showIncreaseAction)
        {
            count++;
        }

        if (currentBuilding.showDecreaseAction)
        {
            count++;
        }

        if (currentBuilding.buildingRole == BuildingRole.WorkerContact)
        {
            count++;
        }

        if (currentBuilding.hasUpgrade)
        {
            count++;
        }

        return Mathf.Max(1, count);
    }

    private Rect GetPanelScreenRect()
    {
        RectTransform rectTransform = panel.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return new Rect(0f, 0f, Screen.width, Screen.height);
        }

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

        float x = bottomLeft.x;
        float y = Screen.height - topRight.y;
        float width = topRight.x - bottomLeft.x;
        float height = topRight.y - bottomLeft.y;

        return new Rect(x, y, width, height);
    }

    private void RefreshText()
    {
        if (currentBuilding == null)
        {
            return;
        }

        descriptionText.text = currentBuilding.GetFullDescription();
    }

    private void PlayPopupOpen()
    {
        if (uiAudioSource != null && popupOpenSound != null)
        {
            uiAudioSource.PlayOneShot(popupOpenSound);
        }
    }

    private void PlayButtonClick()
    {
        if (uiAudioSource != null && buttonClickSound != null)
        {
            uiAudioSource.PlayOneShot(buttonClickSound);
        }
    }
}
