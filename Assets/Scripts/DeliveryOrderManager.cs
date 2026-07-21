using System.Collections.Generic;
using UnityEngine;

public class DeliveryOrderManager : MonoBehaviour
{
    public static bool IsPopupOpen { get; private set; }

    [Header("Orders")]
    [Min(1)] public int offersPerDay = 4;
    public int minReward = 80;
    public int maxReward = 220;
    public int minRisk = 4;
    public int maxRisk = 14;

    private readonly List<DeliveryOrder> orders = new List<DeliveryOrder>();
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
        public bool completed;
        public DeliveryOrderTarget target;
    }

    void Update()
    {
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
        if (!GameEventManager.IsPlayerTurn || GameEventManager.IsPauseMenuOpen)
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
            Rect rect = new Rect((Screen.width - 420f) / 2f, (Screen.height - 250f) / 2f, 420f, 250f);
            GUI.ModalWindow(771002, rect, DrawOrderWindow, "Delivery request");
        }

        GUI.depth = previousDepth;
    }

    public void OpenOrder(int orderIndex)
    {
        if (!GameEventManager.CanPlayerAct || orderIndex < 0 || orderIndex >= orders.Count ||
            orders[orderIndex].completed)
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

        int offerCount = Mathf.Min(offersPerDay, candidates.Count);
        for (int i = 0; i < offerCount; i++)
        {
            MeshCollider candidate = candidates[i];
            DeliveryOrderTarget target = candidate.gameObject.AddComponent<DeliveryOrderTarget>();
            target.Configure(this, i);

            orders.Add(new DeliveryOrder
            {
                customerName = customerNames[i % customerNames.Count],
                reward = Random.Range(minReward, maxReward + 1),
                risk = Random.Range(minRisk, maxRisk + 1),
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
        GUILayout.Label("Choose which requests to complete before the timer expires. A delivery uses one action.");
        GUILayout.Space(10f);

        for (int i = 0; i < orders.Count; i++)
        {
            DeliveryOrder order = orders[i];
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(48f));
            GUILayout.Label($"{i + 1}. {order.customerName}", GUILayout.Width(190f));
            GUILayout.Label($"Reward: {order.reward} €", GUILayout.Width(125f));
            GUILayout.Label($"Risk: +{order.risk}", GUILayout.Width(85f));

            GUI.enabled = !order.completed && GameEventManager.CanPlayerAct;
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
        GUILayout.Space(12f);
        GUILayout.Label($"Customer: {order.customerName}");
        GUILayout.Label($"Payment: +{order.reward} €");
        GUILayout.Label($"Police risk: +{order.risk}");
        GUILayout.Space(16f);

        GUI.enabled = GameEventManager.CanPlayerAct && !order.completed;
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
        if (order == null || order.completed || !GameEventManager.CanPlayerAct || GameResources.Instance == null)
        {
            return;
        }

        order.completed = true;
        GameResources.Instance.Apply(
            dNovac: order.reward, dRizik: order.risk, dReputacija: 1, dKvaliteta: 0,
            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
        order.target.MarkCompleted();
        Debug.Log($"Delivery completed for {order.customerName}: +{order.reward} €, risk +{order.risk}.");

        selectedOrder = -1;
        showOffers = GameEventManager.PlayerActionsRemaining > 1 && HasIncompleteOrders();
        IsPopupOpen = showOffers;
        GameEventManager.CompletePlayerAction();
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
            if (order.target != null)
            {
                order.target.Deactivate();
            }
        }

        orders.Clear();
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
