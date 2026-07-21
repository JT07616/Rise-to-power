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
}
