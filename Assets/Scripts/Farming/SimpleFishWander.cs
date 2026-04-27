using UnityEngine;

namespace OrcFarm.Farming
{
    /// <summary>
    /// Lightweight pond wandering for a small number of decorative fish.
    /// Attach one instance to each fish and set the same pond centre/radius.
    /// </summary>
    public sealed class SimpleFishWander : MonoBehaviour
    {
        [Header("Pond Bounds")]
        [Tooltip("Optional pond centre. If empty, the fish uses its starting position as the pond centre.")]
        [SerializeField] private Transform _pondCentre;

        [Tooltip("Horizontal pond radius in metres on the XZ plane.")]
        [Min(0.1f)]
        [SerializeField] private float _pondRadius = 2f;

        [Tooltip("Keep random targets this far inside the pond edge.")]
        [Min(0f)]
        [SerializeField] private float _edgePadding = 0.25f;

        [Tooltip("If enabled, the fish keeps its starting Y position.")]
        [SerializeField] private bool _lockHeight = true;

        [Header("Movement")]
        [SerializeField] private Vector2 _speedRange = new Vector2(0.35f, 0.8f);
        [Min(0.1f)]
        [SerializeField] private float _turnSpeed = 3f;
        [Min(0.01f)]
        [SerializeField] private float _arrivalDistance = 0.15f;
        [SerializeField] private Vector2 _targetIntervalRange = new Vector2(1.5f, 4f);

        [Header("Pauses")]
        [Range(0f, 1f)]
        [SerializeField] private float _pauseChance = 0.2f;
        [SerializeField] private Vector2 _pauseDurationRange = new Vector2(0.4f, 1.4f);

        [Header("Organic Motion")]
        [Tooltip("Small side-to-side drift while swimming.")]
        [Min(0f)]
        [SerializeField] private float _wobbleStrength = 0.08f;
        [Min(0f)]
        [SerializeField] private float _wobbleFrequency = 2.5f;

        private Vector3 _target;
        private Vector3 _home;
        private float _height;
        private float _currentSpeed;
        private float _targetTimer;
        private float _pauseTimer;
        private float _wobblePhase;

        private void Awake()
        {
            _home = _pondCentre != null ? _pondCentre.position : transform.position;
            _height = transform.position.y;
            _wobblePhase = Random.Range(0f, Mathf.PI * 2f);

            PickNewTarget();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (_pondCentre != null)
                _home = _pondCentre.position;

            if (_pauseTimer > 0f)
            {
                _pauseTimer -= deltaTime;
                return;
            }

            _targetTimer -= deltaTime;

            Vector3 position = transform.position;
            Vector3 toTarget = _target - position;
            toTarget.y = 0f;

            if (_targetTimer <= 0f || toTarget.sqrMagnitude <= _arrivalDistance * _arrivalDistance)
                PickNewTarget();

            KeepTargetInsidePond(position);
            Swim(deltaTime);
        }

        private void Swim(float deltaTime)
        {
            Vector3 position = transform.position;
            Vector3 desired = _target - position;
            desired.y = 0f;

            if (desired.sqrMagnitude <= 0.0001f)
                return;

            Vector3 direction = desired.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _turnSpeed * deltaTime);

            Vector3 forward = transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            float wobble = Mathf.Sin((Time.time + _wobblePhase) * _wobbleFrequency) * _wobbleStrength;
            Vector3 movement = (forward * _currentSpeed + side * wobble) * deltaTime;

            Vector3 nextPosition = position + movement;
            if (_lockHeight)
                nextPosition.y = _height;

            transform.position = ClampToPond(nextPosition);
        }

        private void PickNewTarget()
        {
            float usableRadius = Mathf.Max(0.05f, _pondRadius - _edgePadding);
            Vector2 randomPoint = Random.insideUnitCircle * usableRadius;

            _target = _home + new Vector3(randomPoint.x, 0f, randomPoint.y);
            if (_lockHeight)
                _target.y = _height;

            _currentSpeed = Random.Range(_speedRange.x, _speedRange.y);
            _targetTimer = Random.Range(_targetIntervalRange.x, _targetIntervalRange.y);

            if (Random.value < _pauseChance)
                _pauseTimer = Random.Range(_pauseDurationRange.x, _pauseDurationRange.y);
        }

        private void KeepTargetInsidePond(Vector3 position)
        {
            Vector3 fromHome = position - _home;
            fromHome.y = 0f;

            float edgeStart = Mathf.Max(0.05f, _pondRadius - _edgePadding);
            if (fromHome.sqrMagnitude < edgeStart * edgeStart)
                return;

            Vector3 towardCentre = _home - position;
            towardCentre.y = 0f;
            _target = position + towardCentre.normalized * edgeStart;
            if (_lockHeight)
                _target.y = _height;
        }

        private Vector3 ClampToPond(Vector3 position)
        {
            Vector3 offset = position - _home;
            offset.y = 0f;

            if (offset.sqrMagnitude <= _pondRadius * _pondRadius)
                return position;

            Vector3 clampedOffset = offset.normalized * _pondRadius;
            return new Vector3(_home.x + clampedOffset.x, position.y, _home.z + clampedOffset.z);
        }

        private void OnValidate()
        {
            _pondRadius = Mathf.Max(0.1f, _pondRadius);
            _edgePadding = Mathf.Max(0f, Mathf.Min(_edgePadding, _pondRadius - 0.05f));
            _turnSpeed = Mathf.Max(0.1f, _turnSpeed);
            _arrivalDistance = Mathf.Max(0.01f, _arrivalDistance);

            _speedRange.x = Mathf.Max(0f, _speedRange.x);
            _speedRange.y = Mathf.Max(_speedRange.x, _speedRange.y);
            _targetIntervalRange.x = Mathf.Max(0.1f, _targetIntervalRange.x);
            _targetIntervalRange.y = Mathf.Max(_targetIntervalRange.x, _targetIntervalRange.y);
            _pauseDurationRange.x = Mathf.Max(0f, _pauseDurationRange.x);
            _pauseDurationRange.y = Mathf.Max(_pauseDurationRange.x, _pauseDurationRange.y);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 centre = _pondCentre != null ? _pondCentre.position : transform.position;
            Gizmos.color = new Color(0.1f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireSphere(centre, _pondRadius);
        }
    }
}
