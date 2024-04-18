using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class BombermanAgent : Agent
{
    // Ссылка на компонент Transform для перемещения агента
    private Transform agentTransform;

    // Ссылка на компонент Rigidbody2D для управления физикой агента
    private Rigidbody2D rb;

    // Скорость передвижения агента
    public float moveSpeed = 5f;

    // Расстояние, на котором агент считается близким к цели
    public float closeDistance = 0f;

    // Цель, к которой движется агент (например, игрок)
    public Transform target;
    // Время, отведенное на эпизод
    public float episodeTimeLimit = 5f; // 30 секунд

    // Прошедшее время в текущем эпизоде
    private float elapsedTime = 0f;
    void Start()
    {
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
        agentTransform.position = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0f);

        // Обновляем позицию цели агента
        target.position = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0f);

        // Сбрасываем скорость агента
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
        // Получаем действие агента
        float moveHorizontal = actions.ContinuousActions[0];
        float moveVertical = actions.ContinuousActions[1];

        // Применяем действие к скорости агента
        Vector2 movement = new Vector2(moveHorizontal, moveVertical);
        rb.velocity = movement * moveSpeed;

        // Проверяем, достиг ли агент цели
        if (Vector2.Distance(agentTransform.position, target.position) < closeDistance)
        {
            // Достигли цели, награда
            SetReward(1f);
            EndEpisode();
        }

        // Проверка превышения времени
        elapsedTime += Time.deltaTime;
        if (elapsedTime >= episodeTimeLimit)
        {
            // Время вышло, штраф и перезапуск
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
}