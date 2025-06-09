using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieRunner : Enemy
{
    [SerializeField] private BoxCollider2D attackRange;
    [SerializeField] float groundCheckDistance, wallCheckDistance, normalSpeed, madSpeed;
    [SerializeField] private Transform groundCheck, wallCheck;
    [SerializeField] private LayerMask whatIsGrounded;
    [SerializeField] private LayerMask whatIsWall;
    [SerializeField] private EnemyState currentState;
    [SerializeField] float hurtForce = 400f, deadForce = 500f;
    [Header("Audio Clip")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] AudioClip[] zombieChases;
    [SerializeField] AudioClip hited;
    private Transform player;
    private Vector2 movement, endPos;
    private HitEffect hit;
    private bool groundDetected, wallDetected, isAttack, isHurt;

    // 新增防抖变量
    [Header("Flip Anti-Jitter Settings")]
    [SerializeField] private float flipCooldown = 0.4f; // 翻转冷却时间
    private float lastFlipTime;
    private bool isFlipping;
    public enum EnemyState
    {
        IDLE, MOVEMENT, ATTACK_READY, ATTACK, HURT, DEAD
    }


    Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        lastFlipTime = -flipCooldown; // 确保立即可翻转
        // 优化刚体物理设置
        rb.freezeRotation = true; // 防止意外旋转
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 更精确的碰撞检测
        rb.sharedMaterial = new PhysicsMaterial2D()
        {
            friction = 0.1f,
            bounciness = 0.05f
        };

        hit = GetComponentInChildren<HitEffect>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }
    public void SafeFlip()
    {
        // 检查是否允许翻转（冷却时间已过且不在翻转中）
        if (Time.time - lastFlipTime < flipCooldown || isFlipping)
            return;

        StartCoroutine(FlipWithCooldown());
    }
    // === 带冷却时间的翻转协程 ===
    private IEnumerator FlipWithCooldown()
    {
        // 1. 标记翻转中
        isFlipping = true;

        // 2. 执行实际翻转
        Flip();

        // 3. 记录翻转时间
        lastFlipTime = Time.time;

        // 4. 等待冷却时间（但不阻塞游戏）
        yield return new WaitForSeconds(flipCooldown);

        // 5. 标记翻转完成
        isFlipping = false;
    }
    // === 实际翻转操作 ===
    private void DoActualFlip()
    {
        Vector3 vector = transform.localScale;
        vector.x *= -1;
        transform.localScale = vector;

        // 更新方向状态（确保在其他代码中使用）
        isFacingLeft = !isFacingLeft;
    }
    private void Update()
    {
        if (isDead)
            return;
        CheckIsDead();
        Detect();
        UpdateDirection();
        UpdateStatements();
    }

    private void UpdateStatements()
    {
        switch (currentState)
        {
            case EnemyState.IDLE:
                EnterIdleState();
                break;
            case EnemyState.MOVEMENT:
                EnterMovementState();
                break;
            case EnemyState.ATTACK:
                EnterAttackState();
                break;
            case EnemyState.DEAD:
                EnterDeadState();
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

    private void Detect()
    {
        // 使用更可靠的检测方法
        RaycastHit2D groundHit = Physics2D.Raycast(groundCheck.position, Vector2.down,
                                                 groundCheckDistance, whatIsGrounded);
        groundDetected = groundHit.collider != null;

        // 检查前方是否有墙
        Vector2 direction = isFacingLeft ? Vector2.left : Vector2.right;
        RaycastHit2D wallHit = Physics2D.Raycast(wallCheck.position, direction,
                                              wallCheckDistance, whatIsWall);
        wallDetected = wallHit.collider != null;

        // 添加调试可视化
        Debug.DrawRay(groundCheck.position, Vector2.down * groundCheckDistance, groundDetected ? Color.green : Color.red);
        Debug.DrawRay(wallCheck.position, direction * wallCheckDistance, wallDetected ? Color.green : Color.red);
    }

    private void UpdateAnimatorStatement()
    {
        animator.SetBool("Attack", isAttack);
    }

    private void EnterIdleState()
    {

    }
    private void ExitIdleState()
    {

    }

    private void EnterMovementState()
    {
        // 检查是否需要翻转
        if (!groundDetected || wallDetected)
        {
            Debug.Log("翻转");
            SafeFlip();

            // 更新翻转后的检测方向
            Vector2 direction = isFacingLeft ? Vector2.left : Vector2.right;
            wallDetected = Physics2D.Raycast(wallCheck.position, direction,
                                          wallCheckDistance, whatIsWall);

            // 添加额外安全检查
            if (!groundDetected || wallDetected)
            {
                // 尝试第二次翻转
                SafeFlip();
            }
        }

        // 应用移动速度
        float moveDirection = isFacingLeft ? -1 : 1;

        // 添加轻微随机移动，防止卡在某个位置
        float slightRandom = Mathf.PerlinNoise(Time.time * 0.1f, 0) * 0.2f - 0.1f;
        movement.Set((moveDirection + slightRandom) * normalSpeed, rb.velocity.y);
        rb.velocity = movement;

        // 防止被微小颠簸卡住
        if (Mathf.Abs(rb.velocity.x) < 0.1f)
        {
            rb.AddForce(new Vector2(moveDirection * 5f, 0));
        }
    }
    private void ExitMovementState()
    {

    }

    private void EnterAttackReadyState()
    {
        rb.velocity = Vector2.zero;
        isAttack = true;
        animator.SetTrigger("AttackReady");
        animator.SetBool("Attack", isAttack);
        if (player.position.x - transform.position.x > 0 && isFacingLeft)
        {
            SafeFlip();
        }
        else if (player.position.x - transform.position.x < 0 && !isFacingLeft)
        {
            SafeFlip();
        }
    }

    private void ExitAttackReadyState()
    {

    }

    private void EnterAttackState()
    {
        if (!groundDetected || wallDetected)
        {
            // Flip
            SafeFlip();
            SwitchState(EnemyState.MOVEMENT);
        }
        else
        {
            animator.SetBool("Attack", isAttack);
            // Move
            movement.Set((isFacingLeft ? -1 : 1) * madSpeed, rb.velocity.y);
            rb.velocity = movement;
        }
    }
    private void ExitAttackState()
    {
        isAttack = false;
        animator.SetBool("Attack", isAttack);
    }
    private void EnterHurtState()
    {
        isHurt = true;
        isAttack = false;
        animator.SetBool("Attack", isAttack);
        audioSource.PlayOneShot(hited);
        hit.PlayHitAnimation();
        // 判断角色位置
        Vector2 vector = transform.position - player.position;
        rb.velocity = Vector2.zero;
        if (vector.x > 0)
        {
            rb.AddForce(new Vector2(hurtForce, 0));
        }
        else
        {
            rb.AddForce(new Vector2(-hurtForce, 0));
        }
        SwitchState(EnemyState.ATTACK_READY);
    }
    private void ExitHurtState()
    {
        isHurt = false;
    }

    private void EnterDeadState()
    {
        StartCoroutine(DelayDead());
    }
    private void ExitDeadState()
    {

    }

    void Flip()
    {
        Vector3 vector = transform.localScale;
        vector.x *= -1;
        transform.localScale = vector;

        // 更新方向状态（确保在其他代码中使用）
        isFacingLeft = !isFacingLeft;
    }

    public void SwitchState(EnemyState state)
    {
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

        switch (state)
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

        currentState = state;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawLine(groundCheck.position, new Vector2(groundCheck.position.x, groundCheck.position.y - groundCheckDistance));
        Gizmos.DrawLine(wallCheck.position, new Vector2(wallCheck.position.x - wallCheckDistance, wallCheck.position.y));
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (isDead)
            return;
        if (collision.gameObject.layer == LayerMask.NameToLayer("Hero Detector") && !isAttack && !isHurt)
        {
            float movement = attackRange.bounds.size.x / 2;
            if (player.position.x > 0)
            {
                endPos.Set(transform.position.x + movement, transform.position.y);
            }
            else
            {
                endPos.Set(transform.position.x - movement, transform.position.y);
            }
            SwitchState(EnemyState.ATTACK_READY);
        }
    }

    public override void Hurt(int damage, Transform attackPosition)
    {
        base.Hurt(damage, attackPosition);
        CheckIsDead();
        if (!isDead)
            SwitchState(EnemyState.HURT);
    }
    protected override void Dead()
    {
        base.Dead();
        SwitchState(EnemyState.DEAD);
    }

    IEnumerator DelayDead()
    {
        hit.PlayHitAnimation();
        Vector3 diff = (player.position - transform.position).normalized;
        rb.velocity = Vector2.zero;
        if (diff.x > 0)
        {
            rb.AddForce(Vector2.left * deadForce);
        }
        else if (diff.x < 0)
        {
            rb.AddForce(Vector2.right * deadForce);
        }
        animator.SetTrigger("Dead");
        yield return new WaitForSeconds(1f);
        GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        GetComponent<Collider2D>().enabled = false;
    }

    public void PlayOneShot(AudioClip audioClip)
    {
        audioSource.PlayOneShot(audioClip);
    }

    public void PlayZombieChase()
    {
        int count = zombieChases.Length;
        int r = UnityEngine.Random.Range(0, count);
        audioSource.PlayOneShot(zombieChases[r]);
    }
}
