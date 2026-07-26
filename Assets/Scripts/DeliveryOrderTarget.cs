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

    public int CompletedDeliveryCount
    {
        get
        {
            TerritoryHouse house = GetTerritoryHouse(false);
            return house != null ? house.completedDeliveries : 0;
        }
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

    public bool RegisterCompletedDelivery()
    {
        TerritoryHouse house = GetTerritoryHouse(true);
        if (house == null)
        {
            return false;
        }

        bool captured = house.RegisterDelivery();
        if (house.IsPlayerOwned && targetRenderer != null)
        {
            RestoreColor();
            targetRenderer.material.color = new Color(0.1f, 0.35f, 1f);
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

public class TerritoryHouse : MonoBehaviour
{
    public int completedDeliveries;

    public bool IsPlayerOwned
    {
        get { return completedDeliveries >= 2; }
    }

    public bool RegisterDelivery()
    {
        bool wasPlayerOwned = IsPlayerOwned;
        completedDeliveries++;
        return !wasPlayerOwned && IsPlayerOwned;
    }
}
