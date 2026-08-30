using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingPopupUI : MonoBehaviour
{
    private const float HudBarHeight = 48f;
    private const float ActivityPanelHeight = 210f;
    private const float ScreenMargin = 12f;
    private const float PanelGap = 12f;
    private static readonly Vector2 PanelSize = new Vector2(450f, 550f);

    public static bool IsAnyOpen { get; private set; }

    public GameObject panel;
    public Button closeButton;

    public TMP_Text titleText;
    public TMP_Text descriptionText;

    [Header("Background")]
    public Image backgroundImage;
    public Sprite defaultBackground;
    public Sprite corruptOfficerBackground;

    [Header("Close button")]
    public Sprite closeButtonSprite;

    [Header("Factory action buttons")]
    public Texture2D factoryProduceButtonImage;
    public Texture2D factoryMoveButtonImage;
    public Texture2D warehouseMoveButtonImage;
    public Texture2D factoryUpgradeButtonImage;

    [Header("Corrupt officer action buttons")]
    public Texture2D corruptOfficerTrustButtonImage;
    public Texture2D corruptOfficerProtectionButtonImage;
    public Texture2D corruptOfficerBribeButtonImage;

    [Header("First contact action buttons")]
    public Texture2D hireWorkerButtonImage;
    public Texture2D fireWorkerButtonImage;
    public Texture2D callInFavorButtonImage;

    [Header("Apartment action buttons")]
    public Texture2D buyApartmentButtonImage;
    public Texture2D layLowButtonImage;

    [Header("Police station action buttons")]
    public Texture2D policeAmbushButtonImage;

    [Header("Audio")]
    public AudioSource uiAudioSource;
    public AudioClip popupOpenSound;
    public AudioClip buttonClickSound;

    private BuildingInfo currentBuilding;
    private int layoutScreenWidth;
    private int layoutScreenHeight;

    public bool IsOpen
    {
        get { return panel != null && panel.activeSelf; }
    }

    void Start()
    {
        ConfigurePanelLayout();

        if (backgroundImage == null && panel != null)
        {
            backgroundImage = panel.GetComponent<Image>();
        }

        panel.SetActive(false);
        IsAnyOpen = false;

        if (closeButton != null)
        {
            if (closeButton.image != null && closeButtonSprite != null)
            {
                closeButton.image.sprite = closeButtonSprite;
                closeButton.image.type = Image.Type.Simple;
                closeButton.image.preserveAspect = true;
                closeButton.image.color = Color.white;
            }

            TMP_Text closeButtonText = closeButton.GetComponentInChildren<TMP_Text>();
            if (closeButtonText != null)
            {
                closeButtonText.text = "";
            }

            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    void Update()
    {
        if (layoutScreenWidth != Screen.width || layoutScreenHeight != Screen.height)
        {
            ConfigurePanelLayout();
        }

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
        RefreshBackground();
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
        bool isPoliceStation = currentBuilding != null && currentBuilding.buildingName == "Police Station";
        if (panel == null || !panel.activeSelf || currentBuilding == null ||
            (!currentBuilding.hasProductionControls && !isPoliceStation))
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

        int previousDepth = GUI.depth;
        GUI.depth = -800;
        try
        {

        Rect panelRect = GetPanelScreenRect();
        float buttonHeight = 38f;
        float gap = 12f;
        int buttonCount = GetActionButtonCount();
        float buttonWidth = Mathf.Min(230f, (panelRect.width - 40f - gap * (buttonCount - 1)) / buttonCount);
        float totalWidth = buttonWidth * buttonCount + gap * (buttonCount - 1);
        float x = panelRect.x + (panelRect.width - totalWidth) / 2f;
        float y = panelRect.yMax - buttonHeight - 18f;

        if (isPoliceStation)
        {
            if (DrawActionButton(
                    new Rect(x, y, buttonWidth, buttonHeight),
                    AmbushTrapManager.GetPlayerAmbushButtonLabel(),
                    policeAmbushButtonImage,
                    AmbushTrapManager.CanPlayerSetAmbush))
            {
                PlayButtonClick();
                AmbushTrapManager.TrySetPlayerAmbush();
                RefreshText();
            }

            return;
        }

        if (currentBuilding.buildingRole == BuildingRole.CorruptOfficer)
        {
            if (!currentBuilding.IsUnlocked())
            {
                if (DrawActionButton(
                        new Rect(x, y, buttonWidth, buttonHeight),
                        currentBuilding.GetPurchaseButtonLabel(),
                        corruptOfficerTrustButtonImage,
                        currentBuilding.CanPurchase()))
                {
                    PlayButtonClick();
                    currentBuilding.Purchase();
                    RefreshText();
                }

                return;
            }

            if (DrawActionButton(
                    new Rect(x, y, buttonWidth, buttonHeight),
                    currentBuilding.GetRiskProtectionButtonLabel(),
                    corruptOfficerProtectionButtonImage,
                    currentBuilding.CanBuyRiskProtection()))
            {
                PlayButtonClick();
                currentBuilding.BuyRiskProtection();
                RefreshText();
            }

            float raidButtonX = x + buttonWidth + gap;
            if (DrawActionButton(
                    new Rect(raidButtonX, y, buttonWidth, buttonHeight),
                    currentBuilding.GetRaidBribeButtonLabel(),
                    corruptOfficerBribeButtonImage,
                    currentBuilding.CanBuyRaidBribe()))
            {
                PlayButtonClick();
                currentBuilding.BuyRaidBribe();
                RefreshText();
            }

            return;
        }

        if (!currentBuilding.IsUnlocked())
        {
            Texture2D purchaseButtonImage = currentBuilding.buildingName == "Apartment"
                ? buyApartmentButtonImage
                : null;
            if (DrawActionButton(
                    new Rect(x, y, buttonWidth, buttonHeight),
                    currentBuilding.GetPurchaseButtonLabel(),
                    purchaseButtonImage,
                    currentBuilding.CanPurchase()))
            {
                PlayButtonClick();
                currentBuilding.Purchase();
                RefreshText();
            }

            return;
        }

        int buttonIndex = 0;
        if (currentBuilding.producesGoods)
        {
            Texture2D buttonImage = currentBuilding.buildingRole == BuildingRole.Factory
                ? factoryProduceButtonImage
                : null;
            if (DrawActionButton(
                    new Rect(x, y, buttonWidth, buttonHeight),
                    currentBuilding.GetProduceGoodsButtonLabel(),
                    buttonImage,
                    currentBuilding.CanProduceGoods()))
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
            float buttonX = x + buttonIndex * (buttonWidth + gap);
            Texture2D buttonImage = currentBuilding.buildingRole == BuildingRole.Factory
                ? factoryMoveButtonImage
                : warehouseMoveButtonImage;
            if (DrawActionButton(
                    new Rect(buttonX, y, buttonWidth, buttonHeight),
                    currentBuilding.GetMoveGoodsButtonLabel(),
                    buttonImage,
                    currentBuilding.CanMoveGoods()))
            {
                PlayButtonClick();
                currentBuilding.MoveGoodsToWarehouse();
                RefreshText();
            }
            buttonIndex++;
        }

        if (currentBuilding.showIncreaseAction)
        {
            float buttonX = x + buttonIndex * (buttonWidth + gap);
            Texture2D buttonImage = currentBuilding.buildingRole == BuildingRole.WorkerContact
                ? hireWorkerButtonImage
                : currentBuilding.buildingName == "Apartment" ? layLowButtonImage : null;
            if (DrawActionButton(
                    new Rect(buttonX, y, buttonWidth, buttonHeight),
                    currentBuilding.GetIncreaseButtonLabel(),
                    buttonImage,
                    currentBuilding.CanIncreaseProduction()))
            {
                PlayButtonClick();
                currentBuilding.IncreaseProduction();
                RefreshText();
            }
            buttonIndex++;
        }

        if (currentBuilding.showDecreaseAction)
        {
            float buttonX = x + buttonIndex * (buttonWidth + gap);
            Texture2D buttonImage = currentBuilding.buildingRole == BuildingRole.WorkerContact
                ? fireWorkerButtonImage
                : null;
            if (DrawActionButton(
                    new Rect(buttonX, y, buttonWidth, buttonHeight),
                    currentBuilding.GetDecreaseButtonLabel(),
                    buttonImage,
                    currentBuilding.CanDecreaseProduction()))
            {
                PlayButtonClick();
                currentBuilding.DecreaseProduction();
                RefreshText();
            }
            buttonIndex++;
        }

        if (currentBuilding.buildingRole == BuildingRole.WorkerContact)
        {
            float buttonX = x + buttonIndex * (buttonWidth + gap);
            if (DrawActionButton(
                    new Rect(buttonX, y, buttonWidth, buttonHeight),
                    currentBuilding.GetEmergencyActionButtonLabel(),
                    callInFavorButtonImage,
                    currentBuilding.CanBuyEmergencyAction()))
            {
                PlayButtonClick();
                currentBuilding.BuyEmergencyAction();
                RefreshText();
            }
            buttonIndex++;
        }

        if (currentBuilding.hasUpgrade)
        {
            float buttonX = x + buttonIndex * (buttonWidth + gap);
            if (DrawActionButton(
                    new Rect(buttonX, y, buttonWidth, buttonHeight),
                    currentBuilding.GetUpgradeButtonLabel(),
                    factoryUpgradeButtonImage,
                    currentBuilding.CanUpgrade()))
            {
                PlayButtonClick();
                currentBuilding.Upgrade();
                RefreshText();
            }
        }

        GUI.enabled = true;
        }
        finally
        {
            GUI.depth = previousDepth;
        }
    }

    private void ConfigurePanelLayout()
    {
        layoutScreenWidth = Screen.width;
        layoutScreenHeight = Screen.height;

        if (panel == null)
        {
            return;
        }

        Canvas popupCanvas = GetComponent<Canvas>();
        if (popupCanvas != null)
        {
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = 800;
        }

        RectTransform rectTransform = panel.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        float panelTop = HudBarHeight + ScreenMargin + ActivityPanelHeight + PanelGap;
        float availableHeight = Mathf.Max(1f, Screen.height - panelTop - ScreenMargin);
        float scale = Mathf.Min(1f, availableHeight / PanelSize.y);

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(ScreenMargin, -panelTop);
        rectTransform.sizeDelta = PanelSize;
        rectTransform.localScale = new Vector3(scale, scale, 1f);
        rectTransform.SetAsLastSibling();
    }

    private bool DrawActionButton(Rect rect, string label, Texture2D image, bool enabled)
    {
        if (image == null)
        {
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, label);
            GUI.enabled = true;
            return clicked;
        }

        Color previousColor = GUI.color;
        GUI.color = enabled ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.8f);
        GUI.DrawTexture(rect, image, ScaleMode.StretchToFill, true);
        GUI.color = previousColor;

        GUI.enabled = true;
        return enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private int GetActionButtonCount()
    {
        if (currentBuilding != null && currentBuilding.buildingName == "Police Station")
        {
            return 1;
        }

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

    private void RefreshBackground()
    {
        if (backgroundImage == null || currentBuilding == null)
        {
            return;
        }

        Sprite background = currentBuilding.buildingRole == BuildingRole.CorruptOfficer
            ? corruptOfficerBackground
            : defaultBackground;
        if (background == null)
        {
            return;
        }

        backgroundImage.sprite = background;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = false;
        backgroundImage.color = Color.white;
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
