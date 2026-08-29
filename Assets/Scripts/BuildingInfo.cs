using UnityEngine;

public enum BuildingRole
{
    General,
    Factory,
    WorkerContact,
    Warehouse,
    CorruptOfficer
}

public class BuildingInfo : MonoBehaviour
{
    public string buildingName = "Building";
    public BuildingRole buildingRole = BuildingRole.General;

    [Header("Map label images")]
    public Texture2D[] mapLabelImagesByUpgradeLevel;

    public Texture2D CurrentMapLabelImage
    {
        get
        {
            if (mapLabelImagesByUpgradeLevel == null || mapLabelImagesByUpgradeLevel.Length == 0)
            {
                return null;
            }

            int imageIndex = Mathf.Clamp(upgradeLevel, 0, mapLabelImagesByUpgradeLevel.Length - 1);
            return mapLabelImagesByUpgradeLevel[imageIndex];
        }
    }

    [TextArea(2, 5)]
    public string description = "Short description of this building.";

    [Header("Building actions")]
    public bool hasProductionControls = false;
    public bool requiresPurchase = false;
    public bool isPurchased = false;
    public int purchaseCost = 100;
    public string purchaseButtonText = "Buy location";

    [TextArea(1, 3)]
    public string purchaseDescription = "This location must be bought before you can use its actions.";

    public bool showIncreaseAction = true;
    public bool showDecreaseAction = true;
    public bool actionsChangeLevel = true;
    public int actionCooldownEvents = 1;

    public int productionLevel = 0;
    public int minProductionLevel = -2;
    public int maxProductionLevel = 2;

    [TextArea(2, 4)]
    public string productionDescription =
        "You can push this place harder for short-term money, but every move leaves a trail. " +
        "Pulling back slows the operation and lowers the heat.";

    public string statusName = "Current pressure";
    public string neutralStatusText = "normal";
    public string positiveStatusText = "high";
    public string negativeStatusText = "low";

    public string increaseButtonText = "Push harder (+money, +risk)";
    public string decreaseButtonText = "Pull back (-money, -risk)";

    [Header("Increase action effects")]
    public int increaseNovac = 100;
    public int increaseRizik = 10;
    public int increaseRadnici = 0;

    [Header("Decrease action effects")]
    public int decreaseNovac = -60;
    public int decreaseRizik = -8;
    public int decreaseRadnici = 0;

    [Header("Upgrade")]
    public bool hasUpgrade = false;
    public int upgradeLevel = 0;
    public int maxUpgradeLevel = 2;
    public int upgradeCost = 500;
    public int upgradeCostIncrease = 250;
    public string upgradeButtonText = "Upgrade";

    [TextArea(2, 4)]
    public string upgradeDescription = "Improve this location for long-term benefits.";

    public int upgradeRizik = -5;
    public int upgradeRadnici = 0;

    [Header("Corrupt officer")]
    [Min(0)] public int riskProtectionCost = 300;
    [Min(1)] public int riskProtectionTurns = 3;
    [Min(0)] public int raidBribeCost = 500;
    [Min(0)] public int officerActionCooldownTurns = 3;
    public string riskProtectionButtonText = "Pay for protection";
    public string raidBribeButtonText = "Bribe raid contact";

    [Header("Emergency favor")]
    [Min(0)] public int emergencyActionBaseCost = 700;
    [Min(0)] public int emergencyActionCostIncrease = 400;
    [Min(0)] public int emergencyActionRisk = 6;
    [Min(1)] public int emergencyActionCooldownTurns = 3;
    public string emergencyActionButtonText = "Call in a favor";

    [Header("Goods production")]
    public bool producesGoods = false;
    [Min(1)] public int goodsPerBatch = 50;
    [Min(1f)] public float productionDurationSeconds = 30f;
    [Min(1)] public int productionWorkersRequired = 1;
    public string produceGoodsButtonText = "Produce 50 g";
    public string moveGoodsButtonText = "Move goods to store";
    [Min(0.1f)] public float transferSpeed = 10f;
    [Min(1)] public int warehouseCapacityPerUpgrade = 100;

    public Color hoverColor = Color.yellow;
    public Color selectedColor = Color.blue;

    private Renderer[] renderers;
    private Renderer labelRenderer;
    private Color[] originalColors;
    private int lastActionDay = -1000;
    private int lastOfficerActionDay = -1000;
    private int lastEmergencyActionDay = -1000;
    private int emergencyActionsPurchased;
    private float productionFinishTime = -1f;
    private bool storyLocked;

    public bool IsStoryLocked
    {
        get { return storyLocked; }
    }

    public bool IsProducingGoods
    {
        get { return producesGoods && productionFinishTime > Time.time; }
    }

    public Vector3 LabelPosition
    {
        get
        {
            if (labelRenderer == null)
            {
                return transform.position + Vector3.up * 2f;
            }

            Bounds bounds = labelRenderer.bounds;
            return new Vector3(bounds.center.x, bounds.max.y + 1.5f, bounds.center.z);
        }
    }

    void Awake()
    {
        if (buildingRole == BuildingRole.CorruptOfficer)
        {
            ConfigureCorruptOfficer();
        }

        ConfigureUpgradeProgression();

        renderers = GetComponentsInChildren<Renderer>();
        labelRenderer = GetComponent<Renderer>();
        if (labelRenderer == null)
        {
            labelRenderer = GetComponentInChildren<Renderer>();
        }
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }
    }

    private void ConfigureCorruptOfficer()
    {
        gameObject.name = "Backroom Meeting Spot";
        buildingName = "Corrupt Officer";
        description = "You meet a corrupt officer here, away from the station and anyone who might recognize either of you.";
        hasProductionControls = true;
        requiresPurchase = true;
        isPurchased = false;
        purchaseCost = 2000;
        purchaseButtonText = "Buy the officer's trust";
        purchaseDescription = "Before he risks his badge for you, you must prove that the relationship is worth the danger.";
        showIncreaseAction = false;
        showDecreaseAction = false;
        hasUpgrade = false;
        riskProtectionCost = 800;
        riskProtectionTurns = 2;
        raidBribeCost = 1200;
        officerActionCooldownTurns = 5;
    }

    private void ConfigureUpgradeProgression()
    {
        if (buildingRole == BuildingRole.Factory)
        {
            hasUpgrade = true;
            maxUpgradeLevel = 3;
            upgradeRizik = 0;
            upgradeRadnici = 0;
            upgradeButtonText = "Expand production";
            upgradeDescription = "Larger equipment increases batch size, factory capacity, and eventually production speed.";
        }
        else if (buildingRole == BuildingRole.Warehouse)
        {
            hasUpgrade = true;
            maxUpgradeLevel = 3;
            upgradeRizik = 0;
            upgradeRadnici = 0;
            upgradeButtonText = "Expand warehouse";
            upgradeDescription = "More storage lets you prepare larger stockpiles before accepting deliveries.";
        }
        else if (buildingRole == BuildingRole.WorkerContact)
        {
            emergencyActionBaseCost = 700;
            emergencyActionCostIncrease = 400;
            emergencyActionRisk = 6;
            emergencyActionCooldownTurns = 3;
            emergencyActionButtonText = "Call in a favor";
        }
        else if (buildingName == "Apartment")
        {
            hasUpgrade = true;
            maxUpgradeLevel = 3;
            upgradeRizik = 0;
            upgradeRadnici = 0;
            actionCooldownEvents = Mathf.Max(3, actionCooldownEvents);
            upgradeButtonText = "Improve safehouse";
            upgradeDescription = "A safer routine makes laying low more effective, but always needs at least three turns to cool down.";
        }
    }

    void Start()
    {
        if (upgradeLevel > 0)
        {
            ApplyUpgradeBenefits();
        }
    }

    void Update()
    {
        if (!producesGoods || productionFinishTime < 0f ||
            Time.time < productionFinishTime || GameResources.Instance == null)
        {
            return;
        }

        SharedActionRules.CompleteProduction(
            GameResources.Instance,
            goodsPerBatch,
            productionWorkersRequired);
        GameEventManager.ReportPlayerActivity("📦", $"{buildingName} completed {goodsPerBatch} g of production.");
        Debug.Log($"{buildingName}: production finished, +{goodsPerBatch} g.");
        productionFinishTime = -1f;
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

    public string GetFullDescription()
    {
        if (buildingName == "Police Station")
        {
            return description + "\n\n" +
                   "A contact near dispatch can identify Volkov's delivery routes. " +
                   "Assign one worker to intercept the next suitable convoy.\n\n" +
                   AmbushTrapManager.GetPlayerAmbushStatus();
        }

        if (buildingRole == BuildingRole.CorruptOfficer)
        {
            return GetCorruptOfficerDescription();
        }

        if (!hasProductionControls)
        {
            return description;
        }

        string fullDescription = description + "\n\n" + productionDescription;

        if (requiresPurchase && !isPurchased)
        {
            return fullDescription + "\n\n" + purchaseDescription + $"\n\nPrice: {purchaseCost} €";
        }

        if (showIncreaseAction || showDecreaseAction)
        {
            fullDescription += "\n\n" + GetProductionStatus();
            fullDescription += "\n\n" + GetActionDescriptions();
        }

        if (producesGoods)
        {
            fullDescription += "\n\n" + GetGoodsProductionStatus();
        }

        if (buildingRole == BuildingRole.Warehouse)
        {
            fullDescription += "\n\n" + GetWarehouseStatus();
        }

        if (hasUpgrade)
        {
            fullDescription += "\n" + GetUpgradeStatus();
        }

        int cooldownRemaining = GetCooldownDaysRemaining();
        if (cooldownRemaining > 0)
        {
            fullDescription += $"\nAction available after {cooldownRemaining} more day(s).";
        }

        return fullDescription;
    }

    public string GetPurchaseButtonLabel()
    {
        return GetShortButtonLabel(purchaseButtonText);
    }

    public string GetIncreaseButtonLabel()
    {
        return GetShortButtonLabel(increaseButtonText);
    }

    public string GetDecreaseButtonLabel()
    {
        return GetShortButtonLabel(decreaseButtonText);
    }

    public string GetUpgradeButtonLabel()
    {
        return GetShortButtonLabel(upgradeButtonText);
    }

    public string GetProduceGoodsButtonLabel()
    {
        return GetShortButtonLabel(produceGoodsButtonText);
    }

    public string GetMoveGoodsButtonLabel()
    {
        return GetShortButtonLabel(moveGoodsButtonText);
    }

    public string GetRiskProtectionButtonLabel()
    {
        return GetShortButtonLabel(riskProtectionButtonText);
    }

    public string GetRaidBribeButtonLabel()
    {
        return GetShortButtonLabel(raidBribeButtonText);
    }

    public string GetEmergencyActionButtonLabel()
    {
        return $"{emergencyActionButtonText} - {GetEmergencyActionCost()} €";
    }

    public bool CanBuyEmergencyAction()
    {
        return buildingRole == BuildingRole.WorkerContact && IsUnlocked() &&
               GameEventManager.CanPlayerAct && !IsEmergencyActionOnCooldown() &&
               GameResources.Instance != null &&
               GameResources.Instance.CanAfford(GetEmergencyActionCost());
    }

    public void BuyEmergencyAction()
    {
        if (!CanBuyEmergencyAction())
        {
            return;
        }

        int cost = GetEmergencyActionCost();
        if (!SharedActionRules.TryApplyResourceChange(
                GameResources.Instance,
                -cost,
                emergencyActionRisk,
                0))
        {
            return;
        }

        emergencyActionsPurchased++;
        lastEmergencyActionDay = GameEventManager.CurrentDay;
        GameEventManager.ReportPlayerActivity("🤝", $"Bought an emergency favor from {buildingName} for {cost}.");
        GameEventManager.GrantEmergencyAction();
        Debug.Log($"Emergency favor bought for {cost} €. Next favor costs {GetEmergencyActionCost()} €.");
    }

    public bool CanBuyRiskProtection()
    {
        return buildingRole == BuildingRole.CorruptOfficer && IsUnlocked() &&
               GameEventManager.CanPlayerAct && !IsOfficerActionOnCooldown() &&
               GameResources.Instance != null &&
               !GameResources.Instance.IsRiskProtectionActive &&
               GameResources.Instance.CanAfford(riskProtectionCost);
    }

    public void BuyRiskProtection()
    {
        if (!CanBuyRiskProtection())
        {
            return;
        }

        GameResources resources = GameResources.Instance;
        if (!resources.TrySpendMoney(riskProtectionCost))
        {
            return;
        }

        resources.ActivateRiskProtection(riskProtectionTurns);
        lastOfficerActionDay = GameEventManager.CurrentDay;
        GameStoryManager.ReportOfficerPaid();
        GameEventManager.ReportPlayerActivity("🛡️", $"Bought police protection for {riskProtectionTurns} turns.");
        GameEventManager.CompletePlayerAction();
        Debug.Log($"Police protection active for {riskProtectionTurns} turns.");
    }

    public bool CanBuyRaidBribe()
    {
        return buildingRole == BuildingRole.CorruptOfficer && IsUnlocked() &&
               GameEventManager.CanPlayerAct && !IsOfficerActionOnCooldown() &&
               GameResources.Instance != null &&
               !GameResources.Instance.WillSkipNextPoliceRaid &&
               GameResources.Instance.CanAfford(raidBribeCost);
    }

    public void BuyRaidBribe()
    {
        if (!CanBuyRaidBribe())
        {
            return;
        }

        GameResources resources = GameResources.Instance;
        if (!resources.TrySpendMoney(raidBribeCost))
        {
            return;
        }

        resources.PrepareRaidBribe();
        lastOfficerActionDay = GameEventManager.CurrentDay;
        GameStoryManager.ReportOfficerPaid();
        GameEventManager.ReportPlayerActivity("💵", "Bribed Cross to cancel the next police raid.");
        GameEventManager.CompletePlayerAction();
        Debug.Log("The next police raid will be skipped and will leave risk at 30.");
    }

    public bool IsUnlocked()
    {
        return !storyLocked && (!requiresPurchase || isPurchased);
    }

    public bool CanPurchase()
    {
        return !storyLocked && hasProductionControls && requiresPurchase && !isPurchased &&
               GameEventManager.CanPlayerAct && GameResources.Instance != null &&
               GameResources.Instance.novac >= purchaseCost;
    }

    public void Purchase()
    {
        if (!CanPurchase())
        {
            return;
        }

        if (!ApplyActionEffects(-purchaseCost, 0, 0))
        {
            return;
        }
        isPurchased = true;
        GameStoryManager.ReportBuildingPurchased(this);
        GameEventManager.ReportPlayerActivity("🔑", $"Purchased {buildingName} for {purchaseCost}.");
        GameEventManager.CompletePlayerAction();
        Debug.Log($"🏢 {buildingName}: location bought for {purchaseCost} €.");
    }

    public bool CanUpgrade()
    {
        return hasProductionControls && hasUpgrade && IsUnlocked() &&
               GameEventManager.CanPlayerAct && upgradeLevel < maxUpgradeLevel &&
               (buildingRole != BuildingRole.Factory || !IsProducingGoods) &&
               HasEnoughMoneyFor(-GetUpgradeCost());
    }

    public bool CanProduceGoods()
    {
        return producesGoods && IsUnlocked() && !IsProducingGoods &&
               GameEventManager.CanPlayerAct && GameResources.Instance != null &&
               SharedActionRules.CanStartProduction(
                   GameResources.Instance,
                   goodsPerBatch,
                   productionWorkersRequired);
    }

    public void StartGoodsProduction()
    {
        if (!CanProduceGoods())
        {
            return;
        }

        GameResources resources = GameResources.Instance;
        if (!SharedActionRules.TryStartProduction(
                resources,
                goodsPerBatch,
                productionWorkersRequired,
                productionDurationSeconds))
        {
            return;
        }
        productionFinishTime = Time.time + productionDurationSeconds;
        GameEventManager.ReportPlayerActivity("🏭", $"Started producing {goodsPerBatch} g at {buildingName}.");
        GameEventManager.CompletePlayerAction();
        Debug.Log($"{buildingName}: production started; {goodsPerBatch} g ready in {productionDurationSeconds:0} seconds.");
    }

    public bool CanMoveGoods()
    {
        if ((buildingRole != BuildingRole.Factory && buildingRole != BuildingRole.Warehouse) ||
            !IsUnlocked() || !GameEventManager.CanPlayerAct || GameResources.Instance == null)
        {
            return false;
        }

        BuildingInfo factory = FindBuilding(BuildingRole.Factory);
        BuildingInfo warehouse = FindBuilding(BuildingRole.Warehouse);
        return factory != null && warehouse != null && warehouse.IsUnlocked() &&
               SharedActionRules.CanStartTransfer(GameResources.Instance);
    }

    public void MoveGoodsToWarehouse()
    {
        if (!CanMoveGoods())
        {
            return;
        }

        float duration = GetTransferDurationSeconds();
        if (!GameResources.Instance.TryStartFactoryTransfer(duration))
        {
            return;
        }

        GameObject factoryDeliveryPoint =
            GameObject.Find("FactoryDeliveryPoint");

        GameObject drugStoreDeliveryPoint =
            GameObject.Find("DrugStoreDelivery");

        GameObject storeToFactoryReturnPoint =
            GameObject.Find("StoreToFactoryReturnpoint");

        GameObject factoryToStoreTurnPoint =
            GameObject.Find("FactoryToStoreTurnPoint");

        if (DeliveryVehicleManager.Instance != null &&
            factoryDeliveryPoint != null &&
            drugStoreDeliveryPoint != null &&
            storeToFactoryReturnPoint != null &&
            factoryToStoreTurnPoint != null)
        {
            DeliveryVehicleManager.Instance.StartGoodsTransferJourney(
                factoryDeliveryPoint.transform.position,
                drugStoreDeliveryPoint.transform.position,
                duration,
                returnDestination:
                    storeToFactoryReturnPoint.transform.position,
                outboundWaypoint:
                    factoryToStoreTurnPoint.transform.position
            );
        }
        else
        {
            Debug.LogWarning(
                "Goods transfer vehicle could not start; using timed transfer."
            );
        }

        GameEventManager.ReportPlayerActivity("🚚", $"Moving factory goods to the warehouse ({duration:0}s).");
        GameEventManager.CompletePlayerAction();
        Debug.Log($"Goods transfer started. Travel time: {duration:0} seconds.");
    }

    public void Upgrade()
    {
        if (!CanUpgrade() || GameResources.Instance == null)
        {
            return;
        }

        int cost = GetUpgradeCost();
        if (!ApplyActionEffects(-cost, upgradeRizik, upgradeRadnici))
        {
            return;
        }

        upgradeLevel++;
        ApplyUpgradeBenefits();
        GameEventManager.ReportPlayerActivity("⬆️", $"Upgraded {buildingName} to level {upgradeLevel} for {cost}.");
        GameEventManager.CompletePlayerAction();
        Debug.Log($"🏢 {buildingName}: upgraded to level {upgradeLevel}/{maxUpgradeLevel} for {cost} €.");
    }

    public bool CanIncreaseProduction()
    {
        if (!hasProductionControls || !showIncreaseAction || !IsUnlocked() ||
            !GameEventManager.CanPlayerAct || IsActionOnCooldown())
        {
            return false;
        }

        return (!actionsChangeLevel || productionLevel < maxProductionLevel) && HasEnoughMoneyFor(increaseNovac);
    }

    public bool CanDecreaseProduction()
    {
        if (!hasProductionControls || !showDecreaseAction || !IsUnlocked() ||
            !GameEventManager.CanPlayerAct || IsActionOnCooldown())
        {
            return false;
        }

        bool levelAllowsAction = !actionsChangeLevel || productionLevel > minProductionLevel ||
                                 (decreaseRadnici < 0 && HasEnoughWorkersFor(decreaseRadnici));
        return levelAllowsAction && HasEnoughMoneyFor(decreaseNovac) && HasEnoughWorkersFor(decreaseRadnici);
    }

    public void IncreaseProduction()
    {
        if (!CanIncreaseProduction() || GameResources.Instance == null)
        {
            return;
        }

        if (!ApplyActionEffects(increaseNovac, increaseRizik, increaseRadnici))
        {
            return;
        }

        if (actionsChangeLevel)
        {
            productionLevel++;
        }

        lastActionDay = GameEventManager.CurrentDay;
        GameEventManager.ReportPlayerActivity("📈", $"{buildingName}: {increaseButtonText}.");
        GameEventManager.CompletePlayerAction();
        Debug.Log($"🏢 {buildingName}: {increaseButtonText}.");
    }

    public void DecreaseProduction()
    {
        if (!CanDecreaseProduction() || GameResources.Instance == null)
        {
            return;
        }

        if (!ApplyActionEffects(decreaseNovac, decreaseRizik, decreaseRadnici))
        {
            return;
        }

        if (actionsChangeLevel)
        {
            productionLevel = Mathf.Max(minProductionLevel, productionLevel - 1);
        }

        lastActionDay = GameEventManager.CurrentDay;
        GameEventManager.ReportPlayerActivity("📉", $"{buildingName}: {decreaseButtonText}.");
        GameEventManager.CompletePlayerAction();
        Debug.Log($"🏢 {buildingName}: {decreaseButtonText}.");
    }

    private string GetProductionStatus()
    {
        if (!actionsChangeLevel)
        {
            return $"{statusName}: available";
        }

        string label = productionLevel == 0 ? neutralStatusText : productionLevel > 0 ? positiveStatusText : negativeStatusText;
        return $"{statusName}: {label} ({productionLevel}/{maxProductionLevel})";
    }

    private string GetGoodsProductionStatus()
    {
        if (IsProducingGoods)
        {
            return $"Production: {Mathf.CeilToInt(productionFinishTime - Time.time)} seconds remaining.";
        }

        GameResources resources = GameResources.Instance;
        string stock = resources == null ? "" :
            $" Factory stock: {resources.robaUTvornici}/{resources.kapacitetTvornice} g.";
        string workerPay = resources == null ? "" :
            $" Production bonus: {SharedActionRules.GetProductionWorkerCost(resources, productionWorkersRequired)} €.";
        return $"Production: {goodsPerBatch} g in {productionDurationSeconds:0} seconds, " +
               $"requires {productionWorkersRequired} free worker(s).{workerPay}{stock}";
    }

    private string GetWarehouseStatus()
    {
        GameResources resources = GameResources.Instance;
        if (resources == null)
        {
            return "Warehouse unavailable.";
        }

        string transfer = resources.TransportUTijeku ? $" {resources.robaUTransportu} g is being moved." : "";
        return $"Warehouse: {resources.robaUSkladistu}/{resources.kapacitetSkladista} g.{transfer}";
    }

    private string GetCorruptOfficerDescription()
    {
        GameResources resources = GameResources.Instance;
        if (resources == null)
        {
            return description;
        }

        if (!IsUnlocked())
        {
            return description + "\n\n" + purchaseDescription +
                   $"\n\nTrust payment: {purchaseCost} €";
        }

        string protectionStatus = resources.IsRiskProtectionActive
            ? $"Protection active for {resources.RiskProtectionTurnsRemaining} more turn(s)."
            : "Protection is not active.";
        string raidStatus = resources.WillSkipNextPoliceRaid
            ? "Your contact will cancel the next police raid."
            : "No raid bribe is prepared.";
        int cooldownRemaining = GetOfficerActionCooldownTurnsRemaining();
        string cooldownStatus = cooldownRemaining > 0
            ? $"The officer will not meet you for another {cooldownRemaining} turn(s)."
            : "The officer is available for another favor.";

        return description + "\n\n" +
               $"Current risk: {resources.rizik}\n" +
               $"Pay {riskProtectionCost} € to prevent all risk increases for {riskProtectionTurns} turns.\n" +
               $"Pay {raidBribeCost} € to cancel the next police raid; after it is blocked, risk falls to 30.\n\n" +
               protectionStatus + "\n" + raidStatus + "\n" + cooldownStatus;
    }

    private bool IsOfficerActionOnCooldown()
    {
        return GetOfficerActionCooldownTurnsRemaining() > 0;
    }

    private int GetOfficerActionCooldownTurnsRemaining()
    {
        int turnsPassed = GameEventManager.CurrentDay - lastOfficerActionDay;
        return Mathf.Max(0, officerActionCooldownTurns - turnsPassed);
    }

    private int GetEmergencyActionCost()
    {
        return emergencyActionBaseCost + emergencyActionCostIncrease * emergencyActionsPurchased;
    }

    private bool IsEmergencyActionOnCooldown()
    {
        return GetEmergencyActionCooldownTurnsRemaining() > 0;
    }

    private int GetEmergencyActionCooldownTurnsRemaining()
    {
        int turnsPassed = GameEventManager.CurrentDay - lastEmergencyActionDay;
        return Mathf.Max(0, emergencyActionCooldownTurns - turnsPassed);
    }

    private float GetTransferDurationSeconds()
    {
        BuildingInfo factory = FindBuilding(BuildingRole.Factory);
        BuildingInfo warehouse = FindBuilding(BuildingRole.Warehouse);
        if (factory == null || warehouse == null)
        {
            return 5f;
        }

        return Mathf.Max(5f, Vector3.Distance(factory.transform.position, warehouse.transform.position) / transferSpeed);
    }

    private static BuildingInfo FindBuilding(BuildingRole role)
    {
        BuildingInfo[] buildings = FindObjectsByType<BuildingInfo>(FindObjectsSortMode.None);
        foreach (BuildingInfo building in buildings)
        {
            if (building.buildingRole == role)
            {
                return building;
            }
        }

        return null;
    }

    private string GetActionDescriptions()
    {
        string actionText = "Choices:";

        if (showIncreaseAction)
        {
            actionText += $"\n- {GetIncreaseButtonLabel()}: {DescribeEffects(increaseNovac, increaseRizik, increaseRadnici)}";
        }

        if (showDecreaseAction)
        {
            actionText += $"\n- {GetDecreaseButtonLabel()}: {DescribeEffects(decreaseNovac, decreaseRizik, decreaseRadnici)}";
        }

        if (hasUpgrade && upgradeLevel < maxUpgradeLevel)
        {
            actionText += $"\n- {GetUpgradeButtonLabel()}: costs {GetUpgradeCost()} €. {DescribeEffects(0, upgradeRizik, upgradeRadnici)}";
        }

        if (buildingRole == BuildingRole.WorkerContact)
        {
            actionText += $"\n- {emergencyActionButtonText}: costs {GetEmergencyActionCost()} €, " +
                          $"adds {emergencyActionRisk} risk, and gives 1 net extra action.";
            int cooldownRemaining = GetEmergencyActionCooldownTurnsRemaining();
            if (cooldownRemaining > 0)
            {
                actionText += $" Available again in {cooldownRemaining} turn(s).";
            }
            else
            {
                actionText += $" The next use will cost {GetEmergencyActionCost() + emergencyActionCostIncrease} €.";
            }
        }

        return actionText;
    }

    private string DescribeEffects(int money, int risk, int workers)
    {
        string effects = "";
        AddEffect(ref effects, money, "€", true);
        AddEffect(ref effects, risk, "risk");
        AddEffect(ref effects, workers, "worker");

        return string.IsNullOrEmpty(effects) ? "no direct resource change." : effects + ".";
    }

    private void AddEffect(ref string effects, int amount, string label, bool money = false)
    {
        if (amount == 0)
        {
            return;
        }

        if (!string.IsNullOrEmpty(effects))
        {
            effects += ", ";
        }

        string sign = amount > 0 ? "+" : "";
        effects += money ? $"{sign}{amount} {label}" : $"{sign}{amount} {label}";
    }

    private string GetShortButtonLabel(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        int parenthesisIndex = text.IndexOf(" (");
        if (parenthesisIndex > 0)
        {
            return text.Substring(0, parenthesisIndex);
        }

        return text;
    }

    private string GetUpgradeStatus()
    {
        if (upgradeLevel >= maxUpgradeLevel)
        {
            return $"Upgrade: max level ({upgradeLevel}/{maxUpgradeLevel})";
        }

        return $"Upgrade: {upgradeLevel}/{maxUpgradeLevel}\n" +
               $"Next upgrade: {GetUpgradeCost()} €\n" +
               GetNextUpgradeDescription();
    }

    private int GetUpgradeCost()
    {
        if (buildingRole == BuildingRole.Factory)
        {
            int[] costs = { 700, 1400, 2500 };
            return costs[Mathf.Clamp(upgradeLevel, 0, costs.Length - 1)];
        }

        if (buildingRole == BuildingRole.Warehouse)
        {
            int[] costs = { 600, 1200, 2200 };
            return costs[Mathf.Clamp(upgradeLevel, 0, costs.Length - 1)];
        }

        if (buildingName == "Apartment")
        {
            int[] costs = { 800, 1500, 2500 };
            return costs[Mathf.Clamp(upgradeLevel, 0, costs.Length - 1)];
        }

        return upgradeCost + upgradeCostIncrease * upgradeLevel;
    }

    private string GetNextUpgradeDescription()
    {
        int nextLevel = Mathf.Clamp(upgradeLevel + 1, 1, maxUpgradeLevel);

        if (buildingRole == BuildingRole.Factory)
        {
            int[] batchSizes = { 50, 75, 100, 150 };
            int[] capacities = { 100, 150, 250, 400 };
            float[] durations = { 30f, 30f, 25f, 20f };
            return $"Next level: {batchSizes[nextLevel]} g per cycle, " +
                   $"{capacities[nextLevel]} g capacity, {durations[nextLevel]:0} second cycle.";
        }

        if (buildingRole == BuildingRole.Warehouse)
        {
            int[] capacities = { 100, 200, 350, 550 };
            return $"Next level: {capacities[nextLevel]} g warehouse capacity.";
        }

        if (buildingName == "Apartment")
        {
            int[] riskReduction = { 20, 30, 40, 50 };
            return $"Next level: Lay low reduces risk by {riskReduction[nextLevel]}. " +
                   "Cooldown remains 3 turns.";
        }

        return upgradeDescription;
    }

    private void ApplyUpgradeBenefits()
    {
        GameResources resources = GameResources.Instance;
        if (resources == null)
        {
            return;
        }

        if (buildingRole == BuildingRole.Factory)
        {
            int[] batchSizes = { 50, 75, 100, 150 };
            int[] capacities = { 100, 150, 250, 400 };
            float[] durations = { 30f, 30f, 25f, 20f };
            int level = Mathf.Clamp(upgradeLevel, 0, 3);
            goodsPerBatch = batchSizes[level];
            productionDurationSeconds = durations[level];
            resources.kapacitetTvornice = Mathf.Max(resources.kapacitetTvornice, capacities[level]);
        }
        else if (buildingRole == BuildingRole.Warehouse)
        {
            int[] capacities = { 100, 200, 350, 550 };
            resources.kapacitetSkladista = Mathf.Max(
                resources.kapacitetSkladista,
                capacities[Mathf.Clamp(upgradeLevel, 0, 3)]);
        }
        else if (buildingName == "Apartment")
        {
            int[] riskReduction = { 20, 30, 40, 50 };
            increaseRizik = -riskReduction[Mathf.Clamp(upgradeLevel, 0, 3)];
            actionCooldownEvents = Mathf.Max(3, actionCooldownEvents);
        }
    }

    private bool IsActionOnCooldown()
    {
        return GetCooldownDaysRemaining() > 0;
    }

    private bool HasEnoughMoneyFor(int moneyChange)
    {
        return moneyChange >= 0 ||
               (GameResources.Instance != null && GameResources.Instance.CanAfford(-moneyChange));
    }

    private bool ApplyActionEffects(int money, int risk, int workers)
    {
        GameResources resources = GameResources.Instance;
        return SharedActionRules.TryApplyResourceChange(resources, money, risk, workers);
    }

    private bool HasEnoughWorkersFor(int workerChange)
    {
        return workerChange >= 0 ||
               (GameResources.Instance != null && GameResources.Instance.SlobodniRadnici >= -workerChange);
    }

    private int GetCooldownDaysRemaining()
    {
        if (actionCooldownEvents <= 0)
        {
            return 0;
        }

        int daysPassed = GameEventManager.CurrentDay - lastActionDay;
        return Mathf.Max(0, actionCooldownEvents - daysPassed);
    }

    public void SetStoryLocked(bool locked)
    {
        storyLocked = locked;
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

    public void RefreshRenderers()
    {
        renderers = GetComponentsInChildren<MeshRenderer>();
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }
    }
}
