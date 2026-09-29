using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 이동 (Unity 6 New Input System)
/// [v6 fix] Rigidbody2D.linearVelocity → velocity 대응 (#if 분기)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] float moveSpeed = 5f;
    Rigidbody2D _rb; SpriteRenderer _sr; Animator _anim; Vector2 _moveInput;

    static readonly int H_MX = Animator.StringToHash("MoveX"),
                        H_MY = Animator.StringToHash("MoveY"),
                        H_SP = Animator.StringToHash("Speed"),
                        H_IM = Animator.StringToHash("IsMoving");

    public enum FacingDir { Down, Up, Left, Right }
    public FacingDir CurrentDir { get; private set; } = FacingDir.Down;
    public Vector2 MoveInput => _moveInput;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();
        _anim = GetComponent<Animator>();
        _rb.gravityScale = 0f;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        var kb = Keyboard.current; if (kb == null) return;
        float h = 0f, v = 0f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h =  1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h = -1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v =  1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v = -1f;
        _moveInput = new Vector2(h, v).normalized;

        if (_moveInput.magnitude > 0.1f)
        {
            float ax = Mathf.Abs(_moveInput.x), ay = Mathf.Abs(_moveInput.y);
            if (ax >= ay) CurrentDir = _moveInput.x > 0f ? FacingDir.Right : FacingDir.Left;
            else          CurrentDir = _moveInput.y > 0f ? FacingDir.Up    : FacingDir.Down;
        }

        if (_anim != null)
        {
            _anim.SetFloat(H_MX, _moveInput.x);
            _anim.SetFloat(H_MY, _moveInput.y);
            _anim.SetFloat(H_SP, _moveInput.magnitude);
            _anim.SetBool(H_IM,  _moveInput.magnitude > 0.1f);
        }
        else if (_sr != null && Mathf.Abs(_moveInput.x) > 0.1f)
            _sr.flipX = _moveInput.x < 0f;
    }

    void FixedUpdate()
    {
        // Unity 6000.5+ : velocity 로 통합 (linearVelocity 동일 동작)
        _rb.linearVelocity = _moveInput * moveSpeed;
    }

    public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
}
