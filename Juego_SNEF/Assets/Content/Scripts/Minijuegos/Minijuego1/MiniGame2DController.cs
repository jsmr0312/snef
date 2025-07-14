using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class MiniGame2DController : MonoBehaviour
    {
        [Header("Movimiento")]
        [Tooltip("Velocidad de desplazamiento en m/s")]
        public float MoveSpeed = 4f;
        [Tooltip("Velocidad de sprint en m/s")]
        public float SprintSpeed = 6f;

        [Header("Jump & Fall Timeouts & Height")]
        [Tooltip("Altura del salto")]
        public float JumpHeight = 1.2f;
        [Tooltip("Tiempo mínimo antes de poder volver a saltar (en segundos)")]
        public float JumpTimeout = 0.3f;
        [Tooltip("Tiempo que tarda en entrar al estado de caída tras despegar (en segundos)")]
        public float FallTimeout = 0.15f;

        [Header("Ground Check")]
        [Tooltip("Offset vertical para el chequeo de suelo")]
        public float GroundedOffset = -0.14f;
        [Tooltip("Radio de la esfera de chequeo de suelo")]
        public float GroundedRadius = 0.28f;
        [Tooltip("Capas consideradas como suelo")]
        public LayerMask GroundLayers;

        [Header("Animaciones & Audio")]
        [Tooltip("Valor de la gravedad aplicada (negativo)")]
        public float Gravity = -15f;
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        // Internos
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private Animator _animator;
        private bool _hasAnimator;
        private bool _grounded = true;
        private float _verticalVelocity;
        private float _terminalVelocity = 53f;

        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        private int _animIDSpeed, _animIDGrounded, _animIDJump, _animIDFreeFall, _animIDMotionSpeed;
        private float _speed, _animationBlend;
        private float _speedChangeRate = 10f;

        void Start()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            _hasAnimator = TryGetComponent(out _animator);

            AssignAnimationIDs();

            // Inicializa los timeouts desde los parámetros públicos
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        void Update()
        {
            GroundedCheck();
            JumpAndGravity();
            Move();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            Vector3 spherePos = transform.position + Vector3.up * GroundedOffset;
            _grounded = Physics.CheckSphere(spherePos, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
            if (_hasAnimator)
                _animator.SetBool(_animIDGrounded, _grounded);
        }

        private void JumpAndGravity()
        {
            if (_grounded)
            {
                // Reinicia cuenta de caída
                _fallTimeoutDelta = FallTimeout;

                // Actualiza animaciones: reset Jump y FreeFall
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // Mantener pegado al suelo
                if (_verticalVelocity < 0f)
                    _verticalVelocity = -2f;

                // Saltar
                if (_input.jump && _jumpTimeoutDelta <= 0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    if (_hasAnimator)
                        _animator.SetBool(_animIDJump, true);
                }

                // Cuenta regresiva para próximo salto
                if (_jumpTimeoutDelta >= 0f)
                    _jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                // Reinicia tiempo de salto tras despegar
                _jumpTimeoutDelta = JumpTimeout;

                // Cuenta regresiva para animación de caída
                if (_fallTimeoutDelta >= 0f)
                    _fallTimeoutDelta -= Time.deltaTime;
                else if (_hasAnimator)
                    _animator.SetBool(_animIDFreeFall, true);

                // Evitar multi-saltos
                _input.jump = false;
            }

            // Aplica gravedad
            if (_verticalVelocity < _terminalVelocity)
                _verticalVelocity += Gravity * Time.deltaTime;
        }

        private void Move()
        {
            // Input solo eje X
            Vector2 mv = _input.move;
            mv.y = 0f;

            // Velocidad objetivo con sprint
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            if (Mathf.Approximately(mv.x, 0f)) targetSpeed = 0f;

            // Aceleración / desaceleración
            float currentSpeed = Mathf.Abs(_controller.velocity.x);
            float speedOffset = 0.1f;
            float inputMag = _input.analogMovement ? Mathf.Abs(mv.x) : (Mathf.Abs(mv.x) > 0f ? 1f : 0f);

            if (currentSpeed < targetSpeed - speedOffset || currentSpeed > targetSpeed + speedOffset)
                _speed = Mathf.Lerp(currentSpeed, targetSpeed * inputMag, Time.deltaTime * _speedChangeRate);
            else
                _speed = targetSpeed;

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * _speedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // Giro según dirección
            if (mv.x > 0.01f) transform.forward = Vector3.right;
            else if (mv.x < -0.01f) transform.forward = Vector3.left;

            // Movimiento final + gravedad
            Vector3 dir = new Vector3(mv.x, 0f, 0f).normalized;
            _controller.Move(dir * (_speed * Time.deltaTime) + Vector3.up * (_verticalVelocity * Time.deltaTime));

            // Actualiza animaciones
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMag);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _grounded ? Color.green * 0.35f : Color.red * 0.35f;
            Vector3 spherePos = transform.position + Vector3.up * GroundedOffset;
            Gizmos.DrawSphere(spherePos, GroundedRadius);
        }

        private void OnFootstep(AnimationEvent e)
        {
            if (_controller == null || FootstepAudioClips == null || FootstepAudioClips.Length == 0) return;
            if (e.animatorClipInfo.weight > 0.5f)
                AudioSource.PlayClipAtPoint(FootstepAudioClips[Random.Range(0, FootstepAudioClips.Length)],
                    transform.TransformPoint(_controller.center), FootstepAudioVolume);
        }

        private void OnLand(AnimationEvent e)
        {
            if (_controller == null || LandingAudioClip == null) return;
            if (e.animatorClipInfo.weight > 0.5f)
                AudioSource.PlayClipAtPoint(LandingAudioClip,
                    transform.TransformPoint(_controller.center), FootstepAudioVolume);
        }
    }
}
