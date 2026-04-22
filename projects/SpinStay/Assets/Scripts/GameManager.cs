using UnityEngine;
using UnityEngine.InputSystem;

namespace SpinStay
{
    public enum PickerMode { Roulette, Pendulum }

    public class GameManager : MonoBehaviour
    {
        [SerializeField] private TightropeWalker walker;
        [SerializeField] private Roulette roulette;
        [SerializeField] private Pendulum pendulum;

        [Header("Picker mode")]
        [SerializeField] private PickerMode mode = PickerMode.Roulette;
        [Tooltip("Config used by the auto-created Pendulum. Falls back to the roulette's own config when null.")]
        [SerializeField] private RouletteConfig pendulumConfig;
        [Tooltip("Size of the auto-created PendulumUI RectTransform (width x height). 2:1 aspect renders the gauge without distortion.")]
        [SerializeField] private Vector2 pendulumUISize = new Vector2(520f, 260f);

        [Header("Rules")]
        [Tooltip("Delay (seconds) after the picker stops before it restarts. 0 = instant respin.")]
        [SerializeField, Min(0f)] private float respinDelay = 0.15f;
        [Tooltip("Delay (seconds) after the walker falls before the run resets. (Currently unused — reset is click-driven.)")]
        [SerializeField, Min(0f)] private float resetDelay = 2.0f;

        private float respinTimer = -1f;
        private float resetTimer = -1f;
        private Vector3 walkerStartPos;

        // Queued roulette speed boost (SpeedUpWheel). Only applies when the Roulette is the active picker.
        private float pendingBoostMultiplier;
        private float pendingBoostDuration;

        // Diagnostic: buffer the "before" state so we can log "after" once recovery completes.
        private bool  pendingAfterLog;
        private float pendingAfterLogTimer;
        private float pendingAfterLogTiltBefore;
        private float pendingAfterLogTarget;
        private string pendingAfterLogLabel;
        private float? lastPickAfterTilt;

        void Awake()
        {
            if (walker == null) walker = FindAnyObjectByType<TightropeWalker>();
            if (roulette == null) roulette = FindAnyObjectByType<Roulette>();
            if (pendulum == null) pendulum = FindAnyObjectByType<Pendulum>();
            if (pendulum == null) pendulum = AutoBuildPendulum();
        }

        Pendulum AutoBuildPendulum()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return null;

            var cfg = pendulumConfig != null
                ? pendulumConfig
                : (roulette != null ? roulette.Config : null);

            var go = new GameObject("PendulumUI", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = pendulumUISize;

            var p = go.AddComponent<Pendulum>();
            p.SetConfig(cfg);
            return p;
        }

        void Start()
        {
            if (walker != null)
            {
                walkerStartPos = walker.transform.position;
                walker.OnFell += HandleWalkerFell;
            }
            if (roulette != null) roulette.OnStopped += HandleOptionStopped;
            if (pendulum != null) pendulum.OnStopped += HandleOptionStopped;
            ApplyMode();
        }

        void OnDestroy()
        {
            if (walker != null) walker.OnFell -= HandleWalkerFell;
            if (roulette != null) roulette.OnStopped -= HandleOptionStopped;
            if (pendulum != null) pendulum.OnStopped -= HandleOptionStopped;
        }

        void ApplyMode()
        {
            bool usingRoulette = mode == PickerMode.Roulette;
            if (roulette != null) roulette.gameObject.SetActive(usingRoulette);
            if (pendulum != null) pendulum.gameObject.SetActive(!usingRoulette);

            // Pending roulette boost is meaningless for the pendulum; discard on switch.
            if (!usingRoulette) { pendingBoostMultiplier = 0f; pendingBoostDuration = 0f; }
        }

        void SwitchMode(PickerMode next)
        {
            if (next == mode) return;
            mode = next;
            respinTimer = -1f;
            resetTimer = -1f;
            // Put the newly-active picker into a clean spinning state.
            if (mode == PickerMode.Roulette && roulette != null && walker != null && !walker.IsFallen)
                roulette.RestartSpin();
            else if (mode == PickerMode.Pendulum && pendulum != null && walker != null && !walker.IsFallen)
                pendulum.RestartSpin();
            ApplyMode();
        }

        bool ActivePickerIsSpinning()
        {
            if (mode == PickerMode.Roulette) return roulette != null && roulette.State == RouletteState.Spinning;
            return pendulum != null && pendulum.State == PendulumState.Swinging;
        }

        void ActivePickerRequestStop()
        {
            if (mode == PickerMode.Roulette) { if (roulette != null) roulette.RequestStop(); }
            else                             { if (pendulum != null) pendulum.RequestStop(); }
        }

        void ActivePickerRestart()
        {
            if (mode == PickerMode.Roulette && roulette != null)
            {
                roulette.RestartSpin();
                if (pendingBoostDuration > 0f)
                {
                    roulette.BoostSpin(pendingBoostMultiplier, pendingBoostDuration);
                    pendingBoostDuration = 0f;
                }
            }
            else if (mode == PickerMode.Pendulum && pendulum != null)
            {
                pendulum.RestartSpin();
                pendingBoostDuration = 0f;
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool stopPressed =
                (kb != null && kb.spaceKey.wasPressedThisFrame) ||
                (mouse != null && mouse.leftButton.wasPressedThisFrame);

            if (stopPressed && walker != null && walker.FallAnimationComplete)
            {
                ResetRun();
                return;
            }

            if (stopPressed && ActivePickerIsSpinning() && walker != null && !walker.IsFallen)
            {
                ActivePickerRequestStop();
            }

            if (respinTimer >= 0f)
            {
                respinTimer -= Time.deltaTime;
                if (respinTimer <= 0f)
                {
                    if (walker != null && !walker.IsFallen) ActivePickerRestart();
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

        void HandleOptionStopped(RouletteOption option, int index)
        {
            float tiltBefore = walker != null ? walker.TiltAngle : 0f;
            float angVelBefore = walker != null ? walker.AngularVelocity : 0f;
            float absoluteTarget = tiltBefore;
            if (option.action == RouletteActionType.TiltShift) absoluteTarget = tiltBefore + option.tiltDelta;
            else if (option.action == RouletteActionType.ResetBalance) absoluteTarget = 0f;

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

            string actionLog = option.action.ToString();
            bool scheduleAfterLog = true;
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
                    if (mode == PickerMode.Roulette)
                    {
                        pendingBoostMultiplier = option.wheelSpeedMultiplier;
                        pendingBoostDuration = option.wheelBoostDuration;
                        actionLog = string.Format("SPEED BOOST ×{0:F1} for {1:F1}s", option.wheelSpeedMultiplier, option.wheelBoostDuration);
                    }
                    else
                    {
                        actionLog = "SPEED BOOST (ignored — pendulum mode)";
                    }
                    scheduleAfterLog = false;
                    break;
                case RouletteActionType.Fall:
                    if (walker != null) walker.ForceFall();
                    actionLog = "FORCE FALL";
                    scheduleAfterLog = false;
                    break;
                case RouletteActionType.Nothing:
                    actionLog = "NOTHING (no-op)";
                    scheduleAfterLog = false;
                    break;
            }

            Debug.Log(string.Format(
                "[Pick] #{0} {1}  BEFORE: tilt={2:+0.0;-0.0;0}° angVel={3:+0.0;-0.0;0}°/s  {4}{5}",
                index, option.label, tiltBefore, angVelBefore, actionLog, driftNote));

            if (scheduleAfterLog && walker != null)
            {
                pendingAfterLog = true;
                pendingAfterLogTimer = walker.RecoveryDuration + 0.02f;
                pendingAfterLogTiltBefore = tiltBefore;
                pendingAfterLogTarget = absoluteTarget;
                pendingAfterLogLabel = option.label;
            }

            if (respinDelay <= 0f)
            {
                if (walker != null && !walker.IsFallen) ActivePickerRestart();
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
        }

        void ResetRun()
        {
            if (walker != null)
            {
                walker.transform.position = walkerStartPos;
                walker.ResetWalker();
            }
            ActivePickerRestart();
            respinTimer = -1f;
            resetTimer = -1f;
            pendingAfterLog = false;
            lastPickAfterTilt = null;
            pendingBoostDuration = 0f;
            pendingBoostMultiplier = 0f;
        }

        void OnGUI()
        {
            // Mode switch button — top-left.
            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            string btnLabel = mode == PickerMode.Roulette ? "Mode: Roulette  ▸  Pendulum" : "Mode: Pendulum  ▸  Roulette";
            if (GUI.Button(new Rect(10, 10, 260, 28), btnLabel, btnStyle))
            {
                SwitchMode(mode == PickerMode.Roulette ? PickerMode.Pendulum : PickerMode.Roulette);
            }

            // Stats — top-right.
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
