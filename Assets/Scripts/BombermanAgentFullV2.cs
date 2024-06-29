using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

public class BombermanAgentFullV2 : Agent
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
    private bool isExplosionActive = false;
    private float explosionEndTime;

    // Список для хранения начальных состояний разрушаемых объектов
    private List<DestructibleTile> destructibleTilesInitial = new List<DestructibleTile>();

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
        agentTransform.position = GetRandomFreePosition();

        // Устанавливаем цель в случайное свободное место
        target.position = GetRandomFreePosition();
        rb.velocity = Vector2.zero;
        bombPlaced = false;
        isExplosionActive = false;

        // Восстанавливаем разрушаемые объекты
        RestoreDestructibleTiles();

        // Удаляем оставшиеся бомбы и взрывы
        ClearBombsAndExplosions();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(agentTransform.localPosition);
        sensor.AddObservation(target.localPosition);
        sensor.AddObservation(rb.velocity.x);
        sensor.AddObservation(rb.velocity.y);

        // Добавляем наблюдения о ближайших стенах и разрушаемых объектах (радиус обзора 2 клетки)
        AddSurroundingObservations(sensor, 2);
    }

    private void AddSurroundingObservations(VectorSensor sensor, int range)
    {
        Vector3 agentPos = agentTransform.position;
        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        foreach (var direction in directions)
        {
            for (int i = 1; i <= range; i++)
            {
                Vector3Int cellPos = indestructibleTilemap.WorldToCell(agentPos + (Vector3)direction * i);
                TileBase indestructibleTile = indestructibleTilemap.GetTile(cellPos);
                TileBase destructibleTile = destructibleTilemap.GetTile(cellPos);

                sensor.AddObservation(indestructibleTile != null ? 1f : 0f);
                sensor.AddObservation(destructibleTile != null ? 1f : 0f);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveHorizontal = actions.ContinuousActions[0];
        float moveVertical = actions.ContinuousActions[1];
        bool placeBomb = actions.DiscreteActions[0] > 0;

        Vector2 movement = new Vector2(moveHorizontal, moveVertical);
        rb.velocity = movement * moveSpeed;

        // Если агент не рядом с целью и бомба не была поставлена, проверяем нужно ли ставить бомбу
        if (Vector2.Distance(agentTransform.position, target.position) >= closeDistance)
        {
            if (placeBomb && !bombPlaced && IsDestructibleInRange())
            {
                PlaceBomb(agentTransform.position);
                SetReward(0.1f); // награда за постановку бомбы
            }
        }

        // Награда за перемещение к цели
        float distanceToTarget = Vector2.Distance(agentTransform.position, target.position);
        SetReward(0.01f * (1.0f / distanceToTarget));

        if (IsCollidingWithWall())
        {
            SetReward(-0.1f); // наказание за столкновение с неразрушаемой стеной
        }

        if (IsCollidingWithDestructible())
        {
            SetReward(-0.05f); // наказание за столкновение с разрушаемой стеной
        }

        if (distanceToTarget < closeDistance)
        {
            SetReward(1f); // большая награда за достижение цели
            EndEpisode();
        }

        if (isExplosionActive && Time.time >= explosionEndTime)
        {
            isExplosionActive = false;
        }

        if (isExplosionActive && IsCollidingWithExplosion())
        {
            SetReward(-1f); // большое наказание за попадание в взрыв
            EndEpisode();
        }

        elapsedTime += Time.deltaTime;
        if (elapsedTime >= episodeTimeLimit)
        {
            SetReward(-1f); // наказание за превышение времени эпизода
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

    private bool IsDestructibleInRange()
    {
        Vector3 agentPos = agentTransform.position;
        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.left, Vector3.right };

        foreach (Vector3 dir in directions)
        {
            for (int i = 1; i <= bombRange; i++)
            {
                Vector3Int cellPosition = destructibleTilemap.WorldToCell(agentPos + dir * i);
                if (destructibleTilemap.GetTile(cellPosition) != null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void PlaceBomb(Vector3 position)
    {
        GameObject bomb = Instantiate(bombPrefab, position, Quaternion.identity);
        bomb.tag = "Bomb";
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

        isExplosionActive = true;
        explosionEndTime = Time.time + 1f;

        // Проверка на попадание агента в область взрыва
        if (IsCollidingWithExplosion())
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

    private bool IsCollidingWithExplosion()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(agentTransform.position, 0.5f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Explosion"))
            {
                return true;
            }
        }
        return false;
    }

    private void InstantiateExplosion(Vector3 position, Vector2 direction)
    {
        GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        explosion.tag = "Explosion";
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

    private void ClearBombsAndExplosions()
    {
        GameObject[] bombs = GameObject.FindGameObjectsWithTag("Bomb");
        foreach (GameObject bomb in bombs)
        {
            Destroy(bomb);
        }

        GameObject[] explosions = GameObject.FindGameObjectsWithTag("Explosion");
        foreach (GameObject explosion in explosions)
        {
            Destroy(explosion);
        }
    }
}
