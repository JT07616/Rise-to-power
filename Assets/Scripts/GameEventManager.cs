using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameEventManager : MonoBehaviour
{
    private const int VictoryTerritoryPercent = 90;

    public static bool IsPopupOpen { get; private set; }
    public static bool IsPauseMenuOpen { get; private set; }
    public static int EventsCompleted { get; private set; }
    public static int CurrentDay { get; private set; }
    public static int PlayerActionsRemaining { get; private set; }
    public static int AiActionsRemaining { get; private set; }
    public static bool IsPlayerTurn { get; private set; }
    public static float PlayerTurnTimeRemaining { get; private set; }
    public static float AiTurnTimeRemaining { get; private set; }
    public static bool IsPlayerTurnAnnouncementActive
    {
        get { return instance != null && instance.playerTurnAnnouncementActive; }
    }
    public static bool CanPlayerAct
    {
        get
        {
            return instance != null && IsPlayerTurn && PlayerActionsRemaining > 0 &&
                   !instance.playerTurnAnnouncementActive && !IsPauseMenuOpen &&
                   GameResources.Instance != null &&
                   !GameResources.Instance.gameOver && !GameResources.Instance.chapterEnded;
        }
    }

    private static string PlayerDisplayName
    {
        get
        {
            return string.IsNullOrWhiteSpace(CharacterSelect.playerName)
                ? "Player"
                : CharacterSelect.playerName.Trim();
        }
    }

    private static GameEventManager instance;

    [Header("Turn system")]
    [Min(1)] public int actionsPerSide = 3;
    [Min(1f)] public float playerTurnDuration = 48f;
    [Min(1f)] public float aiTurnDuration = 48f;
    [Min(0f)] public float aiActionDelay = 0.6f;

    [Header("Timing")]
    [Tooltip("Seconds before the first event after game start.")]
    public float firstEventDelay = 0f;

    [Tooltip("Seconds between consecutive events.")]
    public float delayBetweenEvents = 30f;

    [Header("Risk")]
    [Min(0)] public int dailyRiskReduction = 5;
    [Min(0)] public int idleDeliveryRiskReduction = 5;

    [Header("Style")]
    public int barHeight = 48;
    public int popupWidth = 640;
    public int popupHeight = 420;

    [Header("Popup and pause menu images")]
    public Texture2D okButtonImage;
    public Texture2D howToPlayBackgroundImage;
    public Texture2D pauseMenuBackgroundImage;
    public Texture2D continueButtonImage;
    public Texture2D quitButtonImage;
    public Texture2D hudPanelBackgroundImage;
    public Texture2D turnPanelBackgroundImage;

    [Header("Mini map territory")]
    [Range(0f, 100f)] public float opponentTerritoryPercent = 30f;
    public Color playerTerritoryColor = new Color(0.1f, 0.35f, 1f, 0.42f);
    public Color neutralTerritoryColor = new Color(0.45f, 0.45f, 0.45f, 0.48f);
    public Color opponentTerritoryColor = new Color(0.9f, 0.1f, 0.1f, 0.42f);

    [Header("AI facility labels")]
    public Texture2D[] aiFactoryMapLabelImages;
    public Texture2D[] aiWarehouseMapLabelImages;
    public Texture2D[] aiApartmentMapLabelImages;
    public Texture2D[] aiWorkerContactMapLabelImages;

    [Header("Delivery order labels")]
    public Texture2D safeOrderLabelImage;
    public Texture2D volkovOrderLabelImage;
    public Texture2D neutralOrderLabelImage;
    public Texture2D orderPopupBackgroundImage;
    public Texture2D closeButtonImage;
    public Texture2D acceptOrderButtonImage;

    public Texture2D popupBackground;
    public Texture2D maleBackground;
    public Texture2D femaleBackground;

    [Header("Resource bar backgrounds")]
    public Texture2D moneyIcon;
    public Texture2D workersIcon;
    public Texture2D factoryIcon;
    public Texture2D storeIcon;
    public Texture2D riskIcon;
    public Texture2D influenceIcon;

    [Header("Audio")]
    public AudioSource uiAudioSource;
    public AudioClip popupOpenSound;
    public AudioClip buttonClickSound;
    public AudioClip popupCloseSound;

    private bool eventActive;
    private GameEvent currentEvent;
    private bool isNotification;
    private Queue<GameEvent> notificationQueue = new Queue<GameEvent>();
    private bool notificationPausedGame;
    private float notificationPreviousTimeScale = 1f;
    private Func<GameEvent> pendingNext;
    private float previousTimeScale = 1f;
    private Camera miniMapCamera;
    private RenderTexture miniMapTexture;
    private SimpleStrategyCamera strategyCamera;
    private bool playerTerritoryOnLeft = true;
    private readonly List<MiniMapParcel> miniMapParcels = new List<MiniMapParcel>();
    private float miniMapParcelArea;
    private bool playerTurnAnnouncementActive;
    private int playerTurnCountdown;
    private bool aiTurnAnnouncementActive;
    private int aiTurnCountdown;
    private bool playerStartedDeliveryToday;
    private bool victoryShown;
    private readonly List<ActivityEntry> activityLog = new List<ActivityEntry>();

    private bool IsTurnProgressPaused
    {
        get
        {
            return IsPauseMenuOpen || notificationPausedGame ||
                   (strategyCamera != null && strategyCamera.IsFocusing);
        }
    }

    private class ActivityEntry
    {
        public string text;
        public Color color;
    }

    private class MiniMapParcel
    {
        public Vector3 center;
        public Vector3 size;
        public float area;
        public bool playerOwned;
        public GameObject source;
    }

    void Awake()
    {
        instance = this;
    }

    [Serializable]
    public class EventOption
    {
        public string text;
        public Action onChoose;

        public EventOption(string text, Action onChoose)
        {
            this.text = text;
            this.onChoose = onChoose;
        }
    }

    [Serializable]
    public class GameEvent
    {
        public string name;
        public string description;
        public List<EventOption> options;
    }

    void Start()
    {
        // Time scale survives scene changes. A game opened after leaving a paused scene must
        // always start with a running simulation.
        Time.timeScale = 1f;
        IsPauseMenuOpen = false;

        MenuMusic music = FindFirstObjectByType<MenuMusic>();

        if (music != null)
        {
            Destroy(music.gameObject);
        }

        EventsCompleted = 0;
        CurrentDay = 1;
        AiActionsRemaining = actionsPerSide;
        IsPlayerTurn = false;
        PlayerActionsRemaining = 0;
        PlayerTurnTimeRemaining = 0f;
        AiTurnTimeRemaining = 0f;
        IsPopupOpen = false;

        DeliveryOrderManager deliveryOrders = GetComponent<DeliveryOrderManager>();
        if (deliveryOrders == null)
        {
            deliveryOrders = gameObject.AddComponent<DeliveryOrderManager>();
        }
        deliveryOrders.ConfigureLabelImages(
            safeOrderLabelImage,
            volkovOrderLabelImage,
            neutralOrderLabelImage,
            orderPopupBackgroundImage,
            closeButtonImage,
            acceptOrderButtonImage);

        if (GetComponent<AmbushTrapManager>() == null)
        {
            gameObject.AddComponent<AmbushTrapManager>();
        }

        if (GetComponent<TerritoryDistrictManager>() == null)
        {
            gameObject.AddComponent<TerritoryDistrictManager>();
        }

        GameStoryManager storyManager = GetComponent<GameStoryManager>();
        if (storyManager == null)
        {
            storyManager = gameObject.AddComponent<GameStoryManager>();
        }

        SetupMiniMap();
        AddActivity("SYSTEM", "Operation started.", new Color(0.75f, 0.75f, 0.75f));

        if (CharacterSelect.playerCharacter == "Male")
        {
            popupBackground = maleBackground;
        }
        else if (CharacterSelect.playerCharacter == "Female")
        {
            popupBackground = femaleBackground;
        }

        storyManager.BeginIntro(() => StartCoroutine(BeginPlayerTurn()));
    }

    void Update()
    {
        UpdateTurnTimer();

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsPauseMenuOpen)
            {
                ContinueGame();
            }
            else
            {
                OpenPauseMenu();
            }
        }
    }

    void UpdateTurnTimer()
    {
        if (!IsPlayerTurn || playerTurnAnnouncementActive || IsTurnProgressPaused ||
            GameResources.Instance == null ||
            GameResources.Instance.gameOver || GameResources.Instance.chapterEnded)
        {
            return;
        }

        PlayerTurnTimeRemaining = Mathf.Max(0f, PlayerTurnTimeRemaining - Time.unscaledDeltaTime);
        if (PlayerTurnTimeRemaining <= 0f)
        {
            ReportPlayerActivity("⏰", "Your phase reached 12:00; unused actions expired.");
            BeginAiTurn();
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (IsPauseMenuOpen)
        {
            ContinueGame();
        }

        if (miniMapCamera != null)
        {
            Destroy(miniMapCamera.gameObject);
        }

        if (miniMapTexture != null)
        {
            miniMapTexture.Release();
            Destroy(miniMapTexture);
        }

        RestoreTimeAfterNotification();
    }

    void ShowEvent(GameEvent e)
    {
        currentEvent = e;
        eventActive = true;
        IsPopupOpen = true;

        if (uiAudioSource != null && popupOpenSound != null)
        {
            uiAudioSource.PlayOneShot(popupOpenSound);
        }
    }

    private void PlayButtonClick()
    {
        if (uiAudioSource != null && buttonClickSound != null)
        {
            uiAudioSource.PlayOneShot(buttonClickSound);
        }
    }

    private void PlayPopupClose()
    {
        if (uiAudioSource != null && popupCloseSound != null)
        {
            uiAudioSource.PlayOneShot(popupCloseSound);
        }
    }

    void Choose(int idx)
    {
        if (currentEvent == null) return;
        bool isGameOverChoice = GameResources.Instance != null && GameResources.Instance.gameOver;
        if (!isNotification && !isGameOverChoice && !CanPlayerAct) return;

        PlayButtonClick();

        string eventName = currentEvent.name;
        var chosen = currentEvent.options[idx];
        bool wasNotification = isNotification;

        eventActive = false;
        currentEvent = null;
        IsPopupOpen = false;
        isNotification = false;

        if (wasNotification)
        {
            RestoreTimeAfterNotification();
        }

        chosen.onChoose?.Invoke();

        if (!wasNotification)
        {
            ReportPlayerActivity("🎲", $"{eventName}: {chosen.text}.");
            EventsCompleted++;
            CompletePlayerAction();

            if (GameResources.Instance != null && GameResources.Instance.gameOver)
            {
                StopAllCoroutines();
                ShowGameOver();
                return;
            }
        }

        ContinueChain();
    }

    public static void CompletePlayerAction()
    {
        if (instance == null || !IsPlayerTurn || PlayerActionsRemaining <= 0)
        {
            return;
        }

        if (GameResources.Instance != null && GameResources.Instance.gameOver)
        {
            instance.StopAllCoroutines();
            instance.ShowGameOver();
            return;
        }

        PlayerActionsRemaining--;

        if (PlayerActionsRemaining <= 0)
        {
            instance.BeginAiTurn();
        }
    }

    public static void GrantEmergencyAction()
    {
        if (instance == null || !IsPlayerTurn || PlayerActionsRemaining <= 0 ||
            GameResources.Instance == null || GameResources.Instance.gameOver)
        {
            return;
        }

        // The favor pays for the action used to arrange it and leaves one net extra action.
        PlayerActionsRemaining++;
    }

    public static void ReportPlayerDeliveryStarted()
    {
        if (instance != null)
        {
            instance.playerStartedDeliveryToday = true;
        }
    }

    void BeginAiTurn()
    {
        if (!IsPlayerTurn)
        {
            return;
        }

        IsPlayerTurn = false;
        PlayerActionsRemaining = 0;
        PlayerTurnTimeRemaining = 0f;
        AiActionsRemaining = actionsPerSide;
        AiTurnTimeRemaining = aiTurnDuration;
        ReportAiActivity("Volkov is planning his move...", "👻");
        aiTurnAnnouncementActive = true;
        IsPopupOpen = true;
        StartCoroutine(BeginAiTurnCountdown());
    }

    IEnumerator BeginAiTurnCountdown()
    {
        for (int countdown = 4; countdown >= 1; countdown--)
        {
            aiTurnCountdown = countdown;
            yield return StartCoroutine(WaitForTurnCountdownSecond());
        }

        aiTurnCountdown = 0;
        aiTurnAnnouncementActive = false;
        AiTurnTimeRemaining = aiTurnDuration;
        IsPopupOpen = eventActive;
        StartCoroutine(PlayAiTurn());
    }

    IEnumerator PlayAiTurn()
    {
        float actionInterval = Mathf.Max(aiActionDelay, aiTurnDuration / (actionsPerSide + 1f));
        float timeUntilAction = actionInterval;

        while (AiActionsRemaining > 0 && AiTurnTimeRemaining > 0f)
        {
            if (IsTurnProgressPaused)
            {
                yield return null;
                continue;
            }

            float turnDeltaTime = Time.unscaledDeltaTime;
            AiTurnTimeRemaining = Mathf.Max(0f, AiTurnTimeRemaining - turnDeltaTime);
            timeUntilAction -= turnDeltaTime;

            if (timeUntilAction > 0f)
            {
                yield return null;
                continue;
            }

            ExecuteRandomAiAction();
            AiActionsRemaining--;
            timeUntilAction = actionInterval;
            yield return null;
        }

        AiTurnTimeRemaining = 0f;
        CurrentDay++;
        ApplyConsequences();

        if (GameResources.Instance != null && GameResources.Instance.gameOver)
        {
            ShowGameOver();
            yield break;
        }

        yield return StartCoroutine(BeginPlayerTurn());

        if (!eventActive && notificationQueue.Count > 0)
        {
            ShowNotification(notificationQueue.Dequeue());
        }
    }

    void ExecuteRandomAiAction()
    {
        OpponentResources ai = GameResources.Instance != null ? GameResources.Instance.Opponent : null;
        if (ai == null)
        {
            return;
        }

        List<Func<bool>> availableActions = new List<Func<bool>>();
        const int hireCost = SharedActionRules.WorkerHireCost;
        int productionGoods = ai.FactoryProductionGoods;
        const int productionWorkers = 1;
        float productionSeconds = ai.FactoryProductionDurationSeconds;
        const float transferSeconds = 10f;

        if (ai.CanUpgradeFactory)
        {
            availableActions.Add(() =>
            {
                if (!ai.TryUpgradeFactory(out int cost))
                {
                    return false;
                }

                ReportAiActivity(
                    $"Upgraded factory to level {ai.factoryUpgradeLevel} for {cost}.",
                    "??");
                FocusCameraOnAiFacility(AiFacilityRole.Factory);
                return true;
            });
        }

        if (ai.CanUpgradeWarehouse)
        {
            availableActions.Add(() =>
            {
                if (!ai.TryUpgradeWarehouse(out int cost))
                {
                    return false;
                }

                ReportAiActivity(
                    $"Upgraded warehouse to level {ai.warehouseUpgradeLevel} for {cost}.",
                    "??");
                FocusCameraOnAiFacility(AiFacilityRole.Warehouse);
                return true;
            });
        }

        if (ai.CanUpgradeApartment)
        {
            availableActions.Add(() =>
            {
                if (!ai.TryUpgradeApartment(out int cost))
                {
                    return false;
                }

                ReportAiActivity(
                    $"Upgraded apartment to level {ai.apartmentUpgradeLevel} for {cost}.",
                    "??");
                FocusCameraOnAiFacility(AiFacilityRole.Apartment);
                return true;
            });
        }

        if (ai.workers < 5 && SharedActionRules.CanApplyResourceChange(ai, -hireCost, 1))
        {
            availableActions.Add(() =>
            {
                bool completed = SharedActionRules.TryApplyResourceChange(ai, -hireCost, 0, 1);
                if (completed)
                {
                    ReportAiActivity("Hired one worker.", "👷");
                    FocusCameraOnAiFacility(AiFacilityRole.WorkerContact);
                }
                return completed;
            });
        }

        if (!ai.IsProducing &&
            SharedActionRules.CanStartProduction(ai, productionGoods, productionWorkers))
        {
            availableActions.Add(() =>
            {
                bool started = SharedActionRules.TryStartProduction(
                    ai,
                    productionGoods,
                    productionWorkers,
                    productionSeconds);
                if (started)
                {
                    ai.ScheduleProduction(productionGoods, productionSeconds);
                    ReportAiActivity($"Started production of {productionGoods} g.", "🏭");
                    FocusCameraOnAiFacility(AiFacilityRole.Factory);
                }
                return started;
            });
        }

        if (!ai.IsTransferring && SharedActionRules.CanStartTransfer(ai))
        {
            availableActions.Add(() =>
            {
                bool started = SharedActionRules.TryStartTransfer(ai, transferSeconds, out int amount);
                if (started)
                {
                    ai.ScheduleTransfer(transferSeconds);
                    ReportAiActivity($"Moving {amount} g to the warehouse.", "🚚");
                    AnimateAiTransfer(transferSeconds);
                }
                return started;
            });
        }

        if (DeliveryOrderManager.CanAiStartDelivery())
        {
            availableActions.Add(() => DeliveryOrderManager.TryStartRandomAiDelivery());
        }

        if (AmbushTrapManager.CanRivalSetAmbush())
        {
            availableActions.Add(() => AmbushTrapManager.TrySetRivalAmbush());
        }

        if (AmbushTrapManager.CanRivalSetAmbush())
        {
            availableActions.Add(() => AmbushTrapManager.TrySetRivalAmbush());
        }

        if (availableActions.Count == 0)
        {
            ReportAiActivity("No valid action was available; Volkov waited.", "⏳");
            return;
        }

        availableActions[UnityEngine.Random.Range(0, availableActions.Count)].Invoke();
    }

    static void FocusCameraOnAiFacility(AiFacilityRole role)
    {
        AiFacilityMarker facility = AiFacilityMarker.Find(role);
        if (facility != null)
        {
            FocusCameraOnWorldPosition(facility.LabelPosition);
        }
    }

    public static void FocusCameraOnWorldPosition(Vector3 worldPosition)
    {
        FocusCameraOnWorldPosition(worldPosition, null);
    }

    public static void FocusCameraOnWorldPosition(Vector3 worldPosition, Action onCompleted)
    {
        SimpleStrategyCamera camera = instance != null ? instance.strategyCamera : null;
        if (camera == null)
        {
            camera = FindFirstObjectByType<SimpleStrategyCamera>();
        }
        if (camera != null)
        {
            camera.FocusOn(worldPosition, onCompleted);
        }
        else
        {
            onCompleted?.Invoke();
        }
    }

    static void AnimateAiTransfer(float durationSeconds)
    {
        AiFacilityMarker factory = AiFacilityMarker.Find(AiFacilityRole.Factory);
        AiFacilityMarker warehouse = AiFacilityMarker.Find(AiFacilityRole.Warehouse);
        if (factory == null || warehouse == null)
        {
            return;
        }

        FocusCameraOnWorldPosition(Vector3.Lerp(factory.LabelPosition, warehouse.LabelPosition, 0.5f));
        if (DeliveryVehicleManager.Instance != null)
        {
            DeliveryVehicleManager.Instance.StartGoodsTransferJourney(
                factory.transform.position,
                warehouse.transform.position,
                durationSeconds,
                rival: true);
        }
    }

    public static void ReportPlayerActivity(string emoji, string message)
    {
        if (instance == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        instance.AddActivity(PlayerDisplayName, message, new Color(0.35f, 0.82f, 1f));
        Debug.Log($"{PlayerDisplayName}: {message}");
    }

    public static void ReportAiActivity(string message, string emoji = "👻")
    {
        if (instance == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        instance.AddActivity("VOLKOV", message, new Color(1f, 0.42f, 0.42f));
        Debug.Log($"Volkov: {message}");
    }

    void AddActivity(string actor, string message, Color color)
    {
        activityLog.Add(new ActivityEntry
        {
            text = $"{GetActivityClock()}  {actor}: {message}",
            color = color
        });
        while (activityLog.Count > 7)
        {
            activityLog.RemoveAt(0);
        }
    }

    string GetActivityClock()
    {
        return $"D{CurrentDay} {FormatDayClock()}";
    }

    string FormatDayClock()
    {
        float elapsedSeconds;
        if (IsPlayerTurn)
        {
            elapsedSeconds = Mathf.Clamp(
                playerTurnDuration - PlayerTurnTimeRemaining,
                0f,
                playerTurnDuration);
        }
        else if (AiTurnTimeRemaining > 0f)
        {
            elapsedSeconds = playerTurnDuration + Mathf.Clamp(
                aiTurnDuration - AiTurnTimeRemaining,
                0f,
                aiTurnDuration);
        }
        else
        {
            elapsedSeconds = 0f;
        }

        int totalMinutes = Mathf.Clamp(Mathf.FloorToInt(elapsedSeconds) * 15, 0, 1440);
        return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
    }

    public static void NotifyPlayer(string title, string body)
    {
        NotifyPlayer(title, body, null);
    }

    public static void NotifyPlayer(string title, string body, Action onClose)
    {
        if (instance != null)
        {
            instance.EnqueueNotification(title, body, onClose);
            instance.TryShowQueuedNotification();
        }
    }

    IEnumerator BeginPlayerTurn()
    {
        if (strategyCamera != null)
        {
            strategyCamera.CancelFocus();
        }

        IsPlayerTurn = true;
        PlayerActionsRemaining = actionsPerSide;
        PlayerTurnTimeRemaining = playerTurnDuration;
        playerTurnAnnouncementActive = true;
        IsPopupOpen = true;

        for (int countdown = 4; countdown >= 1; countdown--)
        {
            playerTurnCountdown = countdown;
            yield return StartCoroutine(WaitForTurnCountdownSecond());
        }

        playerTurnCountdown = 0;
        playerTurnAnnouncementActive = false;
        PlayerTurnTimeRemaining = playerTurnDuration;
        ReportPlayerActivity("🌅", $"Day {CurrentDay} started with {actionsPerSide} actions.");
        IsPopupOpen = eventActive;
    }

    IEnumerator WaitForTurnCountdownSecond()
    {
        float timeRemaining = 1f;
        while (timeRemaining > 0f)
        {
            if (!IsTurnProgressPaused)
            {
                timeRemaining -= Time.unscaledDeltaTime;
            }

            yield return null;
        }
    }

    void ContinueChain()
    {
        if (notificationQueue.Count > 0)
        {
            ShowNotification(notificationQueue.Dequeue());
            return;
        }
        SchedulePending();
    }

    void Next(Func<GameEvent> nextBuilder)
    {
        if (nextBuilder == null) return;
        if (GameResources.Instance != null && (GameResources.Instance.gameOver || GameResources.Instance.chapterEnded)) return;
        pendingNext = nextBuilder;
    }

    void SchedulePending()
    {
        if (pendingNext == null) return;
        var b = pendingNext;
        pendingNext = null;
        StartCoroutine(DelayedShow(b));
    }

    void ShowNotification(GameEvent notif)
    {
        if (BuildingPopupUI.IsAnyOpen)
        {
            BuildingPopupUI buildingPopup = FindFirstObjectByType<BuildingPopupUI>();
            if (buildingPopup != null && buildingPopup.IsOpen)
            {
                buildingPopup.ClosePanel();
            }
        }

        if (!notificationPausedGame)
        {
            notificationPreviousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            notificationPausedGame = true;
        }
        isNotification = true;
        ShowEvent(notif);
    }

    void EnqueueNotification(string title, string body, Action onClose = null)
    {
        notificationQueue.Enqueue(new GameEvent
        {
            name = title,
            description = body,
            options = new List<EventOption>
            {
                new EventOption("OK", () => onClose?.Invoke())
            }
        });
    }

    void TryShowQueuedNotification()
    {
        bool introCanShow = CurrentDay == 1 && !IsPlayerTurn &&
                            PlayerActionsRemaining == 0 && AiTurnTimeRemaining <= 0f;
        if (notificationQueue.Count > 0 && !eventActive &&
            !playerTurnAnnouncementActive && !aiTurnAnnouncementActive && !IsPauseMenuOpen &&
            (IsPlayerTurn || introCanShow))
        {
            ShowNotification(notificationQueue.Dequeue());
        }
    }

    void RestoreTimeAfterNotification()
    {
        if (!notificationPausedGame)
        {
            return;
        }

        Time.timeScale = notificationPreviousTimeScale;
        notificationPausedGame = false;
    }

    void ApplyConsequences()
    {
        var R = GameResources.Instance;
        if (R == null || R.chapterEnded || R.gameOver) return;

        int playerWagesPaid = R.PayDailyWorkerWages(out int playerWorkersWhoLeft);
        R.Opponent.PayDailyWorkerWages(out _);
        bool policeRaidTriggered = R.rizik >= 100;
        int riskBeforeReduction = R.rizik;
        int riskReduction = dailyRiskReduction +
                            (playerStartedDeliveryToday ? 0 : idleDeliveryRiskReduction);
        R.rizik = Mathf.Max(0, R.rizik - riskReduction);
        playerStartedDeliveryToday = false;
        ReportPlayerActivity(
            "🌙",
            $"Day settled: paid {playerWagesPaid} in wages; risk reduced by {riskBeforeReduction - R.rizik}.");
        if (playerWorkersWhoLeft > 0)
        {
            ReportPlayerActivity("👋", $"{playerWorkersWhoLeft} worker(s) left over unpaid wages.");
            EnqueueNotification(
                "UNPAID WAGES",
                $"You paid {playerWagesPaid} € in daily wages. {playerWorkersWhoLeft} worker(s) left because the rest could not be paid.");
        }

        if (policeRaidTriggered)
        {
            if (R.ConsumeRaidBribe())
            {
                ReportPlayerActivity("🛡️", "Cross cancelled a police raid; risk reset to 30.");
                EnqueueNotification(
                    "POLICE RAID AVOIDED",
                    "Your contact buried the report before the raid could begin.\n\n" +
                    "No money or workers were lost.\nRisk reset to 30."
                );
                GameStoryManager.ReportPoliceRaid(true);
                R.EvaluateGameOver();
                R.Clamp();
                return;
            }

            R.policeRaidCount++;
            int seizure = Mathf.Max(0, R.novac * 30 / 100);
            R.novac -= seizure;
            R.rizik = 30;
            int workerLost = 0;
            Debug.Log($"🚓 Policija te uhvatila! Zapljena: -{seizure} €, rizik = 30.");
            if (R.radnici > 0 && UnityEngine.Random.Range(0, 100) < 20)
            {
                R.radnici -= 1;
                workerLost = 1;
                Debug.Log("🚓 Jedan radnik je uhvaćen.");
            }

            ReportPlayerActivity(
                "🚨",
                $"Police raid #{R.policeRaidCount}: lost {seizure} money and {workerLost} worker(s); risk reset to 30.");

            string body =
                "Sirens. Doors kicked in. Half your stash is gone " +
                "and your name is on every report tonight.\n\n" +
                $"Money seized: −{seizure} €\n" +
                "Risk reset to 30\n" +
                $"Police raids: {R.policeRaidCount}/{R.maxPoliceRaids}";
            if (workerLost > 0) body += "\nA worker was arrested (−1)";
            EnqueueNotification("🚓 POLICE RAID!", body);
            GameStoryManager.ReportPoliceRaid(false);

            if (R.policeRaidCount >= R.maxPoliceRaids)
            {
                R.gameOver = true;
                R.gameOverReason =
                    "The police have caught you too many times. " +
                    "After the last raid, there is no operation left to save.";
            }
        }

        R.EvaluateGameOver();
        R.Clamp();
    }

    void ShowGameOver()
    {
        currentEvent = new GameEvent
        {
            name = "💀 GAME OVER",
            description = GameResources.Instance.gameOverReason,
            options = new List<EventOption>
            {
                new EventOption("End", () =>
                {
                    eventActive = false;
                    currentEvent = null;
                    IsPopupOpen = false;
                    GameResources.Instance.chapterEnded = true;
                    SceneManager.LoadScene("MainMenu");
                })
            }
        };
        eventActive = true;
        IsPopupOpen = true;
    }

    void ShowVictory(int territoryPercent)
    {
        if (victoryShown || GameResources.Instance == null)
        {
            return;
        }

        victoryShown = true;
        StopAllCoroutines();
        IsPlayerTurn = false;
        PlayerActionsRemaining = 0;
        AiActionsRemaining = 0;
        PlayerTurnTimeRemaining = 0f;
        AiTurnTimeRemaining = 0f;
        notificationQueue.Clear();
        pendingNext = null;
        GameResources.Instance.chapterEnded = true;
        isNotification = true;

        ShowEvent(new GameEvent
        {
            name = "VICTORY",
            description =
                $"You control {territoryPercent}% of the city.\n\n" +
                "Volkov no longer has enough territory to challenge your power. " +
                "The city belongs to you.",
            options = new List<EventOption>
            {
                new EventOption("Return to Main Menu", () =>
                {
                    SceneManager.LoadScene("MainMenu");
                })
            }
        });
    }

    IEnumerator DelayedShow(Func<GameEvent> builder)
    {
        yield return new WaitForSeconds(delayBetweenEvents);
        while (!IsPlayerTurn || eventActive || notificationQueue.Count > 0)
        {
            yield return null;
        }
        ShowEvent(builder());
    }

#if false // Retained only as an archive of the disabled legacy story.
    GameEvent BuildEvent1_Arrival()
    {
        return new GameEvent
        {
            name = "Arrival",
            description =
                "\"You're new in this city.\"\n" +
                "\"No one knows you. No one's looking for you.\"\n" +
                "\"That's your only advantage.\"\n\n" +
                "You own only one small location on the edge of the district, " +
                "modest starting capital, and basic equipment — just enough to begin. " +
                "This event has no choice, it only introduces a state of complete humility. " +
                "You are invisible, but also without influence. It is exactly this position " +
                "that allows the first step.",
            options = new List<EventOption>
            {
                new EventOption("Continue", () =>
                {
                    Debug.Log("➡️ Dolazak — priča počinje.");
                    Next(BuildEvent2_FirstProduction);
                })
            }
        };
    }

    GameEvent BuildEvent2_FirstProduction()
    {
        return new GameEvent
        {
            name = "First Production",
            description =
                "\"The setup is rough, but it'll do.\"\n" +
                "\"The question is — fast, or careful?\"\n\n" +
                "The lab already exists, but it is modest, unstable, and far from ideal. " +
                "You must decide whether to chase a quick profit or proceed cautiously.",
            options = new List<EventOption>
            {
                new EventOption("Fast production", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 100, dRizik: 5, dReputacija: 0, dKvaliteta: -10,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Brza proizvodnja: +100 €, kvaliteta -10, rizik +5");
                    Next(BuildEvent3_FirstWorker);
                }),
                new EventOption("Careful production", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 60, dRizik: 2, dReputacija: 0, dKvaliteta: 10,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Pažljiva proizvodnja: +60 €, kvaliteta +10, rizik +2");
                    Next(BuildEvent3_FirstWorker);
                })
            }
        };
    }

    GameEvent BuildEvent3_FirstWorker()
    {
        return new GameEvent
        {
            name = "First Worker",
            description =
                "\"Don't ask questions. Just tell me — am I in?\"\n" +
                "\"...and maybe don't leave me alone with your stuff. People say things.\"\n\n" +
                "Soon after the first production a potential associate appears. " +
                "He is quick, resourceful, and knows some buyers — but he doesn't seem like " +
                "someone you can trust without reservation.",
            options = new List<EventOption>
            {
                new EventOption("Hire", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 10, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 1, dMoral: 0, dEfikasnost: 25);
                    Debug.Log("➡️ Prvi radnik zaposlen: +1 radnik, efikasnost +25, rizik +10");
                    Next(BuildEvent4_FirstDistribution);
                }),
                new EventOption("Decline", () =>
                {
                    Debug.Log("➡️ Prvi radnik odbijen — bez promjene.");
                    Next(BuildEvent4_FirstDistribution);
                })
            }
        };
    }

    GameEvent BuildEvent4_FirstDistribution()
    {
        return new GameEvent
        {
            name = "First Distribution",
            description =
                "\"Time to put your product out there.\"\n" +
                "\"First move is always the riskiest.\"\n\n" +
                "The shipment is small, but symbolically important — the system is now running. " +
                "There is a 30% chance something goes wrong.",
            options = new List<EventOption>
            {
                new EventOption("Send the shipment", () =>
                {
                    if (UnityEngine.Random.Range(0, 100) < 30)
                    {
                        GameResources.Instance.Apply(
                            dNovac: -50, dRizik: 0, dReputacija: -3, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("➡️ Problem s distribucijom: -50 €, reputacija -3");
                    }
                    else
                    {
                        GameResources.Instance.Apply(
                            dNovac: 150, dRizik: 5, dReputacija: 5, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("➡️ Uspješna distribucija: +150 €, reputacija +5, rizik +5");
                    }
                    Next(BuildEvent5_CustomerComplaint);
                })
            }
        };
    }

    GameEvent BuildEvent5_CustomerComplaint()
    {
        return new GameEvent
        {
            name = "First Customer Complaint",
            description =
                "\"This isn't what I expected.\"\n" +
                "\"You fixing this... or do I start talking?\"\n\n" +
                "Following the first distribution, a customer sends a message stating the goods " +
                "were not at the expected level. You face a choice between short-term savings " +
                "and long-term reputation.",
            options = new List<EventOption>
            {
                new EventOption("Ignore", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: -10, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Žalba ignorirana: reputacija -10");
                    Next(BuildEvent6_SuspiciousNeighbor);
                }),
                new EventOption("Compensate", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -50, dRizik: 0, dReputacija: 5, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Kupac obeštećen: -50 €, reputacija +5");
                    Next(BuildEvent6_SuspiciousNeighbor);
                })
            }
        };
    }

    GameEvent BuildEvent6_SuspiciousNeighbor()
    {
        return new GameEvent
        {
            name = "Suspicious Neighbor",
            description =
                "\"Too many people coming in and out...\"\n" +
                "\"You running a business... or a very confusing family reunion?\"\n\n" +
                "As activity around the house increases, a neighbor notices unusual patterns. " +
                "The local environment is becoming a factor — threats don't only come from " +
                "police or rivals.",
            options = new List<EventOption>
            {
                new EventOption("Ignore", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 15, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.suspiciousNeighborIgnored = true;
                    Debug.Log("➡️ Susjed ignoriran: rizik +15");
                    Next(BuildEvent7_PolicePresence);
                }),
                new EventOption("Bribe", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -100, dRizik: -10, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.suspiciousNeighborIgnored = false;
                    Debug.Log("➡️ Susjed podmićen: -100 €, rizik -10");
                    Next(BuildEvent7_PolicePresence);
                })
            }
        };
    }

    GameEvent BuildEvent7_PolicePresence()
    {
        return new GameEvent
        {
            name = "Police Presence",
            description =
                "\"Patrol's in the area.\"\n" +
                "\"And they don't look like they're here for coffee.\"\n\n" +
                "Increased police presence is reported in the neighborhood. " +
                "You must decide whether to stop activities to reduce risk, " +
                "or keep working for profit at the cost of higher visibility.",
            options = new List<EventOption>
            {
                new EventOption("Stop activities", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -100, dRizik: -15, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Aktivnosti zaustavljene: -100 €, rizik -15");
                    ContinueAfterEvent7();
                }),
                new EventOption("Continue work", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 200, dRizik: 20, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Rad nastavljen: +200 €, rizik +20");
                    ContinueAfterEvent7();
                })
            }
        };
    }

    void ContinueAfterEvent7()
    {
        if (GameResources.Instance.radnici > 0)
        {
            Next(BuildEvent8_WorkerDelay);
        }
        else
        {
            Debug.Log("➡️ Kašnjenje radnika i test kvalitete preskočeni — nema zaposlenih radnika.");
            Next(BuildEvent10_EquipmentFailure);
        }
    }

    void ContinueAfterEvent10()
    {
        if (GameResources.Instance.radnici > 0)
        {
            Next(BuildEvent11_BrilliantIdea);
        }
        else
        {
            Debug.Log("➡️ Briljantna ideja preskočena — nema zaposlenih radnika.");
            Next(BuildEvent12_BetterWorker);
        }
    }

    GameEvent BuildEvent8_WorkerDelay()
    {
        return new GameEvent
        {
            name = "Worker Delay",
            description =
                "\"Relax, I'm a little late.\"\n" +
                "\"You know how it is... time is a suggestion.\"\n\n" +
                "Your worker arrives late for an important task. " +
                "It's not a catastrophe, but it's the first sign of unreliability.",
            options = new List<EventOption>
            {
                new EventOption("Forgive", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 10, dEfikasnost: 0);
                    GameResources.Instance.workerMistakeChanceBonus += 5;
                    Debug.Log("➡️ Radniku je oprošteno: moral +10, buduća šansa za grešku +5%");
                    Next(BuildEvent9_QualityTest);
                }),
                new EventOption("Punish", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: -15, dEfikasnost: 0);
                    GameResources.Instance.workerMistakeChanceBonus -= 5;
                    Debug.Log("➡️ Radnik kažnjen: moral -15, buduća šansa za grešku -5%");
                    Next(BuildEvent9_QualityTest);
                })
            }
        };
    }

    GameEvent BuildEvent9_QualityTest()
    {
        return new GameEvent
        {
            name = "Quality Test (Unofficial)",
            description =
                "\"I had to make sure it's good.\"\n" +
                "\"...very good.\"\n\n" +
                "One of your workers has decided to \"test\" the product on their own. " +
                "They are out of action for the rest of the cycle.",
            options = new List<EventOption>
            {
                new EventOption("Ignore", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -50, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: -30);
                    Debug.Log("➡️ Test kvalitete ignoriran: -50 €, efikasnost -30");
                    Next(BuildEvent10_EquipmentFailure);
                }),
                new EventOption("Punish", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: -10, dEfikasnost: 0);
                    GameResources.Instance.workerMistakeChanceBonus -= 10;
                    Debug.Log("➡️ Radnik kažnjen zbog testiranja: moral -10, šansa ponavljanja -10%");
                    Next(BuildEvent10_EquipmentFailure);
                }),
                new EventOption("Joke about it", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 5, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 15, dEfikasnost: 0);
                    Debug.Log("➡️ Situacija okrenuta na šalu: moral +15, rizik +5");
                    Next(BuildEvent10_EquipmentFailure);
                })
            }
        };
    }

    GameEvent BuildEvent10_EquipmentFailure()
    {
        return new GameEvent
        {
            name = "Equipment Failure",
            description =
                "\"Your gear isn't holding up.\"\n" +
                "\"At this point, it's more decoration than equipment.\"\n\n" +
                "The lab is old and improvised. You must decide whether to invest in repairs now " +
                "or keep pushing a malfunctioning system at the cost of performance and stability.",
            options = new List<EventOption>
            {
                new EventOption("Repair", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -150, dRizik: 0, dReputacija: 0, dKvaliteta: 15,
                        dStabilnost: 10, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Oprema popravljena: -150 €, kvaliteta +15, stabilnost +10");
                    ContinueAfterEvent10();
                }),
                new EventOption("Ignore", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: -20,
                        dStabilnost: -10, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.badBatchChanceBonus += 15;
                    Debug.Log("➡️ Kvar ignoriran: kvaliteta -20, stabilnost -10, šansa loše serije +15%");
                    ContinueAfterEvent10();
                })
            }
        };
    }

    GameEvent BuildEvent11_BrilliantIdea()
    {
        return new GameEvent
        {
            name = "Brilliant Idea",
            description =
                "\"I made it better.\"\n" +
                "\"...you're welcome.\"\n\n" +
                "A worker \"improved\" the production process on their own. " +
                "The result looks strange, smells stranger, and no one is sure what it actually is.",
            options = new List<EventOption>
            {
                new EventOption("Test the product", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 10, dReputacija: 0, dKvaliteta: -30,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Briljantna ideja testirana: kvaliteta -30, rizik +10");
                    if (UnityEngine.Random.Range(0, 100) < 10)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 0, dRizik: 0, dReputacija: 15, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Neočekivano, ideja je uspjela: reputacija +15");
                    }
                    Next(BuildEvent12_BetterWorker);
                }),
                new EventOption("Throw everything away", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -120, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 5, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Loš eksperiment odbačen: -120 €, stabilnost +5");
                    Next(BuildEvent12_BetterWorker);
                })
            }
        };
    }

    GameEvent BuildEvent12_BetterWorker()
    {
        return new GameEvent
        {
            name = "Better Worker",
            description =
                "\"I've heard about you. If you want to grow — you need someone like me.\"\n\n" +
                "A capable new individual arrives, offering improved efficiency, broader contacts " +
                "and faster distribution. They look ambitious — and dangerous. Their motivation " +
                "is self-interest, not loyalty.",
            options = new List<EventOption>
            {
                new EventOption("Hire", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 20, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 1, dMoral: 0, dEfikasnost: 50);
                    GameResources.Instance.betrayalChanceLateGame += 25;
                    Debug.Log("➡️ Bolji radnik zaposlen: +1 radnik, efikasnost +50, rizik +20, šansa izdaje +25%");
                    Next(BuildEvent13_BigOrder);
                }),
                new EventOption("Refuse", () =>
                {
                    Debug.Log("➡️ Bolji radnik odbijen — sporiji tempo, ali puna kontrola.");
                    Next(BuildEvent13_BigOrder);
                })
            }
        };
    }

    GameEvent BuildEvent13_BigOrder()
    {
        return new GameEvent
        {
            name = "Big Order",
            description =
                "\"I need a bigger batch.\"\n" +
                "\"You deliver — we both make real money.\"\n\n" +
                "A buyer requests a quantity that far exceeds your previous business. " +
                "It's a real test of ambition — and your tolerance for risk.",
            options = new List<EventOption>
            {
                new EventOption("Accept", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 500, dRizik: 25, dReputacija: 10, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Velika narudžba prihvaćena: +500 €, rizik +25, reputacija +10");
                    Next(BuildEvent14_Negotiator);
                }),
                new EventOption("Refuse", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: -5, dReputacija: -5, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Velika narudžba odbijena: reputacija -5, rizik -5");
                    Next(BuildEvent14_Negotiator);
                })
            }
        };
    }

    GameEvent BuildEvent14_Negotiator()
    {
        return new GameEvent
        {
            name = "Negotiator",
            description =
                "\"I like it.\"\n" +
                "\"But I like my money more.\"\n\n" +
                "A buyer pushes the price down to an absurd level. " +
                "Take the smaller margin, or hold the line and risk losing him.",
            options = new List<EventOption>
            {
                new EventOption("Accept", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 80, dRizik: 0, dReputacija: 3, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Niža cijena prihvaćena: +80 €, reputacija +3");
                    ContinueAfterEvent14();
                }),
                new EventOption("Refuse", () =>
                {
                    if (UnityEngine.Random.Range(0, 100) < 50)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 150, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Kupac je platio punu cijenu: +150 €");
                    }
                    else
                    {
                        GameResources.Instance.Apply(
                            dNovac: 0, dRizik: 0, dReputacija: -5, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Kupac je otišao i proširio loš glas: reputacija -5");
                    }
                    ContinueAfterEvent14();
                })
            }
        };
    }

    void ContinueAfterEvent14()
    {
        if (GameResources.Instance.radnici > 0)
        {
            Next(BuildEvent15_Theft);
        }
        else
        {
            Debug.Log("➡️ Krađa i odbjegli diler preskočeni — nema zaposlenih radnika.");
            Next(BuildEvent17_RivalAppears);
        }
    }

    GameEvent BuildEvent15_Theft()
    {
        return new GameEvent
        {
            name = "Theft",
            description =
                "\"Something's missing.\"\n" +
                "\"This isn't a mistake… someone's taking from you.\"\n\n" +
                "One of your workers is quietly skimming product. " +
                "It hasn't blown up — yet — but efficiency and stock are dropping.",
            options = new List<EventOption>
            {
                new EventOption("Fire the thief", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: -10, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 5, dRadnici: -1, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Lopov otpušten: -1 radnik, rizik -10, stabilnost +5");
                    Next(BuildEvent16_RunawayDealer);
                }),
                new EventOption("Keep the thief", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: -10, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.theftPerCycle += 100;
                    Debug.Log("➡️ Lopov zadržan: -100 €/ciklus, stabilnost -10");
                    Next(BuildEvent16_RunawayDealer);
                })
            }
        };
    }

    GameEvent BuildEvent16_RunawayDealer()
    {
        return new GameEvent
        {
            name = "Runaway Dealer",
            description =
                "\"I'll handle this one.\"\n" +
                "\"...trust me.\"\n\n" +
                "A worker takes the goods and vanishes. Phone off, location unknown. " +
                "You don't know if he ran, got caught, or worse.",
            options = new List<EventOption>
            {
                new EventOption("Search", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -100, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Potraga započeta: -100 €");
                    if (UnityEngine.Random.Range(0, 100) < 50)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 150, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Radnik pronađen, dio robe vraćen: +150 €");
                    }
                    else
                    {
                        Debug.Log("🎲 Radnik je nestao zauvijek.");
                    }
                    Next(BuildEvent17_RivalAppears);
                }),
                new EventOption("Give up", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -200, dRizik: -5, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: -10, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Odustajanje: -200 €, rizik -5, stabilnost -10");
                    Next(BuildEvent17_RivalAppears);
                })
            }
        };
    }

    GameEvent BuildEvent17_RivalAppears()
    {
        return new GameEvent
        {
            name = "Rival Appears",
            description =
                "\"You're not the only one working this area.\"\n" +
                "\"Someone's already watching you.\"\n\n" +
                "A small rival group has noticed you. No open conflict — yet — but their presence " +
                "limits your growth and changes the air around the district.",
            options = new List<EventOption>
            {
                new EventOption("Continue", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 10, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.rivalActive = true;
                    GameResources.Instance.incomePenaltyPercent += 10;
                    Debug.Log("➡️ Rival je aktivan: rizik +10, ulična zarada -10%");
                    ContinueAfterEvent17();
                })
            }
        };
    }

    void ContinueAfterEvent17()
    {
        if (GameResources.Instance.radnici > 0)
        {
            Next(BuildEvent18_FriendlyGuy);
        }
        else
        {
            Debug.Log("➡️ Prijateljski tip preskočen — nema zaposlenih radnika.");
            Next(BuildEvent19_StreetControl);
        }
    }

    void ContinueAfterEvent20()
    {
        if (GameResources.Instance.radnici > 0)
        {
            Next(BuildEvent21_SalaryDemand);
        }
        else
        {
            Debug.Log("➡️ Zahtjev za plaću preskočen — nema zaposlenih radnika.");
            Next(BuildEvent22_Expansion);
        }
    }

    void ContinueAfterEvent22()
    {
        if (GameResources.Instance.suspiciousNeighborIgnored)
        {
            Next(BuildEvent23_NeighborReport);
        }
        else
        {
            Debug.Log("➡️ Prijava susjeda preskočena — susjed je podmićen.");
            ContinueAfterEvent23();
        }
    }

    void ContinueAfterEvent23()
    {
        if (GameResources.Instance.rivalActive)
        {
            Next(BuildEvent24_Sabotage);
        }
        else
        {
            Debug.Log("➡️ Sabotaža preskočena — nema aktivnog rivala.");
            Next(BuildEvent25_ProfitGrowth);
        }
    }

    void ContinueAfterEvent25()
    {
        if (GameResources.Instance.radnici > 0)
        {
            Next(BuildEvent26_WorkerMistake);
        }
        else
        {
            Debug.Log("➡️ Greška radnika preskočena — nema zaposlenih radnika.");
            Next(BuildEvent27_BlackMarket);
        }
    }

    void ContinueAfterEvent28()
    {
        if (GameResources.Instance.radnici > 0)
        {
            Next(BuildEvent29_BeingWatched);
        }
        else
        {
            Debug.Log("➡️ Nadzor preskočen — nema zaposlenih radnika.");
            ContinueAfterEvent29();
        }
    }

    void ContinueAfterEvent29()
    {
        if (GameResources.Instance.rivalActive)
        {
            Next(BuildEvent30_DirectThreat);
        }
        else
        {
            Debug.Log("➡️ Izravna prijetnja preskočena — nema aktivnog rivala.");
            Next(BuildEvent31_ResourceShortage);
        }
    }

    void ContinueAfterEvent32()
    {
        if (GameResources.Instance.rivalActive)
        {
            Next(BuildEvent33_Attack);
        }
        else
        {
            Debug.Log("➡️ Napad i posljedice preskočeni — nema aktivnog rivala.");
            Next(BuildEvent35_EndChapter);
        }
    }

    GameEvent BuildEvent18_FriendlyGuy()
    {
        return new GameEvent
        {
            name = "Friendly Guy",
            description =
                "\"Relax, I know one of them.\"\n" +
                "\"We had a drink.\"\n\n" +
                "A worker claims he's \"handled the situation\" because he knows a cop. " +
                "Either a real connection — or a story that explodes in your face.",
            options = new List<EventOption>
            {
                new EventOption("Believe him", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: -5, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Radniku se vjerovalo: rizik -5");
                    if (UnityEngine.Random.Range(0, 100) < 30)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 0, dRizik: 25, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Bila je katastrofa. Rizik +25.");
                    }
                    else
                    {
                        Debug.Log("🎲 Ovaj put je stvarno uspjelo.");
                    }
                    Next(BuildEvent19_StreetControl);
                }),
                new EventOption("Ignore", () =>
                {
                    Debug.Log("➡️ Priča o prijatelju policajcu je ignorirana.");
                    Next(BuildEvent19_StreetControl);
                })
            }
        };
    }

    GameEvent BuildEvent19_StreetControl()
    {
        return new GameEvent
        {
            name = "Street Control",
            description =
                "\"Cops are stopping people out there.\"\n" +
                "\"Your runners might be next.\"\n\n" +
                "Police are running random checks during a distribution cycle. " +
                "You have seconds to decide: hide the goods, or push the plan through.",
            options = new List<EventOption>
            {
                new EventOption("Hide the goods", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -80, dRizik: -10, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Roba sakrivena: -80 €, rizik -10");
                    Next(BuildEvent20_BadProduct);
                }),
                new EventOption("Continue", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 150, dRizik: 15, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Ruta nastavljena: +150 €, rizik +15");
                    if (UnityEngine.Random.Range(0, 100) < 25)
                    {
                        GameResources.Instance.Apply(
                            dNovac: -120, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Pošiljka izgubljena na kontroli: -120 €");
                    }
                    Next(BuildEvent20_BadProduct);
                })
            }
        };
    }

    GameEvent BuildEvent20_BadProduct()
    {
        return new GameEvent
        {
            name = "Bad Product",
            description =
                "\"This batch is weak.\"\n" +
                "\"You sell this — people will remember.\"\n\n" +
                "Equipment, haste, or worker error — a low-quality batch is in front of you. " +
                "Cash today, or reputation tomorrow.",
            options = new List<EventOption>
            {
                new EventOption("Sell the bad batch", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 120, dRizik: 0, dReputacija: -15, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Loša serija prodana: +120 €, reputacija -15");
                    ContinueAfterEvent20();
                }),
                new EventOption("Destroy the batch", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -80, dRizik: 0, dReputacija: 3, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Loša serija uništena: -80 €, reputacija +3");
                    ContinueAfterEvent20();
                })
            }
        };
    }

    GameEvent BuildEvent21_SalaryDemand()
    {
        return new GameEvent
        {
            name = "Salary Demand",
            description =
                "\"I've been doing more than I signed up for.\"\n" +
                "\"Time you start paying for it.\"\n\n" +
                "A worker who's pulled real weight wants a bigger cut. " +
                "Pay him and lock him in — or refuse and watch the morale slip.",
            options = new List<EventOption>
            {
                new EventOption("Accept", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 20, dEfikasnost: 0);
                    GameResources.Instance.salaryPerCycle += 70;
                    GameResources.Instance.departureChance -= 15;
                    Debug.Log("➡️ Plaća povećana: -70 €/ciklus, moral +20, šansa odlaska -15%");
                    Next(BuildEvent22_Expansion);
                }),
                new EventOption("Reject", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: -20, dEfikasnost: 0);
                    GameResources.Instance.departureChance += 30;
                    Debug.Log("➡️ Povišica odbijena: moral -20, šansa odlaska +30%");
                    Next(BuildEvent22_Expansion);
                })
            }
        };
    }

    GameEvent BuildEvent22_Expansion()
    {
        return new GameEvent
        {
            name = "First Opportunity for Expansion",
            description =
                "\"There's a spot up for grabs.\"\n" +
                "\"Take it, and things change fast.\"\n\n" +
                "A bigger, better-positioned location is on the market. " +
                "You're not quite ready — but chances like this don't come twice.",
            options = new List<EventOption>
            {
                new EventOption("Purchase", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -500, dRizik: 15, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 15, dRadnici: 0, dMoral: 0, dEfikasnost: 50);
                    Debug.Log("➡️ Nova lokacija kupljena: -500 €, stabilnost +15, efikasnost +50, rizik +15");
                    ContinueAfterEvent22();
                }),
                new EventOption("Refuse", () =>
                {
                    Debug.Log("➡️ Širenje odbijeno — bez promjene.");
                    ContinueAfterEvent22();
                })
            }
        };
    }

    GameEvent BuildEvent23_NeighborReport()
    {
        return new GameEvent
        {
            name = "Neighbor Report",
            description =
                "\"Someone talked.\"\n" +
                "\"Cops know where to look now.\"\n\n" +
                "The suspicious neighbor you ignored finally went to the police. " +
                "You need to defuse this fast.",
            options = new List<EventOption>
            {
                new EventOption("Bribe police contact", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -200, dRizik: -20, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Policijski kontakt podmićen: -200 €, rizik -20");
                    ContinueAfterEvent23();
                }),
                new EventOption("Temporary shutdown", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -150, dRizik: -10, dReputacija: -3, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Privremeno zatvaranje: -150 €, rizik -10, reputacija -3");
                    ContinueAfterEvent23();
                })
            }
        };
    }

    GameEvent BuildEvent24_Sabotage()
    {
        return new GameEvent
        {
            name = "Sabotage",
            description =
                "\"Someone's trying to hurt your operation.\"\n" +
                "\"And they're not even subtle about it.\"\n\n" +
                "Supplies disappear, lies spread, a contact cuts ties. " +
                "The rival isn't just watching anymore.",
            options = new List<EventOption>
            {
                new EventOption("Retaliate", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 20, dReputacija: 8, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Uzvraćeno rivalu: rizik +20, reputacija +8");
                    Next(BuildEvent25_ProfitGrowth);
                }),
                new EventOption("Ignore", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -200, dRizik: 0, dReputacija: -10, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Sabotaža ignorirana: -200 €, reputacija -10");
                    Next(BuildEvent25_ProfitGrowth);
                })
            }
        };
    }

    GameEvent BuildEvent25_ProfitGrowth()
    {
        return new GameEvent
        {
            name = "Profit Growth",
            description =
                "\"Money's finally coming in.\"\n" +
                "\"But now... you're visible.\"\n\n" +
                "You've crossed the line from daily survival into real profit. " +
                "That changes your status — and the attention you draw.",
            options = new List<EventOption>
            {
                new EventOption("Continue", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 300, dRizik: 10, dReputacija: 10, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Rast profita: +300 €, reputacija +10, rizik +10");
                    ContinueAfterEvent25();
                })
            }
        };
    }

    GameEvent BuildEvent26_WorkerMistake()
    {
        return new GameEvent
        {
            name = "Worker Mistake",
            description =
                "\"Something went wrong.\"\n" +
                "\"Could've been worse... but it's not good.\"\n\n" +
                "A worker botches a distribution — wrong amount, wrong customer, " +
                "or just leaves a trail. How you handle this sets a precedent.",
            options = new List<EventOption>
            {
                new EventOption("Punish", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: -15, dEfikasnost: 0);
                    GameResources.Instance.workerMistakeChanceBonus -= 10;
                    Debug.Log("➡️ Radnik kažnjen: moral -15, šansa ponavljanja -10%");
                    Next(BuildEvent27_BlackMarket);
                }),
                new EventOption("Forgive", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 10, dEfikasnost: 0);
                    GameResources.Instance.workerMistakeChanceBonus += 5;
                    Debug.Log("➡️ Radniku je oprošteno: moral +10, buduća šansa greške +5%");
                    Next(BuildEvent27_BlackMarket);
                })
            }
        };
    }

    GameEvent BuildEvent27_BlackMarket()
    {
        return new GameEvent
        {
            name = "Black Market Offer",
            description =
                "\"We've got an offer for you.\"\n" +
                "\"More money. More risk.\"\n\n" +
                "Serious criminal circles step in with a proposal. " +
                "Accept and the cash flow explodes — and so does the danger.",
            options = new List<EventOption>
            {
                new EventOption("Accept", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 300, dRizik: 30, dReputacija: 15, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.blackMarketActive = true;
                    Debug.Log("➡️ Crno tržište prihvaćeno: +300 €, reputacija +15, rizik +30");
                    Next(BuildEvent28_ReputationRising);
                }),
                new EventOption("Refuse", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: -5, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Crno tržište odbijeno: rizik -5");
                    Next(BuildEvent28_ReputationRising);
                })
            }
        };
    }

    GameEvent BuildEvent28_ReputationRising()
    {
        return new GameEvent
        {
            name = "Reputation Rising",
            description =
                "\"People know your name now.\"\n" +
                "\"That brings business... and trouble.\"\n\n" +
                "Your activity has picked up enough that your name circulates in the city. " +
                "You're no longer anonymous — and that cuts both ways.",
            options = new List<EventOption>
            {
                new EventOption("Continue", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 10, dReputacija: 15, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.jobsBonusPercent += 20;
                    Debug.Log("➡️ Reputacija raste: reputacija +15, poslovi +20%, rizik +10");
                    ContinueAfterEvent28();
                })
            }
        };
    }

    GameEvent BuildEvent29_BeingWatched()
    {
        return new GameEvent
        {
            name = "Being Watched",
            description =
                "\"Your people are being followed.\"\n" +
                "\"If you push now — it won't stay quiet.\"\n\n" +
                "A suspicious distribution caught the eye of the police. " +
                "They're actively tracking your workers' movements.",
            options = new List<EventOption>
            {
                new EventOption("Withdraw workers", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -150, dRizik: -20, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Radnici povučeni: -150 €, rizik -20");
                    ContinueAfterEvent29();
                }),
                new EventOption("Continue work", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 200, dRizik: 20, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Rad nastavljen pod nadzorom: +200 €, rizik +20");
                    if (UnityEngine.Random.Range(0, 100) < 40)
                    {
                        GameResources.Instance.Apply(
                            dNovac: -250, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Pretres je pogodio posao: -250 €");
                    }
                    ContinueAfterEvent29();
                })
            }
        };
    }

    GameEvent BuildEvent30_DirectThreat()
    {
        return new GameEvent
        {
            name = "Direct Threat from a Rival",
            description =
                "\"This is your last warning.\"\n" +
                "\"You're in our territory.\"\n\n" +
                "The rival drops the subtle act. Your growth has become a problem they're " +
                "ready to act on. The board is set for a final confrontation.",
            options = new List<EventOption>
            {
                new EventOption("Prepare defense", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -200, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 20, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.preparedDefense = true;
                    GameResources.Instance.futureAttackDamageBonus -= 25;
                    Debug.Log("➡️ Obrana pripremljena: -200 €, stabilnost +20, buduća šteta -25%");
                    Next(BuildEvent31_ResourceShortage);
                }),
                new EventOption("Ignore threat", () =>
                {
                    GameResources.Instance.futureAttackDamageBonus += 25;
                    Debug.Log("➡️ Prijetnja ignorirana: buduća šteta napada +25%");
                    Next(BuildEvent31_ResourceShortage);
                })
            }
        };
    }

    GameEvent BuildEvent31_ResourceShortage()
    {
        return new GameEvent
        {
            name = "Resource Shortage",
            description =
                "\"Supplies are running low.\"\n" +
                "\"If you don't act — everything slows down.\"\n\n" +
                "Your growth burns through ingredients faster than the supply chain can refill. " +
                "Pay top dollar to keep moving, or wait and lose tempo.",
            options = new List<EventOption>
            {
                new EventOption("Expensive purchase", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -180, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Skupa nabava kupljena: -180 €");
                    Next(BuildEvent32_SystemEscalation);
                }),
                new EventOption("Wait", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: -5, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: -30);
                    Debug.Log("➡️ Čekanje zaliha: efikasnost -30, reputacija -5");
                    Next(BuildEvent32_SystemEscalation);
                })
            }
        };
    }

    GameEvent BuildEvent32_SystemEscalation()
    {
        return new GameEvent
        {
            name = "System Escalation",
            description =
                "\"Things are moving faster now.\"\n" +
                "\"More deals. More problems. More attention.\"\n\n" +
                "The city now lives against and around you. The pace of business — and trouble — " +
                "ratchets up.",
            options = new List<EventOption>
            {
                new EventOption("Continue", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 5, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 10);
                    Debug.Log("➡️ Sustav eskalira: rizik +5, efikasnost +10 (potencijal zarade)");
                    ContinueAfterEvent32();
                })
            }
        };
    }

    GameEvent BuildEvent33_Attack()
    {
        return new GameEvent
        {
            name = "Attack on the Laboratory",
            description =
                "\"They're coming.\"\n" +
                "\"This isn't business anymore.\"\n\n" +
                "The rival attacks your base. First open conflict — real cost to property, " +
                "people, and standing. Past choices now show their weight.",
            options = new List<EventOption>
            {
                new EventOption("Defend the base", () =>
                {
                    int bonus = GameResources.Instance.futureAttackDamageBonus;
                    int extra = bonus > 0 ? 300 * bonus / 100 : 0;
                    int stabilityLoss = GameResources.Instance.preparedDefense ? -10 : -20;
                    GameResources.Instance.Apply(
                        dNovac: -300 - extra, dRizik: 0, dReputacija: 20, dKvaliteta: 0,
                        dStabilnost: stabilityLoss, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log($"➡️ Baza obranjena: -{300 + extra} €, stabilnost {stabilityLoss}, reputacija +20");
                    if (GameResources.Instance.radnici > 0 && UnityEngine.Random.Range(0, 100) < 50)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: -1, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Radnik izgubljen tijekom obrane.");
                    }
                    GameResources.Instance.baseDefended = true;
                    Next(BuildEvent34_Aftermath);
                }),
                new EventOption("Retreat", () =>
                {
                    int bonus = GameResources.Instance.futureAttackDamageBonus;
                    int extra = bonus > 0 ? 400 * bonus / 100 : 0;
                    int stabilityLoss = GameResources.Instance.preparedDefense ? -25 : -40;
                    GameResources.Instance.Apply(
                        dNovac: -400 - extra, dRizik: -20, dReputacija: -5, dKvaliteta: 0,
                        dStabilnost: stabilityLoss, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log($"➡️ Povlačenje: -{400 + extra} €, stabilnost {stabilityLoss}, rizik -20, reputacija -5");
                    GameResources.Instance.baseDefended = false;
                    Next(BuildEvent34_Aftermath);
                })
            }
        };
    }

    GameEvent BuildEvent34_Aftermath()
    {
        bool defended = GameResources.Instance.baseDefended;
        return new GameEvent
        {
            name = "Aftermath",
            description =
                "\"Damage is done.\"\n" +
                "\"Now you count what's left.\"\n\n" +
                (defended
                    ? "You held the line. The base stands, but you're still bleeding."
                    : "You walked away. The base is wrecked, but you're alive."),
            options = new List<EventOption>
            {
                new EventOption("Assess the damage", () =>
                {
                    if (defended)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 0, dRizik: 10, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("➡️ Baza obranjena — slika snage privlači pažnju: rizik +10");
                    }
                    else
                    {
                        GameResources.Instance.incomePenaltyPercent += 20;
                        Debug.Log("➡️ Baza napuštena — buduća zarada -20%");
                    }
                    Next(BuildEvent35_EndChapter);
                })
            }
        };
    }

    GameEvent BuildEvent35_EndChapter()
    {
        var R = GameResources.Instance;
        return new GameEvent
        {
            name = "End of Chapter",
            description =
                "\"When you got here — you were nothing.\"\n" +
                "\"Now everyone sees you.\"\n" +
                "\"And that always comes with a price.\"\n\n" +
                "Chapter 1 final state:\n" +
                $"💰 Money: {R.novac} €    ⚠️ Risk: {R.rizik}    ⭐ Reputation: {R.reputacija}\n" +
                $"🧪 Quality: {R.kvaliteta}    🏚 Stability: {R.stabilnost}    👷 Workers: {R.radnici}\n" +
                $"🙂 Morale: {R.moral}    ⚙ Efficiency: {R.efikasnost}%",
            options = new List<EventOption>
            {
                new EventOption("End Chapter", () =>
                {
                    Debug.Log("🏁 Poglavlje 1 završeno.");
                    GameResources.Instance.chapterEnded = true;
                })
            }
        };
    }

#endif

    void OnGUI()
    {
        DrawResourceBar();
        DrawTurnPanel();
        DrawMiniMap();
        DrawActivityPanel();

        if (IsPauseMenuOpen)
        {
            DrawPauseMenu();
            return;
        }

        if (playerTurnAnnouncementActive)
        {
            DrawTurnAnnouncement($"{PlayerDisplayName.ToUpperInvariant()} TURN", playerTurnCountdown);
            return;
        }

        if (aiTurnAnnouncementActive)
        {
            DrawTurnAnnouncement("VOLKOV TURN", aiTurnCountdown);
            return;
        }

        if (eventActive && currentEvent != null)
        {
            DrawEventPopup();
        }
    }

    void DrawTurnPanel()
    {
        const float minimumPanelWidth = 310f;
        const float panelHeight = 54f;
        const float margin = 12f;

        GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Clip,
            normal = { textColor = Color.white }
        };

        string status;
        if (playerTurnAnnouncementActive)
        {
            status = $"Day {CurrentDay}  |  {PlayerDisplayName} turn starts in {playerTurnCountdown}";
        }
        else if (aiTurnAnnouncementActive)
        {
            status = $"Day {CurrentDay}  |  Volkov turn starts in {aiTurnCountdown}";
        }
        else
        {
            status = IsPlayerTurn
                ? $"Day {CurrentDay}  |  {PlayerDisplayName} turn  |  Actions: {PlayerActionsRemaining}/{actionsPerSide}  |  {FormatDayClock()}"
                : $"Day {CurrentDay}  |  Volkov: {AiActionsRemaining}/{actionsPerSide}  |  {FormatDayClock()}";
        }

        float maximumTextWidth = Mathf.Max(1f, Screen.width - margin * 2f - 32f);
        float textWidth = statusStyle.CalcSize(new GUIContent(status)).x;
        if (textWidth > maximumTextWidth)
        {
            statusStyle.fontSize = Mathf.Max(11, Mathf.FloorToInt(statusStyle.fontSize * maximumTextWidth / textWidth));
            textWidth = statusStyle.CalcSize(new GUIContent(status)).x;
        }

        float panelWidth = Mathf.Min(
            Screen.width - margin * 2f,
            Mathf.Max(minimumPanelWidth, textWidth + 32f));
        Rect panelRect = new Rect(Screen.width - panelWidth - margin, barHeight + margin, panelWidth, panelHeight);
        if (turnPanelBackgroundImage != null)
        {
            GUI.DrawTexture(panelRect, turnPanelBackgroundImage, ScaleMode.StretchToFill, true);
        }
        else
        {
            GUI.Box(panelRect, GUIContent.none);
        }

        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y, panelRect.width - 32f, panelRect.height), status, statusStyle);
    }

    void DrawActivityPanel()
    {
        const float width = 390f;
        const float height = 210f;
        const float margin = 12f;
        Rect panel = new Rect(margin, barHeight + margin, width, height);
        DrawHudPanelBackground(panel, "ACTIVITY LOG");

        GUIStyle activityStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            wordWrap = true
        };

        float y = panel.y + 30f;
        for (int i = activityLog.Count - 1; i >= 0; i--)
        {
            activityStyle.normal.textColor = activityLog[i].color;
            float textWidth = panel.width - 50f;
            float entryHeight = Mathf.Max(
                27f,
                activityStyle.CalcHeight(new GUIContent(activityLog[i].text), textWidth));
            if (y + entryHeight > panel.yMax - 8f)
            {
                break;
            }

            GUI.Label(
                new Rect(panel.x + 40f, y, textWidth, entryHeight),
                activityLog[i].text,
                activityStyle);
            y += entryHeight + 4f;
        }
    }

    void DrawTurnAnnouncement(string title, int countdown)
    {
        int previousDepth = GUI.depth;
        GUI.depth = -900;

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 42,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUIStyle countdownStyle = new GUIStyle(titleStyle)
        {
            fontSize = 76
        };

        float centerY = Screen.height * 0.5f;
        GUI.Label(
            new Rect(0f, centerY - 100f, Screen.width, 60f),
            title,
            titleStyle);
        GUI.Label(new Rect(0f, centerY - 35f, Screen.width, 100f), countdown.ToString(), countdownStyle);

        GUI.depth = previousDepth;
    }

    void SetupMiniMap()
    {
        strategyCamera = FindFirstObjectByType<SimpleStrategyCamera>();
        if (strategyCamera == null)
        {
            return;
        }

        float centerX = (strategyCamera.minX + strategyCamera.maxX) * 0.5f;
        float centerZ = (strategyCamera.minZ + strategyCamera.maxZ) * 0.5f;
        float mapWidth = strategyCamera.maxX - strategyCamera.minX;
        float mapHeight = strategyCamera.maxZ - strategyCamera.minZ;

        GameObject cameraObject = new GameObject("Territory Mini Map Camera");
        cameraObject.hideFlags = HideFlags.DontSave;
        miniMapCamera = cameraObject.AddComponent<Camera>();
        miniMapCamera.transform.position = new Vector3(centerX, 1000f, centerZ);
        miniMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        miniMapCamera.orthographic = true;
        const float textureAspect = 432f / 512f;
        miniMapCamera.orthographicSize = Mathf.Max(mapHeight * 0.5f, mapWidth / (2f * textureAspect)) * 1.02f;
        miniMapCamera.nearClipPlane = 0.1f;
        miniMapCamera.farClipPlane = 2000f;
        miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
        miniMapCamera.backgroundColor = new Color(0.08f, 0.08f, 0.08f);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            miniMapCamera.cullingMask = mainCamera.cullingMask;
        }

        miniMapTexture = new RenderTexture(432, 512, 16)
        {
            name = "TerritoryMiniMap",
            filterMode = FilterMode.Bilinear
        };
        miniMapCamera.targetTexture = miniMapTexture;

        BuildingInfo[] buildings = FindObjectsByType<BuildingInfo>(FindObjectsSortMode.None);
        if (buildings.Length > 0)
        {
            float averageX = 0f;
            foreach (BuildingInfo building in buildings)
            {
                averageX += building.transform.position.x;
            }
            playerTerritoryOnLeft = averageX / buildings.Length <= centerX;
        }

        CacheMiniMapParcels();
    }

    void DrawMiniMap()
    {
        if (miniMapTexture == null || strategyCamera == null)
        {
            return;
        }

        const float width = 360f;
        const float height = 470f;
        const float margin = 12f;
        Rect panel = new Rect(Screen.width - width - margin, Screen.height - height - margin, width, height);
        Rect map = new Rect(panel.x + 10f, panel.y + 56f, panel.width - 20f, panel.height - 66f);

        DrawHudPanelBackground(panel, "CITY TERRITORY");

        float playerPercent = GetTerritoryPercent(TerritoryOwner.Player);
        float opponentPercent = GetTerritoryPercent(TerritoryOwner.AI);
        float neutralPercent = Mathf.Max(0f, 100f - playerPercent - opponentPercent);

        DrawMiniMapLegend(
            new Rect(panel.xMax - 352f, panel.y + 28f, 342f, 22f),
            playerPercent,
            neutralPercent,
            opponentPercent);
        GUI.DrawTexture(map, miniMapTexture, ScaleMode.StretchToFill, false);

        DrawMiniMapTerritories(map);
        DrawMiniMapView(map);
        DrawMiniMapMarkers(map);
        DrawDistrictLabels(map);
    }

    void DrawHudPanelBackground(Rect panel, string title)
    {
        if (hudPanelBackgroundImage != null)
        {
            GUI.DrawTexture(panel, hudPanelBackgroundImage, ScaleMode.StretchToFill, true);
        }
        else
        {
            GUI.Box(panel, GUIContent.none);
        }

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(panel.x, panel.y + 2f, panel.width, 24f), title, titleStyle);
    }

    void DrawMiniMapLegend(
        Rect rect,
        float playerPercent,
        float neutralPercent,
        float opponentPercent)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 11,
            normal = { textColor = Color.white }
        };
        string[] labels =
        {
            $"{PlayerDisplayName} {playerPercent:0}%",
            $"Neutral {neutralPercent:0}%",
            $"Volkov {opponentPercent:0}%"
        };
        Color[] colors =
        {
            playerTerritoryColor,
            neutralTerritoryColor,
            opponentTerritoryColor
        };
        float itemWidth = rect.width / labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            float x = rect.x + i * itemWidth;
            DrawTerritoryRect(
                new Rect(x, rect.y + 5f, 11f, 11f),
                new Color(colors[i].r, colors[i].g, colors[i].b, 1f));
            GUI.Label(new Rect(x + 16f, rect.y, itemWidth - 16f, rect.height), labels[i], style);
        }
    }

    void DrawDistrictLabels(Rect map)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9,
            fontStyle = FontStyle.Normal,
            normal = { textColor = new Color(1f, 1f, 1f, 0.82f) }
        };

        for (int id = 0; id < TerritoryDistrictManager.DistrictSlotCount; id++)
        {
            if (!TerritoryDistrictManager.TryGetDistrictStatus(
                    id,
                    out string districtName,
                    out int controlScore,
                    out _,
                    out _,
                    out Vector3 center))
            {
                continue;
            }

            Vector2 labelPosition = WorldToMiniMapPosition(map, center);
            Rect labelRect = new Rect(
                labelPosition.x - 48f,
                labelPosition.y - 9f,
                96f,
                18f);
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.32f);
            GUI.DrawTexture(labelRect, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Label(labelRect, $"{districtName} {controlScore:+0;-0;0}", style);
        }
    }

    float GetTerritoryPercent(TerritoryOwner owner)
    {
        if (miniMapParcelArea <= 0f)
        {
            return 0f;
        }

        float ownedArea = 0f;
        foreach (MiniMapParcel parcel in miniMapParcels)
        {
            TerritoryHouse house = parcel.source != null
                ? parcel.source.GetComponent<TerritoryHouse>()
                : null;
            TerritoryOwner parcelOwner = house != null
                ? house.Owner
                : parcel.playerOwned ? TerritoryOwner.Player : TerritoryOwner.Neutral;
            if (parcelOwner == owner)
            {
                ownedArea += parcel.area;
            }
        }

        return ownedArea / miniMapParcelArea * 100f;
    }

    public static void RefreshTerritoryInfluence()
    {
        if (instance == null || GameResources.Instance == null)
        {
            return;
        }

        int playerTerritoryPercent = Mathf.RoundToInt(
            instance.GetTerritoryPercent(TerritoryOwner.Player));
        int opponentTerritoryPercent = Mathf.RoundToInt(
            instance.GetTerritoryPercent(TerritoryOwner.AI));
        GameResources.Instance.utjecaj = playerTerritoryPercent;
        GameResources.Instance.Opponent.influence = opponentTerritoryPercent;

        GameStoryManager.ReportTerritoryPercent(playerTerritoryPercent);

        if (playerTerritoryPercent >= VictoryTerritoryPercent &&
            !GameResources.Instance.gameOver &&
            !GameResources.Instance.chapterEnded)
        {
            if (!GameStoryManager.BeginVictorySequence(
                    playerTerritoryPercent,
                    () => instance.ShowVictory(playerTerritoryPercent)))
            {
                instance.ShowVictory(playerTerritoryPercent);
            }
        }
        else if (opponentTerritoryPercent >= VictoryTerritoryPercent &&
                 !GameResources.Instance.gameOver &&
                 !GameResources.Instance.chapterEnded)
        {
            GameResources.Instance.gameOver = true;
            GameResources.Instance.gameOverReason =
                $"Volkov controls {opponentTerritoryPercent}% of the city. " +
                "Your organization no longer has enough territory to challenge him.";
            instance.StopAllCoroutines();
            instance.ShowGameOver();
        }
    }

    void DrawMiniMapMarkers(Rect map)
    {
        BuildingInfo[] buildings = FindObjectsByType<BuildingInfo>(FindObjectsSortMode.None);
        foreach (BuildingInfo building in buildings)
        {
            if (building.IsStoryLocked)
            {
                continue;
            }
            DrawMiniMapMarker(map, building.transform.position, Color.blue, 7f);
        }

        if (Camera.main != null)
        {
            Vector2 playerPosition = WorldToMiniMapPosition(map, Camera.main.transform.position);
            DrawTerritoryRect(
                new Rect(playerPosition.x - 6f, playerPosition.y - 6f, 12f, 12f),
                new Color(0f, 0f, 0f, 0.8f));
            DrawTerritoryRect(
                new Rect(playerPosition.x - 4f, playerPosition.y - 4f, 8f, 8f),
                Color.white);
            DrawTerritoryRect(
                new Rect(playerPosition.x - 2f, playerPosition.y - 2f, 4f, 4f),
                new Color(0.15f, 0.85f, 1f, 1f));
        }
    }

    void DrawMiniMapView(Rect map)
    {
        Camera playerCamera = Camera.main;
        if (playerCamera == null)
        {
            return;
        }

        Vector2[] viewCorners =
        {
            WorldToMiniMapPosition(map, GetCameraGroundViewPoint(playerCamera, 0f, 0f)),
            WorldToMiniMapPosition(map, GetCameraGroundViewPoint(playerCamera, 1f, 0f)),
            WorldToMiniMapPosition(map, GetCameraGroundViewPoint(playerCamera, 1f, 1f)),
            WorldToMiniMapPosition(map, GetCameraGroundViewPoint(playerCamera, 0f, 1f))
        };
        Color viewColor = new Color(0.15f, 0.85f, 1f, 0.78f);
        for (int i = 0; i < viewCorners.Length; i++)
        {
            DrawMiniMapLine(
                viewCorners[i],
                viewCorners[(i + 1) % viewCorners.Length],
                viewColor,
                2f);
        }

        Vector2 cameraPosition = WorldToMiniMapPosition(map, playerCamera.transform.position);
        DrawMiniMapLine(cameraPosition, viewCorners[2], new Color(0.15f, 0.85f, 1f, 0.28f), 1f);
        DrawMiniMapLine(cameraPosition, viewCorners[3], new Color(0.15f, 0.85f, 1f, 0.28f), 1f);
    }

    Vector3 GetCameraGroundViewPoint(Camera playerCamera, float viewportX, float viewportY)
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(viewportX, viewportY));
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        Vector3 point;
        if (ground.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
        }
        else
        {
            Vector3 flatDirection = Vector3.ProjectOnPlane(ray.direction, Vector3.up).normalized;
            float mapWidth = strategyCamera.maxX - strategyCamera.minX;
            float mapHeight = strategyCamera.maxZ - strategyCamera.minZ;
            point = playerCamera.transform.position + flatDirection * Mathf.Sqrt(
                mapWidth * mapWidth + mapHeight * mapHeight);
        }

        point.x = Mathf.Clamp(point.x, strategyCamera.minX, strategyCamera.maxX);
        point.z = Mathf.Clamp(point.z, strategyCamera.minZ, strategyCamera.maxZ);
        point.y = 0f;
        return point;
    }

    void DrawMiniMapLine(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 difference = end - start;
        if (difference.sqrMagnitude < 0.01f)
        {
            return;
        }

        Matrix4x4 previousMatrix = GUI.matrix;
        float angle = Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle, start);
        DrawTerritoryRect(
            new Rect(start.x, start.y - width * 0.5f, difference.magnitude, width),
            color);
        GUI.matrix = previousMatrix;
    }

    void DrawMiniMapMarker(Rect map, Vector3 worldPosition, Color color, float size)
    {
        Vector2 markerPosition = WorldToMiniMapPosition(map, worldPosition);
        Rect marker = new Rect(
            markerPosition.x - size * 0.5f,
            markerPosition.y - size * 0.5f,
            size,
            size);
        DrawTerritoryRect(marker, color);
    }

    Vector2 WorldToMiniMapPosition(Rect map, Vector3 worldPosition)
    {
        Vector3 viewport = miniMapCamera.WorldToViewportPoint(worldPosition);
        return new Vector2(
            map.x + viewport.x * map.width,
            map.y + (1f - viewport.y) * map.height);
    }

    void CacheMiniMapParcels()
    {
        miniMapParcels.Clear();
        miniMapParcelArea = 0f;
        MeshCollider[] colliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
        HashSet<Renderer> usedRenderers = new HashSet<Renderer>();

        foreach (MeshCollider collider in colliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = collider.GetComponentInChildren<Renderer>();
            }
            if (renderer == null || !usedRenderers.Add(renderer))
            {
                continue;
            }

            Vector3 size = renderer.bounds.size;
            if (size.y < 4f || Mathf.Max(size.x, size.z) < 3f)
            {
                continue;
            }

            float area = Mathf.Max(1f, size.x * size.z);
            miniMapParcels.Add(new MiniMapParcel
            {
                center = renderer.bounds.center,
                size = size,
                area = area,
                playerOwned = collider.GetComponentInParent<BuildingInfo>() != null,
                source = renderer.gameObject
            });
            miniMapParcelArea += area;
        }

        miniMapParcels.Sort((a, b) => playerTerritoryOnLeft
            ? a.center.x.CompareTo(b.center.x)
            : b.center.x.CompareTo(a.center.x));

        InitializeTerritoryOwners();
        SetupAiFacilities();
    }

    void InitializeTerritoryOwners()
    {
        float playerPercent = Mathf.Clamp(GameResources.Instance != null ? GameResources.Instance.utjecaj : 30f, 0f, 70f);
        float opponentPercent = Mathf.Clamp(opponentTerritoryPercent, 0f, 100f - playerPercent);
        float playerAreaLimit = miniMapParcelArea * playerPercent / 100f;
        float neutralAreaLimit = miniMapParcelArea * (100f - opponentPercent) / 100f;
        float accumulatedArea = 0f;

        foreach (MiniMapParcel parcel in miniMapParcels)
        {
            float parcelMiddle = accumulatedArea + parcel.area * 0.5f;
            TerritoryOwner initialOwner = parcel.playerOwned || parcelMiddle <= playerAreaLimit
                ? TerritoryOwner.Player
                : parcelMiddle <= neutralAreaLimit ? TerritoryOwner.Neutral : TerritoryOwner.AI;

            if (parcel.source != null)
            {
                TerritoryHouse house = parcel.source.GetComponent<TerritoryHouse>();
                if (house == null)
                {
                    house = parcel.source.AddComponent<TerritoryHouse>();
                }
                house.InitializeOwner(initialOwner);
            }

            accumulatedArea += parcel.area;
        }

        TerritoryDistrictManager districtManager = GetComponent<TerritoryDistrictManager>();
        if (districtManager != null)
        {
            districtManager.InitializeDistricts();
        }
        RefreshTerritoryInfluence();
    }

    void SetupAiFacilities()
    {
        List<MiniMapParcel> candidates = new List<MiniMapParcel>();
        Vector3 aiCenter = Vector3.zero;
        foreach (MiniMapParcel parcel in miniMapParcels)
        {
            TerritoryHouse house = parcel.source != null
                ? parcel.source.GetComponent<TerritoryHouse>()
                : null;
            if (house == null || !house.IsAiOwned ||
                parcel.source.GetComponentInParent<BuildingInfo>() != null)
            {
                continue;
            }

            candidates.Add(parcel);
            aiCenter += parcel.center;
        }

        if (candidates.Count < 4)
        {
            Debug.LogWarning("Not enough AI-owned city buildings were found to assign all AI facilities.");
            return;
        }

        aiCenter /= candidates.Count;
        MiniMapParcel factory = TakeClosestParcel(candidates, aiCenter);
        MiniMapParcel warehouse = TakeClosestParcel(candidates, factory.center);
        MiniMapParcel workerContact = TakeFarthestParcel(candidates, factory.center);
        MiniMapParcel apartment = TakeFarthestParcel(candidates, workerContact.center);

        AddAiFacility(factory, AiFacilityRole.Factory);
        AddAiFacility(warehouse, AiFacilityRole.Warehouse);
        AddAiFacility(workerContact, AiFacilityRole.WorkerContact);
        AddAiFacility(apartment, AiFacilityRole.Apartment);
    }

    static MiniMapParcel TakeClosestParcel(List<MiniMapParcel> candidates, Vector3 position)
    {
        int selectedIndex = 0;
        float selectedDistance = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            float distance = (candidates[i].center - position).sqrMagnitude;
            if (distance < selectedDistance)
            {
                selectedDistance = distance;
                selectedIndex = i;
            }
        }

        MiniMapParcel selected = candidates[selectedIndex];
        candidates.RemoveAt(selectedIndex);
        return selected;
    }

    static MiniMapParcel TakeFarthestParcel(List<MiniMapParcel> candidates, Vector3 position)
    {
        int selectedIndex = 0;
        float selectedDistance = float.MinValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            float distance = (candidates[i].center - position).sqrMagnitude;
            if (distance > selectedDistance)
            {
                selectedDistance = distance;
                selectedIndex = i;
            }
        }

        MiniMapParcel selected = candidates[selectedIndex];
        candidates.RemoveAt(selectedIndex);
        return selected;
    }

    static void AddAiFacility(MiniMapParcel parcel, AiFacilityRole role)
    {
        if (parcel == null || parcel.source == null)
        {
            return;
        }

        AiFacilityMarker marker = parcel.source.GetComponent<AiFacilityMarker>();
        if (marker == null)
        {
            marker = parcel.source.AddComponent<AiFacilityMarker>();
        }
        Texture2D[] labelImages = null;
        if (instance != null)
        {
            if (role == AiFacilityRole.Factory)
            {
                labelImages = instance.aiFactoryMapLabelImages;
            }
            else if (role == AiFacilityRole.Warehouse)
            {
                labelImages = instance.aiWarehouseMapLabelImages;
            }
            else if (role == AiFacilityRole.Apartment)
            {
                labelImages = instance.aiApartmentMapLabelImages;
            }
            else if (role == AiFacilityRole.WorkerContact)
            {
                labelImages = instance.aiWorkerContactMapLabelImages;
            }
        }
        marker.Configure(role, labelImages);
    }

    void DrawMiniMapTerritories(Rect map)
    {
        for (int id = 0; id < TerritoryDistrictManager.DistrictSlotCount; id++)
        {
            if (!TerritoryDistrictManager.TryGetDistrictStatus(
                    id,
                    out _,
                    out int controlScore,
                    out int controlLimit,
                    out _,
                    out _) ||
                !TerritoryDistrictManager.TryGetDistrictBounds(
                    id,
                    out Vector3 boundsMinimum,
                    out Vector3 boundsMaximum))
            {
                continue;
            }

            Vector2 minimum = WorldToMiniMapPosition(map, boundsMinimum);
            Vector2 maximum = WorldToMiniMapPosition(map, boundsMaximum);
            Rect districtRect = Rect.MinMaxRect(
                minimum.x,
                maximum.y,
                maximum.x,
                minimum.y);

            DrawTerritoryRect(districtRect, GetDistrictControlColor(controlScore, controlLimit));
            Color borderColor = new Color(1f, 1f, 1f, 0.42f);
            DrawTerritoryRect(new Rect(districtRect.x, districtRect.y, districtRect.width, 1f), borderColor);
            DrawTerritoryRect(new Rect(districtRect.x, districtRect.yMax - 1f, districtRect.width, 1f), borderColor);
            DrawTerritoryRect(new Rect(districtRect.x, districtRect.y, 1f, districtRect.height), borderColor);
            DrawTerritoryRect(new Rect(districtRect.xMax - 1f, districtRect.y, 1f, districtRect.height), borderColor);
        }
    }

    Color GetDistrictControlColor(int controlScore, int controlLimit)
    {
        float strength = Mathf.Clamp01(Mathf.Abs(controlScore) / (float)Mathf.Max(1, controlLimit));
        Color color = controlScore > 0
            ? Color.Lerp(neutralTerritoryColor, playerTerritoryColor, strength)
            : controlScore < 0
                ? Color.Lerp(neutralTerritoryColor, opponentTerritoryColor, strength)
                : neutralTerritoryColor;
        color.a *= 0.55f;
        return color;
    }

    void DrawTerritoryRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    void DrawPauseMenu()
    {
        int previousDepth = GUI.depth;
        GUI.depth = -1000;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        const float windowWidth = 650f;
        const float windowHeight = 408f;
        Rect windowRect = new Rect(
            (Screen.width - windowWidth) / 2f,
            (Screen.height - windowHeight) / 2f,
            windowWidth,
            windowHeight
        );

        GUIStyle windowStyle = new GUIStyle(GUIStyle.none);
        if (pauseMenuBackgroundImage != null)
        {
            windowStyle.normal.background = pauseMenuBackgroundImage;
        }
        GUI.ModalWindow(987654, windowRect, DrawPauseWindow, GUIContent.none, windowStyle);
        GUI.depth = previousDepth;
    }

    void DrawPauseWindow(int windowId)
    {
        Rect continueRect = new Rect((650f - 483f) / 2f, 135f, 483f, 99f);
        bool continueClicked = continueButtonImage != null
            ? GUI.Button(continueRect, continueButtonImage, GUIStyle.none)
            : GUI.Button(continueRect, "Continue Game");
        if (continueClicked)
        {
            ContinueGame();
        }

        Rect quitRect = new Rect((650f - 392f) / 2f, 265f, 392f, 99f);
        bool quitClicked = quitButtonImage != null
            ? GUI.Button(quitRect, quitButtonImage, GUIStyle.none)
            : GUI.Button(quitRect, "Quit Game");
        if (quitClicked)
        {
            QuitGame();
        }
    }

    void OpenPauseMenu()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsPauseMenuOpen = true;
    }

    void ContinueGame()
    {
        Time.timeScale = previousTimeScale;
        IsPauseMenuOpen = false;
    }

    void QuitGame()
    {
        Time.timeScale = previousTimeScale;
        IsPauseMenuOpen = false;

        Debug.Log("Game exited from pause menu");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void DrawResourceBar()
    {
        var r = GameResources.Instance;
        if (r == null) return;

        GUI.Box(new Rect(0, 0, Screen.width, barHeight), GUIContent.none);

        string[] resourceTexts =
        {
            $"{r.novac} €",
            $"{r.robaUTvornici}/{r.kapacitetTvornice} g",
            $"{r.robaUSkladistu}/{r.kapacitetSkladista} g" +
                (r.robaUTransportu > 0 ? $" (+{r.robaUTransportu} g)" : ""),
            $"{r.SlobodniRadnici}/{r.radnici}",
            $"{r.rizik}/100",
            $"{r.utjecaj}%"
        };

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Overflow,
            normal = { textColor = Color.white }
        };

        const int resourceCount = 6;
        const float itemGap = 12f;
        const float horizontalPadding = 6f;
        float panelWidth = Mathf.Max(
            1f,
            (Screen.width - horizontalPadding * 2f - itemGap * (resourceCount - 1)) / resourceCount);

        float x = horizontalPadding;
        DrawResourceItem(ref x, moneyIcon, resourceTexts[0], style, panelWidth, itemGap);
        DrawResourceItem(ref x, factoryIcon, resourceTexts[1], style, panelWidth, itemGap);
        DrawResourceItem(ref x, storeIcon, resourceTexts[2], style, panelWidth, itemGap);
        DrawResourceItem(ref x, workersIcon, resourceTexts[3], style, panelWidth, itemGap);
        DrawResourceItem(ref x, riskIcon, resourceTexts[4], style, panelWidth, itemGap);
        DrawResourceItem(ref x, influenceIcon, resourceTexts[5], style, panelWidth, itemGap);
    }

    void DrawResourceItem(ref float x, Texture2D background, string text, GUIStyle style,
                          float panelWidth, float itemGap)
    {
        Rect panelRect = new Rect(x, 2f, panelWidth, barHeight - 4f);
        if (background != null)
        {
            GUI.DrawTexture(panelRect, background, ScaleMode.StretchToFill, true);
        }
        else
        {
            GUI.Box(panelRect, GUIContent.none);
        }

        Rect textRect = new Rect(panelRect.x + 4f, panelRect.y - 2f, panelRect.width, panelRect.height);
        GUI.Label(textRect, text, style);
        x += panelWidth + itemGap;
    }

    void DrawEventPopup()
    {
        if (currentEvent.name == "COMMANDS" && howToPlayBackgroundImage != null)
        {
            DrawHowToPlayPopup();
            return;
        }

        int previousDepth = GUI.depth;
        GUI.depth = -1000;

        float x = (Screen.width - popupWidth) / 2f;
        float y = (Screen.height - popupHeight) / 2f;
        Rect rect = new Rect(x, y, popupWidth, popupHeight);

        if (popupBackground != null)
        {
            GUI.DrawTexture(rect, popupBackground, ScaleMode.StretchToFill);
        }
        else
        {
            GUI.Box(rect, GUIContent.none);
        }

        GUILayout.BeginArea(new Rect(rect.x + 20, rect.y + 15, rect.width - 40, rect.height - 30));
        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUILayout.Label(currentEvent.name, title);

        GUILayout.Space(10);

        GUIStyle desc = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = true,
            normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };
        GUILayout.Label(currentEvent.description, desc);

        GUILayout.FlexibleSpace();

        for (int i = 0; i < currentEvent.options.Count; i++)
        {
            bool clicked;
            if (currentEvent.options[i].text == "OK" && okButtonImage != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                clicked = GUILayout.Button(
                    okButtonImage,
                    GUIStyle.none,
                    GUILayout.Width(253f),
                    GUILayout.Height(60f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                clicked = GUILayout.Button(currentEvent.options[i].text, GUILayout.Height(36));
            }

            if (clicked)
            {
                Choose(i);
                break;
            }
        }

        GUILayout.EndArea();
        GUI.depth = previousDepth;
    }

    void DrawHowToPlayPopup()
    {
        int previousDepth = GUI.depth;
        GUI.depth = -1000;

        const float sourceWidth = 620f;
        const float sourceHeight = 786f;
        float scale = Mathf.Min(0.8f, (Screen.height - 30f) / sourceHeight);
        Rect rect = new Rect(
            (Screen.width - sourceWidth * scale) / 2f,
            (Screen.height - sourceHeight * scale) / 2f,
            sourceWidth * scale,
            sourceHeight * scale);
        GUI.DrawTexture(rect, howToPlayBackgroundImage, ScaleMode.StretchToFill, true);

        Rect buttonRect = new Rect(
            rect.x + (sourceWidth - 253f) * 0.5f * scale,
            rect.y + 715f * scale,
            253f * scale,
            60f * scale);
        bool clicked = okButtonImage != null
            ? GUI.Button(buttonRect, okButtonImage, GUIStyle.none)
            : GUI.Button(buttonRect, "OK");
        if (clicked)
        {
            Choose(0);
        }

        GUI.depth = previousDepth;
    }
}
