using Godot;
using System;
using System.Collections.Generic;

namespace GameLensAnalytics.Runtime
{
    /// <summary>
    /// GameLens main runtime entry point (autoload).
    /// Owns config + state, exposes a small public API, and coordinates capture scheduling.
    /// </summary>
    public partial class AnalyticsAutoload : Node
    {
        // -------------------------
        // Singleton-style access
        // -------------------------
        public static AnalyticsAutoload Instance { get; private set; }

        // -------------------------
        // Public API surface 
        // -------------------------
        [Export] public bool Enabled { get; private set; } = true;

        // Minimal config for now; we’ll replace with a Resource later (AnalyticsConfig.tres)
        [Export] public string EndpointBase { get; set; } = "";
        [Export] public string ProjectId { get; set; } = "";
        [Export] public string GameId { get; set; } = "";

        // Sensitive scene handling
        private readonly HashSet<string> _sensitiveScenePaths = new();
        private bool _currentSceneSensitive = false;

        // Basic throttling / dedupe
        private double _lastSnapTimeSec = -9999.0;
        [Export] public double MinSecondsBetweenSnaps { get; set; } = 0.35;

        // Queue for future “capture worker”
        private readonly Queue<SnapRequest> _pendingSnaps = new();

        // Signals to observe plugin behavior
        [Signal] public delegate void SnapQueuedEventHandler(string reason);
        [Signal] public delegate void SnapDroppedEventHandler(string reason, string why);
        [Signal] public delegate void SensitiveSceneChangedEventHandler(bool isSensitive);

        public override void _EnterTree()
        {
            // Enforce single instance even if user misconfigures
            if (Instance != null && Instance != this)
            {
                QueueFree();
                return;
            }
            Instance = this;
            Name = "GameLens"; // the node name

            // We want this to exist across scene changes
            ProcessMode = ProcessModeEnum.Always;
        }

        public override void _Ready()
        {
            // Track scene changes to apply sensitive-scene policy
            if (GetTree() != null)
                GetTree().SceneChanged += OnSceneChanged;

            // Initialize sensitivity based on current scene (if any)
            UpdateCurrentSceneSensitivity();
        }

        public override void _ExitTree()
        {
            if (Instance == this) Instance = null;

            if (GetTree() != null)
                GetTree().SceneChanged -= OnSceneChanged;
        }

        // -------------------------
        // API methods 
        // -------------------------
        public void Enable() => Enabled = true;

        public void Disable() => Enabled = false;

        /// <summary>
        /// Mark a scene path as sensitive/non-sensitive (e.g., login, payments, personal info screens).
        /// scenePath should look like: res://Scenes/Login.tscn
        /// </summary>
        public void SetSensitiveScene(string scenePath, bool isSensitive)
        {
            GD.Print($"SetSensitiveScene: '{scenePath}' isSensitive={isSensitive}");
            if (string.IsNullOrWhiteSpace(scenePath))
                return;

            if (isSensitive) _sensitiveScenePaths.Add(scenePath);
            else _sensitiveScenePaths.Remove(scenePath);

            UpdateCurrentSceneSensitivity();
        }

        /// <summary>
        /// Request a capture ("Snap") with a reason and optional metadata.
        /// For now we just enqueue; next step we’ll implement the capture pipeline.
        /// </summary>
        public void Snap(string reason, Godot.Collections.Dictionary meta = null)
        {
            GD.Print($"Snap requested: reason='{reason}' meta={meta}");
            reason ??= "unspecified";

            if (!Enabled)
            {
                EmitSignal(SignalName.SnapDropped, reason, "disabled");
                return;
            }

            if (_currentSceneSensitive)
            {
                EmitSignal(SignalName.SnapDropped, reason, "sensitive_scene");
                return;
            }

            double now = Time.GetUnixTimeFromSystem(); // seconds
            if (now - _lastSnapTimeSec < MinSecondsBetweenSnaps)
            {
                EmitSignal(SignalName.SnapDropped, reason, "rate_limited");
                return;
            }

            _lastSnapTimeSec = now;

            var req = new SnapRequest
            {
                Reason = reason,
                Meta = meta ?? new Godot.Collections.Dictionary(),
                UtcUnixSeconds = now
            };

            _pendingSnaps.Enqueue(req);
            EmitSignal(SignalName.SnapQueued, reason);
        }

        // -------------------------
        // Internal helpers
        // -------------------------
        private void OnSceneChanged()
        {
            UpdateCurrentSceneSensitivity();
        }

        private void UpdateCurrentSceneSensitivity()
        {
            var sceneFilePath = GetTree()?.CurrentScene?.SceneFilePath ?? "";
            bool isSensitive = !string.IsNullOrEmpty(sceneFilePath) && _sensitiveScenePaths.Contains(sceneFilePath);

            if (isSensitive != _currentSceneSensitive)
            {
                _currentSceneSensitive = isSensitive;
                EmitSignal(SignalName.SensitiveSceneChanged, _currentSceneSensitive);
            }
        }

        // -------------------------
        // For next step: capture worker will drain this queue
        // -------------------------
        public bool TryDequeueSnap(out string reason, out Godot.Collections.Dictionary meta, out double utcUnixSeconds)
        {
            if (_pendingSnaps.Count == 0)
            {
                reason = default;
                meta = default;
                utcUnixSeconds = default;
                return false;
            }

            var req = _pendingSnaps.Dequeue();
            reason = req.Reason;
            meta = req.Meta;
            utcUnixSeconds = req.UtcUnixSeconds;
            return true;
        }

        private struct SnapRequest
        {
            public string Reason;
            public Godot.Collections.Dictionary Meta;
            public double UtcUnixSeconds;
        }
    }
}
