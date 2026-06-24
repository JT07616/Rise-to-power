using UnityEngine;

public class BuildingInfo : MonoBehaviour
{
    public string buildingName = "Building";

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
    public int increaseReputacija = 0;
    public int increaseKvaliteta = 0;
    public int increaseStabilnost = -3;
    public int increaseRadnici = 0;
    public int increaseMoral = 0;
    public int increaseEfikasnost = 5;

    [Header("Decrease action effects")]
    public int decreaseNovac = -60;
    public int decreaseRizik = -8;
    public int decreaseReputacija = 0;
    public int decreaseKvaliteta = 0;
    public int decreaseStabilnost = 3;
    public int decreaseRadnici = 0;
    public int decreaseMoral = 0;
    public int decreaseEfikasnost = -5;

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
    public int upgradeReputacija = 0;
    public int upgradeKvaliteta = 0;
    public int upgradeStabilnost = 5;
    public int upgradeRadnici = 0;
    public int upgradeMoral = 0;
    public int upgradeEfikasnost = 10;

    public Color hoverColor = Color.yellow;
    public Color selectedColor = Color.blue;

    private Renderer[] renderers;
    private Color[] originalColors;
    private int lastActionEvent = -1000;

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

    public string GetFullDescription()
    {
        if (!hasProductionControls)
        {
            return description;
        }

        string fullDescription = description + "\n\n" + productionDescription;

        if (requiresPurchase && !isPurchased)
        {
            return fullDescription + "\n\n" + purchaseDescription + $"\n\nPrice: {purchaseCost} €";
        }

        fullDescription += "\n\n" + GetProductionStatus();
        fullDescription += "\n\n" + GetActionDescriptions();

        if (hasUpgrade)
        {
            fullDescription += "\n" + GetUpgradeStatus();
        }

        int cooldownRemaining = GetCooldownEventsRemaining();
        if (cooldownRemaining > 0)
        {
            fullDescription += $"\nAction available after {cooldownRemaining} more event(s).";
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

    public bool IsUnlocked()
    {
        return !requiresPurchase || isPurchased;
    }

    public bool CanPurchase()
    {
        return hasProductionControls && requiresPurchase && !isPurchased &&
               GameResources.Instance != null && GameResources.Instance.novac >= purchaseCost;
    }

    public void Purchase()
    {
        if (!CanPurchase())
        {
            return;
        }

        GameResources.Instance.Apply(
            dNovac: -purchaseCost, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
        isPurchased = true;
        Debug.Log($"🏢 {buildingName}: location bought for {purchaseCost} €.");
    }

    public bool CanUpgrade()
    {
        return hasProductionControls && hasUpgrade && IsUnlocked() &&
               upgradeLevel < maxUpgradeLevel && HasEnoughMoneyFor(-GetUpgradeCost());
    }

    public void Upgrade()
    {
        if (!CanUpgrade() || GameResources.Instance == null)
        {
            return;
        }

        int cost = GetUpgradeCost();
        upgradeLevel++;
        GameResources.Instance.Apply(
            dNovac: -cost, dRizik: upgradeRizik, dReputacija: upgradeReputacija, dKvaliteta: upgradeKvaliteta,
            dStabilnost: upgradeStabilnost, dRadnici: upgradeRadnici, dMoral: upgradeMoral, dEfikasnost: upgradeEfikasnost);
        Debug.Log($"🏢 {buildingName}: upgraded to level {upgradeLevel}/{maxUpgradeLevel} for {cost} €.");
    }

    public bool CanIncreaseProduction()
    {
        if (!hasProductionControls || !showIncreaseAction || !IsUnlocked() || IsActionOnCooldown())
        {
            return false;
        }

        return (!actionsChangeLevel || productionLevel < maxProductionLevel) && HasEnoughMoneyFor(increaseNovac);
    }

    public bool CanDecreaseProduction()
    {
        if (!hasProductionControls || !showDecreaseAction || !IsUnlocked() || IsActionOnCooldown())
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

        if (actionsChangeLevel)
        {
            productionLevel++;
        }

        GameResources.Instance.Apply(
            dNovac: increaseNovac, dRizik: increaseRizik, dReputacija: increaseReputacija, dKvaliteta: increaseKvaliteta,
            dStabilnost: increaseStabilnost, dRadnici: increaseRadnici, dMoral: increaseMoral, dEfikasnost: increaseEfikasnost);
        lastActionEvent = GameEventManager.EventsCompleted;
        Debug.Log($"🏢 {buildingName}: {increaseButtonText}.");
    }

    public void DecreaseProduction()
    {
        if (!CanDecreaseProduction() || GameResources.Instance == null)
        {
            return;
        }

        if (actionsChangeLevel)
        {
            productionLevel = Mathf.Max(minProductionLevel, productionLevel - 1);
        }

        GameResources.Instance.Apply(
            dNovac: decreaseNovac, dRizik: decreaseRizik, dReputacija: decreaseReputacija, dKvaliteta: decreaseKvaliteta,
            dStabilnost: decreaseStabilnost, dRadnici: decreaseRadnici, dMoral: decreaseMoral, dEfikasnost: decreaseEfikasnost);
        lastActionEvent = GameEventManager.EventsCompleted;
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

    private string GetActionDescriptions()
    {
        string actionText = "Choices:";

        if (showIncreaseAction)
        {
            actionText += $"\n- {GetIncreaseButtonLabel()}: {DescribeEffects(increaseNovac, increaseRizik, increaseReputacija, increaseKvaliteta, increaseStabilnost, increaseRadnici, increaseMoral, increaseEfikasnost)}";
        }

        if (showDecreaseAction)
        {
            actionText += $"\n- {GetDecreaseButtonLabel()}: {DescribeEffects(decreaseNovac, decreaseRizik, decreaseReputacija, decreaseKvaliteta, decreaseStabilnost, decreaseRadnici, decreaseMoral, decreaseEfikasnost)}";
        }

        if (hasUpgrade && upgradeLevel < maxUpgradeLevel)
        {
            actionText += $"\n- {GetUpgradeButtonLabel()}: costs {GetUpgradeCost()} €. {DescribeEffects(0, upgradeRizik, upgradeReputacija, upgradeKvaliteta, upgradeStabilnost, upgradeRadnici, upgradeMoral, upgradeEfikasnost)}";
        }

        return actionText;
    }

    private string DescribeEffects(int money, int risk, int reputation, int quality, int stability, int workers, int morale, int efficiency)
    {
        string effects = "";
        AddEffect(ref effects, money, "€", true);
        AddEffect(ref effects, risk, "risk");
        AddEffect(ref effects, reputation, "reputation");
        AddEffect(ref effects, quality, "quality");
        AddEffect(ref effects, stability, "stability");
        AddEffect(ref effects, workers, "worker");
        AddEffect(ref effects, morale, "morale");
        AddEffect(ref effects, efficiency, "efficiency");

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
               upgradeDescription;
    }

    private int GetUpgradeCost()
    {
        return upgradeCost + upgradeCostIncrease * upgradeLevel;
    }

    private bool IsActionOnCooldown()
    {
        return GetCooldownEventsRemaining() > 0;
    }

    private bool HasEnoughMoneyFor(int moneyChange)
    {
        return moneyChange >= 0 ||
               (GameResources.Instance != null && GameResources.Instance.novac >= -moneyChange);
    }

    private bool HasEnoughWorkersFor(int workerChange)
    {
        return workerChange >= 0 ||
               (GameResources.Instance != null && GameResources.Instance.radnici >= -workerChange);
    }

    private int GetCooldownEventsRemaining()
    {
        if (actionCooldownEvents <= 0)
        {
            return 0;
        }

        int eventsPassed = GameEventManager.EventsCompleted - lastActionEvent;
        return Mathf.Max(0, actionCooldownEvents - eventsPassed);
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
