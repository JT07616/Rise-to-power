using System.Collections.Generic;
using UnityEngine;

public class DeliveryOrderManager : MonoBehaviour
{
    public static bool IsPopupOpen { get; private set; }

    [Header("Orders")]
    [Min(1)] public int offersPerDay = 4;
    public int minRisk = 4;
    public int maxRisk = 30;
    [Min(1)] public int minGoodsPerDelivery = 5;
    [Min(1)] public int maxGoodsPerDelivery = 45;
    [Min(1)] public int pricePerGram = 10;
    [Min(0f)] public float distancePricePerUnit = 0.5f;
    [Min(0.1f)] public float deliverySpeed = 8f;
    [Min(1f)] public float minimumDeliverySeconds = 5f;
    [Min(0f)] public float handlingSecondsPerGram = 0.1f;

    private readonly List<DeliveryOrder> orders = new List<DeliveryOrder>();
    private readonly List<DeliveryOrder> activeDeliveries = new List<DeliveryOrder>();
    private int generatedDay = -1;
    private int selectedOrder = -1;
    private bool showOffers;

    private static readonly string[] AliasPrefixes =
    {
        "Neon", "Velvet", "Silent", "Midnight", "Chrome", "Crimson",
        "Hollow", "Static", "Black", "Silver", "Ash", "Frost"
    };

    private static readonly string[] AliasCodenames =
    {
        "Jackal", "Crow", "Viper", "Fox", "Raven", "Moth",
        "Wolf", "Cobra", "Finch", "Wasp", "Lynx", "Hound"
    };

    private class DeliveryOrder
    {
        public string customerName;
        public int reward;
        public int risk;
        public int grams;
        public float distance;
        public float duration;
        public float finishTime;
        public bool inProgress;
        public bool completed;
        public DeliveryOrderTarget target;
    }

    void Update()
    {
        CompleteFinishedDeliveries();

        if (!GameEventManager.IsPlayerTurn)
        {
            ClosePopups();
            return;
        }

        if (generatedDay != GameEventManager.CurrentDay)
        {
            GenerateDailyOrders();
        }
    }

    void OnDestroy()
    {
        ClearOrders();
        IsPopupOpen = false;
    }

    void OnGUI()
    {
        if (!GameEventManager.IsPlayerTurn || GameEventManager.IsPauseMenuOpen ||
            GameEventManager.IsPopupOpen)
        {
            return;
        }

        DrawOrderLabels();

        if (!showOffers && selectedOrder < 0)
        {
            return;
        }

        int previousDepth = GUI.depth;
        GUI.depth = -900;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (showOffers)
        {
            Rect rect = new Rect((Screen.width - 650f) / 2f, (Screen.height - 390f) / 2f, 650f, 390f);
            GUI.ModalWindow(771001, rect, DrawOffersWindow, "Today's delivery requests");
        }
        else if (selectedOrder >= 0 && selectedOrder < orders.Count)
        {
            Rect rect = new Rect((Screen.width - 440f) / 2f, (Screen.height - 300f) / 2f, 440f, 300f);
            GUI.ModalWindow(771002, rect, DrawOrderWindow, "Delivery request");
        }

        GUI.depth = previousDepth;
    }

    public void OpenOrder(int orderIndex)
    {
        if (!GameEventManager.CanPlayerAct || orderIndex < 0 || orderIndex >= orders.Count ||
            orders[orderIndex].completed || orders[orderIndex].inProgress)
        {
            return;
        }

        selectedOrder = orderIndex;
        showOffers = false;
        IsPopupOpen = true;
    }

    private void GenerateDailyOrders()
    {
        ClearOrders();
        generatedDay = GameEventManager.CurrentDay;

        List<MeshCollider> candidates = FindDeliveryCandidates();
        Shuffle(candidates);
        List<string> customerNames = BuildCustomerAliases();
        Shuffle(customerNames);
        Vector3 factoryPosition = GetFactoryPosition();

        int offerCount = Mathf.Min(offersPerDay, candidates.Count);
        for (int i = 0; i < offerCount; i++)
        {
            MeshCollider candidate = candidates[i];
            DeliveryOrderTarget target = candidate.gameObject.AddComponent<DeliveryOrderTarget>();
            target.Configure(this, i);

            int grams = Random.Range(minGoodsPerDelivery, maxGoodsPerDelivery + 1);
            float distance = Vector3.Distance(factoryPosition, candidate.transform.position);
            float baseDuration = distance / deliverySpeed + grams * handlingSecondsPerGram;
            float duration = Mathf.Max(minimumDeliverySeconds, baseDuration * Random.Range(0.85f, 1.2f));
            int reward = Mathf.RoundToInt((grams * pricePerGram + distance * distancePricePerUnit) * Random.Range(0.9f, 1.15f));
            int risk = Mathf.Clamp(
                Mathf.RoundToInt(minRisk + grams / 5f + distance / 40f + Random.Range(0f, 4f)),
                minRisk,
                Mathf.Max(minRisk, maxRisk));

            orders.Add(new DeliveryOrder
            {
                customerName = customerNames[i % customerNames.Count],
                reward = reward,
                risk = risk,
                grams = grams,
                distance = distance,
                duration = duration,
                target = target
            });
        }

        showOffers = orders.Count > 0;
        selectedOrder = -1;
        IsPopupOpen = showOffers;

        if (orders.Count < offersPerDay)
        {
            Debug.LogWarning($"Only {orders.Count} suitable delivery buildings were found; expected {offersPerDay}.");
        }
    }

    private List<MeshCollider> FindDeliveryCandidates()
    {
        MeshCollider[] colliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
        List<MeshCollider> candidates = new List<MeshCollider>();
        HashSet<Renderer> usedRenderers = new HashSet<Renderer>();

        foreach (MeshCollider candidate in colliders)
        {
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy ||
                candidate.GetComponentInParent<BuildingInfo>() != null ||
                candidate.GetComponent<DeliveryOrderTarget>() != null)
            {
                continue;
            }

            Renderer renderer = candidate.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = candidate.GetComponentInChildren<Renderer>();
            }

            if (renderer == null)
            {
                continue;
            }

            if (!usedRenderers.Add(renderer))
            {
                continue;
            }

            Vector3 size = renderer.bounds.size;
            if (size.y < 4f || Mathf.Max(size.x, size.z) < 3f)
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates;
    }

    private void DrawOffersWindow(int windowId)
    {
        GUILayout.Space(8f);
        GUILayout.Label("Each delivery uses one action, warehouse goods and one free worker.");
        GUILayout.Space(10f);

        for (int i = 0; i < orders.Count; i++)
        {
            DeliveryOrder order = orders[i];
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(48f));
            GUILayout.Label($"{i + 1}. {order.customerName}", GUILayout.Width(160f));
            GUILayout.Label($"{order.grams} g / {order.reward} €", GUILayout.Width(130f));
            GUILayout.Label($"Risk: +{order.risk}", GUILayout.Width(85f));
            GUILayout.Label($"{order.duration:0}s", GUILayout.Width(55f));

            GUI.enabled = !order.completed && !order.inProgress && GameEventManager.CanPlayerAct;
            if (GUILayout.Button(order.completed ? "Completed" : "Select", GUILayout.Width(100f), GUILayout.Height(30f)))
            {
                OpenOrder(i);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close and choose on map", GUILayout.Height(34f)))
        {
            showOffers = false;
            IsPopupOpen = false;
        }
    }

    private void DrawOrderWindow(int windowId)
    {
        DeliveryOrder order = orders[selectedOrder];
        GameResources resources = GameResources.Instance;
        GUILayout.Space(12f);
        GUILayout.Label($"Customer: {order.customerName}");
        GUILayout.Label($"Requested goods: {order.grams} g");
        int completedAtHouse = order.target != null ? order.target.CompletedDeliveryCount : 0;
        GUILayout.Label($"Territory progress at this house: {Mathf.Min(2, completedAtHouse)}/2");
        GUILayout.Label($"Payment: +{order.reward} €");
        GUILayout.Label($"Police risk: +{order.risk}");
        GUILayout.Label($"Worker pay: -{resources?.placaRadnikaPoZadatku ?? 15} €");
        GUILayout.Label($"Distance from factory: {order.distance:0} m");
        GUILayout.Label($"Estimated delivery time: {order.duration:0} seconds");

        if (resources != null && resources.robaUSkladistu < order.grams)
        {
            GUILayout.Label($"Missing goods in store: {order.grams - resources.robaUSkladistu} g");
        }
        if (resources != null && resources.SlobodniRadnici <= 0)
        {
            GUILayout.Label("A free worker is required.");
        }
        GUILayout.Space(16f);

        GUI.enabled = CanCompleteDelivery(order) && !order.completed && !order.inProgress;
        if (GUILayout.Button("Accept & Deliver", GUILayout.Height(40f)))
        {
            CompleteDelivery(order);
        }
        GUI.enabled = true;

        if (GUILayout.Button("Back to requests", GUILayout.Height(32f)))
        {
            selectedOrder = -1;
            showOffers = true;
        }
    }

    private void CompleteDelivery(DeliveryOrder order)
    {
        if (order == null || order.completed || order.inProgress || !CanCompleteDelivery(order))
        {
            return;
        }

        GameResources resources = GameResources.Instance;
        int workerPay = resources.placaRadnikaPoZadatku;
        if (!resources.TrySpendMoney(workerPay))
        {
            return;
        }

        if (!resources.TryConsumeWarehouseGoods(order.grams))
        {
            resources.novac += workerPay;
            return;
        }

        if (!resources.TryAssignWorkers(1, order.duration))
        {
            resources.robaUSkladistu += order.grams;
            resources.novac += workerPay;
            return;
        }

        order.inProgress = true;
        order.finishTime = Time.time + order.duration;
        activeDeliveries.Add(order);
        Debug.Log($"Delivery started for {order.customerName}: {order.grams} g, ETA {order.duration:0}s.");

        selectedOrder = -1;
        showOffers = GameEventManager.PlayerActionsRemaining > 1 && HasIncompleteOrders();
        IsPopupOpen = showOffers;
        GameEventManager.CompletePlayerAction();
    }

    private bool CanCompleteDelivery(DeliveryOrder order)
    {
        return order != null && GameEventManager.CanPlayerAct && GameResources.Instance != null &&
               GameResources.Instance.robaUSkladistu >= order.grams &&
               GameResources.Instance.CanAfford(GameResources.Instance.placaRadnikaPoZadatku) &&
               GameResources.Instance.SlobodniRadnici > 0;
    }

    private void CompleteFinishedDeliveries()
    {
        for (int i = activeDeliveries.Count - 1; i >= 0; i--)
        {
            DeliveryOrder order = activeDeliveries[i];
            if (Time.time < order.finishTime)
            {
                continue;
            }

            order.inProgress = false;
            order.completed = true;
            int influence = Mathf.Max(1, Mathf.CeilToInt(order.grams / 10f));
            if (GameResources.Instance != null)
            {
                GameResources.Instance.Apply(dNovac: order.reward, dRizik: order.risk, dRadnici: 0);
                GameResources.Instance.AddInfluence(influence);
            }

            bool territoryCaptured = order.target != null && order.target.RegisterCompletedDelivery();
            if (order.target != null)
            {
                order.target.Deactivate();
            }

            Debug.Log($"Delivery completed for {order.customerName}: {order.grams} g, +{order.reward} €, risk +{order.risk}.");
            if (territoryCaptured)
            {
                Debug.Log($"House captured after the second delivery for {order.customerName}.");
            }
            activeDeliveries.RemoveAt(i);
        }
    }

    private bool HasIncompleteOrders()
    {
        foreach (DeliveryOrder order in orders)
        {
            if (!order.completed)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawOrderLabels()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        for (int i = 0; i < orders.Count; i++)
        {
            DeliveryOrder order = orders[i];
            if (order.completed || order.target == null)
            {
                continue;
            }

            Vector3 screen = camera.WorldToScreenPoint(order.target.LabelPosition);
            if (screen.z <= 0f)
            {
                continue;
            }

            Rect rect = new Rect(screen.x - 55f, Screen.height - screen.y - 15f, 110f, 30f);
            GUI.Box(rect, $"ORDER {i + 1}");
        }
    }

    private void ClosePopups()
    {
        showOffers = false;
        selectedOrder = -1;
        IsPopupOpen = false;
    }

    private void ClearOrders()
    {
        foreach (DeliveryOrder order in orders)
        {
            if (order.target == null)
            {
                continue;
            }

            if (order.inProgress)
            {
                order.target.DisableInteraction();
            }
            else
            {
                order.target.Deactivate();
            }
        }

        orders.Clear();
    }

    private static Vector3 GetFactoryPosition()
    {
        BuildingInfo[] buildings = FindObjectsByType<BuildingInfo>(FindObjectsSortMode.None);
        foreach (BuildingInfo building in buildings)
        {
            if (building.buildingRole == BuildingRole.Factory)
            {
                return building.transform.position;
            }
        }

        return Vector3.zero;
    }

    private static void Shuffle<T>(List<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            T value = items[i];
            items[i] = items[swapIndex];
            items[swapIndex] = value;
        }
    }

    private static List<string> BuildCustomerAliases()
    {
        List<string> aliases = new List<string>(AliasPrefixes.Length * AliasCodenames.Length);
        foreach (string prefix in AliasPrefixes)
        {
            foreach (string codename in AliasCodenames)
            {
                aliases.Add($"{prefix} {codename}");
            }
        }

        return aliases;
    }
}
