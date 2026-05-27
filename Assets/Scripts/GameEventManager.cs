using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameEventManager : MonoBehaviour
{
    public static bool IsPopupOpen { get; private set; }

    [Header("Timing")]
    [Tooltip("Seconds before the first event after game start.")]
    public float firstEventDelay = 60f;

    [Tooltip("Seconds between consecutive events.")]
    public float delayBetweenEvents = 30f;

    [Header("Style")]
    public int barHeight = 40;
    public int popupWidth = 640;
    public int popupHeight = 420;

    private bool eventActive;
    private GameEvent currentEvent;
    private bool isNotification;
    private Queue<GameEvent> notificationQueue = new Queue<GameEvent>();
    private Func<GameEvent> pendingNext;

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
        bool wasNotification = isNotification;

        eventActive = false;
        currentEvent = null;
        IsPopupOpen = false;
        isNotification = false;

        chosen.onChoose?.Invoke();

        if (!wasNotification)
        {
            ApplyConsequences();

            if (GameResources.Instance != null && GameResources.Instance.gameOver)
            {
                StopAllCoroutines();
                ShowGameOver();
                return;
            }
        }

        ContinueChain();
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
        isNotification = true;
        ShowEvent(notif);
    }

    void EnqueueNotification(string title, string body)
    {
        notificationQueue.Enqueue(new GameEvent
        {
            name = title,
            description = body,
            options = new List<EventOption>
            {
                new EventOption("OK", () => { })
            }
        });
    }

    void ApplyConsequences()
    {
        var R = GameResources.Instance;
        if (R == null || R.chapterEnded || R.gameOver) return;

        if (R.theftPerCycle > 0 && UnityEngine.Random.Range(0, 100) < 20)
        {
            R.theftPerCycle = 0;
            Debug.Log("🕵️ Lopov je prestao krasti.");
        }

        if (R.kvaliteta > 70 && R.reputacija > 20)
        {
            R.novac += 60;
            Debug.Log("⭐ Premium proizvod + reputacija: bonus +60 €");
            EnqueueNotification(
                "⭐ Premium Bonus",
                "Word is out: your product is the best in the district, " +
                "and your name carries weight.\n\n+60 € bonus this cycle.");
        }

        float income = 40 + 30 * R.radnici + R.kvaliteta / 6f + R.reputacija / 6f;
        income *= R.efikasnost / 100f;
        if (R.blackMarketActive) income += 120;
        if (R.incomePenaltyPercent > 0) income *= 1f - R.incomePenaltyPercent / 100f;
        int incomeInt = Mathf.FloorToInt(income);
        R.novac += incomeInt;
        Debug.Log($"💰 Pasivni prihod: +{incomeInt} €");

        if (R.salaryPerCycle > 0 && R.radnici > 0)
        {
            R.novac -= R.salaryPerCycle;
            Debug.Log($"💸 Plaće: -{R.salaryPerCycle} €");
        }

        if (R.theftPerCycle > 0 && R.radnici > 0)
        {
            R.novac -= R.theftPerCycle;
            Debug.Log($"🕵️ Krađa: -{R.theftPerCycle} €");
        }

        if (R.reputacija <= -30)
        {
            R.efikasnost -= 10;
            Debug.Log("📉 Vrlo loša reputacija ruši efikasnost (-10).");
        }

        if (R.kvaliteta < 30)
        {
            R.reputacija -= 5;
            Debug.Log("🧪 Loša kvaliteta ruši reputaciju (-5).");
        }

        if (R.stabilnost <= 0)
        {
            R.efikasnost -= 20;
            R.rizik += 10;
            Debug.Log("🏚️ Baza u raspadu: efikasnost -20, rizik +10.");
            EnqueueNotification(
                "🏚 Base Collapsing",
                "Your base is falling apart. Walls leak, locks don't hold, " +
                "everyone moves slower in the chaos.\n\n" +
                "Efficiency −20\nRisk +10");
        }

        if (R.moral <= 0 && R.radnici > 0)
        {
            int lost = R.radnici;
            Debug.Log("👥 Radnici su napustili organizaciju zbog niskog morala.");
            R.radnici = 0;
            R.efikasnost = Mathf.Max(50, R.efikasnost - 50);
            EnqueueNotification(
                "👥 Workers Walked Out",
                "Morale hit zero. Every single worker packed up and left.\n\n" +
                $"Workers: −{lost}\nEfficiency: −50 (floor 50)");
        }

        if (R.rizik >= 100)
        {
            int seizure = Mathf.Max(0, R.novac * 30 / 100);
            R.novac -= seizure;
            R.reputacija -= 20;
            R.rizik = 30;
            int workerLost = 0;
            int stabilityLoss = 0;
            Debug.Log($"🚓 Policija te uhvatila! Zapljena: -{seizure} €, reputacija -20, rizik = 30.");
            if (R.radnici > 0 && UnityEngine.Random.Range(0, 100) < 20)
            {
                R.radnici -= 1;
                workerLost = 1;
                Debug.Log("🚓 Jedan radnik je uhvaćen.");
            }
            if (UnityEngine.Random.Range(0, 100) < 15)
            {
                R.stabilnost -= 20;
                stabilityLoss = 20;
                Debug.Log("🚓 Šteta na bazi: stabilnost -20.");
            }

            string body =
                "Sirens. Doors kicked in. Half your stash is gone " +
                "and your name is on every report tonight.\n\n" +
                $"Money seized: −{seizure} €\n" +
                "Reputation: −20\n" +
                "Risk reset to 30";
            if (workerLost > 0) body += "\nA worker was arrested (−1)";
            if (stabilityLoss > 0) body += $"\nBase damaged (Stability −{stabilityLoss})";
            EnqueueNotification("🚓 POLICE RAID!", body);
        }

        if (R.novac < 0 && UnityEngine.Random.Range(0, 100) < 50)
        {
            R.novac -= 20;
            R.reputacija -= 1;
            Debug.Log("💸 Trošak duga: -20 €");
        }

        if (R.novac > 2000)
        {
            R.rizik += 5;
            Debug.Log("💰 Velik profit privlači pažnju (+5 rizik)");
            EnqueueNotification(
                "💰 Drawing Attention",
                "Money like yours doesn't move quietly. Eyes are on you now.\n\nRisk +5");
        }

        R.Clamp();

        if (R.novac < -1000)
        {
            R.gameOver = true;
            R.gameOverReason = "Bankrotirao si.";
        }
        else if (R.stabilnost <= -50)
        {
            R.gameOver = true;
            R.gameOverReason = "Baza je potpuno uništena.";
        }
        else if (R.reputacija <= -50)
        {
            R.gameOver = true;
            R.gameOverReason = "Nitko više ne želi poslovati s tobom.";
        }
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
                })
            }
        };
        eventActive = true;
        IsPopupOpen = true;
    }

    IEnumerator DelayedShow(Func<GameEvent> builder)
    {
        yield return new WaitForSeconds(delayBetweenEvents);
        ShowEvent(builder());
    }

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
                    Debug.Log("➡️ Fast production: +100 €, Quality -10, Risk +5");
                    Next(BuildEvent3_FirstWorker);
                }),
                new EventOption("Careful production", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 60, dRizik: 2, dReputacija: 0, dKvaliteta: 10,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Careful production: +60 €, Quality +10, Risk +2");
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
                    Debug.Log("➡️ First worker hired: +1 worker, Efficiency +25, Risk +10");
                    Next(BuildEvent4_FirstDistribution);
                }),
                new EventOption("Decline", () =>
                {
                    Debug.Log("➡️ First worker declined — no change.");
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
                        Debug.Log("➡️ Distribution problem: -50 €, Reputation -3");
                    }
                    else
                    {
                        GameResources.Instance.Apply(
                            dNovac: 150, dRizik: 5, dReputacija: 5, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("➡️ Successful distribution: +150 €, Reputation +5, Risk +5");
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
                    Debug.Log("➡️ Complaint ignored: Reputation -10");
                    Next(BuildEvent6_SuspiciousNeighbor);
                }),
                new EventOption("Compensate", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -50, dRizik: 0, dReputacija: 5, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Customer compensated: -50 €, Reputation +5");
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
                    Debug.Log("➡️ Neighbor ignored: Risk +15");
                    Next(BuildEvent7_PolicePresence);
                }),
                new EventOption("Bribe", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -100, dRizik: -10, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.suspiciousNeighborIgnored = false;
                    Debug.Log("➡️ Neighbor bribed: -100 €, Risk -10");
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
                    Debug.Log("➡️ Activities paused: -100 €, Risk -15");
                    ContinueAfterEvent7();
                }),
                new EventOption("Continue work", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 200, dRizik: 20, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Continued working: +200 €, Risk +20");
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
            Debug.Log("➡️ Worker Delay & Quality Test skipped — no workers hired.");
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
            Debug.Log("➡️ Brilliant Idea skipped — no workers hired.");
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
                    Debug.Log("➡️ Worker forgiven: Morale +10, future mistake chance +5%");
                    Next(BuildEvent9_QualityTest);
                }),
                new EventOption("Punish", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: -15, dEfikasnost: 0);
                    GameResources.Instance.workerMistakeChanceBonus -= 5;
                    Debug.Log("➡️ Worker punished: Morale -15, future mistake chance -5%");
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
                    Debug.Log("➡️ Quality test ignored: -50 €, Efficiency -30");
                    Next(BuildEvent10_EquipmentFailure);
                }),
                new EventOption("Punish", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: -10, dEfikasnost: 0);
                    GameResources.Instance.workerMistakeChanceBonus -= 10;
                    Debug.Log("➡️ Worker punished for testing: Morale -10, repeat chance -10%");
                    Next(BuildEvent10_EquipmentFailure);
                }),
                new EventOption("Joke about it", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 5, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 15, dEfikasnost: 0);
                    Debug.Log("➡️ Joked about it: Morale +15, Risk +5");
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
                    Debug.Log("➡️ Equipment repaired: -150 €, Quality +15, Stability +10");
                    ContinueAfterEvent10();
                }),
                new EventOption("Ignore", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: -20,
                        dStabilnost: -10, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.badBatchChanceBonus += 15;
                    Debug.Log("➡️ Failure ignored: Quality -20, Stability -10, bad batch chance +15%");
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
                    Debug.Log("➡️ Tested the brilliant idea: Quality -30, Risk +10");
                    if (UnityEngine.Random.Range(0, 100) < 10)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 0, dRizik: 0, dReputacija: 15, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Unexpectedly, the idea became a hit: Reputation +15");
                    }
                    Next(BuildEvent12_BetterWorker);
                }),
                new EventOption("Throw everything away", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -120, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 5, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Bad experiment discarded: -120 €, Stability +5");
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
                    Debug.Log("➡️ Better worker hired: +1 worker, Efficiency +50, Risk +20, betrayal chance +25%");
                    Next(BuildEvent13_BigOrder);
                }),
                new EventOption("Refuse", () =>
                {
                    Debug.Log("➡️ Better worker refused — slower tempo, full control.");
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
                    Debug.Log("➡️ Big order accepted: +500 €, Risk +25, Reputation +10");
                    Next(BuildEvent14_Negotiator);
                }),
                new EventOption("Refuse", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: -5, dReputacija: -5, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Big order refused: Reputation -5, Risk -5");
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
                    Debug.Log("➡️ Accepted lower price: +80 €, Reputation +3");
                    ContinueAfterEvent14();
                }),
                new EventOption("Refuse", () =>
                {
                    if (UnityEngine.Random.Range(0, 100) < 50)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 150, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Buyer paid full price: +150 €");
                    }
                    else
                    {
                        GameResources.Instance.Apply(
                            dNovac: 0, dRizik: 0, dReputacija: -5, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Buyer left and spread bad word: Reputation -5");
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
            Debug.Log("➡️ Theft & Runaway Dealer skipped — no workers hired.");
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
                    Debug.Log("➡️ Thief fired: -1 worker, Risk -10, Stability +5");
                    Next(BuildEvent16_RunawayDealer);
                }),
                new EventOption("Keep the thief", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: -10, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    GameResources.Instance.theftPerCycle += 100;
                    Debug.Log("➡️ Thief kept: -100 €/cycle, Stability -10");
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
                    Debug.Log("➡️ Search started: -100 €");
                    if (UnityEngine.Random.Range(0, 100) < 50)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 150, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Worker found, part of the goods recovered: +150 €");
                    }
                    else
                    {
                        Debug.Log("🎲 Worker gone forever.");
                    }
                    Next(BuildEvent17_RivalAppears);
                }),
                new EventOption("Give up", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -200, dRizik: -5, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: -10, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Gave up: -200 €, Risk -5, Stability -10");
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
                    Debug.Log("➡️ Rival is active: Risk +10, street earnings -10%");
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
            Debug.Log("➡️ Friendly Guy skipped — no workers hired.");
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
            Debug.Log("➡️ Salary Demand skipped — no workers hired.");
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
            Debug.Log("➡️ Neighbor Report skipped — neighbor was bribed.");
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
            Debug.Log("➡️ Sabotage skipped — no active rival.");
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
            Debug.Log("➡️ Worker Mistake skipped — no workers hired.");
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
            Debug.Log("➡️ Being Watched skipped — no workers hired.");
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
            Debug.Log("➡️ Direct Threat skipped — no active rival.");
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
            Debug.Log("➡️ Attack & Aftermath skipped — no active rival.");
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
                    Debug.Log("➡️ Believed the worker: Risk -5");
                    if (UnityEngine.Random.Range(0, 100) < 30)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 0, dRizik: 25, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 It was a disaster. Risk +25.");
                    }
                    else
                    {
                        Debug.Log("🎲 This time it actually worked out.");
                    }
                    Next(BuildEvent19_StreetControl);
                }),
                new EventOption("Ignore", () =>
                {
                    Debug.Log("➡️ Ignored the cop-friend story.");
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
                    Debug.Log("➡️ Goods hidden: -80 €, Risk -10");
                    Next(BuildEvent20_BadProduct);
                }),
                new EventOption("Continue", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 150, dRizik: 15, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Continued the run: +150 €, Risk +15");
                    if (UnityEngine.Random.Range(0, 100) < 25)
                    {
                        GameResources.Instance.Apply(
                            dNovac: -120, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Shipment lost to the check: -120 €");
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
                    Debug.Log("➡️ Sold bad batch: +120 €, Reputation -15");
                    ContinueAfterEvent20();
                }),
                new EventOption("Destroy the batch", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -80, dRizik: 0, dReputacija: 3, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Destroyed bad batch: -80 €, Reputation +3");
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
                    Debug.Log("➡️ Salary raised: -70 €/cycle, Morale +20, departure chance -15%");
                    Next(BuildEvent22_Expansion);
                }),
                new EventOption("Reject", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: -20, dEfikasnost: 0);
                    GameResources.Instance.departureChance += 30;
                    Debug.Log("➡️ Salary refused: Morale -20, departure chance +30%");
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
                    Debug.Log("➡️ New location bought: -500 €, Stability +15, Efficiency +50, Risk +15");
                    ContinueAfterEvent22();
                }),
                new EventOption("Refuse", () =>
                {
                    Debug.Log("➡️ Expansion refused — no change.");
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
                    Debug.Log("➡️ Police contact bribed: -200 €, Risk -20");
                    ContinueAfterEvent23();
                }),
                new EventOption("Temporary shutdown", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -150, dRizik: -10, dReputacija: -3, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Temporary shutdown: -150 €, Risk -10, Reputation -3");
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
                    Debug.Log("➡️ Retaliated against rival: Risk +20, Reputation +8");
                    Next(BuildEvent25_ProfitGrowth);
                }),
                new EventOption("Ignore", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: -200, dRizik: 0, dReputacija: -10, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Sabotage ignored: -200 €, Reputation -10");
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
                    Debug.Log("➡️ Profit growth: +300 €, Reputation +10, Risk +10");
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
                    Debug.Log("➡️ Worker punished: Morale -15, repeat chance -10%");
                    Next(BuildEvent27_BlackMarket);
                }),
                new EventOption("Forgive", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 10, dEfikasnost: 0);
                    GameResources.Instance.workerMistakeChanceBonus += 5;
                    Debug.Log("➡️ Worker forgiven: Morale +10, future mistake chance +5%");
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
                    Debug.Log("➡️ Black market accepted: +300 €, Reputation +15, Risk +30");
                    Next(BuildEvent28_ReputationRising);
                }),
                new EventOption("Refuse", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: -5, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Black market refused: Risk -5");
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
                    Debug.Log("➡️ Reputation rising: Reputation +15, jobs +20%, Risk +10");
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
                    Debug.Log("➡️ Workers withdrawn: -150 €, Risk -20");
                    ContinueAfterEvent29();
                }),
                new EventOption("Continue work", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 200, dRizik: 20, dReputacija: 0, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                    Debug.Log("➡️ Continued under surveillance: +200 €, Risk +20");
                    if (UnityEngine.Random.Range(0, 100) < 40)
                    {
                        GameResources.Instance.Apply(
                            dNovac: -250, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Raid hit: -250 €");
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
                    Debug.Log("➡️ Defense prepared: -200 €, Stability +20, future damage -25%");
                    Next(BuildEvent31_ResourceShortage);
                }),
                new EventOption("Ignore threat", () =>
                {
                    GameResources.Instance.futureAttackDamageBonus += 25;
                    Debug.Log("➡️ Threat ignored: future attack damage +25%");
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
                    Debug.Log("➡️ Bought expensive supplies: -180 €");
                    Next(BuildEvent32_SystemEscalation);
                }),
                new EventOption("Wait", () =>
                {
                    GameResources.Instance.Apply(
                        dNovac: 0, dRizik: 0, dReputacija: -5, dKvaliteta: 0,
                        dStabilnost: 0, dRadnici: 0, dMoral: 0, dEfikasnost: -30);
                    Debug.Log("➡️ Waited for supplies: Efficiency -30, Reputation -5");
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
                    Debug.Log("➡️ System escalates: Risk +5, Efficiency +10 (earning potential)");
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
                    Debug.Log($"➡️ Defended base: -{300 + extra} €, Stability {stabilityLoss}, Reputation +20");
                    if (GameResources.Instance.radnici > 0 && UnityEngine.Random.Range(0, 100) < 50)
                    {
                        GameResources.Instance.Apply(
                            dNovac: 0, dRizik: 0, dReputacija: 0, dKvaliteta: 0,
                            dStabilnost: 0, dRadnici: -1, dMoral: 0, dEfikasnost: 0);
                        Debug.Log("🎲 Lost a worker during the defense.");
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
                    Debug.Log($"➡️ Retreated: -{400 + extra} €, Stability {stabilityLoss}, Risk -20, Reputation -5");
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
                        Debug.Log("➡️ Base defended — image of strength brings more heat: Risk +10");
                    }
                    else
                    {
                        GameResources.Instance.incomePenaltyPercent += 20;
                        Debug.Log("➡️ Base abandoned — future earnings -20%");
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
                    Debug.Log("🏁 Chapter 1 ended.");
                    GameResources.Instance.chapterEnded = true;
                })
            }
        };
    }

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
