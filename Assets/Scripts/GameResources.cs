using System.Collections.Generic;
using UnityEngine;

public interface IActionResources
{
    int Money { get; set; }
    int FactoryGoods { get; set; }
    int WarehouseGoods { get; set; }
    int GoodsInTransit { get; set; }
    int FactoryCapacity { get; set; }
    int WarehouseCapacity { get; set; }
    int Risk { get; set; }
    int Influence { get; set; }
    int Workers { get; set; }
    int WorkerPayPerAction { get; }
    int FreeWorkers { get; }

    bool CanAfford(int amount);
    bool TrySpendMoney(int amount);
    bool TryConsumeWarehouseGoods(int amount);
    bool TryAssignWorkers(int count, float durationSeconds);
    void Clamp();
}

public static class SharedActionRules
{
    public static bool CanApplyResourceChange(IActionResources resources, int money, int workers)
    {
        return resources != null &&
               (money >= 0 || resources.CanAfford(-money)) &&
               (workers >= 0 || resources.FreeWorkers >= -workers);
    }

    public static bool TryApplyResourceChange(
        IActionResources resources,
        int money,
        int risk,
        int workers)
    {
        if (!CanApplyResourceChange(resources, money, workers))
        {
            return false;
        }

        if (money < 0)
        {
            resources.TrySpendMoney(-money);
        }
        else
        {
            resources.Money += money;
        }

        resources.Risk += risk;
        resources.Workers += workers;
        resources.Clamp();
        return true;
    }

    public static bool CanStartProduction(
        IActionResources resources,
        int goodsPerBatch,
        int workersRequired)
    {
        return resources != null && goodsPerBatch > 0 && workersRequired > 0 &&
               resources.CanAfford(resources.WorkerPayPerAction) &&
               resources.FactoryGoods + goodsPerBatch <= resources.FactoryCapacity &&
               resources.FreeWorkers >= workersRequired;
    }

    public static bool TryStartProduction(
        IActionResources resources,
        int goodsPerBatch,
        int workersRequired,
        float durationSeconds)
    {
        if (!CanStartProduction(resources, goodsPerBatch, workersRequired) ||
            !resources.TrySpendMoney(resources.WorkerPayPerAction))
        {
            return false;
        }

        if (resources.TryAssignWorkers(workersRequired, durationSeconds))
        {
            return true;
        }

        resources.Money += resources.WorkerPayPerAction;
        return false;
    }

    public static bool CanStartTransfer(IActionResources resources)
    {
        return resources != null && resources.GoodsInTransit <= 0 &&
               resources.FactoryGoods > 0 &&
               resources.WarehouseGoods < resources.WarehouseCapacity &&
               resources.FreeWorkers > 0;
    }

    public static bool TryStartTransfer(
        IActionResources resources,
        float durationSeconds,
        out int transferredGoods)
    {
        transferredGoods = 0;
        if (!CanStartTransfer(resources) || !resources.TryAssignWorkers(1, durationSeconds))
        {
            return false;
        }

        transferredGoods = Mathf.Min(
            resources.FactoryGoods,
            resources.WarehouseCapacity - resources.WarehouseGoods);
        resources.FactoryGoods -= transferredGoods;
        resources.GoodsInTransit = transferredGoods;
        return true;
    }

    public static bool CanStartDelivery(IActionResources resources, int goods)
    {
        return resources != null && goods > 0 &&
               resources.WarehouseGoods >= goods &&
               resources.CanAfford(resources.WorkerPayPerAction) &&
               resources.FreeWorkers > 0;
    }

    public static bool TryStartDelivery(
        IActionResources resources,
        int goods,
        float durationSeconds)
    {
        if (!CanStartDelivery(resources, goods) ||
            !resources.TrySpendMoney(resources.WorkerPayPerAction))
        {
            return false;
        }

        if (!resources.TryConsumeWarehouseGoods(goods))
        {
            resources.Money += resources.WorkerPayPerAction;
            return false;
        }

        if (resources.TryAssignWorkers(1, durationSeconds))
        {
            return true;
        }

        resources.WarehouseGoods += goods;
        resources.Money += resources.WorkerPayPerAction;
        return false;
    }

    public static void CompleteProduction(IActionResources resources, int goods)
    {
        if (resources == null || goods <= 0)
        {
            return;
        }

        resources.FactoryGoods = Mathf.Min(
            resources.FactoryCapacity,
            resources.FactoryGoods + goods);
    }

    public static void CompleteTransfer(IActionResources resources)
    {
        if (resources == null || resources.GoodsInTransit <= 0)
        {
            return;
        }

        resources.WarehouseGoods = Mathf.Min(
            resources.WarehouseCapacity,
            resources.WarehouseGoods + resources.GoodsInTransit);
        resources.GoodsInTransit = 0;
    }

    public static void CompleteDelivery(
        IActionResources resources,
        int reward,
        int risk,
        int influence)
    {
        if (resources == null)
        {
            return;
        }

        resources.Money += reward;
        resources.Risk += risk;
        resources.Influence += influence;
        resources.Clamp();
    }
}

[System.Serializable]
public class OpponentResources : IActionResources
{
    public int money;
    public int factoryGoods;
    public int warehouseGoods;
    public int goodsInTransit;
    [Min(1)] public int factoryCapacity = 100;
    [Min(1)] public int warehouseCapacity = 100;
    public int risk;
    [Range(0, 100)] public int influence;
    public int workers;
    [Min(0)] public int workerPayPerAction;

    public int factoryUpgradeLevel;
    public int warehouseUpgradeLevel;
    public int workerContactUpgradeLevel;
    public int apartmentUpgradeLevel;

    [SerializeField] private List<float> busyWorkersUntil = new List<float>();
    [SerializeField] private float productionFinishTime = -1f;
    [SerializeField] private int pendingProductionGoods;
    [SerializeField] private float transferFinishTime = -1f;

    public int BusyWorkers
    {
        get { return busyWorkersUntil.Count; }
    }

    public int FreeWorkers
    {
        get { return Mathf.Max(0, workers - BusyWorkers); }
    }

    public int TotalGoods
    {
        get { return factoryGoods + warehouseGoods + goodsInTransit; }
    }

    public bool IsProducing
    {
        get { return productionFinishTime >= 0f; }
    }

    public bool IsTransferring
    {
        get { return transferFinishTime >= 0f && goodsInTransit > 0; }
    }

    public int Money { get { return money; } set { money = value; } }
    public int FactoryGoods { get { return factoryGoods; } set { factoryGoods = value; } }
    public int WarehouseGoods { get { return warehouseGoods; } set { warehouseGoods = value; } }
    public int GoodsInTransit { get { return goodsInTransit; } set { goodsInTransit = value; } }
    public int FactoryCapacity { get { return factoryCapacity; } set { factoryCapacity = value; } }
    public int WarehouseCapacity { get { return warehouseCapacity; } set { warehouseCapacity = value; } }
    public int Risk { get { return risk; } set { risk = value; } }
    public int Influence { get { return influence; } set { influence = value; } }
    public int Workers { get { return workers; } set { workers = value; } }
    public int WorkerPayPerAction { get { return workerPayPerAction; } }

    public void InitializeFromPlayer(GameResources player)
    {
        money = player.novac;
        factoryGoods = player.robaUTvornici;
        warehouseGoods = player.robaUSkladistu;
        goodsInTransit = 0;
        factoryCapacity = player.kapacitetTvornice;
        warehouseCapacity = player.kapacitetSkladista;
        risk = player.rizik;
        influence = player.utjecaj;
        workers = player.radnici;
        workerPayPerAction = player.placaRadnikaPoZadatku;
        factoryUpgradeLevel = 0;
        warehouseUpgradeLevel = 0;
        workerContactUpgradeLevel = 0;
        apartmentUpgradeLevel = 0;
        busyWorkersUntil.Clear();
        productionFinishTime = -1f;
        pendingProductionGoods = 0;
        transferFinishTime = -1f;
        Clamp();
    }

    public bool CanAfford(int amount)
    {
        return amount >= 0 && money >= amount;
    }

    public bool TrySpendMoney(int amount)
    {
        if (!CanAfford(amount))
        {
            return false;
        }

        money -= amount;
        return true;
    }

    public void AddMoney(int amount)
    {
        money += amount;
    }

    public void AddFactoryGoods(int amount)
    {
        factoryGoods = Mathf.Clamp(factoryGoods + amount, 0, factoryCapacity);
    }

    public bool TryConsumeWarehouseGoods(int amount)
    {
        if (amount < 0 || warehouseGoods < amount)
        {
            return false;
        }

        warehouseGoods -= amount;
        return true;
    }

    public bool TryAssignWorkers(int count, float durationSeconds)
    {
        ReleaseFinishedWorkers();
        if (count <= 0 || FreeWorkers < count)
        {
            return false;
        }

        float busyUntil = Time.time + Mathf.Max(0.1f, durationSeconds);
        for (int i = 0; i < count; i++)
        {
            busyWorkersUntil.Add(busyUntil);
        }

        return true;
    }

    public void ReleaseFinishedWorkers()
    {
        busyWorkersUntil.RemoveAll(time => time <= Time.time);
    }

    public void AddInfluence(int amount)
    {
        influence = Mathf.Clamp(influence + amount, 0, 100);
    }

    public void ScheduleProduction(int goods, float durationSeconds)
    {
        pendingProductionGoods = goods;
        productionFinishTime = Time.time + Mathf.Max(0.1f, durationSeconds);
    }

    public void ScheduleTransfer(float durationSeconds)
    {
        transferFinishTime = Time.time + Mathf.Max(0.1f, durationSeconds);
    }

    public void UpdateTimedActions()
    {
        ReleaseFinishedWorkers();

        if (IsProducing && Time.time >= productionFinishTime)
        {
            SharedActionRules.CompleteProduction(this, pendingProductionGoods);
            productionFinishTime = -1f;
            pendingProductionGoods = 0;
            GameEventManager.ReportAiActivity("Production completed.");
        }

        if (IsTransferring && Time.time >= transferFinishTime)
        {
            SharedActionRules.CompleteTransfer(this);
            transferFinishTime = -1f;
            GameEventManager.ReportAiActivity("Goods arrived at the warehouse.");
        }
    }

    public void Clamp()
    {
        factoryCapacity = Mathf.Max(1, factoryCapacity);
        warehouseCapacity = Mathf.Max(1, warehouseCapacity);
        factoryGoods = Mathf.Clamp(factoryGoods, 0, factoryCapacity);
        warehouseGoods = Mathf.Clamp(warehouseGoods, 0, warehouseCapacity);
        goodsInTransit = Mathf.Max(0, goodsInTransit);
        risk = Mathf.Max(0, risk);
        influence = Mathf.Clamp(influence, 0, 100);
        workers = Mathf.Max(0, workers);
        workerPayPerAction = Mathf.Max(0, workerPayPerAction);
    }
}

public class GameResources : MonoBehaviour, IActionResources
{
    public static GameResources Instance { get; private set; }

    [Header("Resursi")]
    public int novac = 500;
    public int robaUTvornici = 50;
    public int robaUSkladistu = 0;
    public int robaUTransportu = 0;
    [Min(1)] public int kapacitetTvornice = 100;
    [Min(1)] public int kapacitetSkladista = 100;
    public int rizik = 10;
    [Range(0, 100)] public int utjecaj = 30;
    public int radnici = 1;

    [Header("Radnici")]
    [Min(0)] public int placaRadnikaPoZadatku = 15;
    [SerializeField] private List<float> radniciZauzetiDoVremena = new List<float>();

    [Header("AI Resources")]
    [SerializeField] private OpponentResources opponentResources = new OpponentResources();

    public OpponentResources Opponent
    {
        get { return opponentResources; }
    }

    private float zavrsetakTransporta = -1f;

    public int UkupnoRobe
    {
        get { return robaUTvornici + robaUSkladistu + robaUTransportu; }
    }

    public bool TransportUTijeku
    {
        get { return robaUTransportu > 0; }
    }

    public int ZauzetiRadnici
    {
        get { return radniciZauzetiDoVremena.Count; }
    }

    public int SlobodniRadnici
    {
        get { return Mathf.Max(0, radnici - ZauzetiRadnici); }
    }

    public int Money { get { return novac; } set { novac = value; } }
    public int FactoryGoods { get { return robaUTvornici; } set { robaUTvornici = value; } }
    public int WarehouseGoods { get { return robaUSkladistu; } set { robaUSkladistu = value; } }
    public int GoodsInTransit { get { return robaUTransportu; } set { robaUTransportu = value; } }
    public int FactoryCapacity { get { return kapacitetTvornice; } set { kapacitetTvornice = value; } }
    public int WarehouseCapacity { get { return kapacitetSkladista; } set { kapacitetSkladista = value; } }
    public int Risk { get { return rizik; } set { rizik = value; } }
    public int Influence { get { return utjecaj; } set { utjecaj = value; } }
    public int Workers { get { return radnici; } set { radnici = value; } }
    public int WorkerPayPerAction { get { return placaRadnikaPoZadatku; } }
    public int FreeWorkers { get { return SlobodniRadnici; } }

    [Header("Police")]
    public int policeRaidCount = 0;
    public int maxPoliceRaids = 3;

    [Header("Game state")]
    public bool gameOver = false;
    public string gameOverReason = "";
    public bool chapterEnded = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        opponentResources.InitializeFromPlayer(this);
    }

    void Update()
    {
        ReleaseFinishedWorkers();
        opponentResources.UpdateTimedActions();

        if (TransportUTijeku && Time.time >= zavrsetakTransporta)
        {
            SharedActionRules.CompleteTransfer(this);
            zavrsetakTransporta = -1f;
            Debug.Log("Goods transfer to the warehouse completed.");
        }
    }

    public void Apply(int dNovac, int dRizik, int dRadnici)
    {
        novac += dNovac;
        rizik += dRizik;
        radnici += dRadnici;

        EvaluateGameOver();
        Clamp();
    }

    public void AddFactoryGoods(int amount)
    {
        robaUTvornici = Mathf.Clamp(robaUTvornici + amount, 0, kapacitetTvornice);
    }

    public bool CanAfford(int amount)
    {
        return amount >= 0 && novac >= amount;
    }

    public bool TrySpendMoney(int amount)
    {
        if (!CanAfford(amount))
        {
            return false;
        }

        novac -= amount;
        return true;
    }

    public bool TryConsumeWarehouseGoods(int amount)
    {
        if (amount < 0 || robaUSkladistu < amount)
        {
            return false;
        }

        robaUSkladistu -= amount;
        return true;
    }

    public bool TryAssignWorkers(int count, float durationSeconds)
    {
        ReleaseFinishedWorkers();
        if (count <= 0 || SlobodniRadnici < count)
        {
            return false;
        }

        float busyUntilTime = Time.time + Mathf.Max(0.1f, durationSeconds);
        for (int i = 0; i < count; i++)
        {
            radniciZauzetiDoVremena.Add(busyUntilTime);
        }

        return true;
    }

    public bool TryStartFactoryTransfer(float durationSeconds)
    {
        if (!SharedActionRules.TryStartTransfer(this, durationSeconds, out int amount))
        {
            return false;
        }

        zavrsetakTransporta = Time.time + durationSeconds;
        return true;
    }

    public void ReleaseFinishedWorkers()
    {
        radniciZauzetiDoVremena.RemoveAll(time => time <= Time.time);
    }

    public void AddInfluence(int amount)
    {
        utjecaj = Mathf.Clamp(utjecaj + amount, 0, 100);
    }

    public void EvaluateGameOver()
    {
        if (gameOver)
        {
            return;
        }

        if (novac < 0)
        {
            gameOver = true;
            gameOverReason = "You ran out of money. Without cash, the operation cannot continue.";
        }
    }

    public void Clamp()
    {
        kapacitetTvornice = Mathf.Max(1, kapacitetTvornice);
        kapacitetSkladista = Mathf.Max(1, kapacitetSkladista);
        robaUTvornici = Mathf.Clamp(robaUTvornici, 0, kapacitetTvornice);
        robaUSkladistu = Mathf.Clamp(robaUSkladistu, 0, kapacitetSkladista);
        robaUTransportu = Mathf.Max(0, robaUTransportu);
        utjecaj = Mathf.Clamp(utjecaj, 0, 100);
        rizik = Mathf.Max(0, rizik);
        radnici = Mathf.Max(0, radnici);
    }
}
