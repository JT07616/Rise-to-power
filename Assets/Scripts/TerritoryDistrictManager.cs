using System.Collections.Generic;
using UnityEngine;

public class TerritoryDistrictManager : MonoBehaviour
{
    private class District
    {
        public TerritoryOwner owner;
        public int controlScore;
        public Vector3 center;
        public readonly List<TerritoryHouse> houses = new List<TerritoryHouse>();
    }

    private static TerritoryDistrictManager instance;
    private static readonly string[] DistrictNames =
    {
        "Downtown",
        "Grove Street",
        "Industrial Zone",
        "Old Town",
        "Riverside",
        "Uptown"
    };

    [Header("District control")]
    [Min(1)] public int columns = 3;
    [Min(1)] public int rows = 2;
    [Min(1)] public int deliveriesToCapture = 3;

    private readonly Dictionary<int, District> districts = new Dictionary<int, District>();
    private Vector2 minimumPosition;
    private Vector2 maximumPosition;
    private bool initialized;

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void InitializeDistricts()
    {
        TerritoryHouse[] houses = FindObjectsByType<TerritoryHouse>(FindObjectsSortMode.None);
        if (houses.Length == 0)
        {
            return;
        }

        minimumPosition = new Vector2(float.MaxValue, float.MaxValue);
        maximumPosition = new Vector2(float.MinValue, float.MinValue);
        foreach (TerritoryHouse house in houses)
        {
            Vector3 position = house.transform.position;
            minimumPosition = Vector2.Min(minimumPosition, new Vector2(position.x, position.z));
            maximumPosition = Vector2.Max(maximumPosition, new Vector2(position.x, position.z));
        }

        districts.Clear();
        foreach (TerritoryHouse house in houses)
        {
            int id = GetDistrictId(house.transform.position);
            if (!districts.TryGetValue(id, out District district))
            {
                district = new District();
                districts.Add(id, district);
            }
            district.houses.Add(house);
            district.center += house.transform.position;
        }

        foreach (District district in districts.Values)
        {
            district.owner = GetMajorityOwner(district.houses);
            district.controlScore = district.owner == TerritoryOwner.Player
                ? deliveriesToCapture
                : district.owner == TerritoryOwner.AI ? -deliveriesToCapture : 0;
            district.center /= district.houses.Count;
            foreach (TerritoryHouse house in district.houses)
            {
                house.SetOwner(district.owner);
            }
        }

        initialized = true;
    }

    public static bool TryRegisterDelivery(TerritoryHouse house, TerritoryOwner side, out bool captured)
    {
        captured = false;
        if (instance == null || !instance.initialized || house == null || side == TerritoryOwner.Neutral)
        {
            return false;
        }

        int districtId = instance.GetDistrictId(house.transform.position);
        if (!instance.districts.TryGetValue(districtId, out District district))
        {
            return false;
        }

        int previousScore = district.controlScore;
        district.controlScore += side == TerritoryOwner.Player ? 1 : -1;
        district.controlScore = Mathf.Clamp(
            district.controlScore,
            -instance.deliveriesToCapture,
            instance.deliveriesToCapture);

        TerritoryOwner previousOwner = district.owner;
        district.owner = district.controlScore > 0
            ? TerritoryOwner.Player
            : district.controlScore < 0 ? TerritoryOwner.AI : TerritoryOwner.Neutral;

        if (district.owner != previousOwner)
        {
            foreach (TerritoryHouse districtHouse in district.houses)
            {
                districtHouse.SetOwner(district.owner);
            }
        }

        captured = side == TerritoryOwner.Player
            ? previousScore < instance.deliveriesToCapture &&
              district.controlScore == instance.deliveriesToCapture
            : previousScore > -instance.deliveriesToCapture &&
              district.controlScore == -instance.deliveriesToCapture;
        GameStoryManager.ReportTerritoryDelivery(
            side,
            previousScore,
            district.controlScore,
            captured);
        string territoryMessage = captured
            ? $"Captured {GetDistrictName(districtId)} at {district.controlScore:+0;-0;0}."
            : $"{GetDistrictName(districtId)} control {previousScore:+0;-0;0} → {district.controlScore:+0;-0;0}.";
        if (side == TerritoryOwner.Player)
        {
            GameEventManager.ReportPlayerActivity(captured ? "🏴" : "🗺️", territoryMessage);
        }
        else
        {
            GameEventManager.ReportAiActivity(territoryMessage, captured ? "🏴" : "🗺️");
        }
        return true;
    }

    public static int DistrictSlotCount
    {
        get { return instance != null ? instance.columns * instance.rows : 0; }
    }

    public static bool TryGetDistrictStatus(
        int districtId,
        out string districtName,
        out int controlScore,
        out int controlLimit,
        out TerritoryOwner owner,
        out Vector3 center)
    {
        districtName = GetDistrictName(districtId);
        controlScore = 0;
        controlLimit = instance != null ? instance.deliveriesToCapture : 3;
        owner = TerritoryOwner.Neutral;
        center = Vector3.zero;
        if (instance == null || !instance.initialized ||
            !instance.districts.TryGetValue(districtId, out District district))
        {
            return false;
        }

        controlScore = district.controlScore;
        owner = district.owner;
        center = district.center;
        return true;
    }

    public static bool TryGetDistrictStatus(
        TerritoryHouse house,
        out string districtName,
        out int controlScore,
        out int controlLimit)
    {
        districtName = "Unknown district";
        controlScore = 0;
        controlLimit = instance != null ? instance.deliveriesToCapture : 3;
        if (instance == null || !instance.initialized || house == null)
        {
            return false;
        }

        int districtId = instance.GetDistrictId(house.transform.position);
        return TryGetDistrictStatus(
            districtId,
            out districtName,
            out controlScore,
            out controlLimit,
            out _,
            out _);
    }

    public static bool TryGetDistrictBounds(
        int districtId,
        out Vector3 boundsMinimum,
        out Vector3 boundsMaximum)
    {
        boundsMinimum = Vector3.zero;
        boundsMaximum = Vector3.zero;
        if (instance == null || !instance.initialized ||
            !instance.districts.ContainsKey(districtId))
        {
            return false;
        }

        int column = districtId % instance.columns;
        int row = districtId / instance.columns;
        float minimumX = Mathf.Lerp(
            instance.minimumPosition.x,
            instance.maximumPosition.x,
            column / (float)instance.columns);
        float maximumX = Mathf.Lerp(
            instance.minimumPosition.x,
            instance.maximumPosition.x,
            (column + 1f) / instance.columns);
        float minimumZ = Mathf.Lerp(
            instance.minimumPosition.y,
            instance.maximumPosition.y,
            row / (float)instance.rows);
        float maximumZ = Mathf.Lerp(
            instance.minimumPosition.y,
            instance.maximumPosition.y,
            (row + 1f) / instance.rows);

        boundsMinimum = new Vector3(minimumX, 0f, minimumZ);
        boundsMaximum = new Vector3(maximumX, 0f, maximumZ);
        return true;
    }

    private int GetDistrictId(Vector3 position)
    {
        float width = Mathf.Max(0.01f, maximumPosition.x - minimumPosition.x);
        float height = Mathf.Max(0.01f, maximumPosition.y - minimumPosition.y);
        int column = Mathf.Clamp(Mathf.FloorToInt((position.x - minimumPosition.x) / width * columns), 0, columns - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt((position.z - minimumPosition.y) / height * rows), 0, rows - 1);
        return row * columns + column;
    }

    private static string GetDistrictName(int districtId)
    {
        return districtId >= 0 && districtId < DistrictNames.Length
            ? DistrictNames[districtId]
            : $"District {districtId + 1}";
    }

    private static TerritoryOwner GetMajorityOwner(List<TerritoryHouse> houses)
    {
        int player = 0;
        int rival = 0;
        int neutral = 0;
        foreach (TerritoryHouse house in houses)
        {
            if (house.Owner == TerritoryOwner.Player) player++;
            else if (house.Owner == TerritoryOwner.AI) rival++;
            else neutral++;
        }

        if (player > rival && player > neutral)
        {
            return TerritoryOwner.Player;
        }

        if (rival > player && rival > neutral)
        {
            return TerritoryOwner.AI;
        }

        return TerritoryOwner.Neutral;
    }
}
