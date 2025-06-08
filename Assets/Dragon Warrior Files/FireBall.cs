using System.Collections;
using UnityEngine;

public class Fireball : MonoBehaviour
{
    public int damage = 25;
    public float speed = 12f;
    public LayerMask playerLayer;
    public float lifetime = 3f;

    private Vector2 direction;
    private Collider2D ballCollider;

    void Start()
    {
        ballCollider = GetComponent<Collider2D>();
        if (ballCollider != null)
        {
            ballCollider.enabled = false;
            StartCoroutine(EnableColliderAfter(0.1f));
        }

        // 确保最终销毁
        Destroy(gameObject, lifetime);
    }

    IEnumerator EnableColliderAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ballCollider != null)
            ballCollider.enabled = true;
    }

    public void SetDirection(Vector2 newDirection)
    {
        direction = newDirection.normalized;

        // 根据方向旋转火球
        if (direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    void Update()
    {
        if (direction == Vector2.zero)
        {
            Debug.LogWarning("Fireball has no direction, destroying");
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)direction * speed * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 忽略与发射者的碰撞
        if (other.gameObject.layer == LayerMask.NameToLayer("Enemy"))
            return;

        // 玩家检测
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            CharacterController2D player = other.GetComponent<CharacterController2D>();
            if (player != null)
            {
                CharacterData playerData = player.GetComponent<CharacterData>();
                if (playerData != null && !playerData.GetDeadStatement())
                {
                    StartCoroutine(player.TakeDamage());
                }
            }
            Destroy(gameObject);
        }
        // 地形检测
        else if (other.gameObject.layer == LayerMask.NameToLayer("Terrain"))
        {
            Destroy(gameObject);
        }
    }
}