using UnityEngine;
using UnityEngine.AI;

// Drzi vozilo na sredini trake tako da mjeri rubove navmesha lijevo i desno.
// Ceste su odvojene jednosmjerne pruge sirine ~6 m, pa se centrira samo
// kad izmjerena sirina odgovara jednoj traci.
[RequireComponent(typeof(NavMeshAgent))]
public class LaneCentering : MonoBehaviour
{
    [Header("Lane detection")]
    [Min(1f)] public float expectedLaneWidth = 6f;
    [Min(0f)] public float laneWidthTolerance = 1.5f;
    [Min(0.1f)] public float probeExtraDistance = 1f;
    [Min(0f)] public float lookAheadDistance = 2f;

    [Header("Centering")]
    [Min(0f)] public float centeringStrength = 1.5f;
    [Min(0f)] public float maximumCorrectionSpeed = 0.8f;
    [Min(0f)] public float correctionDeadZone = 0.15f;
    [Min(0.1f)] public float correctionSmoothing = 5f;
    [Range(0f, 90f)] public float maximumCenteringAngle = 45f;

    private NavMeshAgent agent;
    private float sideError;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void LateUpdate()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped)
        {
            sideError = 0f;
            return;
        }

        // U zavrsnom prilazu cilju se gasi, da ne gura auto bocno dok koci.
        if (!agent.pathPending && agent.remainingDistance < expectedLaneWidth)
        {
            sideError = 0f;
            return;
        }

        Vector3 forward = agent.desiredVelocity;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }
        if (forward.sqrMagnitude < 0.01f)
        {
            sideError = 0f;
            return;
        }
        forward.Normalize();

        // U ostrom zavoju se ne centrira, korekcija bi vukla auto van zavoja.
        Vector3 heading = transform.forward;
        heading.y = 0f;
        if (heading.sqrMagnitude > 0.01f && Vector3.Angle(heading, forward) > maximumCenteringAngle)
        {
            sideError = 0f;
            return;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 probe = agent.nextPosition + forward * lookAheadDistance;

        if (!NavMesh.SamplePosition(probe, out NavMeshHit probeHit,
                Mathf.Max(1f, lookAheadDistance), agent.areaMask))
        {
            return;
        }
        probe = probeHit.position;

        float probeDistance = expectedLaneWidth * 0.5f + laneWidthTolerance + probeExtraDistance;
        bool hasLeft = NavMesh.Raycast(
            probe, probe - right * probeDistance, out NavMeshHit left, agent.areaMask);
        bool hasRight = NavMesh.Raycast(
            probe, probe + right * probeDistance, out NavMeshHit rightEdge, agent.areaMask);

        if (!hasLeft || !hasRight)
        {
            sideError = 0f;
            return;
        }

        // Na raskrizjima i prosirenjima mjera ne valja pa se korekcija gasi.
        float width = Vector3.Distance(left.position, rightEdge.position);
        if (Mathf.Abs(width - expectedLaneWidth) > laneWidthTolerance)
        {
            sideError = 0f;
            return;
        }

        Vector3 laneCenter = (left.position + rightEdge.position) * 0.5f;
        float error = Vector3.Dot(laneCenter - agent.nextPosition, right);

        float blend = 1f - Mathf.Exp(-correctionSmoothing * Time.deltaTime);
        sideError = Mathf.Lerp(sideError, error, blend);

        if (Mathf.Abs(sideError) <= correctionDeadZone) return;

        float effective = Mathf.Sign(sideError) * (Mathf.Abs(sideError) - correctionDeadZone);
        float correction = Mathf.Clamp(
            effective * centeringStrength, -maximumCorrectionSpeed, maximumCorrectionSpeed);

        agent.Move(right * correction * Time.deltaTime);
    }
}
