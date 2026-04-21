using UnityEngine;

namespace SpinStay
{
    public class TightropeWalker : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float forwardSpeed = 1.5f;

        [Header("Debug")]
        [Tooltip("Freeze the walker's balance/fall logic entirely. Keeps tilt at 0 and ignores roulette recovery / drift / bird impulses. Use when focusing on roulette tuning.")]
        [SerializeField] private bool disableBalance = false;

        [Header("Balance — limits")]
        [Tooltip("Max absolute tilt angle (degrees) before the walker starts the grace period.")]
        [SerializeField] private float fallAngle = 60f;
        [Tooltip("Seconds the walker can stay at the fall limit before it actually falls. Give the player a chance to recover.")]
        [SerializeField, Min(0f)] private float graceAtLimit = 0.9f;
        [Tooltip("While teetering, the walker stops teetering once tilt drops below this fraction of the fall angle (0..1).")]
        [SerializeField, Range(0.1f, 0.99f)] private float limitRecoveryThreshold = 0.75f;

        [Header("Balance — lean")]
        [Tooltip("Constant tilt rate (°/s). Walker always leans at this speed in the direction of the current tilt sign.")]
        [SerializeField, Min(0f)] private float leanSpeed = 12f;
        [Tooltip("If |tilt| falls at or below this, the walker picks a random side so it never stays balanced at zero.")]
        [SerializeField, Min(0f)] private float zeroTiltThreshold = 0.25f;

        [Header("Recovery response")]
        [Tooltip("Seconds the walker takes to reach the target tilt when a roulette option is picked. Shorter = snappier save, longer = more dramatic animated lean.")]
        [SerializeField, Min(0.05f)] private float recoveryDuration = 0.45f;
        [Tooltip("If true, during recovery the walker's normal physics (pendulum tipping, drift, noise) are suppressed for a clean, readable animation. If false, other forces still act and may fight the recovery.")]
        [SerializeField] private bool suppressPhysicsDuringRecovery = true;

        [Header("Shake at limit")]
        [Tooltip("Fraction of the fall angle at which the walker visual starts shaking. 0.6 = begin shaking at 60% of the limit.")]
        [SerializeField, Range(0f, 0.99f)] private float shakeThreshold = 0.6f;
        [Tooltip("Peak shake amplitude (world units) when tilt is at the fall limit.")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.08f;
        [Tooltip("Shake frequency (Hz).")]
        [SerializeField, Min(0f)] private float shakeFrequency = 18f;

        [Header("Fall sequence")]
        [Tooltip("World Y the walker drops to when falling — the water surface.")]
        [SerializeField] private float fallWaterY = -8f;
        [Tooltip("Total duration of the fall animation (tilt + drop).")]
        [SerializeField, Min(0.1f)] private float fallAnimationDuration = 1.4f;

        [Header("Visual")]
        [SerializeField] private Transform tiltRoot;

        public float TiltAngle { get; private set; }
        public float AngularVelocity { get; private set; }
        public bool IsFallen { get; private set; }
        public float DistanceTravelled { get; private set; }
        public float FallAngle => fallAngle;
        public float NormalizedTilt => Mathf.Clamp(TiltAngle / fallAngle, -1f, 1f);
        public bool IsTeetering { get; private set; }
        public float GraceRemaining { get; private set; }
        public float RecoveryDuration => recoveryDuration;
        public bool IsRecovering => recoveryActive;
        public bool IsFallAnimating => fallAnimActive;
        public bool FallAnimationComplete => IsFallen && !fallAnimActive;

        public System.Action OnFell;

        // --- runtime state ---
        int leanSign = 1;

        // Recovery pull state (roulette picks).
        bool  recoveryActive;
        float recoveryTargetTilt;
        float recoveryStartTilt;
        float recoveryStartAngVel;
        float recoveryElapsed;

        float graceTimer;

        // Fall animation state.
        bool   fallAnimActive;
        float  fallAnimTimer;
        float  fallStartTilt;
        int    fallDirSign;
        Vector3 fallStartPos;

        void Reset() { tiltRoot = transform; }

        void Awake()
        {
            if (tiltRoot == null) tiltRoot = transform;
            leanSign = Random.value < 0.5f ? -1 : 1;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (disableBalance)
            {
                TiltAngle = 0f;
                AngularVelocity = 0f;
                IsTeetering = false;
                GraceRemaining = 0f;
                recoveryActive = false;
                fallAnimActive = false;
                IsFallen = false;
                if (tiltRoot != null)
                {
                    tiltRoot.localRotation = Quaternion.identity;
                    tiltRoot.localPosition = Vector3.zero;
                }
                transform.position += transform.forward * forwardSpeed * dt;
                DistanceTravelled += forwardSpeed * dt;
                return;
            }

            // --- Fall sequence: tilt to 90° in the fall direction, drop body to water. ---
            if (fallAnimActive)
            {
                fallAnimTimer += dt;
                float tk = Mathf.Clamp01(fallAnimTimer / Mathf.Max(0.0001f, fallAnimationDuration * 0.3f));
                float dk = Mathf.Clamp01((fallAnimTimer - fallAnimationDuration * 0.2f) / Mathf.Max(0.0001f, fallAnimationDuration * 0.8f));

                float tilt = Mathf.Lerp(fallStartTilt, fallDirSign * 90f, Mathf.SmoothStep(0f, 1f, tk));
                tiltRoot.localRotation = Quaternion.Euler(0f, 0f, tilt);

                float y = Mathf.Lerp(fallStartPos.y, fallWaterY, Mathf.SmoothStep(0f, 1f, dk));
                // Sideways nudge so the body arcs as it falls.
                float xOffset = Mathf.Lerp(0f, fallDirSign * 0.6f, Mathf.SmoothStep(0f, 1f, dk));
                transform.position = new Vector3(fallStartPos.x + xOffset, y, fallStartPos.z);

                if (fallAnimTimer >= fallAnimationDuration)
                {
                    fallAnimActive = false;
                }
                return;
            }

            if (IsFallen) return;

            // --- 0. If a recovery pull is active, drive the tilt directly toward the target
            //       using smooth-damp easing, optionally suppressing other physics for a clean arc. ---
            if (recoveryActive)
            {
                recoveryElapsed += dt;
                float k = Mathf.Clamp01(recoveryElapsed / recoveryDuration);
                // Ease-out cubic: fast start, settles at the end — gives the roulette pull a punchy, immediate feel.
                float inv = 1f - k;
                float eased = 1f - inv * inv * inv;
                TiltAngle = Mathf.Lerp(recoveryStartTilt, recoveryTargetTilt, eased);
                float dEased = 3f * inv * inv;
                AngularVelocity = (recoveryTargetTilt - recoveryStartTilt) * dEased / Mathf.Max(0.0001f, recoveryDuration);

                // Fall-limit check even during recovery: repeated same-side picks push
                // the walker past the limit, which must be able to trigger teeter/fall.
                if (Mathf.Abs(TiltAngle) >= fallAngle)
                {
                    TiltAngle = Mathf.Sign(TiltAngle) * fallAngle;
                    if (!IsTeetering)
                    {
                        IsTeetering = true;
                        graceTimer = 0f;
                    }
                    graceTimer += dt;
                    GraceRemaining = Mathf.Max(0f, graceAtLimit - graceTimer);
                    if (graceTimer >= graceAtLimit)
                    {
                        StartFall();
                        return;
                    }
                }
                else if (IsTeetering && Mathf.Abs(TiltAngle) < fallAngle * limitRecoveryThreshold)
                {
                    IsTeetering = false;
                    graceTimer = 0f;
                    GraceRemaining = 0f;
                }

                ApplyVisualTiltAndShake();
                transform.position += transform.forward * forwardSpeed * dt;
                DistanceTravelled += forwardSpeed * dt;

                if (k >= 1f)
                {
                    recoveryActive = false;
                    TiltAngle = Mathf.Clamp(recoveryTargetTilt, -fallAngle, fallAngle);
                    AngularVelocity = 0f;
                }
                if (suppressPhysicsDuringRecovery) return;
            }

            // --- Constant lean: pick side from current tilt sign; re-roll when at (or near) zero. ---
            if (Mathf.Abs(TiltAngle) <= zeroTiltThreshold)
                leanSign = Random.value < 0.5f ? -1 : 1;
            else
                leanSign = TiltAngle > 0f ? 1 : -1;

            AngularVelocity = leanSign * leanSpeed;
            TiltAngle += AngularVelocity * dt;

            if (Mathf.Abs(TiltAngle) >= fallAngle)
            {
                TiltAngle = Mathf.Sign(TiltAngle) * fallAngle;

                if (!IsTeetering)
                {
                    IsTeetering = true;
                    graceTimer = 0f;
                }
                graceTimer += dt;
                GraceRemaining = Mathf.Max(0f, graceAtLimit - graceTimer);
                if (graceTimer >= graceAtLimit)
                {
                    StartFall();
                    return;
                }
            }
            else if (IsTeetering && Mathf.Abs(TiltAngle) < fallAngle * limitRecoveryThreshold)
            {
                IsTeetering = false;
                graceTimer = 0f;
                GraceRemaining = 0f;
            }

            ApplyVisualTiltAndShake();
            transform.position += transform.forward * forwardSpeed * dt;
            DistanceTravelled += forwardSpeed * dt;
        }

        void ApplyVisualTiltAndShake()
        {
            tiltRoot.localRotation = Quaternion.Euler(0f, 0f, TiltAngle);

            float absN = Mathf.Abs(NormalizedTilt);
            float intensity = Mathf.InverseLerp(shakeThreshold, 1f, absN);
            if (intensity > 0f && shakeAmplitude > 0f)
            {
                float t = Time.time;
                float sx = Mathf.Sin(t * Mathf.PI * 2f * shakeFrequency) * shakeAmplitude * intensity;
                float sy = Mathf.Cos(t * Mathf.PI * 2f * shakeFrequency * 1.3f) * shakeAmplitude * 0.5f * intensity;
                tiltRoot.localPosition = new Vector3(sx, sy, 0f);
            }
            else
            {
                tiltRoot.localPosition = Vector3.zero;
            }
        }

        void StartFall()
        {
            if (fallAnimActive || IsFallen) return;
            IsFallen = true;
            AngularVelocity = 0f;
            recoveryActive = false;

            fallAnimActive = true;
            fallAnimTimer = 0f;
            fallStartTilt = TiltAngle;
            fallDirSign = TiltAngle >= 0f ? 1 : -1;
            fallStartPos = transform.position;

            OnFell?.Invoke();
        }

        /// <summary>Force an immediate fall (used by the roulette's FALL option).</summary>
        public void ForceFall()
        {
            if (IsFallen || disableBalance) return;
            if (Mathf.Abs(TiltAngle) < 1f) TiltAngle = (Random.value < 0.5f ? -1f : 1f) * fallAngle * 0.5f;
            TiltAngle = Mathf.Sign(TiltAngle) * fallAngle;
            StartFall();
        }

        /// <summary>
        /// Animated recovery: pulls the walker's tilt toward <paramref name="targetTilt"/> over
        /// <see cref="recoveryDuration"/> seconds with ease-in-out. Use for roulette picks —
        /// choosing LEFT (target &lt; 0) while the walker is falling right will visibly recover.
        /// </summary>
        public void PullToTargetTilt(float targetTilt)
        {
            if (IsFallen || disableBalance) return;
            recoveryActive = true;
            // Allow targets beyond the fall angle — the recovery loop itself will detect
            // the overshoot and trigger the teeter/fall path, so repeated same-side picks
            // can actually cause the walker to fall.
            recoveryTargetTilt = Mathf.Clamp(targetTilt, -fallAngle * 1.2f, fallAngle * 1.2f);
            recoveryStartTilt = TiltAngle;
            recoveryStartAngVel = AngularVelocity;
            recoveryElapsed = 0f;
        }

        /// <summary>Instant tilt nudge. Use for disturbances (birds, wind gusts). Under the simplified
        /// constant-lean model there is no angular velocity to kick, so the magnitude is interpreted
        /// as a direct tilt delta (degrees).</summary>
        public void ApplyAngularImpulse(float tiltDelta)
        {
            if (IsFallen || disableBalance) return;
            TiltAngle = Mathf.Clamp(TiltAngle + tiltDelta, -fallAngle, fallAngle);
        }

        /// <summary>Back-compat alias. Treats the argument as a target-tilt recovery (matches roulette semantics).</summary>
        [System.Obsolete("Use PullToTargetTilt for recovery picks or ApplyAngularImpulse for raw kicks.")]
        public void ApplyBalanceImpulse(float value) => PullToTargetTilt(value);

        public void ResetWalker()
        {
            TiltAngle = 0f;
            AngularVelocity = 0f;
            IsFallen = false;
            IsTeetering = false;
            graceTimer = 0f;
            GraceRemaining = 0f;
            DistanceTravelled = 0f;
            leanSign = Random.value < 0.5f ? -1 : 1;
            recoveryActive = false;
            recoveryElapsed = 0f;
            fallAnimActive = false;
            fallAnimTimer = 0f;
            tiltRoot.localRotation = Quaternion.identity;
            tiltRoot.localPosition = Vector3.zero;
        }
    }
}
