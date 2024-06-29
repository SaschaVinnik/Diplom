using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class BombermanAgentEmpty : Agent
{

    private Transform agentTransform;
    private Rigidbody2D rb;

    public float moveSpeed = 5f;
    public float closeDistance = 0f;
    public Transform target;
    public float episodeTimeLimit = 5f;
    private float elapsedTime = 0f;
    void Start()
    {
        agentTransform = transform;
        rb = GetComponent<Rigidbody2D>();
        target = GameObject.FindGameObjectWithTag("Player").transform;
    }

    public override void OnEpisodeBegin()
    {
        elapsedTime = 0f;
        agentTransform.position = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0f);
        target.position = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0f);
        rb.velocity = Vector2.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {

        sensor.AddObservation(agentTransform.localPosition);
        sensor.AddObservation(target.localPosition);

        sensor.AddObservation(rb.velocity.x);
        sensor.AddObservation(rb.velocity.y);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {

        float moveHorizontal = actions.ContinuousActions[0];
        float moveVertical = actions.ContinuousActions[1];

        Vector2 movement = new Vector2(moveHorizontal, moveVertical);
        rb.velocity = movement * moveSpeed;

        if (Vector2.Distance(agentTransform.position, target.position) < closeDistance)
        {
            SetReward(1f);
            EndEpisode();
        }


        elapsedTime += Time.deltaTime;
        if (elapsedTime >= episodeTimeLimit)
        {

            SetReward(-1f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.ContinuousActions;
        actions[0] = Input.GetAxisRaw("Horizontal");
        actions[1] = Input.GetAxisRaw("Vertical");
    }
}