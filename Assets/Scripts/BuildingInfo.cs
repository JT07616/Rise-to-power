using UnityEngine;

public class BuildingInfo : MonoBehaviour
{
    public string buildingName = "Building";

    [TextArea(2, 5)]
    public string description = "Short description of this building.";

    public Color hoverColor = Color.yellow;
    public Color selectedColor = Color.blue;

    private Renderer[] renderers;
    private Color[] originalColors;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }
    }

    public void ShowHover()
    {
        SetColor(hoverColor);
    }

    public void ShowSelected()
    {
        SetColor(selectedColor);
    }

    public void ClearColor()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].material.color = originalColors[i];
            }
        }
    }

    private void SetColor(Color color)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].material.color = color;
            }
        }
    }
}