using System;
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

    private NavMeshAgent agent;
    private DeliveryIndicator indicator;

    private Vector3 destination;
    private Vector3 returnPoint;

    private Action onArrival;
    private Action onFinished;

    private State state = State.Idle;
    private float waitUntil;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!TryGetComponent(out LaneCentering _)) gameObject.AddComponent<LaneCentering>();
        if (!TryGetComponent(out indicator)) indicator = gameObject.AddComponent<DeliveryIndicator>();
    }

    private void Update()
    {
        if (state == State.Idle) return;

        if (state == State.Waiting)
        {
            if (Time.time >= waitUntil) StartReturn();
            return;
        }

        if (!agent.enabled || !agent.isOnNavMesh || agent.pathPending) return;

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

    public bool BeginJourney(
        Vector3 from,
        Vector3 to,
        float duration,
        Action arrived,
        Action finished = null,
        Vector3? returnTo = null,
        Vector3? viaWaypoint = null)
    {
        // Cilj je cesto zgrada daleko od ceste, pa se trazi navmesh u sirem radijusu.
        Vector3 start = Snap(from);
        destination = Snap(to, 50f);
        returnPoint = returnTo.HasValue ? Snap(returnTo.Value) : start;

        onArrival = arrived;
        onFinished = finished;

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
        agent.speed = Mathf.Max(0.1f, PathLength(start, destination) / Mathf.Max(0.1f, duration));
        agent.acceleration = Mathf.Max(8f, agent.speed * 2.5f);
        agent.angularSpeed = Mathf.Max(300f, agent.speed / Mathf.Max(0.5f, arrivalDistance) * Mathf.Rad2Deg);
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
        indicator.Hide();
        agent.isStopped = true;

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
