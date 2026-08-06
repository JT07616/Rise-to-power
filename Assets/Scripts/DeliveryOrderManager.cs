using System.Collections.Generic;
using UnityEngine;

public class DeliveryOrderManager : MonoBehaviour
{
    public static bool IsPopupOpen { get; private set; }
    private static DeliveryOrderManager instance;

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
    private readonly List<AiDelivery> activeAiDeliveries = new List<AiDelivery>();
    private int generatedDay = -1;
    private int selectedOrder = -1;

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

    private class AiDelivery
    {
        public int reward;
        public int risk;
        public int grams;
        public float finishTime;
        public TerritoryHouse target;
        public Renderer targetRenderer;
        public bool usesVehicle;
        public bool completed;
    }

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        CompleteFinishedDeliveries();
        CompleteFinishedAiDeliveries();

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
        if (instance == this)
        {
            instance = null;
        }
    }

    public static bool IsPointerOverMapLabel(Vector2 screenPosition)
    {
        if (instance == null)
        {
            return false;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return false;
        }

        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        for (int i = 0; i < instance.orders.Count; i++)
        {
            DeliveryOrder order = instance.orders[i];
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
            if (rect.Contains(guiPosition))
            {
                return true;
            }
        }

        return false;
    }

    public static bool CanAiStartDelivery()
    {
        if (instance == null || GameResources.Instance == null ||
            GameResources.Instance.Opponent.warehouseGoods < instance.minGoodsPerDelivery)
        {
            return false;
        }

        return instance.FindAiDeliveryCandidates().Count > 0;
    }

    public static bool TryStartRandomAiDelivery()
    {
        if (!CanAiStartDelivery())
        {
            return false;
        }

        OpponentResources ai = GameResources.Instance.Opponent;
        List<TerritoryHouse> candidates = instance.FindAiDeliveryCandidates();
        TerritoryHouse target = candidates[Random.Range(0, candidates.Count)];
        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            targetRenderer = target.GetComponentInChildren<Renderer>();
        }

        int maximumGoods = Mathf.Min(instance.maxGoodsPerDelivery, ai.warehouseGoods);
        int grams = Random.Range(instance.minGoodsPerDelivery, maximumGoods + 1);
        Vector3 targetPosition = targetRenderer != null
            ? targetRenderer.bounds.center
            : target.transform.position;
        float distance = Vector3.Distance(instance.GetAiOperationPosition(), targetPosition);
        float baseDuration = distance / instance.deliverySpeed + grams * instance.handlingSecondsPerGram;
        float duration = Mathf.Max(
            instance.minimumDeliverySeconds,
            baseDuration * Random.Range(0.85f, 1.2f));
        int reward = Mathf.RoundToInt(
            (grams * instance.pricePerGram + distance * instance.distancePricePerUnit) *
            Random.Range(0.9f, 1.15f));
        int risk = Mathf.Clamp(
            Mathf.RoundToInt(instance.minRisk + grams / 5f + distance / 40f + Random.Range(0f, 4f)),
            instance.minRisk,
            Mathf.Max(instance.minRisk, instance.maxRisk));

        if (!SharedActionRules.TryStartDelivery(ai, grams, duration))
        {
            return false;
        }

        AiDelivery delivery = new AiDelivery
        {
            reward = reward,
            risk = risk,
            grams = grams,
            finishTime = Time.time + duration,
            target = target,
            targetRenderer = targetRenderer
        };
        instance.activeAiDeliveries.Add(delivery);

        delivery.usesVehicle = DeliveryVehicleManager.Instance != null &&
            DeliveryVehicleManager.Instance.StartVehicleJourney(
                instance.GetAiOperationPosition(),
                targetPosition,
                duration,
                () => instance.CompleteAiDelivery(delivery));

        GameEventManager.FocusCameraOnWorldPosition(targetPosition);

        GameEventManager.ReportAiActivity(
            delivery.usesVehicle
                ? $"Vehicle started a {grams} g delivery to {target.Owner} territory."
                : $"Started a timed {grams} g delivery to {target.Owner} territory ({duration:0}s).");
        return true;
    }

    void OnGUI()
    {
        if (!GameEventManager.IsPlayerTurn || GameEventManager.IsPauseMenuOpen ||
            GameEventManager.IsPopupOpen)
        {
            return;
        }

        if (!BuildingPopupUI.IsAnyOpen)
        {
            DrawOrderLabels();
        }

        if (selectedOrder < 0 || selectedOrder >= orders.Count)
        {
            return;
        }

        int previousDepth = GUI.depth;
        GUI.depth = -900;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        Rect rect = new Rect((Screen.width - 440f) / 2f, (Screen.height - 400f) / 2f, 440f, 400f);
        GUI.Window(771002, rect, DrawOrderWindow, "Delivery request");

        GUI.depth = previousDepth;
    }

    public void OpenOrder(int orderIndex)
    {
        if (!GameEventManager.CanPlayerAct || orderIndex < 0 || orderIndex >= orders.Count ||
            orders[orderIndex].completed || orders[orderIndex].inProgress)
        {
            return;
        }

        DeliveryOrderTarget target = orders[orderIndex].target;
        SimpleStrategyCamera strategyCamera = Camera.main != null
            ? Camera.main.GetComponent<SimpleStrategyCamera>()
            : null;

        if (strategyCamera != null && target != null)
        {
            strategyCamera.FocusOn(target.LabelPosition, () => ShowOrder(orderIndex));
            return;
        }

        ShowOrder(orderIndex);
    }

    private void ShowOrder(int orderIndex)
    {
        if (!GameEventManager.CanPlayerAct || orderIndex < 0 || orderIndex >= orders.Count ||
            orders[orderIndex].completed || orders[orderIndex].inProgress)
        {
            return;
        }

        selectedOrder = orderIndex;
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

        selectedOrder = -1;
        IsPopupOpen = false;

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
                candidate.GetComponentInParent<AiFacilityMarker>() != null ||
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

    private void DrawOrderWindow(int windowId)
    {
        if (GUI.Button(new Rect(410f, 2f, 26f, 20f), "X"))
        {
            selectedOrder = -1;
            IsPopupOpen = false;
            return;
        }

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
    }

    private void CompleteDelivery(DeliveryOrder order)
    {
        if (order == null || order.completed || order.inProgress || !CanCompleteDelivery(order))
        {
            return;
        }

        GameResources resources = GameResources.Instance;
        if (!SharedActionRules.TryStartDelivery(resources, order.grams, order.duration))
        {
            return;
        }

        order.inProgress = true;

        Vector3 startPosition = GetVehicleStartPosition();
        Vector3 destinationPosition = order.target != null
            ? order.target.transform.position
            : startPosition;

        bool vehicleStarted = DeliveryVehicleManager.Instance != null &&
            DeliveryVehicleManager.Instance.StartVehicleJourney(
                startPosition,
                destinationPosition,
                order.duration,
                () => CompleteVehicleDelivery(order));

        if (vehicleStarted)
        {
            Debug.Log($"Delivery vehicle started for {order.customerName}: {order.grams} g, ETA {order.duration:0}s.");
        }
        else
        {
            order.finishTime = Time.time + order.duration;
            activeDeliveries.Add(order);
            Debug.LogWarning($"Delivery vehicle could not start for {order.customerName}; using timed delivery.");
        }

        selectedOrder = -1;
        IsPopupOpen = false;
        GameEventManager.CompletePlayerAction();
    }

    private void CompleteVehicleDelivery(DeliveryOrder order)
    {
        if (order == null || order.completed)
        {
            return;
        }

        order.inProgress = false;
        order.completed = true;
        int influence = Mathf.Max(1, Mathf.CeilToInt(order.grams / 10f));

        if (GameResources.Instance != null)
        {
            SharedActionRules.CompleteDelivery(
                GameResources.Instance,
                order.reward,
                order.risk,
                influence);
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
    }

    private bool CanCompleteDelivery(DeliveryOrder order)
    {
        return order != null && GameEventManager.CanPlayerAct && GameResources.Instance != null &&
               SharedActionRules.CanStartDelivery(GameResources.Instance, order.grams);
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
                SharedActionRules.CompleteDelivery(
                    GameResources.Instance,
                    order.reward,
                    order.risk,
                    influence);
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

    private void CompleteFinishedAiDeliveries()
    {
        for (int i = activeAiDeliveries.Count - 1; i >= 0; i--)
        {
            AiDelivery delivery = activeAiDeliveries[i];
            if (delivery.usesVehicle || Time.time < delivery.finishTime)
            {
                continue;
            }

            CompleteAiDelivery(delivery);
        }
    }

    private void CompleteAiDelivery(AiDelivery delivery)
    {
        if (delivery == null || delivery.completed)
        {
            return;
        }

        delivery.completed = true;
        OpponentResources ai = GameResources.Instance != null
            ? GameResources.Instance.Opponent
            : null;
        int influence = Mathf.Max(1, Mathf.CeilToInt(delivery.grams / 10f));
        SharedActionRules.CompleteDelivery(ai, delivery.reward, delivery.risk, influence);

        TerritoryOwner previousOwner = delivery.target != null
            ? delivery.target.Owner
            : TerritoryOwner.Neutral;
        bool captured = delivery.target != null &&
            delivery.target.RegisterDelivery(TerritoryOwner.AI);

        if (captured && delivery.targetRenderer != null &&
            delivery.targetRenderer.GetComponent<DeliveryOrderTarget>() == null)
        {
            delivery.targetRenderer.material.color = new Color(0.9f, 0.1f, 0.1f);
        }

        GameEventManager.ReportAiActivity(
            captured
                ? $"Captured a {previousOwner} territory with a {delivery.grams} g delivery."
                : $"Completed a {delivery.grams} g delivery for {delivery.reward} €.");
        activeAiDeliveries.Remove(delivery);
    }

    private List<TerritoryHouse> FindAiDeliveryCandidates()
    {
        TerritoryHouse[] houses = FindObjectsByType<TerritoryHouse>(FindObjectsSortMode.None);
        List<TerritoryHouse> candidates = new List<TerritoryHouse>();
        foreach (TerritoryHouse house in houses)
        {
            if (house == null || !house.gameObject.activeInHierarchy ||
                house.GetComponentInParent<BuildingInfo>() != null ||
                house.GetComponentInParent<AiFacilityMarker>() != null || IsAiTargetActive(house))
            {
                continue;
            }

            Renderer renderer = house.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = house.GetComponentInChildren<Renderer>();
            }
            if (renderer != null)
            {
                candidates.Add(house);
            }
        }

        return candidates;
    }

    private bool IsAiTargetActive(TerritoryHouse house)
    {
        foreach (AiDelivery delivery in activeAiDeliveries)
        {
            if (delivery.target == house)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetAiOperationPosition()
    {
        AiFacilityMarker warehouse = AiFacilityMarker.Find(AiFacilityRole.Warehouse);
        if (warehouse != null)
        {
            return warehouse.transform.position;
        }

        TerritoryHouse[] houses = FindObjectsByType<TerritoryHouse>(FindObjectsSortMode.None);
        Vector3 total = Vector3.zero;
        int count = 0;
        foreach (TerritoryHouse house in houses)
        {
            if (house != null && house.IsAiOwned)
            {
                total += house.transform.position;
                count++;
            }
        }

        return count > 0 ? total / count : GetFactoryPosition();
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
            if (GUI.Button(rect, $"ORDER {i + 1}"))
            {
                OpenOrder(i);
            }
        }
    }

    private void ClosePopups()
    {
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

    private static Vector3 GetVehicleStartPosition()
    {
        GameObject deliveryPoint = GameObject.Find("DrugStoreDelivery");
        return deliveryPoint != null
            ? deliveryPoint.transform.position
            : GetFactoryPosition();
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
