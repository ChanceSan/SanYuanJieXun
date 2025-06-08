using UnityEngine;
using System.Collections;

public class DragonBehavior : Enemy // �̳���Enemy����
{
    // ע�⣺�ӻ���̳е��ֶ�����Enemy�ж��壬���ﲻ��Ҫ�ظ�����
#pragma warning disable 0649 // ����δ��ֵ�ֶξ���

    [Header("Movement Settings")]
    public float groundCheckDistance = 0.5f;
    public float wallCheckDistance = 0.3f;
    public float normalSpeed = 2f;
    public float madSpeed = 5f;
    [SerializeField] private Transform groundCheck; // ���л���Transform
    [SerializeField] private Transform wallCheck; // ���л���Transform
    public LayerMask whatIsGrounded;
    public LayerMask whatIsWall;

    [Header("Attack Settings")]
    public GameObject fireballPrefab;
    [SerializeField] private Transform firePoint; // ���л���Transform
    public float fireRate = 3f;
    public int fireDamage = 25;
    public LayerMask playerLayer;
    public float attackRange = 5f;

    [Header("Combat Settings")]
    public float hurtForce = 400f;
    public float deadForce = 500f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource; // ���л���AudioSource
    [SerializeField] private AudioClip fireSound; // ���л���AudioClip
    [SerializeField] private AudioClip hurtSound; // ���л���AudioClip
    [SerializeField] private AudioClip deathSound; // ���л���AudioClip

    // ״̬ϵͳ
    public enum EnemyState
    {
        IDLE, MOVEMENT, ATTACK_READY, ATTACK, HURT, DEAD
    }
    [SerializeField] private EnemyState currentState;

    // �Ƴ���private Animator animator; ��Ϊ����ֶ��ڻ������Ѷ���

    private float nextFireTime;
    private bool groundDetected, wallDetected;
    private bool isAttack, isHurt;
    private Rigidbody2D rb;
    private Transform player;
    private Vector2 movement;
    private HitEffect hit;

    void Start()
    {
        // �����е�animator�ֶ�Ӧ���ѳ�ʼ��
        // �������û�г�ʼ�������������ﰲȫ��ʼ��
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // ���һ򴴽�HitEffect
        hit = GetComponentInChildren<HitEffect>(true);
        if (hit == null)
        {
            Debug.LogWarning("No HitEffect found. Creating default one.");
            GameObject hitObj = new GameObject("DefaultHitEffect");
            hitObj.transform.SetParent(transform);
            hit = hitObj.AddComponent<HitEffect>();
        }

        // ȷ����Ҫ�ļ������
        groundCheck = GetOrCreateTransform(groundCheck, "GroundCheck", new Vector2(0, -0.5f));
        wallCheck = GetOrCreateTransform(wallCheck, "WallCheck", new Vector2(isFacingLeft ? -0.5f : 0.5f, 0));
        firePoint = GetOrCreateTransform(firePoint, "FirePoint", new Vector2(isFacingLeft ? -0.8f : 0.8f, 0.2f));

        currentState = EnemyState.IDLE;
    }

    // ������������ȡ�򴴽������Transform
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

        // ������
        RaycastHit2D groundHit = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            groundCheckDistance,
            whatIsGrounded
        );
        groundDetected = groundHit.collider != null;

        // ǽ�ڼ��
        Vector2 direction = isFacingLeft ? Vector2.left : Vector2.right;
        RaycastHit2D wallHit = Physics2D.Raycast(
            wallCheck.position,
            direction,
            wallCheckDistance,
            whatIsWall
        );
        wallDetected = wallHit.collider != null;

        // ���Կ��ӻ�
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
        // �����ʼ�ƶ�
        if (Random.value < 0.5f)
        {
            SwitchState(EnemyState.MOVEMENT);
        }
    }

    private void UpdateMovementState()
    {
        // ����Ƿ���Ҫ��ת����
        if (!groundDetected || wallDetected)
        {
            Flip();
        }

        // �������Ƿ��ڹ�����Χ��
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
        // ȷ���������
        if (!IsFacingPlayer())
        {
            Flip();
        }

        // ׼������
        isAttack = true;
        animator.SetBool("Attacking", true);

        // �ӳ��л�������״̬
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
        // ��⻷����ȫ
        if (!groundDetected || wallDetected)
        {
            Flip();
            SwitchState(EnemyState.MOVEMENT);
        }

        // ִ�й���
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
        // ����״̬����Э���д���
    }

    // �������
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

    // ״̬�л�����
    public void SwitchState(EnemyState newState)
    {
        // �˳���ǰ״̬
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

        // ������״̬
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

    // ״̬����/�˳�����
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

        // ����Ч��
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

    // ����ת
    void Flip()
    {
        Vector3 vector = transform.localScale;
        vector.x *= -1;
        transform.localScale = vector;
    }

    // �˺�����
    public override void Hurt(int damage, Transform attackPosition)
    {
        base.Hurt(damage, attackPosition);
        if (!isDead)
            SwitchState(EnemyState.HURT);
    }

    // ��������
    protected override void Dead()
    {
        base.Dead();
        SwitchState(EnemyState.DEAD);
    }

    // ����Э��
    private IEnumerator DeadCoroutine()
    {
        // 0. ��ȫУ��
        if (this == null || gameObject == null)
            yield break;

        // 1. ������Ч
        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        // 2. ������Ч
        if (hit != null)
        {
            hit.PlayHitAnimation();
        }

        // 3. ��������Ч��
        if (player != null && rb != null)
        {
            Vector3 direction = (transform.position - player.position).normalized;
            // ��ֹNaN��������������
            if (direction.sqrMagnitude > 0.01f)
            {
                Vector2 forceDirection = new Vector2(-Mathf.Sign(direction.x), 0);
                rb.velocity = forceDirection * deadForce;
            }
        }

        // 4. ������������
        if (animator != null)
        {
            animator.SetTrigger("Dead");
        }

        // 5. �ȴ������������ţ�ȷ�����������ʱ�䣩
        float minDeathTime = 1f;
        float elapsed = 0f;

        while (elapsed < minDeathTime)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 6. ������ײ��
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        // 7. ֹͣ����ģ��
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
            rb.simulated = false;
        }

        // 8. ��ѡ����������
        // yield return new WaitForSeconds(2f);
        // Destroy(gameObject);
    }

    // ��Ҽ��
    private void OnTriggerStay2D(Collider2D other)
    {
        if (isDead || isHurt || isAttack)
            return;

        if (other != null && other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            SwitchState(EnemyState.ATTACK_READY);
        }
    }

    // Gizmos���ӻ�
    private void OnDrawGizmosSelected()
    {
        // ������Χ
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // ��������
        if (groundCheck != null)
        {
            Gizmos.color = groundDetected ? Color.green : Color.red;
            Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDistance);
        }

        // ǽ�ڼ����
        if (wallCheck != null)
        {
            Vector2 direction = isFacingLeft ? Vector2.left : Vector2.right;
            Gizmos.color = wallDetected ? Color.green : Color.red;
            Gizmos.DrawLine(wallCheck.position, wallCheck.position + (Vector3)direction * wallCheckDistance);
        }
    }

#pragma warning restore 0649 // �ָ�δ��ֵ�ֶξ���
}