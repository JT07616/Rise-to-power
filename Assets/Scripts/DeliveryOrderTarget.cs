using UnityEngine;

public class DeliveryOrderTarget : MonoBehaviour
{
    private DeliveryOrderManager owner;
    private int orderIndex;
    private Renderer targetRenderer;
    private Color originalColor;
    private bool hasOriginalColor;

    public Vector3 LabelPosition
    {
        get
        {
            return targetRenderer != null
                ? targetRenderer.bounds.center + Vector3.up * targetRenderer.bounds.extents.y
                : transform.position;
        }
    }

    public bool TryGetDistrictControl(
        out string districtName,
        out int controlScore,
        out int controlLimit)
    {
        TerritoryHouse house = GetTerritoryHouse(false);
        if (TerritoryDistrictManager.TryGetDistrictStatus(
                house,
                out districtName,
                out controlScore,
                out controlLimit))
        {
            return true;
        }

        districtName = "Unknown district";
        controlScore = house != null && house.IsPlayerOwned ? 3 :
            house != null && house.IsAiOwned ? -3 : 0;
        controlLimit = 3;
        return false;
    }

    public void Configure(DeliveryOrderManager orderOwner, int index)
    {
        owner = orderOwner;
        orderIndex = index;
        targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        if (targetRenderer != null)
        {
            originalColor = targetRenderer.material.color;
            hasOriginalColor = true;
            targetRenderer.material.color = new Color(1f, 0.72f, 0.12f);
        }
    }

    public void OpenOrder()
    {
        if (owner != null)
        {
            owner.OpenOrder(orderIndex);
        }
    }

    public void MarkCompleted()
    {
        RestoreColor();
    }

    public bool RegisterCompletedDelivery(
        TerritoryOwner deliveryOwner = TerritoryOwner.Player,
        int capturePoints = 1)
    {
        TerritoryHouse house = GetTerritoryHouse(true);
        if (house == null)
        {
            return false;
        }

        bool captured = house.RegisterDelivery(deliveryOwner, capturePoints);

        if (targetRenderer != null && house.Owner != TerritoryOwner.Neutral)
        {
            RestoreColor();
            targetRenderer.material.color = house.IsPlayerOwned
                ? new Color(0.1f, 0.35f, 1f)
                : new Color(0.9f, 0.1f, 0.1f);
        }

        return captured;
    }

    public void DisableInteraction()
    {
        owner = null;
    }

    public void Deactivate()
    {
        RestoreColor();
        Destroy(this);
    }

    void OnDestroy()
    {
        RestoreColor();
    }

    private void RestoreColor()
    {
        if (hasOriginalColor && targetRenderer != null)
        {
            targetRenderer.material.color = originalColor;
            hasOriginalColor = false;
        }
    }

    private TerritoryHouse GetTerritoryHouse(bool create)
    {
        GameObject houseObject = targetRenderer != null ? targetRenderer.gameObject : gameObject;
        TerritoryHouse house = houseObject.GetComponent<TerritoryHouse>();
        if (house == null && create)
        {
            house = houseObject.AddComponent<TerritoryHouse>();
        }

        return house;
    }
}

public enum TerritoryOwner
{
    Neutral,
    Player,
    AI
}

public enum AiFacilityRole
{
    Factory,
    Warehouse,
    WorkerContact,
    Apartment
}

public class AiFacilityMarker : MonoBehaviour
{
    [SerializeField] private AiFacilityRole role;
    private Renderer facilityRenderer;
    private Texture2D[] mapLabelImagesByUpgradeLevel;

    public AiFacilityRole Role
    {
        get { return role; }
    }

    public Vector3 LabelPosition
    {
        get
        {
            return facilityRenderer != null
                ? facilityRenderer.bounds.center + Vector3.up * facilityRenderer.bounds.extents.y
                : transform.position + Vector3.up * 2f;
        }
    }

    public void Configure(AiFacilityRole facilityRole, Texture2D[] labelImagesByUpgradeLevel = null)
    {
        role = facilityRole;
        mapLabelImagesByUpgradeLevel = labelImagesByUpgradeLevel;
        facilityRenderer = GetComponent<Renderer>();
        if (facilityRenderer == null)
        {
            facilityRenderer = GetComponentInChildren<Renderer>();
        }
    }

    public static AiFacilityMarker Find(AiFacilityRole facilityRole)
    {
        AiFacilityMarker[] facilities = FindObjectsByType<AiFacilityMarker>(FindObjectsSortMode.None);
        foreach (AiFacilityMarker facility in facilities)
        {
            if (facility != null && facility.role == facilityRole)
            {
                return facility;
            }
        }

        return null;
    }

    void OnGUI()
    {
        if (GameEventManager.IsPauseMenuOpen || GameEventManager.IsPopupOpen ||
            DeliveryOrderManager.IsPopupOpen || BuildingPopupUI.IsAnyOpen || Camera.main == null)
        {
            return;
        }

        int previousDepth = GUI.depth;
        GUI.depth = 1000;
        try
        {
        Vector3 screen = Camera.main.WorldToScreenPoint(LabelPosition);
        if (screen.z <= 0f)
        {
            return;
        }

        Texture2D labelImage = GetMapLabelImage();
        if (labelImage != null)
        {
            Rect imageRect = new Rect(screen.x - 72.5f, Screen.height - screen.y - 25f, 145f, 50f);
            GUI.DrawTexture(imageRect, labelImage, ScaleMode.ScaleToFit, true);
            return;
        }

        string label = role == AiFacilityRole.WorkerContact
            ? "VOLKOV WORKERS"
            : $"VOLKOV {role.ToString().ToUpperInvariant()}";
        Rect rect = new Rect(screen.x - 65f, Screen.height - screen.y - 15f, 130f, 30f);
        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 0.45f, 0.45f);
        GUI.Box(rect, label);
        GUI.color = previousColor;
        }
        finally
        {
            GUI.depth = previousDepth;
        }
    }

    private Texture2D GetMapLabelImage()
    {
        if (mapLabelImagesByUpgradeLevel == null || mapLabelImagesByUpgradeLevel.Length == 0 ||
            GameResources.Instance == null)
        {
            return null;
        }

        int upgradeLevel;
        if (role == AiFacilityRole.Factory)
        {
            upgradeLevel = GameResources.Instance.Opponent.factoryUpgradeLevel;
        }
        else if (role == AiFacilityRole.Warehouse)
        {
            upgradeLevel = GameResources.Instance.Opponent.warehouseUpgradeLevel;
        }
        else if (role == AiFacilityRole.Apartment)
        {
            upgradeLevel = GameResources.Instance.Opponent.apartmentUpgradeLevel;
        }
        else if (role == AiFacilityRole.WorkerContact)
        {
            upgradeLevel = 0;
        }
        else
        {
            return null;
        }

        int level = Mathf.Clamp(
            upgradeLevel,
            0,
            mapLabelImagesByUpgradeLevel.Length - 1);
        return mapLabelImagesByUpgradeLevel[level];
    }
}

public class TerritoryHouse : MonoBehaviour
{
    [SerializeField] private TerritoryOwner owner = TerritoryOwner.Neutral;
    [SerializeField, Min(0)] private int playerCaptureProgress;
    [SerializeField, Min(0)] private int aiCaptureProgress;

    public TerritoryOwner Owner
    {
        get { return owner; }
    }

    public bool IsPlayerOwned
    {
        get { return owner == TerritoryOwner.Player; }
    }

    public bool IsAiOwned
    {
        get { return owner == TerritoryOwner.AI; }
    }

    public int GetCaptureProgress(TerritoryOwner side)
    {
        return side == TerritoryOwner.Player ? playerCaptureProgress :
               side == TerritoryOwner.AI ? aiCaptureProgress : 0;
    }

    public int GetCaptureRequirement(TerritoryOwner side)
    {
        if (side == TerritoryOwner.Neutral || owner == side)
        {
            return 0;
        }

        return owner == TerritoryOwner.Neutral ? 6 : 10;
    }

    public void InitializeOwner(TerritoryOwner initialOwner)
    {
        if (owner == TerritoryOwner.Neutral && playerCaptureProgress == 0 && aiCaptureProgress == 0)
        {
            owner = initialOwner;
        }
    }

    public void SetOwner(TerritoryOwner newOwner)
    {
        owner = newOwner;
        playerCaptureProgress = 0;
        aiCaptureProgress = 0;
    }

    public bool RegisterDelivery(TerritoryOwner side, int capturePoints = 1)
    {
        if (side == TerritoryOwner.Neutral)
        {
            return false;
        }

        if (TerritoryDistrictManager.TryRegisterDelivery(this, side, out bool districtCaptured))
        {
            GameEventManager.RefreshTerritoryInfluence();
            return districtCaptured;
        }

        if (owner == side)
        {
            playerCaptureProgress = 0;
            aiCaptureProgress = 0;
            return false;
        }

        if (side == TerritoryOwner.Player)
        {
            playerCaptureProgress += Mathf.Max(1, capturePoints);
            aiCaptureProgress = 0;
            if (playerCaptureProgress < GetCaptureRequirement(side))
            {
                return false;
            }
        }
        else
        {
            aiCaptureProgress += Mathf.Max(1, capturePoints);
            playerCaptureProgress = 0;
            if (aiCaptureProgress < GetCaptureRequirement(side))
            {
                return false;
            }
        }

        owner = side;
        playerCaptureProgress = 0;
        aiCaptureProgress = 0;
        return true;
    }
}
