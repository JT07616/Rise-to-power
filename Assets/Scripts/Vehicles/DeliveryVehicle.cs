using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class DeliveryVehicle : MonoBehaviour
{
    private enum State { Idle, ToWaypoint, ToDestination, Waiting, Returning }

    [Header("Arrival")]
    [Min(0.1f)] public float arrivalDistance = 1.5f;
    [Min(0f)] public float stopDuration = 1f;
    [Min(0.5f)] public float waypointPassRadius = 3f;
    [Min(1f)] public float journeyTimeoutMultiplier = 3f;
    [Min(1f)] public float minimumJourneyTimeout = 15f;
    public GameObject ripplePrefab;
    public Vector3 rippleSpot;

    private NavMeshAgent agent;
    private NavMeshObstacle parkedObstacle;
    private DeliveryIndicator indicator;

    private Vector3 destination;
    private Vector3 returnPoint;

    private Action onArrival;
    private Action onFinished;

    private State state = State.Idle;
    private float waitUntil;
    private float desiredSpeed;
    private float arrivalDeadline;

    // sva ziva vozila, da svako moze provjeriti tko mu je ispred
    private static readonly List<DeliveryVehicle> activeVehicles = new List<DeliveryVehicle>();

    private void OnEnable() { activeVehicles.Add(this); }
    private void OnDisable() { activeVehicles.Remove(this); }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (TryGetComponent(out parkedObstacle)) parkedObstacle.enabled = false;
        if (!TryGetComponent(out LaneCentering _)) gameObject.AddComponent<LaneCentering>();
        if (!TryGetComponent(out indicator)) indicator = gameObject.AddComponent<DeliveryIndicator>();
    }

    private void Update()
    {
        if (state == State.Idle) return;

        if (state != State.Returning && Time.time >= arrivalDeadline)
        {
            Debug.LogWarning("Delivery vehicle timed out before reaching its destination; completing the journey without the vehicle.");
            Fail();
            return;
        }

        if (state == State.Waiting)
        {
            if (Time.time >= waitUntil) StartReturn();
            return;
        }

        if (!agent.enabled || !agent.isOnNavMesh || agent.pathPending) return;

        KeepDistanceFromCarAhead();

        // Kroz medju-waypoint se prolazi u voznji, bez kocenja.
        if (state == State.ToWaypoint)
        {
            if (agent.remainingDistance > waypointPassRadius) return;

            state = State.ToDestination;
            if (!agent.SetDestination(destination)) Fail();
            return;
        }

        if (agent.remainingDistance > Mathf.Max(arrivalDistance, agent.stoppingDistance)) return;

        if (state == State.ToDestination) Arrive();
        else if (state == State.Returning) Finish();
    }

    // Ako je drugo vozilo ispred na istoj cesti, koci umjesto da prodje kroz njega.
    private void KeepDistanceFromCarAhead()
    {
        DeliveryVehicle ahead = null;

        foreach (DeliveryVehicle other in activeVehicles)
        {
            if (other == this) continue;

            Vector3 toOther = other.transform.position - transform.position;
            if (toOther.magnitude > 7f) continue;
            if (Vector3.Dot(transform.forward, toOther.normalized) < 0.8f) continue;

            ahead = other;
            break;
        }

        if (ahead == null)
        {
            agent.speed = desiredSpeed;
            return;
        }

        float aheadSpeed = ahead.agent != null && ahead.agent.enabled
            ? ahead.agent.velocity.magnitude
            : 0f;

        agent.speed = Mathf.Min(desiredSpeed, Mathf.Max(0f, aheadSpeed - 0.5f));
    }

    public bool BeginJourney(
        Vector3 from,
        Vector3 to,
        float duration,
        Action arrived,
        Action finished = null,
        Vector3? returnTo = null,
        Vector3? viaWaypoint = null)
    {
        rippleSpot = to;

        // Cilj je cesto zgrada daleko od ceste, pa se trazi navmesh u sirem radijusu.
        Vector3 start = Snap(from);
        destination = Snap(to, 50f);
        returnPoint = returnTo.HasValue ? Snap(returnTo.Value) : start;

        onArrival = arrived;
        onFinished = finished;
        arrivalDeadline = Time.time + Mathf.Max(
            minimumJourneyTimeout,
            duration * journeyTimeoutMultiplier);

        agent.enabled = true;
        if (!agent.Warp(start))
        {
            Debug.LogError($"Vozilo se ne moze postaviti na NavMesh kod {start}.");
            return false;
        }

        Vector3 firstTarget = viaWaypoint.HasValue ? Snap(viaWaypoint.Value) : destination;

        // Brzina prati zadano trajanje puta. Ubrzanje i brzina skretanja rastu s njom,
        // inace brzom autu radijus skretanja postane veci od praga dolaska
        // pa zauvijek kruzi oko cilja.
        desiredSpeed = Mathf.Max(0.1f, PathLength(start, destination) / Mathf.Max(0.1f, duration));
        agent.speed = desiredSpeed;
        agent.acceleration = Mathf.Max(8f, desiredSpeed * 2.5f);
        agent.angularSpeed = Mathf.Max(300f, desiredSpeed / Mathf.Max(0.5f, arrivalDistance) * Mathf.Rad2Deg);
        agent.isStopped = false;

        // Odmah okreni auto prema cilju da ne radi piruetu na spawnu.
        Vector3 facing = firstTarget - start;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(facing);

        if (!agent.SetDestination(firstTarget))
        {
            Debug.LogWarning($"Vozilo ne moze naci put do {firstTarget} — provjeri je li navmesh spojen do tog dijela grada.");
            return false;
        }

        state = viaWaypoint.HasValue ? State.ToWaypoint : State.ToDestination;
        indicator.Show();
        return true;
    }

    private void Arrive()
    {
        if (ripplePrefab != null && GetComponentInChildren<PoliceBeacon>() == null)
            StartCoroutine(SpawnRipples());

        indicator.Hide();
        agent.isStopped = true;

        // dok stoji, auto postane prava prepreka pa drugi voze oko njega
        if (parkedObstacle != null)
        {
            agent.enabled = false;
            parkedObstacle.enabled = true;
        }

        Action callback = onArrival;
        onArrival = null;
        callback?.Invoke();

        if (stopDuration <= 0f)
        {
            StartReturn();
            return;
        }

        state = State.Waiting;
        waitUntil = Time.time + stopDuration;
    }

    private void StartReturn()
    {
        indicator.Hide();

        if (parkedObstacle != null)
        {
            parkedObstacle.enabled = false;
            agent.enabled = true;
            if (!agent.isOnNavMesh) agent.Warp(transform.position);
        }

        if (!agent.enabled || !agent.isOnNavMesh || !agent.SetDestination(returnPoint))
        {
            Finish();
            return;
        }

        agent.isStopped = false;
        state = State.Returning;
    }

    private void Finish()
    {
        indicator.Hide();
        state = State.Idle;

        Action callback = onFinished;
        onFinished = null;
        callback?.Invoke();

        Destroy(gameObject);
    }

    // Put je propao usred voznje: isporuka se odmah dovrsava
    // da narudzba ne ostane trajno zaglavljena.
    private void Fail()
    {
        Action callback = onArrival;
        onArrival = null;
        callback?.Invoke();
        Finish();
    }

    IEnumerator SpawnRipples()
    {
        for (int i = 0; i < 3; i++)
        {
            Instantiate(ripplePrefab, rippleSpot + Vector3.up * 0.5f, ripplePrefab.transform.rotation);
            yield return new WaitForSeconds(0.25f);
        }
    }

    // Najbliza tocka na navmeshu, ili original ako nista nije blizu.
    private static Vector3 Snap(Vector3 position, float searchRadius = 10f)
    {
        return NavMesh.SamplePosition(position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas)
            ? hit.position
            : position;
    }

    // Duljina puta po navmeshu; bez valjanog puta je zracna linija dovoljna procjena.
    private static float PathLength(Vector3 from, Vector3 to)
    {
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete ||
            path.corners.Length < 2)
        {
            return Vector3.Distance(from, to);
        }

        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return length;
    }
}
