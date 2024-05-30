using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.Tilemaps;

public class BombermanAgentWall : Agent
{
    // Ссылка на компонент Transform для перемещения агента
    private Transform agentTransform;

    // Ссылка на компонент Rigidbody2D для управления физикой агента
    private Rigidbody2D rb;

    // Скорость передвижения агента
    public float moveSpeed = 5f;

    // Расстояние, на котором агент считается близким к цели
    public float closeDistance = 1f;

    // Цель, к которой движется агент (например, игрок)
    public Transform target;
    // Время, отведенное на эпизод
    public float episodeTimeLimit = 5f; // 30 секунд
    // Объект тайлмапа для разрушаемых объектов
    public Tilemap tilemap; // Tilemap for all tiles
    public Sprite[] floorSprites; // Array of sprites for floor tiles
    // Прошедшее время в текущем эпизоде
    private float elapsedTime = 0f;
    void Start()
    {
        Application.runInBackground = true;
        // Получаем ссылки на компоненты Transform и Rigidbody2D
        agentTransform = transform;
        rb = GetComponent<Rigidbody2D>();

        // Находим цель, к которой будет двигаться агент
        target = GameObject.FindGameObjectWithTag("Player").transform;
    }


    public override void OnEpisodeBegin()
    {
        elapsedTime = 0f;
        // Перемещаем агента в случайную позицию в лабиринте
        agentTransform.position = GetRandomFreePosition();

        // Устанавливаем цель в случайное свободное место
        target.position = GetRandomFreePosition();

        rb.velocity = Vector2.zero;
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // Добавляем наблюдения о положении агента и цели
        sensor.AddObservation(agentTransform.localPosition);
        sensor.AddObservation(target.localPosition);

        // Agent velocity
        sensor.AddObservation(rb.velocity.x);
        sensor.AddObservation(rb.velocity.y);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveHorizontal = actions.ContinuousActions[0];
        float moveVertical = actions.ContinuousActions[1];

        Vector2 movement = new Vector2(moveHorizontal, moveVertical);
        rb.velocity = movement * moveSpeed;

        // Проверка столкновения с стенами
        if (IsCollidingWithWall())
        {
            SetReward(-0.1f);
        }

        // Проверка, достиг ли агент цели
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
        // Для управления агентом вручную при отладке
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

        // Check if the tile is not a floor tile, which means it is a wall tile
        Sprite tileSprite = tilemap.GetSprite(cellPosition);
        return !IsFloorSprite(tileSprite);
    }

    // private bool IsCollidingWithWall()
    // {
    //     Vector3Int cellPosition = destructibleTilemap.WorldToCell(agentTransform.position);
    //     if (destructibleTilemap.HasTile(cellPosition) || indestructibleTilemap.HasTile(cellPosition))
    //     {
    //         return true;
    //     }
    //     return false;
    // }
}