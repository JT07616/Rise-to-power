using UnityEngine;

public class ActionIcons : MonoBehaviour
{
    public BuildingInfo contactBuilding;
    public Texture2D hammerIcon;
    public Texture2D gearIcon;
    public Texture2D workerIcon;
    public float iconSize = 110f;
    public float duration = 3f;
    public float rise = 60f;
    public Color playerColor = new Color(0.4f, 0.7f, 1f);
    public Color rivalColor = new Color(1f, 0.25f, 0.25f);

    private Texture2D[] icons = new Texture2D[5];
    private Vector3[] spots = new Vector3[5];
    private float[] times = new float[5];

    private int lastWorkers = -1;
    private int lastRivalWorkers;
    private int lastFactory;
    private int lastWarehouse;
    private int lastApartment;
    private bool wasProducing;
    private bool wasPlayerTurn;

    void Start()
    {
        for (int i = 0; i < times.Length; i++) times[i] = 1f;
    }

    void Update()
    {
        GameResources resources = GameResources.Instance;
        if (resources == null) return;

        OpponentResources ai = resources.Opponent;

        // prvi frame se samo zapamti stanje
        if (lastWorkers < 0)
        {
            lastWorkers = resources.radnici;
            lastRivalWorkers = ai.workers;
            lastFactory = ai.factoryUpgradeLevel;
            lastWarehouse = ai.warehouseUpgradeLevel;
            lastApartment = ai.apartmentUpgradeLevel;
            wasProducing = ai.IsProducing;
            return;
        }

        if (resources.radnici > lastWorkers && contactBuilding != null)
            Show(0, workerIcon, contactBuilding.LabelPosition);

        if (ai.factoryUpgradeLevel > lastFactory)
            Show(1, hammerIcon, RivalPosition(AiFacilityRole.Factory));
        else if (ai.IsProducing && !wasProducing)
            Show(1, gearIcon, RivalPosition(AiFacilityRole.Factory));

        if (ai.warehouseUpgradeLevel > lastWarehouse)
            Show(2, hammerIcon, RivalPosition(AiFacilityRole.Warehouse));

        if (ai.apartmentUpgradeLevel > lastApartment)
            Show(3, hammerIcon, RivalPosition(AiFacilityRole.Apartment));

        if (ai.workers > lastRivalWorkers)
            Show(4, workerIcon, RivalPosition(AiFacilityRole.WorkerContact));

        lastWorkers = resources.radnici;
        lastRivalWorkers = ai.workers;
        lastFactory = ai.factoryUpgradeLevel;
        lastWarehouse = ai.warehouseUpgradeLevel;
        lastApartment = ai.apartmentUpgradeLevel;
        wasProducing = ai.IsProducing;

        // rivalove ikone stoje cijeli njegov potez, gase se kad igrac dodje na red
        bool playerTurn = GameEventManager.IsPlayerTurn;
        bool turnStarted = playerTurn && !wasPlayerTurn;
        wasPlayerTurn = playerTurn;

        times[0] = Mathf.Min(1f, times[0] + Time.deltaTime / duration);

        for (int i = 1; i < times.Length; i++)
        {
            if (turnStarted) times[i] = Mathf.Min(times[i], 0.5f);
            if (playerTurn) times[i] = Mathf.Min(1f, times[i] + Time.deltaTime / duration);
        }
    }

    void Show(int slot, Texture2D shown, Vector3 position)
    {
        if (shown == null || position == Vector3.zero) return;

        icons[slot] = shown;
        spots[slot] = position;
        times[slot] = 0f;
    }

    Vector3 RivalPosition(AiFacilityRole role)
    {
        AiFacilityMarker facility = AiFacilityMarker.Find(role);
        return facility != null ? facility.LabelPosition : Vector3.zero;
    }

    void OnGUI()
    {
        if (Camera.main == null || GameEventManager.IsPauseMenuOpen ||
            GameEventManager.IsPopupOpen || DeliveryOrderManager.IsPopupOpen ||
            BuildingPopupUI.IsAnyOpen)
        {
            return;
        }

        int previousDepth = GUI.depth;
        GUI.depth = 1000;

        for (int i = 0; i < icons.Length; i++)
        {
            if (times[i] >= 1f || icons[i] == null) continue;

            Vector3 screen = Camera.main.WorldToScreenPoint(spots[i]);
            if (screen.z <= 0f) continue;

            // ikona lagano poskakuje da privuce pogled
            float bob = Mathf.Abs(Mathf.Sin(Time.time * 3f + i)) * 12f;
            Rect rect = new Rect(
                screen.x - iconSize * 0.5f,
                Screen.height - screen.y - iconSize - bob - rise * times[i],
                iconSize, iconSize);

            Color color = i == 0 ? playerColor : rivalColor;
            Color previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, 1f - times[i]);
            GUI.DrawTexture(rect, icons[i], ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        GUI.depth = previousDepth;
    }
}
