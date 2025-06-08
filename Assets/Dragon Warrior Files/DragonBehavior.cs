using UnityEngine;
using System.Collections;

public class DragonBehavior : Enemy // 继承自Enemy基类
{
    // 注意：从基类继承的字段已在Enemy中定义，这里不需要重复定义
#pragma warning disable 0649 // 禁用未赋值字段警告

    [Header("Movement Settings")]
    public float groundCheckDistance = 0.5f;
    public float wallCheckDistance = 0.3f;
    public float normalSpeed = 2f;
    public float madSpeed = 5f;
    [SerializeField] private Transform groundCheck; // 序列化的Transform
    [SerializeField] private Transform wallCheck; // 序列化的Transform
    public LayerMask whatIsGrounded;
    public LayerMask whatIsWall;

    [Header("Attack Settings")]
    public GameObject fireballPrefab;
    [SerializeField] private Transform firePoint; // 序列化的Transform
    public float fireRate = 3f;
    public int fireDamage = 25;
    public LayerMask playerLayer;
    public float attackRange = 5f;

    [Header("Combat Settings")]
    public float hurtForce = 400f;
    public float deadForce = 500f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource; // 序列化的AudioSource
    [SerializeField] private AudioClip fireSound; // 序列化的AudioClip
    [SerializeField] private AudioClip hurtSound; // 序列化的AudioClip
    [SerializeField] private AudioClip deathSound; // 序列化的AudioClip

    // 状态系统
    public enum EnemyState
    {
        IDLE, MOVEMENT, ATTACK_READY, ATTACK, HURT, DEAD
    }
    [SerializeField] private EnemyState currentState;

    // 移除了private Animator animator; 因为这个字段在基类中已定义

    private float nextFireTime;
    private bool groundDetected, wallDetected;
    private bool isAttack, isHurt;
    private Rigidbody2D rb;
    private Transform player;
    private Vector2 movement;
    private HitEffect hit;

    void Start()
    {
        // 基类中的animator字段应该已初始化
        // 如果基类没有初始化，可以在这里安全初始化
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // 查找或创建HitEffect
        hit = GetComponentInChildren<HitEffect>(true);
        if (hit == null)
        {
            Debug.LogWarning("No HitEffect found. Creating default one.");
            GameObject hitObj = new GameObject("DefaultHitEffect");
            hitObj.transform.SetParent(transform);
            hit = hitObj.AddComponent<HitEffect>();
        }

        // 确保必要的检测点存在
        groundCheck = GetOrCreateTransform(groundCheck, "GroundCheck", new Vector2(0, -0.5f));
        wallCheck = GetOrCreateTransform(wallCheck, "WallCheck", new Vector2(isFacingLeft ? -0.5f : 0.5f, 0));
        firePoint = GetOrCreateTransform(firePoint, "FirePoint", new Vector2(isFacingLeft ? -0.8f : 0.8f, 0.2f));

        currentState = EnemyState.IDLE;
    }

    // 辅助方法：获取或创建必需的Transform
    private Transform GetOrCreateTransform(Transform existing, string name, Vector2 offset)
    {
        if (existing != null) return existing;

        GameObject newObj = new GameObject(name);
        newObj.transform.SetParent(transform);
        newObj.transform.localPosition = offset;
        return newObj.transform;
    }

    void Update()
    {
        if (isDead)
        {
            SwitchState(EnemyState.DEAD);
            return;
        }

        CheckIsDead();
        DetectEnvironment();
        UpdateDirection();
        UpdateStates();
    }

    void FixedUpdate()
    {
        HandleMovementPhysics();
    }

    private void UpdateStates()
    {
        switch (currentState)
        {
            case EnemyState.IDLE:
                UpdateIdleState();
                break;
            case EnemyState.MOVEMENT:
                UpdateMovementState();
                break;
            case EnemyState.ATTACK_READY:
                UpdateAttackReadyState();
                break;
            case EnemyState.ATTACK:
                UpdateAttackState();
                break;
            case EnemyState.HURT:
                UpdateHurtState();
                break;
            case EnemyState.DEAD:
                //UpdateDeadState();
                break;
        }
    }

    protected override void UpdateDirection()
    {
        if (transform.localScale.x == -1)
        {
            isFacingLeft = true;
        }
        else if (transform.localScale.x == 1)
        {
            isFacingLeft = false;
        }
    }

    private void DetectEnvironment()
    {
        if (groundCheck == null || wallCheck == null)
            return;

        // 地面检测
        RaycastHit2D groundHit = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            groundCheckDistance,
            whatIsGrounded
        );
        groundDetected = groundHit.collider != null;

        // 墙壁检测
        Vector2 direction = isFacingLeft ? Vector2.left : Vector2.right;
        RaycastHit2D wallHit = Physics2D.Raycast(
            wallCheck.position,
            direction,
            wallCheckDistance,
            whatIsWall
        );
        wallDetected = wallHit.collider != null;

        // 调试可视化
        Debug.DrawRay(groundCheck.position, Vector2.down * groundCheckDistance,
                     groundDetected ? Color.green : Color.red);
        Debug.DrawRay(wallCheck.position, direction * wallCheckDistance,
                     wallDetected ? Color.green : Color.red);
    }

    private void HandleMovementPhysics()
    {
        if (currentState == EnemyState.MOVEMENT || currentState == EnemyState.ATTACK)
        {
            float moveDirection = isFacingLeft ? -1 : 1;
            float speed = currentState == EnemyState.ATTACK ? madSpeed : normalSpeed;

            movement.Set(moveDirection * speed, rb.velocity.y);
            rb.velocity = movement;
        }
    }

    private void UpdateIdleState()
    {
        // 随机开始移动
        if (Random.value < 0.5f)
        {
            SwitchState(EnemyState.MOVEMENT);
        }
    }

    private void UpdateMovementState()
    {
        // 检查是否需要翻转方向
        if (!groundDetected || wallDetected)
        {
            Flip();
        }

        // 检测玩家是否在攻击范围内
        if (PlayerInAttackRange())
        {
            SwitchState(EnemyState.ATTACK_READY);
        }
    }

    private bool PlayerInAttackRange()
    {
        if (player == null)
            return false;

        return Vector2.Distance(transform.position, player.position) < attackRange;
    }

    private void UpdateAttackReadyState()
    {
        // 确保面向玩家
        if (!IsFacingPlayer())
        {
            Flip();
        }

        // 准备攻击
        isAttack = true;
        animator.SetBool("Attacking", true);

        // 延迟切换到攻击状态
        Invoke("StartAttack", 0.5f);
    }

    private bool IsFacingPlayer()
    {
        if (player == null)
            return false;

        bool playerOnLeft = player.position.x < transform.position.x;
        return playerOnLeft == isFacingLeft;
    }

    private void StartAttack()
    {
        if (currentState == EnemyState.ATTACK_READY)
        {
            SwitchState(EnemyState.ATTACK);
        }
    }

    private void UpdateAttackState()
    {
        // 检测环境安全
        if (!groundDetected || wallDetected)
        {
            Flip();
            SwitchState(EnemyState.MOVEMENT);
        }

        // 执行攻击
        if (Time.time >= nextFireTime)
        {
            LaunchFireball();
            nextFireTime = Time.time + fireRate;
        }
    }

    private void UpdateHurtState()
    {
        isHurt = false;
        SwitchState(EnemyState.ATTACK_READY);
    }

    private void UpdateDeadState()
    {
        // 死亡状态已在协程中处理
    }

    // 发射火球
    public void LaunchFireball()
    {
        if (fireballPrefab == null || firePoint == null)
            return;

        GameObject fireball = Instantiate(fireballPrefab, firePoint.position, firePoint.rotation);
        Fireball fireballScript = fireball.GetComponent<Fireball>();

        if (fireballScript != null)
        {
            fireballScript.damage = fireDamage;
            fireballScript.playerLayer = playerLayer;

            Vector2 direction = (player.position - firePoint.position).normalized;
            fireballScript.SetDirection(direction);

            if (audioSource != null && fireSound != null)
                audioSource.PlayOneShot(fireSound);
        }
    }

    // 状态切换方法
    public void SwitchState(EnemyState newState)
    {
        // 退出当前状态
        switch (currentState)
        {
            case EnemyState.IDLE:
                ExitIdleState();
                break;
            case EnemyState.MOVEMENT:
                ExitMovementState();
                break;
            case EnemyState.ATTACK_READY:
                ExitAttackReadyState();
                break;
            case EnemyState.ATTACK:
                ExitAttackState();
                break;
            case EnemyState.HURT:
                ExitHurtState();
                break;
            case EnemyState.DEAD:
                ExitDeadState();
                break;
        }

        // 进入新状态
        switch (newState)
        {
            case EnemyState.IDLE:
                EnterIdleState();
                break;
            case EnemyState.MOVEMENT:
                EnterMovementState();
                break;
            case EnemyState.ATTACK_READY:
                EnterAttackReadyState();
                break;
            case EnemyState.ATTACK:
                EnterAttackState();
                break;
            case EnemyState.HURT:
                EnterHurtState();
                break;
            case EnemyState.DEAD:
                EnterDeadState();
                break;
        }

        currentState = newState;
    }

    // 状态进入/退出方法
    private void EnterIdleState()
    {
        animator.SetBool("Moving", false);
    }
    private void ExitIdleState() { }

    private void EnterMovementState()
    {
        animator.SetBool("Moving", true);
    }
    private void ExitMovementState()
    {
        animator.SetBool("Moving", false);
    }

    private void EnterAttackReadyState()
    {
        isAttack = true;
        animator.SetBool("Attacking", true);
    }
    private void ExitAttackReadyState()
    {
        isAttack = false;
        animator.SetBool("Attacking", false);
    }

    private void EnterAttackState()
    {
        animator.SetTrigger("Attack");
    }
    private void ExitAttackState()
    {
        animator.ResetTrigger("Attack");
    }

    private void EnterHurtState()
    {
        isHurt = true;

        if (audioSource != null && hurtSound != null)
            audioSource.PlayOneShot(hurtSound);

        if (hit != null)
            hit.PlayHitAnimation();

        // 击退效果
        if (player != null && rb != null)
        {
            Vector2 direction = (transform.position - player.position).normalized;
            rb.velocity = direction * hurtForce;
        }
    }
    private void ExitHurtState()
    {
        isHurt = false;
    }

    private void EnterDeadState()
    {
        StartCoroutine(DeadCoroutine());
    }
    private void ExitDeadState() { }

    // 方向翻转
    void Flip()
    {
        Vector3 vector = transform.localScale;
        vector.x *= -1;
        transform.localScale = vector;
    }

    // 伤害处理
    public override void Hurt(int damage, Transform attackPosition)
    {
        base.Hurt(damage, attackPosition);
        if (!isDead)
            SwitchState(EnemyState.HURT);
    }

    // 死亡处理
    protected override void Dead()
    {
        base.Dead();
        SwitchState(EnemyState.DEAD);
    }

    // 死亡协程
    private IEnumerator DeadCoroutine()
    {
        // 0. 安全校验
        if (this == null || gameObject == null)
            yield break;

        // 1. 死亡音效
        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        // 2. 死亡特效
        if (hit != null)
        {
            hit.PlayHitAnimation();
        }

        // 3. 死亡击飞效果
        if (player != null && rb != null)
        {
            Vector3 direction = (transform.position - player.position).normalized;
            // 防止NaN（除以零等情况）
            if (direction.sqrMagnitude > 0.01f)
            {
                Vector2 forceDirection = new Vector2(-Mathf.Sign(direction.x), 0);
                rb.velocity = forceDirection * deadForce;
            }
        }

        // 4. 触发死亡动画
        if (animator != null)
        {
            animator.SetTrigger("Dead");
        }

        // 5. 等待死亡动画播放（确保至少有最短时间）
        float minDeathTime = 1f;
        float elapsed = 0f;

        while (elapsed < minDeathTime)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 6. 禁用碰撞器
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        // 7. 停止物理模拟
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
            rb.simulated = false;
        }

        // 8. 可选：最终销毁
        // yield return new WaitForSeconds(2f);
        // Destroy(gameObject);
    }

    // 玩家检测
    private void OnTriggerStay2D(Collider2D other)
    {
        if (isDead || isHurt || isAttack)
            return;

        if (other != null && other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            SwitchState(EnemyState.ATTACK_READY);
        }
    }

    // Gizmos可视化
    private void OnDrawGizmosSelected()
    {
        // 攻击范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 地面检测线
        if (groundCheck != null)
        {
            Gizmos.color = groundDetected ? Color.green : Color.red;
            Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDistance);
        }

        // 墙壁检测线
        if (wallCheck != null)
        {
            Vector2 direction = isFacingLeft ? Vector2.left : Vector2.right;
            Gizmos.color = wallDetected ? Color.green : Color.red;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + (Vector3)direction * wallCheckDistance);
        }
    }

#pragma warning restore 0649 // 恢复未赋值字段警告
}