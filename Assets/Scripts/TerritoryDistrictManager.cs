using System.Collections.Generic;
using UnityEngine;

public class TerritoryDistrictManager : MonoBehaviour
{
    private class District
    {
        public TerritoryOwner owner;
        public int controlScore;
        public readonly List<TerritoryHouse> houses = new List<TerritoryHouse>();
    }

    private static TerritoryDistrictManager instance;

    [Header("District control")]
    [Min(1)] public int columns = 3;
    [Min(1)] public int rows = 2;
    [Min(1)] public int deliveriesToCapture = 5;

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
        }

        foreach (District district in districts.Values)
        {
            district.owner = GetMajorityOwner(district.houses);
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

        if (!instance.districts.TryGetValue(instance.GetDistrictId(house.transform.position), out District district))
        {
            return false;
        }

        district.controlScore += side == TerritoryOwner.Player ? 1 : -1;
        district.controlScore = Mathf.Clamp(
            district.controlScore,
            -instance.deliveriesToCapture,
            instance.deliveriesToCapture);

        TerritoryOwner previousOwner = district.owner;
        if (district.controlScore >= instance.deliveriesToCapture)
        {
            district.owner = TerritoryOwner.Player;
        }
        else if (district.controlScore <= -instance.deliveriesToCapture)
        {
            district.owner = TerritoryOwner.AI;
        }

        if (district.owner != previousOwner)
        {
            captured = district.owner == side;
            foreach (TerritoryHouse districtHouse in district.houses)
            {
                districtHouse.SetOwner(district.owner);
            }
        }

        return true;
    }

    public static bool TryGetProgress(TerritoryHouse house, TerritoryOwner side, out int progress, out int requirement)
    {
        progress = 0;
        requirement = 0;
        if (instance == null || !instance.initialized || house == null || side == TerritoryOwner.Neutral ||
            !instance.districts.TryGetValue(instance.GetDistrictId(house.transform.position), out District district))
        {
            return false;
        }

        if (district.owner == side)
        {
            return true;
        }

        requirement = instance.deliveriesToCapture;
        progress = side == TerritoryOwner.Player
            ? Mathf.Max(0, district.controlScore)
            : Mathf.Max(0, -district.controlScore);
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

    private static TerritoryOwner GetMajorityOwner(List<TerritoryHouse> houses)
    {
        int player = 0;
        int rival = 0;
        foreach (TerritoryHouse house in houses)
        {
            if (house.Owner == TerritoryOwner.Player) player++;
            else if (house.Owner == TerritoryOwner.AI) rival++;
        }

        return player > rival ? TerritoryOwner.Player : rival > player ? TerritoryOwner.AI : TerritoryOwner.Neutral;
    }
}
