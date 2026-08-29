using System.Collections.Generic;
using UnityEngine;

public class DeliveryOrderManager : MonoBehaviour
{
    public static bool IsPopupOpen { get; private set; }
    private static DeliveryOrderManager instance;

    [Header("Orders")]
    [Min(1)] public int offersPerDay = 5;
    public int minRisk = 4;
    public int maxRisk = 30;
    [Min(1)] public int minGoodsPerDelivery = 5;
    [Min(1)] public int maxGoodsPerDelivery = 45;
    [Min(1)] public int pricePerGram = 10;
    [Min(0f)] public float distancePricePerUnit = 0.5f;
    [Min(0.1f)] public float deliverySpeed = 8f;
    [Min(1f)] public float minimumDeliverySeconds = 5f;
    [Min(0f)] public float handlingSecondsPerGram = 0.1f;

    private Texture2D safeOrderLabelImage;
    private Texture2D volkovOrderLabelImage;
    private Texture2D neutralOrderLabelImage;
    private Texture2D popupBackgroundImage;
    private Texture2D closeButtonImage;
    private Texture2D acceptOrderButtonImage;

    private readonly List<DeliveryOrder> orders = new List<DeliveryOrder>();
    private readonly List<DeliveryOrder> activeDeliveries = new List<DeliveryOrder>();
    private readonly List<DeliveryOrder> pendingPlayerDeliveries = new List<DeliveryOrder>();
    private readonly List<AiDelivery> activeAiDeliveries = new List<AiDelivery>();
    private int generatedDay = -1;
    private int selectedOrder = -1;

    public static int PendingPlayerRisk
    {
        get
        {
            if (instance == null)
            {
                return 0;
            }

            int pendingRisk = 0;
            foreach (DeliveryOrder order in instance.pendingPlayerDeliveries)
            {
                if (order != null && order.inProgress && !order.completed)
                {
                    pendingRisk += order.risk;
                }
            }

            return pendingRisk;
        }
    }

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
        public TerritoryOwner territoryOwner;
        public float marketMultiplier;
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

    public void ConfigureLabelImages(
        Texture2D safeImage,
        Texture2D volkovImage,
        Texture2D neutralImage,
        Texture2D popupImage,
        Texture2D closeImage,
        Texture2D acceptImage)
    {
        safeOrderLabelImage = safeImage;
        volkovOrderLabelImage = volkovImage;
        neutralOrderLabelImage = neutralImage;
        popupBackgroundImage = popupImage;
        closeButtonImage = closeImage;
        acceptOrderButtonImage = acceptImage;
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

            Rect rect = new Rect(screen.x - 72.5f, Screen.height - screen.y - 25f, 145f, 50f);
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
                () => instance.CompleteAiDelivery(delivery), null, true);

        GameEventManager.FocusCameraOnWorldPosition(targetPosition);

        GameEventManager.ReportAiActivity(
            delivery.usesVehicle
                ? $"Vehicle started a {grams} g delivery to {target.Owner} territory."
                : $"Started a timed {grams} g delivery to {target.Owner} territory ({duration:0}s).",
            "🚐");
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

        Rect rect = new Rect((Screen.width - 440f) / 2f, (Screen.height - 580f) / 2f, 440f, 580f);
        if (popupBackgroundImage != null)
        {
            GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = popupBackgroundImage;
            windowStyle.border = new RectOffset(0, 0, 0, 0);
            GUI.Window(771002, rect, DrawOrderWindow, "Delivery request", windowStyle);
        }
        else
        {
            GUI.Window(771002, rect, DrawOrderWindow, "Delivery request");
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
        List<MeshCollider> selectedCandidates = SelectVariedCandidates(candidates);
        List<string> customerNames = BuildCustomerAliases();
        Shuffle(customerNames);
        Vector3 factoryPosition = GetFactoryPosition();
        GetOrderSizeRange(out int minimumGoods, out int maximumGoods);

        int offerCount = selectedCandidates.Count;
        for (int i = 0; i < offerCount; i++)
        {
            MeshCollider candidate = selectedCandidates[i];
            DeliveryOrderTarget target = candidate.gameObject.AddComponent<DeliveryOrderTarget>();
            target.Configure(this, i);

            int grams = Random.Range(minimumGoods, maximumGoods + 1);
            float distance = Vector3.Distance(factoryPosition, candidate.transform.position);
            float baseDuration = distance / deliverySpeed + grams * handlingSecondsPerGram;
            float duration = Mathf.Max(minimumDeliverySeconds, baseDuration * Random.Range(0.85f, 1.2f));
            TerritoryOwner territoryOwner = GetTerritoryOwner(candidate);
            float marketMultiplier = Random.Range(0.8f, 1.4f);
            float territoryRewardMultiplier = GetTerritoryRewardMultiplier(territoryOwner);
            int reward = Mathf.RoundToInt(
                (grams * pricePerGram + distance * distancePricePerUnit) *
                marketMultiplier * territoryRewardMultiplier);
            float baseRisk = minRisk + grams / 7f + distance / 100f + Random.Range(0f, 4f);
            int risk = Mathf.Clamp(
                Mathf.RoundToInt(GetTerritoryRisk(baseRisk, territoryOwner)),
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
                territoryOwner = territoryOwner,
                marketMultiplier = marketMultiplier,
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

    private void GetOrderSizeRange(out int minimumGoods, out int maximumGoods)
    {
        int warehouseLevel = 0;
        BuildingInfo[] buildings = FindObjectsByType<BuildingInfo>(FindObjectsSortMode.None);
        foreach (BuildingInfo building in buildings)
        {
            if (building != null && building.buildingRole == BuildingRole.Warehouse)
            {
                warehouseLevel = Mathf.Clamp(building.upgradeLevel, 0, 3);
                break;
            }
        }

        int[] minimumByLevel = { 5, 15, 25, 40 };
        int[] maximumByLevel = { 35, 60, 90, 150 };
        minimumGoods = Mathf.Max(minGoodsPerDelivery, minimumByLevel[warehouseLevel]);
        maximumGoods = Mathf.Max(minimumGoods, maximumByLevel[warehouseLevel]);
    }

    private List<MeshCollider> SelectVariedCandidates(List<MeshCollider> candidates)
    {
        List<MeshCollider> selected = new List<MeshCollider>();
        if (selected.Count < offersPerDay) AddCandidateForTerritory(candidates, selected, TerritoryOwner.Player);
        if (selected.Count < offersPerDay) AddCandidateForTerritory(candidates, selected, TerritoryOwner.Neutral);
        if (selected.Count < offersPerDay) AddCandidateForTerritory(candidates, selected, TerritoryOwner.AI);

        foreach (MeshCollider candidate in candidates)
        {
            if (selected.Count >= offersPerDay)
            {
                break;
            }

            if (!selected.Contains(candidate))
            {
                selected.Add(candidate);
            }
        }

        Shuffle(selected);
        return selected;
    }

    private static void AddCandidateForTerritory(
        List<MeshCollider> candidates,
        List<MeshCollider> selected,
        TerritoryOwner owner)
    {
        foreach (MeshCollider candidate in candidates)
        {
            if (!selected.Contains(candidate) && GetTerritoryOwner(candidate) == owner)
            {
                selected.Add(candidate);
                return;
            }
        }
    }

    private static TerritoryOwner GetTerritoryOwner(MeshCollider candidate)
    {
        if (candidate == null)
        {
            return TerritoryOwner.Neutral;
        }

        Renderer renderer = candidate.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = candidate.GetComponentInChildren<Renderer>();
        }

        TerritoryHouse house = renderer != null
            ? renderer.GetComponent<TerritoryHouse>()
            : null;
        return house != null ? house.Owner : TerritoryOwner.Neutral;
    }

    private static float GetTerritoryRewardMultiplier(TerritoryOwner owner)
    {
        switch (owner)
        {
            case TerritoryOwner.Player:
                return 0.9f;
            case TerritoryOwner.AI:
                return 1.4f;
            default:
                return 1f;
        }
    }

    private static float GetTerritoryRisk(float baseRisk, TerritoryOwner owner)
    {
        switch (owner)
        {
            case TerritoryOwner.Player:
                return baseRisk * 0.7f;
            case TerritoryOwner.AI:
                return baseRisk + 10f;
            default:
                return baseRisk + 3f;
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
        Rect closeRect = new Rect(400f, 4f, 36f, 36f);
        bool closeClicked = closeButtonImage != null
            ? GUI.Button(closeRect, closeButtonImage, GUIStyle.none)
            : GUI.Button(closeRect, "X");
        if (closeClicked)
        {
            selectedOrder = -1;
            IsPopupOpen = false;
            return;
        }

        DeliveryOrder order = orders[selectedOrder];
        GameResources resources = GameResources.Instance;
        GUILayout.Space(12f);
        GUILayout.Label($"Customer: {order.customerName}");
        GUILayout.Label($"Territory: {GetTerritoryName(order.territoryOwner)}");
        GUILayout.Label($"Requested goods: {order.grams} g");
        if (order.target != null && order.target.TryGetDistrictControl(
                out string districtName,
                out int controlScore,
                out int controlLimit))
        {
            GUILayout.Label($"{districtName} control: {controlScore:+0;-0;0} " +
                            $"(-{controlLimit} Volkov / +{controlLimit} you)");
        }
        GUILayout.Label($"Payment: +{order.reward} €");
        GUILayout.Label($"Market demand: {GetDemandName(order.marketMultiplier)}");
        GUILayout.Label(GetTerritoryTerms(order.territoryOwner));
        GUILayout.Label($"Police risk: +{order.risk}");
        int workerCommission = SharedActionRules.GetDeliveryWorkerCommission(resources, order.reward);
        GUILayout.Label($"Courier commission: -{workerCommission} € (on success)");
        GUILayout.Label($"Net profit: +{Mathf.Max(0, order.reward - workerCommission)} €");
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

        bool canAccept = CanCompleteDelivery(order) && !order.completed && !order.inProgress;
        GUI.enabled = canAccept;
        Color previousColor = GUI.color;
        GUI.color = canAccept ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.8f);
        bool accepted;
        if (acceptOrderButtonImage != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            accepted = GUILayout.Button(
                acceptOrderButtonImage,
                GUIStyle.none,
                GUILayout.Width(315f),
                GUILayout.Height(60f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        else
        {
            accepted = GUILayout.Button("Accept & Deliver", GUILayout.Height(40f));
        }
        GUI.color = previousColor;
        if (accepted)
        {
            CompleteDelivery(order);
        }
        GUI.enabled = true;
    }

    private static string GetTerritoryName(TerritoryOwner owner)
    {
        switch (owner)
        {
            case TerritoryOwner.Player:
                return "YOUR TERRITORY";
            case TerritoryOwner.AI:
                return "VOLKOV TERRITORY";
            default:
                return "NEUTRAL TERRITORY";
        }
    }

    private static string GetTerritoryTerms(TerritoryOwner owner)
    {
        switch (owner)
        {
            case TerritoryOwner.Player:
                return "Safe route: -30% risk, -10% territory payment.";
            case TerritoryOwner.AI:
                return "Hostile route: +10 risk, +40% territory payment.";
            default:
                return "Uncontrolled route: +3 risk, standard territory payment.";
        }
    }

    private static string GetDemandName(float multiplier)
    {
        if (multiplier >= 1.25f)
        {
            return "HIGH (premium price)";
        }

        if (multiplier < 0.95f)
        {
            return "LOW (poor price)";
        }

        return "NORMAL";
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
        pendingPlayerDeliveries.Add(order);
        GameEventManager.ReportPlayerDeliveryStarted();
        GameEventManager.ReportPlayerActivity(
            "🚐",
            $"Started {order.grams} g delivery to {order.customerName} ({order.duration:0}s).");

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

        if (AmbushTrapManager.TryTriggerAmbush(TerritoryOwner.Player, order.grams, order.reward))
        {
            order.inProgress = false;
            order.completed = true;
            pendingPlayerDeliveries.Remove(order);
            if (order.target != null)
            {
                order.target.Deactivate();
            }
            return;
        }

        order.inProgress = false;
        order.completed = true;
        pendingPlayerDeliveries.Remove(order);

        if (GameResources.Instance != null)
        {
            SharedActionRules.CompleteDelivery(
                GameResources.Instance,
                order.reward,
                order.risk);
        }

        bool territoryCaptured = order.target != null &&
                                 order.target.RegisterCompletedDelivery(TerritoryOwner.Player);
        if (order.target != null)
        {
            order.target.Deactivate();
        }

        GameStoryManager.ReportSuccessfulPlayerDelivery();
        GameEventManager.ReportPlayerActivity(
            territoryCaptured ? "🏴" : "💰",
            territoryCaptured
                ? $"Delivery complete: captured territory near {order.customerName}."
                : $"Delivery complete for {order.customerName}: +{order.reward}, risk +{order.risk}.");

        Debug.Log($"Delivery completed for {order.customerName}: {order.grams} g, +{order.reward} €, risk +{order.risk}.");
        if (territoryCaptured)
        {
            Debug.Log($"Territory captured after reaching full control for {order.customerName}.");
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

            if (AmbushTrapManager.TryTriggerAmbush(TerritoryOwner.Player, order.grams, order.reward))
            {
                order.inProgress = false;
                order.completed = true;
                pendingPlayerDeliveries.Remove(order);
                if (order.target != null)
                {
                    order.target.Deactivate();
                }
                activeDeliveries.RemoveAt(i);
                continue;
            }

            order.inProgress = false;
            order.completed = true;
            pendingPlayerDeliveries.Remove(order);
            if (GameResources.Instance != null)
            {
                SharedActionRules.CompleteDelivery(
                    GameResources.Instance,
                    order.reward,
                    order.risk);
            }

            bool territoryCaptured = order.target != null &&
                                     order.target.RegisterCompletedDelivery(TerritoryOwner.Player);
            if (order.target != null)
            {
                order.target.Deactivate();
            }

            GameStoryManager.ReportSuccessfulPlayerDelivery();
            GameEventManager.ReportPlayerActivity(
                territoryCaptured ? "🏴" : "💰",
                territoryCaptured
                    ? $"Delivery complete: captured territory near {order.customerName}."
                    : $"Delivery complete for {order.customerName}: +{order.reward}, risk +{order.risk}.");

            Debug.Log($"Delivery completed for {order.customerName}: {order.grams} g, +{order.reward} €, risk +{order.risk}.");
            if (territoryCaptured)
            {
                Debug.Log($"Territory captured after reaching full control for {order.customerName}.");
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

        if (AmbushTrapManager.TryTriggerAmbush(TerritoryOwner.AI, delivery.grams, delivery.reward))
        {
            delivery.completed = true;
            activeAiDeliveries.Remove(delivery);
            return;
        }

        delivery.completed = true;
        OpponentResources ai = GameResources.Instance != null
            ? GameResources.Instance.Opponent
            : null;
        SharedActionRules.CompleteDelivery(ai, delivery.reward, delivery.risk);

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
                : $"Completed a {delivery.grams} g delivery for {delivery.reward} €.",
            captured ? "🏴" : "💰");
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

            Rect rect = new Rect(screen.x - 72.5f, Screen.height - screen.y - 25f, 145f, 50f);
            Texture2D labelImage = GetOrderLabelImage(order.territoryOwner);
            bool clicked;
            if (labelImage != null)
            {
                clicked = GUI.Button(rect, labelImage, GUIStyle.none);
            }
            else
            {
                string territoryLabel = order.territoryOwner == TerritoryOwner.Player
                    ? "SAFE"
                    : order.territoryOwner == TerritoryOwner.AI ? "VOLKOV" : "NEUTRAL";
                clicked = GUI.Button(rect, $"{territoryLabel} {i + 1}");
            }

            if (clicked)
            {
                OpenOrder(i);
            }
        }
    }

    private Texture2D GetOrderLabelImage(TerritoryOwner owner)
    {
        if (owner == TerritoryOwner.Player)
        {
            return safeOrderLabelImage;
        }

        if (owner == TerritoryOwner.AI)
        {
            return volkovOrderLabelImage;
        }

        return neutralOrderLabelImage;
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
