using UnityEngine;

public class AmbushTrapManager : MonoBehaviour
{
    private static AmbushTrapManager instance;

    [Header("Ambush rules")]
    [Min(1)] public int cooldownDays = 2;
    [Range(0f, 1f)] public float rivalAmbushChance = 0.25f;
    [Min(1)] public int minimumRivalDeliveryGoods = 10;

    private TerritoryOwner trapOwner = TerritoryOwner.Neutral;
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

    void OnGUI()
    {
        if (!GameEventManager.IsPlayerTurn || GameEventManager.IsPauseMenuOpen ||
            GameEventManager.IsPopupOpen || DeliveryOrderManager.IsPopupOpen ||
            BuildingPopupUI.IsAnyOpen)
        {
            return;
        }

        Rect buttonRect = new Rect(Screen.width - 205f, 48f, 190f, 32f);
        if (trapOwner == TerritoryOwner.Player)
        {
            GUI.Label(buttonRect, "Ambush active: worker is waiting");
            return;
        }

        if (trapOwner == TerritoryOwner.AI)
        {
            GUI.Label(buttonRect, "Rival may have set an ambush");
            return;
        }

        if (GameEventManager.CurrentDay < playerAmbushAvailableDay)
        {
            GUI.Label(buttonRect, $"Ambush ready on day {playerAmbushAvailableDay}");
            return;
        }

        GUI.enabled = CanPlayerSetAmbush;
        if (GUI.Button(buttonRect, "Set delivery ambush"))
        {
            TrySetPlayerAmbush();
        }
        GUI.enabled = true;
    }

    public static bool TrySetPlayerAmbush()
    {
        if (!CanPlayerSetAmbush || !GameResources.Instance.TryReserveWorker())
        {
            return false;
        }

        instance.trapOwner = TerritoryOwner.Player;
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
        GameEventManager.ReportAiActivity("Set a delivery ambush.");
        return true;
    }

    public static bool CanRivalSetAmbush()
    {
        return instance != null && instance.trapOwner == TerritoryOwner.Neutral &&
               GameEventManager.CurrentDay >= instance.rivalAmbushAvailableDay &&
               GameResources.Instance != null && GameResources.Instance.Opponent.FreeWorkers > 0;
    }

    public static bool TryTriggerAmbush(TerritoryOwner deliveryOwner, int deliveredGoods, int deliveryValue)
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
        if (winner == TerritoryOwner.Player)
        {
            instance.playerAmbushAvailableDay = GameEventManager.CurrentDay + instance.cooldownDays;
            GameEventManager.NotifyPlayer(
                "AMBUSH SUCCESS",
                $"Rival delivery was intercepted. You took {stolenMoney} money and {recoveredGoods} g of goods.");
        }
        else
        {
            instance.rivalAmbushAvailableDay = GameEventManager.CurrentDay + instance.cooldownDays;
            GameEventManager.NotifyPlayer(
                "RIVAL AMBUSH",
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
