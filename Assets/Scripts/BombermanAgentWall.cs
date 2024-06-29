using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.Tilemaps;

public class BombermanAgentWall : Agent
{    
    private Transform agentTransform;
 
    private Rigidbody2D rb;

    public float moveSpeed = 5f;

    
    public float closeDistance = 1f;

   public Transform target;
   public float episodeTimeLimit = 5f; 
    
    public Tilemap tilemap; 
    public Sprite[] floorSprites; // Array of sprites for floor tiles
     private float elapsedTime = 0f;
    void Start()
    {
        Application.runInBackground = true;
        
        agentTransform = transform;
        rb = GetComponent<Rigidbody2D>();

        
        target = GameObject.FindGameObjectWithTag("Player").transform;
    }


    public override void OnEpisodeBegin()
    {
        elapsedTime = 0f;
      
        agentTransform.position = GetRandomFreePosition();
        
        target.position = GetRandomFreePosition();

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

        
        if (IsCollidingWithWall())
        {
            SetReward(-0.1f);
        }

        
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

   private Vector3 GetRandomFreePosition()
    {
        Vector3 randomPosition;
        do
        {
            randomPosition = new Vector3(
                Random.Range(-7.5f, 5f),
                Random.Range(-5f, 5f),
                0f);
        } while (!IsValidPosition(randomPosition));

        return randomPosition;
    }

    private bool IsValidPosition(Vector3 position)
    {
        Vector3Int cellPosition = tilemap.WorldToCell(position);

        TileBase tile = tilemap.GetTile(cellPosition);
        if (tile == null)
        {
            return false;
        }

        // Check if the tile is a floor tile by comparing its sprite
        Sprite tileSprite = tilemap.GetSprite(cellPosition);
        bool isFloor = IsFloorSprite(tileSprite);

        return isFloor;
    }

    private bool IsFloorSprite(Sprite sprite)
    {
        foreach (var floorSprite in floorSprites)
        {
            if (sprite == floorSprite)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsCollidingWithWall()
    {
        Vector3Int cellPosition = tilemap.WorldToCell(agentTransform.position);

        TileBase tile = tilemap.GetTile(cellPosition);
        if (tile == null)
        {
            return false;
        }
        
        Sprite tileSprite = tilemap.GetSprite(cellPosition);
        return !IsFloorSprite(tileSprite);
    }
    
}