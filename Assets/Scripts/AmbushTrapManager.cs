using UnityEngine;

public class AmbushTrapManager : MonoBehaviour
{
    private static AmbushTrapManager instance;

    [Header("Ambush rules")]
    [Min(1)] public int cooldownDays = 2;
    [Range(0f, 1f)] public float rivalAmbushChance = 0.25f;
    [Min(1)] public int minimumRivalDeliveryGoods = 10;

    private TerritoryOwner trapOwner = TerritoryOwner.Neutral;
    private static float lastAmbushTime = -1f;

    // presretnuta posiljka nije isporucena, pa se preskace efekt dolaska
    public static bool JustAmbushed
    {
        get { return Time.time - lastAmbushTime < 0.2f; }
    }

    private int playerAmbushAvailableDay = 1;
    private int rivalAmbushAvailableDay = 1;

    public static bool HasPlayerAmbush
    {
        get { return instance != null && instance.trapOwner == TerritoryOwner.Player; }
    }

    public static bool CanPlayerSetAmbush
    {
        get
        {
            GameResources resources = GameResources.Instance;
            return instance != null && GameEventManager.CanPlayerAct && resources != null &&
                   instance.trapOwner == TerritoryOwner.Neutral &&
                   GameEventManager.CurrentDay >= instance.playerAmbushAvailableDay &&
                   resources.SlobodniRadnici > 0;
        }
    }

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        ReleaseTrapWorker();
        if (instance == this)
        {
            instance = null;
        }
    }

    public static string GetPlayerAmbushButtonLabel()
    {
        if (instance == null)
        {
            return "Ambush unavailable";
        }

        if (instance.trapOwner == TerritoryOwner.Player)
        {
            return "Ambush active";
        }

        if (instance.trapOwner == TerritoryOwner.AI)
        {
            return "No route available";
        }

        if (GameEventManager.CurrentDay < instance.playerAmbushAvailableDay)
        {
            return $"Ready on day {instance.playerAmbushAvailableDay}";
        }

        return "Set delivery ambush";
    }

    public static string GetPlayerAmbushStatus()
    {
        if (instance == null)
        {
            return "Delivery-route intelligence is unavailable.";
        }

        if (instance.trapOwner == TerritoryOwner.Player)
        {
            return "Ambush: active; one worker is waiting for Volkov's next suitable delivery.";
        }

        if (instance.trapOwner == TerritoryOwner.AI)
        {
            return "Ambush: Cross cannot confirm a safe route right now.";
        }

        if (GameEventManager.CurrentDay < instance.playerAmbushAvailableDay)
        {
            return $"Ambush: new route intelligence arrives on day {instance.playerAmbushAvailableDay}.";
        }

        return "Ambush: available; requires one free worker and one action.";
    }

    public static bool TrySetPlayerAmbush()
    {
        if (!CanPlayerSetAmbush || !GameResources.Instance.TryReserveWorker())
        {
            return false;
        }

        instance.trapOwner = TerritoryOwner.Player;
        GameEventManager.ReportPlayerActivity("🕵️", "Set an ambush for Volkov's next suitable delivery.");
        GameEventManager.CompletePlayerAction();
        Debug.Log("Player set a delivery ambush. The assigned worker remains busy until the rival is caught.");
        return true;
    }

    public static bool TrySetRivalAmbush()
    {
        if (instance == null || instance.trapOwner != TerritoryOwner.Neutral ||
            GameEventManager.CurrentDay < instance.rivalAmbushAvailableDay ||
            Random.value > instance.rivalAmbushChance || GameResources.Instance == null)
        {
            return false;
        }

        OpponentResources rival = GameResources.Instance.Opponent;
        if (rival.FreeWorkers <= 0 || !rival.TryReserveWorker())
        {
            return false;
        }

        instance.trapOwner = TerritoryOwner.AI;
        GameEventManager.ReportAiActivity("Set a delivery ambush.", "🕵️");
        return true;
    }

    public static bool CanRivalSetAmbush()
    {
        return instance != null && instance.trapOwner == TerritoryOwner.Neutral &&
               GameEventManager.CurrentDay >= instance.rivalAmbushAvailableDay &&
               GameResources.Instance != null && GameResources.Instance.Opponent.FreeWorkers > 0;
    }

    public static bool TryTriggerAmbush(
        TerritoryOwner deliveryOwner,
        int deliveredGoods,
        int deliveryValue,
        Vector3? spot = null)
    {
        if (instance == null || instance.trapOwner == TerritoryOwner.Neutral ||
            instance.trapOwner == deliveryOwner)
        {
            return false;
        }

        if (instance.trapOwner == TerritoryOwner.AI &&
            deliveredGoods < instance.minimumRivalDeliveryGoods)
        {
            return false;
        }

        GameResources player = GameResources.Instance;
        if (player == null)
        {
            return false;
        }

        IActionResources victim = deliveryOwner == TerritoryOwner.Player
            ? player
            : player.Opponent;
        IActionResources ambusher = instance.trapOwner == TerritoryOwner.Player
            ? player
            : player.Opponent;
        int stolenMoney = Mathf.Min(victim.Money, Mathf.Max(0, deliveryValue));
        int recoveredGoods = Mathf.Min(
            Mathf.Max(0, deliveredGoods),
            Mathf.Max(0, ambusher.WarehouseCapacity - ambusher.WarehouseGoods));

        victim.Money -= stolenMoney;
        ambusher.Money += stolenMoney;
        ambusher.WarehouseGoods += recoveredGoods;
        victim.Clamp();
        ambusher.Clamp();

        TerritoryOwner winner = instance.trapOwner;
        instance.ReleaseTrapWorker();
        instance.trapOwner = TerritoryOwner.Neutral;

        lastAmbushTime = Time.time;
        if (spot.HasValue) AmbushEffect.Play(spot.Value, winner);
        if (winner == TerritoryOwner.Player)
        {
            instance.playerAmbushAvailableDay = GameEventManager.CurrentDay + instance.cooldownDays;
            GameEventManager.ReportPlayerActivity(
                "⚔️",
                $"Ambushed Volkov: took {stolenMoney} money and recovered {recoveredGoods} g.");
            if (!GameStoryManager.ReportPlayerAmbushSucceeded(stolenMoney, recoveredGoods))
            {
                GameEventManager.NotifyPlayer(
                    "AMBUSH SUCCESS",
                    $"Volkov's delivery was intercepted. You took {stolenMoney} money and {recoveredGoods} g of goods.");
            }
        }
        else
        {
            instance.rivalAmbushAvailableDay = GameEventManager.CurrentDay + instance.cooldownDays;
            GameEventManager.ReportAiActivity(
                $"Ambushed your delivery: took {stolenMoney} money and {deliveredGoods} g.",
                "⚔️");
            GameEventManager.NotifyPlayer(
                "VOLKOV AMBUSH",
                $"Your delivery was intercepted. You lost {stolenMoney} money and {deliveredGoods} g of goods.");
        }

        Debug.Log($"{winner} ambush intercepted a {deliveryOwner} delivery: {stolenMoney} money, {recoveredGoods} g recovered.");
        return true;
    }

    private void ReleaseTrapWorker()
    {
        if (trapOwner == TerritoryOwner.Player && GameResources.Instance != null)
        {
            GameResources.Instance.ReleaseReservedWorker();
        }
        else if (trapOwner == TerritoryOwner.AI && GameResources.Instance != null)
        {
            GameResources.Instance.Opponent.ReleaseReservedWorker();
        }
    }
}
