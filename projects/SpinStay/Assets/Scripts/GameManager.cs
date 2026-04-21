using UnityEngine;
using UnityEngine.InputSystem;

namespace SpinStay
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private TightropeWalker walker;
        [SerializeField] private Roulette roulette;

        [Header("Rules")]
        [Tooltip("Delay (seconds) after the roulette stops before it spins again. 0 = instant respin (no pause between picks).")]
        [SerializeField, Min(0f)] private float respinDelay = 0.15f;
        [Tooltip("Delay (seconds) after the walker falls before the run resets.")]
        [SerializeField, Min(0f)] private float resetDelay = 2.0f;

        private float respinTimer = -1f;
        private float resetTimer = -1f;
        private Vector3 walkerStartPos;

        // Queued roulette speed boost (set by a SpeedUpWheel option, applied on next restart).
        private float pendingBoostMultiplier;
        private float pendingBoostDuration;

        // Diagnostic: buffer the "before" state so we can log "after" once recovery completes.
        private bool  pendingAfterLog;
        private float pendingAfterLogTimer;
        private float pendingAfterLogTiltBefore;
        private float pendingAfterLogTarget;
        private string pendingAfterLogLabel;
        private float? lastPickAfterTilt; // tilt at the end of the previous pick, for drift-since-last reporting

        void Awake()
        {
            if (walker == null) walker = FindAnyObjectByType<TightropeWalker>();
            if (roulette == null) roulette = FindAnyObjectByType<Roulette>();
        }

        void Start()
        {
            if (walker != null)
            {
                walkerStartPos = walker.transform.position;
                walker.OnFell += HandleWalkerFell;
            }
            if (roulette != null)
            {
                roulette.OnStopped += HandleRouletteStopped;
            }
        }

        void OnDestroy()
        {
            if (walker != null) walker.OnFell -= HandleWalkerFell;
            if (roulette != null) roulette.OnStopped -= HandleRouletteStopped;
        }

        void Update()
        {
            // Input: Space / left mouse to stop the wheel.
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool stopPressed =
                (kb != null && kb.spaceKey.wasPressedThisFrame) ||
                (mouse != null && mouse.leftButton.wasPressedThisFrame);

            // Click-to-reset once the fall animation has finished.
            if (stopPressed && walker != null && walker.FallAnimationComplete)
            {
                ResetRun();
                return;
            }

            if (stopPressed && roulette != null && roulette.State == RouletteState.Spinning && walker != null && !walker.IsFallen)
            {
                roulette.RequestStop();
            }

            if (respinTimer >= 0f)
            {
                respinTimer -= Time.deltaTime;
                if (respinTimer <= 0f)
                {
                    if (roulette != null && walker != null && !walker.IsFallen)
                    {
                        roulette.RestartSpin();
                        // Apply any queued speed-boost immediately after restart so it overrides the baseline.
                        if (pendingBoostDuration > 0f)
                        {
                            roulette.BoostSpin(pendingBoostMultiplier, pendingBoostDuration);
                            pendingBoostDuration = 0f;
                        }
                    }
                    respinTimer = -1f;
                }
            }

            if (pendingAfterLog)
            {
                pendingAfterLogTimer -= Time.deltaTime;
                if (pendingAfterLogTimer <= 0f)
                {
                    pendingAfterLog = false;
                    float tiltAfter = walker != null ? walker.TiltAngle : 0f;
                    float delivered = tiltAfter - pendingAfterLogTiltBefore;
                    float error = tiltAfter - pendingAfterLogTarget;
                    Debug.Log(string.Format(
                        "[Pick] {0}  AFTER:  tilt={1:+0.0;-0.0;0}°  Δ={2:+0.0;-0.0;0}°  (target {3:+0.0;-0.0;0}°, error {4:+0.0;-0.0;0}°)",
                        pendingAfterLogLabel, tiltAfter, delivered, pendingAfterLogTarget, error));
                    lastPickAfterTilt = tiltAfter;
                }
            }
        }

        void HandleRouletteStopped(RouletteOption option, int index)
        {
            float tiltBefore = walker != null ? walker.TiltAngle : 0f;
            float angVelBefore = walker != null ? walker.AngularVelocity : 0f;
            // "absolute target" describes the tilt we expect after the pick, for logging.
            float absoluteTarget = tiltBefore;
            if (option.action == RouletteActionType.TiltShift) absoluteTarget = tiltBefore + option.tiltDelta;
            else if (option.action == RouletteActionType.ResetBalance) absoluteTarget = 0f;

            // If a previous pick's AFTER log is still queued, flush it NOW so we don't lose it
            // when picks come in faster than the recovery completes.
            if (pendingAfterLog)
            {
                float tiltAtStack = walker != null ? walker.TiltAngle : 0f;
                float deliveredStack = tiltAtStack - pendingAfterLogTiltBefore;
                float errorStack = tiltAtStack - pendingAfterLogTarget;
                Debug.Log(string.Format(
                    "[Pick] {0}  AFTER*: tilt={1:+0.0;-0.0;0}°  Δ={2:+0.0;-0.0;0}°  (target {3:+0.0;-0.0;0}°, error {4:+0.0;-0.0;0}°)  [flushed — next pick arrived]",
                    pendingAfterLogLabel, tiltAtStack, deliveredStack, pendingAfterLogTarget, errorStack));
                lastPickAfterTilt = tiltAtStack;
                pendingAfterLog = false;
            }

            string driftNote = lastPickAfterTilt.HasValue
                ? string.Format("  (drift since last pick: {0:+0.0;-0.0;0}°)", tiltBefore - lastPickAfterTilt.Value)
                : "";

            // Dispatch by action type.
            string actionLog = option.action.ToString();
            switch (option.action)
            {
                case RouletteActionType.TiltShift:
                    if (walker != null) walker.PullToTargetTilt(absoluteTarget);
                    actionLog = string.Format("shift={0:+0.0;-0.0;0}° → target={1:+0.0;-0.0;0}°", option.tiltDelta, absoluteTarget);
                    break;
                case RouletteActionType.ResetBalance:
                    if (walker != null) walker.PullToTargetTilt(0f);
                    actionLog = "RESET balance → 0°";
                    break;
                case RouletteActionType.SpeedUpWheel:
                    pendingBoostMultiplier = option.wheelSpeedMultiplier;
                    pendingBoostDuration = option.wheelBoostDuration;
                    actionLog = string.Format("SPEED BOOST ×{0:F1} for {1:F1}s", option.wheelSpeedMultiplier, option.wheelBoostDuration);
                    break;
                case RouletteActionType.Fall:
                    if (walker != null) walker.ForceFall();
                    actionLog = "FORCE FALL";
                    break;
            }

            Debug.Log(string.Format(
                "[Pick] #{0} {1}  BEFORE: tilt={2:+0.0;-0.0;0}° angVel={3:+0.0;-0.0;0}°/s  {4}{5}",
                index, option.label, tiltBefore, angVelBefore, actionLog, driftNote));

            // Schedule an AFTER-recovery log so we can see what the actual landing tilt is.
            if (walker != null)
            {
                pendingAfterLog = true;
                pendingAfterLogTimer = walker.RecoveryDuration + 0.02f;
                pendingAfterLogTiltBefore = tiltBefore;
                pendingAfterLogTarget = absoluteTarget;
                pendingAfterLogLabel = option.label;
            }

            if (respinDelay <= 0f)
            {
                if (roulette != null && walker != null && !walker.IsFallen)
                    roulette.RestartSpin();
                respinTimer = -1f;
            }
            else
            {
                respinTimer = respinDelay;
            }
        }

        void HandleWalkerFell()
        {
            Debug.Log($"[Walker] Fell after {walker.DistanceTravelled:F1} m. Click / Space to restart.");
            // No auto-reset: the player must press Space/Click once the fall animation finishes.
        }

        void ResetRun()
        {
            if (walker != null)
            {
                walker.transform.position = walkerStartPos;
                walker.ResetWalker();
            }
            if (roulette != null)
            {
                roulette.RestartSpin();
            }
            respinTimer = -1f;
            resetTimer = -1f;
            pendingAfterLog = false;
            lastPickAfterTilt = null;
            pendingBoostDuration = 0f;
            pendingBoostMultiplier = 0f;
        }

        void OnGUI()
        {
            // Tucked into the top-right so it doesn't cover the walker or wheel.
            float w = 260f;
            float x = Screen.width - w - 12f;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperRight };
            if (walker != null)
            {
                GUI.Label(new Rect(x, 8, w, 22), $"Distance: {walker.DistanceTravelled:F1} m", style);
                GUI.Label(new Rect(x, 30, w, 22), $"Tilt {walker.TiltAngle:F1}°  v {walker.AngularVelocity:F1}°/s", style);
                if (walker.IsFallen)
                    GUI.Label(new Rect(x, 52, w, 22), "FELL - resetting...", style);
            }
        }
    }
}
