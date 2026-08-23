using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class BuildingSelector : MonoBehaviour
{
    private static BuildingSelector instance;

    public Camera mainCamera;
    public LayerMask clickableLayers = ~0;
    public BuildingPopupUI popupUI;

    [Header("Building shortcut images")]
    public Texture2D corruptOfficerShortcutImage;
    public Texture2D warehouseShortcutImage;
    public Texture2D policeStationShortcutImage;
    public Texture2D factoryShortcutImage;
    public Texture2D apartmentShortcutImage;
    public Texture2D workerContactShortcutImage;

    private BuildingInfo hoveredBuilding;
    private BuildingInfo selectedBuilding;
    private BuildingInfo[] labeledBuildings;
    private Texture2D shortcutButtonTexture;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        labeledBuildings = FindObjectsByType<BuildingInfo>(FindObjectsSortMode.None);
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (shortcutButtonTexture != null)
        {
            Destroy(shortcutButtonTexture);
        }
    }

    void Update()
    {
        if (Mouse.current == null)
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

        if (!GameEventManager.CanPlayerAct)
        {
            return;
        }

        if (popupUI != null && popupUI.IsOpen)
        {
            return;
        }

        if (popupUI != null && selectedBuilding != null)
        {
            selectedBuilding.ClearColor();
            selectedBuilding = null;
            hoveredBuilding = null;
        }

        HandleHover();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            if (!IsPointerOverMapLabel(pointerPosition) &&
                !DeliveryOrderManager.IsPointerOverMapLabel(pointerPosition) &&
                !IsPointerOverShortcutBar(pointerPosition))
            {
                HandleClick();
            }
        }
    }

    public static bool IsPointerOverShortcutBar(Vector2 screenPosition)
    {
        if (instance == null || instance.labeledBuildings == null)
        {
            return false;
        }

        int shortcutCount = instance.GetShortcutBuildings().Count;
        if (shortcutCount == 0)
        {
            return false;
        }

        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        return GetShortcutBarRect(shortcutCount).Contains(guiPosition);
    }

    public static bool IsPointerOverMapLabel(Vector2 screenPosition)
    {
        if (instance == null || instance.mainCamera == null || instance.labeledBuildings == null)
        {
            return false;
        }

        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        foreach (BuildingInfo building in instance.labeledBuildings)
        {
            if (building == null || !IsLabeledBuilding(building))
            {
                continue;
            }

            Vector3 screen = instance.mainCamera.WorldToScreenPoint(building.LabelPosition);
            if (screen.z <= 0f)
            {
                continue;
            }

            Rect rect = new Rect(screen.x - 65f, Screen.height - screen.y - 15f, 130f, 30f);
            if (rect.Contains(guiPosition))
            {
                return true;
            }
        }

        return false;
    }

    void HandleHover()
    {
        BuildingInfo building = GetBuildingUnderMouse();

        if (building == hoveredBuilding)
        {
            return;
        }

        if (hoveredBuilding != null && hoveredBuilding != selectedBuilding)
        {
            hoveredBuilding.ClearColor();
        }

        hoveredBuilding = building;

        if (hoveredBuilding != null && hoveredBuilding != selectedBuilding)
        {
            hoveredBuilding.ShowHover();
        }
    }

    void HandleClick()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        DeliveryOrderTarget deliveryTarget = GetDeliveryTargetUnderMouse();
        if (deliveryTarget != null)
        {
            deliveryTarget.OpenOrder();
            return;
        }

        if (hoveredBuilding == null)
        {
            return;
        }

        if (IsLabeledBuilding(hoveredBuilding))
        {
            FocusAndShow(hoveredBuilding);
            return;
        }

        if (selectedBuilding != null)
        {
            selectedBuilding.ClearColor();
        }

        selectedBuilding = hoveredBuilding;
        selectedBuilding.ShowSelected();

        if (popupUI != null)
        {
            popupUI.Show(selectedBuilding);
        }
    }

    void OnGUI()
    {
        if (labeledBuildings == null)
        {
            return;
        }

        int previousDepth = GUI.depth;
        GUI.depth = 100;

        if (mainCamera != null && !GameEventManager.IsPauseMenuOpen &&
            !GameEventManager.IsPopupOpen && !DeliveryOrderManager.IsPopupOpen &&
            GameEventManager.CanPlayerAct)
        {
            DrawBuildingLabels();
        }

        DrawBuildingShortcuts();

        GUI.depth = previousDepth;
    }

    void DrawBuildingLabels()
    {
        foreach (BuildingInfo building in labeledBuildings)
        {
            if (building == null || !IsLabeledBuilding(building))
            {
                continue;
            }

            Vector3 screen = mainCamera.WorldToScreenPoint(building.LabelPosition);
            if (screen.z <= 0f)
            {
                continue;
            }

            Texture2D labelImage = building.CurrentMapLabelImage;
            bool clicked;

            if (labelImage != null)
            {
                Rect imageRect = new Rect(screen.x - 72.5f, Screen.height - screen.y - 25f, 145f, 50f);
                clicked = GUI.Button(imageRect, labelImage, GUIStyle.none);
            }
            else
            {
                Rect textRect = new Rect(screen.x - 65f, Screen.height - screen.y - 15f, 130f, 30f);
                clicked = GUI.Button(textRect, GetBuildingLabel(building));
            }

            if (clicked)
            {
                FocusAndShow(building);
            }
        }
    }

    void DrawBuildingShortcuts()
    {
        List<BuildingInfo> buildings = GetShortcutBuildings();
        if (buildings.Count == 0)
        {
            return;
        }

        if (shortcutButtonTexture == null)
        {
            shortcutButtonTexture = CreateShortcutButtonTexture();
        }

        Rect bar = GetShortcutBarRect(buildings.Count);
        const float buttonSize = 50f;
        const float gap = 10f;
        GUIStyle buttonStyle = new GUIStyle(GUIStyle.none)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            hover = { textColor = Color.white },
            active = { textColor = Color.white }
        };
        GUIStyle tooltipStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11
        };

        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingInfo building = buildings[i];
            Rect buttonRect = new Rect(
                bar.x + i * (buttonSize + gap),
                bar.y,
                buttonSize,
                buttonSize);
            bool hovered = buttonRect.Contains(Event.current.mousePosition);
            Texture2D shortcutImage = GetBuildingShortcutImage(building);
            bool clicked;
            if (shortcutImage != null)
            {
                clicked = GUI.Button(buttonRect, shortcutImage, GUIStyle.none);
            }
            else
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.72f);
                GUI.DrawTexture(
                    new Rect(buttonRect.x + 2f, buttonRect.y + 3f, buttonSize, buttonSize),
                    shortcutButtonTexture);
                GUI.color = hovered
                    ? new Color(0.2f, 0.75f, 1f, 1f)
                    : new Color(0.1f, 0.38f, 0.72f, 0.96f);
                GUI.DrawTexture(buttonRect, shortcutButtonTexture);
                GUI.color = previousColor;
                clicked = GUI.Button(buttonRect, GetBuildingShortcut(building), buttonStyle);
            }

            if (clicked &&
                Vector2.Distance(Event.current.mousePosition, buttonRect.center) <= buttonSize * 0.5f)
            {
                FocusOnly(building);
            }

            if (hovered)
            {
                GUI.Box(
                    new Rect(buttonRect.center.x - 75f, buttonRect.y - 30f, 150f, 24f),
                    building.buildingName,
                    tooltipStyle);
            }
        }
    }

    Texture2D GetBuildingShortcutImage(BuildingInfo building)
    {
        if (building.buildingName == "Apartment")
        {
            return apartmentShortcutImage;
        }

        if (building.buildingName == "Police Station")
        {
            return policeStationShortcutImage;
        }

        switch (building.buildingRole)
        {
            case BuildingRole.Factory:
                return factoryShortcutImage;
            case BuildingRole.Warehouse:
                return warehouseShortcutImage;
            case BuildingRole.WorkerContact:
                return workerContactShortcutImage;
            case BuildingRole.CorruptOfficer:
                return corruptOfficerShortcutImage;
            default:
                return null;
        }
    }

    List<BuildingInfo> GetShortcutBuildings()
    {
        List<BuildingInfo> buildings = new List<BuildingInfo>();
        foreach (BuildingInfo building in labeledBuildings)
        {
            if (building != null && IsLabeledBuilding(building))
            {
                buildings.Add(building);
            }
        }

        buildings.Sort((left, right) =>
        {
            int roleComparison = GetShortcutOrder(left).CompareTo(GetShortcutOrder(right));
            return roleComparison != 0
                ? roleComparison
                : string.Compare(left.buildingName, right.buildingName, StringComparison.Ordinal);
        });
        return buildings;
    }

    static Rect GetShortcutBarRect(int shortcutCount)
    {
        const float buttonSize = 50f;
        const float gap = 10f;
        float width = shortcutCount * buttonSize + (shortcutCount - 1) * gap;
        return new Rect((Screen.width - width) * 0.5f, Screen.height - buttonSize - 12f, width, buttonSize);
    }

    static int GetShortcutOrder(BuildingInfo building)
    {
        switch (building.buildingRole)
        {
            case BuildingRole.Factory:
                return 0;
            case BuildingRole.Warehouse:
                return 1;
            case BuildingRole.WorkerContact:
                return 2;
            case BuildingRole.CorruptOfficer:
                return 4;
            default:
                return building.buildingName == "Apartment" ? 3 : 5;
        }
    }

    static string GetBuildingShortcut(BuildingInfo building)
    {
        if (building.buildingName == "Apartment")
        {
            return "APT";
        }

        if (building.buildingName == "Police Station")
        {
            return "POL";
        }

        switch (building.buildingRole)
        {
            case BuildingRole.Factory:
                return "FAC";
            case BuildingRole.Warehouse:
                return "WH";
            case BuildingRole.WorkerContact:
                return "WRK";
            case BuildingRole.CorruptOfficer:
                return "CROSS";
            default:
                return "LOC";
        }
    }

    Texture2D CreateShortcutButtonTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "BuildingShortcutButton",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float edge = radius - Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(edge));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    void FocusOnly(BuildingInfo building)
    {
        SimpleStrategyCamera camera = mainCamera != null
            ? mainCamera.GetComponent<SimpleStrategyCamera>()
            : null;
        if (camera != null)
        {
            camera.FocusOn(building.LabelPosition, null);
        }
    }

    void FocusAndShow(BuildingInfo building)
    {
        if (building == null || popupUI == null)
        {
            return;
        }

        if (popupUI.IsOpen)
        {
            popupUI.ClosePanel();
        }

        SimpleStrategyCamera strategyCamera = mainCamera != null
            ? mainCamera.GetComponent<SimpleStrategyCamera>()
            : null;

        if (strategyCamera != null)
        {
            strategyCamera.FocusOn(building.LabelPosition, () => SelectAndShow(building));
            return;
        }

        SelectAndShow(building);
    }

    void SelectAndShow(BuildingInfo building)
    {
        if (building == null || popupUI == null)
        {
            return;
        }

        if (selectedBuilding != null && selectedBuilding != building)
        {
            selectedBuilding.ClearColor();
        }

        selectedBuilding = building;
        selectedBuilding.ShowSelected();
        popupUI.Show(selectedBuilding);
    }

    static bool IsLabeledBuilding(BuildingInfo building)
    {
        return building != null && !building.IsStoryLocked &&
               (building.buildingRole != BuildingRole.General ||
                building.buildingName == "Apartment" ||
                building.buildingName == "Police Station");
    }

    static string GetBuildingLabel(BuildingInfo building)
    {
        if (building.buildingName == "Apartment")
        {
            return "APARTMENT";
        }

        if (building.buildingName == "Police Station")
        {
            return "POLICE STATION";
        }

        switch (building.buildingRole)
        {
            case BuildingRole.Factory:
                return "FACTORY";
            case BuildingRole.WorkerContact:
                return "HIRE WORKERS";
            case BuildingRole.Warehouse:
                return "WAREHOUSE";
            case BuildingRole.CorruptOfficer:
                return "LEDGER CROSS";
            default:
                return "BUILDING";
        }
    }

    BuildingInfo GetBuildingUnderMouse()
    {
        if (mainCamera == null)
        {
            return null;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, clickableLayers))
        {
            BuildingInfo building = hit.collider.GetComponentInParent<BuildingInfo>();
            return building != null && !building.IsStoryLocked ? building : null;
        }

        return null;
    }

    DeliveryOrderTarget GetDeliveryTargetUnderMouse()
    {
        if (mainCamera == null)
        {
            return null;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, clickableLayers))
        {
            return hit.collider.GetComponentInParent<DeliveryOrderTarget>();
        }

        return null;
    }
}
