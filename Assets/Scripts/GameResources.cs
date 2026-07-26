using System.Collections.Generic;
using UnityEngine;

public class GameResources : MonoBehaviour
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

    void Update()
    {
        ReleaseFinishedWorkers();

        if (TransportUTijeku && Time.time >= zavrsetakTransporta)
        {
            robaUSkladistu += robaUTransportu;
            robaUTransportu = 0;
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
        int freeWarehouseSpace = kapacitetSkladista - robaUSkladistu;
        int amount = Mathf.Min(robaUTvornici, freeWarehouseSpace);
        if (TransportUTijeku || amount <= 0 || !TryAssignWorkers(1, durationSeconds))
        {
            return false;
        }

        robaUTvornici -= amount;
        robaUTransportu = amount;
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
