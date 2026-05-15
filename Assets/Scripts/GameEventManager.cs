using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameEventManager : MonoBehaviour
{
    // Globalni flag da drugi sustavi (kamera, selector) znaju da je popup otvoren
    public static bool IsPopupOpen { get; private set; }

    [Header("Tajming")]
    [Tooltip("Sekunde do prvog eventa nakon pokretanja igre.")]
    public float firstEventDelay = 60f;

    [Header("Stil")]
    public int barHeight = 40;
    public int popupWidth = 640;
    public int popupHeight = 360;

    private bool eventActive;
    private GameEvent currentEvent;

    // ---------- Event definicija ----------
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

    // ---------- Lifecycle ----------
    void Start()
    {
        StartCoroutine(TriggerFirstEvent());
    }

    IEnumerator TriggerFirstEvent()
    {
        yield return new WaitForSeconds(firstEventDelay);
        ShowEvent(BuildEvent1_Arrival());
    }

    void ShowEvent(GameEvent e)
    {
        currentEvent = e;
        eventActive = true;
        IsPopupOpen = true;
    }

    void Choose(int idx)
    {
        if (currentEvent == null) return;

        var chosen = currentEvent.options[idx];

        // Clear current event first so the callback can chain a new one.
        eventActive = false;
        currentEvent = null;
        IsPopupOpen = false;

        chosen.onChoose?.Invoke();
    }

    // ---------- Event #1: Arrival ----------
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
                    Debug.Log("➡️ Arrival — story begins.");
                    // Chain to the next event immediately
                    ShowEvent(BuildEvent2_FirstProduction());
                })
            }
        };
    }

    // ---------- Event #2: First Production ----------
    GameEvent BuildEvent2_FirstProduction()
    {
        return new GameEvent
        {
            name = "First Production",
            description =
                "\"The setup is rough, but it'll do.\"\n" +
                "\"The question is — fast, or careful?\"",
            options = new List<EventOption>
            {
                new EventOption("Fast production", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 100, dRizik: 5, dReputacija: 0, dKvaliteta: -10,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Fast production: +100 €, Quality -10, Risk +5");
                }),
                new EventOption("Careful production", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 60, dRizik: 2, dReputacija: 0, dKvaliteta: 10,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Careful production: +60 €, Quality +10, Risk +2");
                })
            }
        };
    }

    // ---------- IMGUI rendering ----------
    void OnGUI()
    {
        DrawResourceBar();

        if (eventActive && currentEvent != null)
        {
            DrawEventPopup();
        }
    }

    void DrawResourceBar()
    {
        var r = GameResources.Instance;
        if (r == null) return;

        GUI.Box(new Rect(0, 0, Screen.width, barHeight), GUIContent.none);

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        string text =
            $"💰 Money: {r.novac} €   " +
            $"⚠️ Risk: {r.rizik}   " +
            $"⭐ Reputation: {r.reputacija}   " +
            $"🧪 Quality: {r.kvaliteta}   " +
            $"🏚 Stability: {r.stabilnost}   " +
            $"👷 Workers: {r.radnici}   " +
            $"🙂 Morale: {r.moral}   " +
            $"⚙ Efficiency: {r.efikasnost}%";

        GUI.Label(new Rect(15, 0, Screen.width - 30, barHeight), text, style);
    }

    void DrawEventPopup()
    {
        float x = (Screen.width - popupWidth) / 2f;
        float y = (Screen.height - popupHeight) / 2f;
        Rect rect = new Rect(x, y, popupWidth, popupHeight);

        GUI.Box(rect, GUIContent.none);

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
            if (GUILayout.Button(currentEvent.options[i].text, GUILayout.Height(36)))
            {
                Choose(i);
                break;
            }
        }

        GUILayout.EndArea();
    }
}
