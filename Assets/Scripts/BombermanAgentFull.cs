using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

public class BombermanAgentFull : Agent
{
    private Transform agentTransform;
    private Rigidbody2D rb;
    public float moveSpeed = 5f;
    public float closeDistance = 0.5f;
    public float episodeTimeLimit = 30f;
    public Tilemap indestructibleTilemap;
    public Tilemap destructibleTilemap;
    public Sprite[] floorSprites;
    public GameObject bombPrefab;
    public GameObject explosionPrefab;
    public float bombTimer = 3f;
    public float bombRange = 1f;
    private float elapsedTime = 0f;
    public Transform target;

    private bool bombPlaced = false;

    // Список для хранения начальных состояний разрушаемых объектов
    private List<DestructibleTile> destructibleTilesInitial = new List<DestructibleTile>();

    private Vector3[] roomCenters = new Vector3[]
    {
        new Vector3(-5f, 2.5f, 0f), // Комната 1
        new Vector3(2f, 2.5f, 0f),  // Комната 2
        new Vector3(4f, -3.5f, 0f),   // Комната 3
        new Vector3(0.5f,-3.5f, 0f),   // Комната 4
        new Vector3(-3f, -3.5f, 0f),  // Комната 5
        new Vector3(-6.5f, -3.5f, 0f)   // Комната 6
    };

    // Размеры комнат (ширина, высота)
    private Vector2[] roomSizes = new Vector2[]
    {
        new Vector2(5f, 6f),  // Комната 1
        new Vector2(5f, 6f),  // Комната 2
        new Vector2(2f, 5f),  // Комната 3
        new Vector2(3f, 5f),  // Комната 4
        new Vector2(2f, 5f),  // Комната 5
        new Vector2(3f, 5f)   // Комната 6
    };

    void Start()
    {
        Application.runInBackground = true;
        agentTransform = transform;
        rb = GetComponent<Rigidbody2D>();
        target = GameObject.FindGameObjectWithTag("Player").transform;

        // Сохраняем начальные состояния разрушаемых объектов
        SaveDestructibleTiles();
    }

    public override void OnEpisodeBegin()
    {
        elapsedTime = 0f;
        Vector3 agentPosition, targetPosition;
        GetSpawnPositions(out agentPosition, out targetPosition);
        agentTransform.position = agentPosition;
        target.position = targetPosition;
        rb.velocity = Vector2.zero;
        bombPlaced = false;

        // Восстанавливаем разрушаемые объекты
        RestoreDestructibleTiles();
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
        bool placeBomb = actions.DiscreteActions[0] > 0;

        Vector2 movement = new Vector2(moveHorizontal, moveVertical);
        rb.velocity = movement * moveSpeed;

        if (placeBomb && !bombPlaced && !IsCollidingWithDestructible())
        {
            PlaceBomb(agentTransform.position);
            SetReward(0.1f);
        }

        if (IsCollidingWithWall())
        {
            SetReward(-0.1f);
        }

        if (IsCollidingWithDestructible())
        {
            SetReward(-0.05f);
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
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");

        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void GetSpawnPositions(out Vector3 agentPosition, out Vector3 targetPosition)
    {
        int agentRoomIndex, targetRoomIndex;
        do
        {
            agentRoomIndex = Random.Range(0, roomCenters.Length);
            targetRoomIndex = Random.Range(0, roomCenters.Length);
        } while (agentRoomIndex == targetRoomIndex);

        agentPosition = GetRandomFreePositionInRoom(agentRoomIndex);
        targetPosition = GetRandomFreePositionInRoom(targetRoomIndex);
    }

    private Vector3 GetRandomFreePositionInRoom(int roomIndex)
    {
        Vector3 roomCenter = roomCenters[roomIndex];
        Vector2 roomSize = roomSizes[roomIndex];
        Vector3 randomPosition;

        do
        {
            randomPosition = new Vector3(
                roomCenter.x + Random.Range(-roomSize.x / 2, roomSize.x / 2),
                roomCenter.y + Random.Range(-roomSize.y / 2, roomSize.y / 2),
                0f);
        } while (!IsValidPosition(randomPosition));

        return randomPosition;
    }

    private bool IsValidPosition(Vector3 position)
    {
        Vector3Int cellPosition = indestructibleTilemap.WorldToCell(position);
        TileBase tile = indestructibleTilemap.GetTile(cellPosition);
        TileBase destructibleTile = destructibleTilemap.GetTile(cellPosition);

        if (tile == null || destructibleTile != null)
        {
            return false;
        }

        Sprite tileSprite = indestructibleTilemap.GetSprite(cellPosition);
        return IsFloorSprite(tileSprite);
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
        Vector3Int cellPosition = indestructibleTilemap.WorldToCell(agentTransform.position);
        TileBase tile = indestructibleTilemap.GetTile(cellPosition);

        if (tile == null)
        {
            return false;
        }

        Sprite tileSprite = indestructibleTilemap.GetSprite(cellPosition);
        return !IsFloorSprite(tileSprite);
    }

    private bool IsCollidingWithDestructible()
    {
        Vector3Int cellPosition = destructibleTilemap.WorldToCell(agentTransform.position);
        TileBase tile = destructibleTilemap.GetTile(cellPosition);
        return tile != null;
    }

    private void PlaceBomb(Vector3 position)
    {
        GameObject bomb = Instantiate(bombPrefab, position, Quaternion.identity);
        bombPlaced = true;
        StartCoroutine(BombCountdown(bomb));
    }

    private IEnumerator BombCountdown(GameObject bomb)
    {
        yield return new WaitForSeconds(bombTimer);
        ExplodeBomb(bomb);
    }

    private void ExplodeBomb(GameObject bomb)
    {
        Vector3 bombPosition = bomb.transform.position;
        Destroy(bomb);
        bombPlaced = false;

        InstantiateExplosion(bombPosition, Vector2.zero);

        // Взрыв вверх
        ExplodeInDirection(bombPosition, Vector2.up);
        // Взрыв вниз
        ExplodeInDirection(bombPosition, Vector2.down);
        // Взрыв влево
        ExplodeInDirection(bombPosition, Vector2.left);
        // Взрыв вправо
        ExplodeInDirection(bombPosition, Vector2.right);

        // Проверка на попадание агента в область взрыва
        if (Vector3.Distance(agentTransform.position, bombPosition) <= bombRange)
        {
            SetReward(-1f);
            EndEpisode();
        }
    }

    private void ExplodeInDirection(Vector3 origin, Vector2 direction)
    {
        for (int i = 1; i <= bombRange; i++)
        {
            Vector3Int cellPosition = destructibleTilemap.WorldToCell(origin + new Vector3(direction.x * i, direction.y * i, 0f));
            TileBase tile = destructibleTilemap.GetTile(cellPosition);
            if (tile != null)
            {
                destructibleTilemap.SetTile(cellPosition, null);
                InstantiateExplosion(destructibleTilemap.CellToWorld(cellPosition), direction);
                break; // Прерываем цикл после попадания в разрушаемый тайл
            }
            else
            {
                InstantiateExplosion(origin + new Vector3(direction.x * i, direction.y * i, 0f), direction);
            }
        }
    }

    private void InstantiateExplosion(Vector3 position, Vector2 direction)
    {
        GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        Explosion explosionComponent = explosion.GetComponent<Explosion>();
        if (explosionComponent != null)
        {
            explosionComponent.SetDirection(direction);
            if (direction == Vector2.zero)
            {
                explosionComponent.SetActiveRenderer(explosionComponent.start);
            }
            else if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                explosionComponent.SetActiveRenderer(explosionComponent.middle);
            }
            else
            {
                explosionComponent.SetActiveRenderer(explosionComponent.end);
            }

            StartCoroutine(DisableAfter(explosionComponent, 1f));
        }
    }

    private IEnumerator DisableAfter(Explosion explosionComponent, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        explosionComponent.start.enabled = false;
        explosionComponent.middle.enabled = false;
        explosionComponent.end.enabled = false;
        Destroy(explosionComponent.gameObject);
    }

    private void SaveDestructibleTiles()
    {
        destructibleTilesInitial.Clear();

        foreach (var position in destructibleTilemap.cellBounds.allPositionsWithin)
        {
            if (destructibleTilemap.HasTile(position))
            {
                TileBase tile = destructibleTilemap.GetTile(position);
                destructibleTilesInitial.Add(new DestructibleTile(position, tile));
            }
        }
    }

    private void RestoreDestructibleTiles()
    {
        destructibleTilemap.ClearAllTiles();

        foreach (var destructibleTile in destructibleTilesInitial)
        {
            destructibleTilemap.SetTile(destructibleTile.position, destructibleTile.tile);
        }
    }
}
