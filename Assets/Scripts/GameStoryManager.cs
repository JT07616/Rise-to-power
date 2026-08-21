using System;
using System.Collections;
using UnityEngine;

public class GameStoryManager : MonoBehaviour
{
    private static GameStoryManager instance;

    private BuildingInfo apartment;
    private BuildingInfo corruptOfficer;
    private bool introStarted;
    private bool introCompleted;
    private bool apartmentRevealStarted;
    private bool apartmentPurchasedStoryShown;
    private bool officerRevealStarted;
    private bool officerPaidStoryShown;
    private bool firstPlayerDeliveryShown;
    private bool firstPlayerAmbushShown;
    private bool firstPlayerAttackShown;
    private bool firstRivalAttackShown;
    private bool firstDistrictCapturedShown;
    private bool firstRaidStoryShown;
    private bool pendingRaidStory;
    private bool pendingRaidAvoided;
    private bool highRiskStoryShown;
    private bool territory50StoryShown;
    private bool territory70StoryShown;
    private bool victorySequenceStarted;
    private bool territoryPercentInitialized;
    private int lastTerritoryPercent;

    void Awake()
    {
        instance = this;

        BuildingInfo[] buildings = FindObjectsByType<BuildingInfo>(FindObjectsSortMode.None);
        foreach (BuildingInfo building in buildings)
        {
            if (building == null)
            {
                continue;
            }

            if (building.buildingName == "Apartment")
            {
                apartment = building;
                apartment.SetStoryLocked(true);
            }
            else if (building.buildingRole == BuildingRole.CorruptOfficer)
            {
                corruptOfficer = building;
                corruptOfficer.SetStoryLocked(true);
            }
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void BeginIntro(Action onCompleted)
    {
        if (introStarted)
        {
            onCompleted?.Invoke();
            return;
        }

        introStarted = true;
        GameEventManager.NotifyPlayer(
            "COMMANDS",
            "Left click a building or delivery marker to interact.\n" +
            "Right-drag to rotate the camera and use the mouse wheel to zoom.\n\n" +
            "You receive 3 actions from 00:00 to 12:00, then Volkov acts from 12:00 to 24:00. " +
            "Every real second advances the clock by 15 minutes. " +
            "Gameplay time pauses while the camera focuses or an important message is open.");
        GameEventManager.NotifyPlayer(
            "NADIA \"KEYS\" SOKOLOVA",
            "So you actually came.\n\n" +
            "The factory is abandoned, the warehouse is barely standing, and every useful street belongs to someone else. " +
            "Produce goods, move them into storage, and take the city one district at a time.\n\n" +
            "District control runs from -3 to +3. Risk 100 brings a police raid; three raids end your operation. " +
            "Control 90% of the city and you win.\n\n" +
            "I also have a contact watching police dispatch. Open the Police Station if you want to assign a worker to ambush one of Volkov's deliveries.\n\n" +
            "And if anyone asks, we have never met.");
        GameEventManager.NotifyPlayer(
            "VIKTOR \"GHOST\" VOLKOV",
            "A stranger opens a factory in my city and expects me not to notice.\n\n" +
            "I know where your goods come from. I know where they are stored. Soon I will know everyone who works for you.\n\n" +
            "Leave now, and you may keep what little you have. Stay, and I will teach you why they call me Ghost.",
            () =>
            {
                introCompleted = true;
                onCompleted?.Invoke();
            });
    }

    public static void ReportSuccessfulPlayerDelivery()
    {
        if (instance == null || instance.firstPlayerDeliveryShown)
        {
            return;
        }

        instance.firstPlayerDeliveryShown = true;
        instance.StartApartmentReveal();
    }

    public static bool ReportPlayerAmbushSucceeded(int stolenMoney, int recoveredGoods)
    {
        if (instance == null || instance.firstPlayerAmbushShown)
        {
            return false;
        }

        instance.firstPlayerAmbushShown = true;
        GameEventManager.NotifyPlayer(
            "VIKTOR \"GHOST\" VOLKOV",
            $"Your people took {stolenMoney} money and {recoveredGoods} g from my convoy. Enjoy it.\n\n" +
            "You found my route through police dispatch records. Clever. But records also contain names, addresses, and vehicle plates.\n\n" +
            "Now I know exactly where to start looking for you.");
        return true;
    }

    public static void ReportTerritoryDelivery(
        TerritoryOwner side,
        int previousScore,
        int currentScore,
        bool captured)
    {
        if (instance == null)
        {
            return;
        }

        if (side == TerritoryOwner.Player && previousScore < 0 && !instance.firstPlayerAttackShown)
        {
            instance.firstPlayerAttackShown = true;
            GameEventManager.NotifyPlayer(
                "VIKTOR \"GHOST\" VOLKOV",
                "One of your vehicles crossed into my district today.\n\n" +
                "My people wanted to burn it. I told them to let it pass. I wanted you to believe you had succeeded.\n\n" +
                "Try it again.");
        }

        if (side == TerritoryOwner.AI && previousScore > 0 && !instance.firstRivalAttackShown)
        {
            instance.firstRivalAttackShown = true;
            GameEventManager.NotifyPlayer(
                "VIKTOR \"GHOST\" VOLKOV",
                "Your customers opened their doors for my people. No resistance. No loyalty. Just fear.\n\n" +
                "You paint streets blue and call them yours. I only need one night to remind them who owns this city.");
        }

        if (side == TerritoryOwner.Player && captured && currentScore > 0 &&
            !instance.firstDistrictCapturedShown)
        {
            instance.firstDistrictCapturedShown = true;
            GameEventManager.NotifyPlayer(
                "NADIA \"KEYS\" SOKOLOVA",
                "The district is talking about you now.\n\n" +
                "Some call you brave. Others call you temporary. Either way, Volkov is listening.");
        }
    }

    public static void ReportRiskChanged(int risk)
    {
        if (instance == null)
        {
            return;
        }

        if (risk >= 50 && !instance.officerRevealStarted)
        {
            instance.StartOfficerReveal();
        }

        if (risk >= 80 && !instance.highRiskStoryShown &&
            (instance.corruptOfficer == null || !instance.corruptOfficer.IsStoryLocked))
        {
            instance.highRiskStoryShown = true;
            GameEventManager.NotifyPlayer(
                "NADIA \"KEYS\" SOKOLOVA",
                "Patrols are asking about your vehicles. Detectives are watching the warehouse.\n\n" +
                "Use the apartment. Pay Cross. Stop moving goods if you must. Owning the city will mean nothing if you are inside a cell when it happens.");
        }
    }

    public static void ReportBuildingPurchased(BuildingInfo building)
    {
        if (instance == null || building == null || building != instance.apartment ||
            instance.apartmentPurchasedStoryShown)
        {
            return;
        }

        instance.apartmentPurchasedStoryShown = true;
        GameEventManager.NotifyPlayer(
            "NADIA \"KEYS\" SOKOLOVA",
            "Do not bring workers here. Do not store goods here. And never use the same route twice.\n\n" +
            "This place is for disappearing when the city becomes too interested in you.");
    }

    public static void ReportOfficerPaid()
    {
        if (instance == null || instance.officerPaidStoryShown)
        {
            return;
        }

        instance.officerPaidStoryShown = true;
        GameEventManager.NotifyPlayer(
            "DETECTIVE ELIAS \"LEDGER\" CROSS",
            "You are not buying protection. You are buying time.\n\nProtection costs much more.");
    }

    public static void ReportPoliceRaid(bool avoided)
    {
        if (instance == null || instance.firstRaidStoryShown)
        {
            return;
        }

        if (instance.corruptOfficer != null && instance.corruptOfficer.IsStoryLocked)
        {
            instance.pendingRaidStory = true;
            instance.pendingRaidAvoided = avoided;
            return;
        }

        instance.firstRaidStoryShown = true;
        GameEventManager.NotifyPlayer(
            "DETECTIVE ELIAS \"LEDGER\" CROSS",
            avoided
                ? "The raid team received a new address ten minutes before departure.\n\n" +
                  "Some unfortunate warehouse across town is having a very bad evening. You are welcome."
                : "I warned you that reports were accumulating.\n\n" +
                  "Next time, call me before the doors come down. Afterward is always more expensive.");
    }

    public static void ReportTerritoryPercent(int territoryPercent)
    {
        if (instance == null)
        {
            return;
        }

        int previousTerritoryPercent = instance.lastTerritoryPercent;
        instance.lastTerritoryPercent = territoryPercent;
        if (!instance.territoryPercentInitialized)
        {
            instance.territoryPercentInitialized = true;
            return;
        }

        if (!instance.introCompleted)
        {
            return;
        }

        if (previousTerritoryPercent < 50 && territoryPercent >= 50 &&
            !instance.territory50StoryShown)
        {
            instance.territory50StoryShown = true;
            GameEventManager.NotifyPlayer(
                "VIKTOR \"GHOST\" VOLKOV",
                "Half the city.\n\n" +
                "Do you know how many bodies are buried beneath those streets? You inherited every debt, every enemy and every promise made there.\n\n" +
                "Territory is easy to take. Keeping it is where people die.");
        }

        if (previousTerritoryPercent < 70 && territoryPercent >= 70 &&
            !instance.territory70StoryShown)
        {
            instance.territory70StoryShown = true;
            string pressureLine = instance.corruptOfficer != null && !instance.corruptOfficer.IsStoryLocked
                ? "Even Cross has stopped returning my calls."
                : "Even the men I paid for years have stopped returning my calls.";
            GameEventManager.NotifyPlayer(
                "VIKTOR \"GHOST\" VOLKOV",
                "Seventy percent.\n\n" +
                $"My soldiers are leaving. My friends no longer answer their phones. {pressureLine}\n\n" +
                "But I still own enough of this city to bury you. Come for the rest.");
        }
    }

    public static bool BeginVictorySequence(int territoryPercent, Action onCompleted)
    {
        if (instance == null)
        {
            return false;
        }

        if (instance.victorySequenceStarted)
        {
            return true;
        }

        instance.victorySequenceStarted = true;
        GameEventManager.NotifyPlayer(
            "VIKTOR \"GHOST\" VOLKOV",
            $"{territoryPercent} percent. The city has made its decision.\n\n" +
            "Do not celebrate too loudly. Power attracts strangers, just as my power attracted you.\n\n" +
            "One day, someone will enter your city and you will send them the same warning I sent you. Now you understand.");
        GameEventManager.NotifyPlayer(
            "NADIA \"KEYS\" SOKOLOVA",
            "Volkov is gone. His people are offering names, addresses and loyalty.\n\n" +
            "The city is yours.\n\n" +
            "The question is what kind of ruler you intend to become.",
            onCompleted);
        return true;
    }

    private void StartApartmentReveal()
    {
        if (apartmentRevealStarted)
        {
            return;
        }

        apartmentRevealStarted = true;
        StartCoroutine(RevealBuilding(
            apartment,
            "NADIA \"KEYS\" SOKOLOVA",
            "Your first customer is satisfied. That means people will start remembering your face.\n\n" +
            "I have an apartment under a dead man's name. No questions, no neighbors and no connection to your operation.\n\n" +
            "Pay me, and the keys are yours.",
            null));
    }

    private void StartOfficerReveal()
    {
        if (officerRevealStarted)
        {
            return;
        }

        officerRevealStarted = true;
        StartCoroutine(RevealBuilding(
            corruptOfficer,
            "DETECTIVE ELIAS \"LEDGER\" CROSS",
            "Your name appeared in three reports this morning. By noon, two reports were missing. That was not luck. That was me.\n\n" +
            "Do not approach me at the station. Meet me after my shift, away from uniforms, cameras, and officers who still believe in their badge.\n\n" +
            "Volkov pays for police loyalty. You can pay for police forgetfulness.",
            () =>
            {
                if (corruptOfficer != null)
                {
                    corruptOfficer.buildingName = "Detective Elias \"Ledger\" Cross";
                    corruptOfficer.description =
                        "Meet Cross here outside duty hours. He can suppress risk increases or redirect the next police raid, provided his price is paid.";
                }

                if (GameResources.Instance != null && GameResources.Instance.rizik >= 80 && !highRiskStoryShown)
                {
                    ReportRiskChanged(GameResources.Instance.rizik);
                }

                if (pendingRaidStory)
                {
                    pendingRaidStory = false;
                    ReportPoliceRaid(pendingRaidAvoided);
                }
            }));
    }

    private IEnumerator RevealBuilding(
        BuildingInfo building,
        string title,
        string body,
        Action onRevealed)
    {
        SimpleStrategyCamera camera = FindFirstObjectByType<SimpleStrategyCamera>();
        BuildingPopupUI openBuildingPopup = FindFirstObjectByType<BuildingPopupUI>();
        if (openBuildingPopup != null && openBuildingPopup.IsOpen)
        {
            openBuildingPopup.ClosePanel();
            yield return null;
        }

        while (!GameEventManager.IsPlayerTurn || GameEventManager.IsPopupOpen ||
               GameEventManager.IsPauseMenuOpen ||
               DeliveryOrderManager.IsPopupOpen || (camera != null && camera.IsFocusing))
        {
            yield return null;
        }

        if (building == null)
        {
            GameEventManager.NotifyPlayer(title, body, onRevealed);
            yield break;
        }

        GameEventManager.FocusCameraOnWorldPosition(building.LabelPosition, () =>
        {
            GameEventManager.NotifyPlayer(title, body, () =>
            {
                building.SetStoryLocked(false);
                onRevealed?.Invoke();
            });
        });
    }
}
