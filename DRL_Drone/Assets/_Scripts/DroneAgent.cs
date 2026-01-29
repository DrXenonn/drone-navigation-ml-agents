using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class DroneAgent : Agent
{
    [Header("Target")] 
    public Transform Target;
    
    [Header("Environment")] 
    public BuildingSpawner buildingSpawner;

    [Header("Movement Settings")] [SerializeField]
    private float thrustForce = 10f;
    [SerializeField] private float maxVelocity = 15f;

    [Header("LIDAR Settings")] 
    [SerializeField] private int numRays = 16;
    [SerializeField] private float rayDistance = 10f;
    [SerializeField] private float rayAngleRange = 180f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float downwardRayDistance = 10f;

    [Header("Reward Settings")] 
    [SerializeField] private float continuousPenalty = -0.005f;
    [SerializeField] private float goalReward = 10f;
    [SerializeField] private float collisionPenalty = -5f;
    [SerializeField] private float obstacleProximityPenalty = -0.3f;
    [SerializeField] private float proximityThreshold = 5f;
    [SerializeField] private float targetReachedDistance = 2f;
    [SerializeField] private float maxDistanceFromTarget = 150f;

    [SerializeField] private Rigidbody rb;
    private Vector3 startPos;
    private float previousDistanceToTarget;
    private bool[] raycastHits;
    

    public override void Initialize()
    {
        raycastHits = new bool[numRays];
    }

    public override void OnEpisodeBegin()
    {
        buildingSpawner?.Generate();

        startPos = new Vector3(
            Random.Range(-25f, -45f),
            Random.Range(1.5f, 4f),
            Random.Range(-40f, 40f)
        );

        transform.localPosition = startPos;
        transform.localRotation = Quaternion.Euler(0, 90, 0);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (Target != null)
        {
            Target.localPosition = new Vector3(
                Random.Range(120f, 135f),
                0.65f,
                Random.Range(-40f, -15f)
            );
        }

        previousDistanceToTarget = Vector3.Distance(transform.localPosition, Target.localPosition);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        float distanceToTarget = Vector3.Distance(transform.localPosition, Target.localPosition);
        sensor.AddObservation(distanceToTarget / maxDistanceFromTarget);

        Vector3 directionToTarget = (Target.localPosition - transform.localPosition).normalized;
        sensor.AddObservation(directionToTarget);

        sensor.AddObservation(rb.linearVelocity / maxVelocity);
        sensor.AddObservation(rb.angularVelocity / 10f);
        sensor.AddObservation(transform.localRotation);

        PerformLIDARScan();
        for (int i = 0; i < numRays; i++)
        {
            sensor.AddObservation(raycastHits[i] ? 1f : 0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        AddReward(continuousPenalty);

        float aiY = actions.ContinuousActions[0] * 1.5f;
        float aiZ = actions.ContinuousActions[1];
        float aiX = actions.ContinuousActions[2];

        float humanY = 0f;
        if (Input.GetKey(KeyCode.Space)) humanY = 1f;
        if (Input.GetKey(KeyCode.LeftControl)) humanY = -1f;

        float humanZ = Input.GetAxis("Vertical");
        float humanX = Input.GetAxis("Horizontal");

        const float humanWeight = 1.25f;
        const float aiWeight = 1f;

        float finalY = (aiY * aiWeight + humanY * humanWeight) / (aiWeight + humanWeight);
        float finalZ = (aiZ * aiWeight + humanZ * humanWeight) / (aiWeight + humanWeight);
        float finalX = (aiX * aiWeight + humanX * humanWeight) / (aiWeight + humanWeight);

        Vector3 thrust = new Vector3(finalX, finalY, finalZ) * thrustForce;
        rb.AddRelativeForce(thrust);

        if (rb.linearVelocity.magnitude > maxVelocity)
            rb.linearVelocity = rb.linearVelocity.normalized * maxVelocity;

        float currentDistance = Vector3.Distance(transform.localPosition, Target.localPosition);
        float distanceDelta = previousDistanceToTarget - currentDistance;
        AddReward(distanceDelta * 0.1f);
        previousDistanceToTarget = currentDistance;
        
        if (currentDistance > maxDistanceFromTarget)
        {
            AddReward(collisionPenalty);
            EndEpisode();
            return;
        }

        if (currentDistance < targetReachedDistance)
        {
            AddReward(goalReward);
            EndEpisode();
            return;
        }

        for (int i = 0; i < numRays; i++)
        {
            if (raycastHits[i])
            {
                AddReward(obstacleProximityPenalty);
                break;
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = 0f;
        if (Input.GetKey(KeyCode.Space))
            continuousActions[0] = 1f;
        if (Input.GetKey(KeyCode.LeftControl))
            continuousActions[0] = -1f;

        continuousActions[1] = Input.GetAxis("Vertical");
        continuousActions[2] = Input.GetAxis("Horizontal");
    }

    private void PerformLIDARScan()
    {
        float angleStep = rayAngleRange / numRays;
        float startAngle = -rayAngleRange / 2f;

        for (int i = 0; i < numRays; i++)
        {
            float angle = startAngle + (angleStep * i);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            RaycastHit hit;
            raycastHits[i] = Physics.Raycast(
                transform.position,
                direction,
                out hit,
                rayDistance,
                obstacleLayer
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            AddReward(collisionPenalty);
            EndEpisode();
        }
        else if (other.CompareTag("Goal"))
        {
            AddReward(goalReward);
            EndEpisode();
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || raycastHits == null)
            return;

        float angleStep = rayAngleRange / numRays;
        float startAngle = -rayAngleRange / 2f;

        for (int i = 0; i < numRays; i++)
        {
            float angle = startAngle + (angleStep * i);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            Gizmos.color = raycastHits[i] ? Color.red : Color.green;
            Gizmos.DrawRay(transform.position, direction * rayDistance);
        }
    }
}