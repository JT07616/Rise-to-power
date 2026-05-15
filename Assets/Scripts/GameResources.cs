using UnityEngine;

public class GameResources : MonoBehaviour
{
    public static GameResources Instance { get; private set; }

    [Header("Resursi (početne vrijednosti iz test.html)")]
    public int novac = 200;
    public int rizik = 10;
    public int reputacija = 0;
    public int kvaliteta = 50;
    public int stabilnost = 50;
    public int radnici = 0;
    public int moral = 50;
    public int efikasnost = 100;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Apply(int dNovac, int dRizik, int dReputacija, int dKvaliteta,
                     int dStabilnost, int dRadnici, int dMoral, int dEfikasnost)
    {
        novac += dNovac;
        rizik += dRizik;
        reputacija += dReputacija;
        kvaliteta += dKvaliteta;
        stabilnost += dStabilnost;
        radnici += dRadnici;
        moral += dMoral;
        efikasnost += dEfikasnost;

        Clamp();
    }

    void Clamp()
    {
        kvaliteta = Mathf.Clamp(kvaliteta, 0, 100);
        stabilnost = Mathf.Clamp(stabilnost, -100, 100);
        moral = Mathf.Clamp(moral, -100, 100);
        efikasnost = Mathf.Clamp(efikasnost, 0, 300);
        rizik = Mathf.Max(0, rizik);
    }
}
