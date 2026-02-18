using Godot;
using System;
using System.Collections.Generic;
using System.IO;


namespace GameLensAnalytics.Runtime
{
    // -------------------------
    // Snap reason enum
    // -------------------------
    public enum SnapReason
    {
        AutoTimer,
        Manual,
    }
    
    /// <summary>
    /// GameLens main runtime entry point (autoload).
    /// Owns config + state, exposes a small public API, and coordinates capture scheduling.
    /// </summary>
    public partial class GameLensOrchestrator : Node
    {
        // -------------------------
        // Singleton access
        // -------------------------
        public static GameLensOrchestrator Instance { get; private set; }

        // -------------------------
        // Public API surface 
        // -------------------------
        [Export] public bool Enabled { get; private set; } = true;

        public void Enable() => Enabled = true;
        public void Disable() => Enabled = false;

        // -------------------------
        // Managed instances
        // -------------------------
        private GameLensCapturer _capturer;
        private LocalCaptureStore _store;
        private UploadQueueWorker _uploader;

        // -------------------------
        // Sub viewport related
        // -------------------------
        private SubViewport _capVp;
        private TextureRect _capRect;

        // -------------------------
        // Coalesced snap state (max 1 snap per frame)
        // -------------------------
        private bool _snapPending = false;
        private bool _snapFinalized = false;
        private ulong _snapPendingFrame = 0;

        private Godot.Collections.Array<SnapReason> _pendingReasons = new();
        private double _pendingUtcUnixSeconds = 0.0;

        // -------------------------
        // Events (for debugging)
        // -------------------------
        [Signal] public delegate void SnapQueuedEventHandler();
        [Signal] public delegate void SnapCapturedEventHandler(int snapId);
        [Signal] public delegate void SnapDroppedEventHandler(string why);


        public override void _EnterTree()
        {
            if (Instance != null && Instance != this)
            {
                QueueFree();
                return;
            }
            Instance = this;
            Name = "Orchestrator";
            ProcessMode = ProcessModeEnum.Always;
        }

        public override void _ExitTree()
        {
            if (Instance == this) Instance = null;

            _store?.Dispose();
            _uploader?.Dispose();
        }
        
        public override void _Ready()
        {
            _capturer = new GameLensCapturer();
            AddChild(_capturer);

            // Globalize user:// once on main thread
            var rootGlobal = ProjectSettings.GlobalizePath("user://gamelens");
            Directory.CreateDirectory(rootGlobal);

            _uploader = new UploadQueueWorker();
            _store = new LocalCaptureStore(rootGlobal);

            // When storage finishes writing a capture -> enqueue for upload
            _store.CaptureSaved += (imgPath, jsonPath, captureId) =>
            {
                _uploader.Enqueue(imgPath, jsonPath, captureId);
            };
        }

        private void SetupCaptureViewport(int w, int h)
        {
            _capVp = new SubViewport
            {
                Size = new Vector2I(w, h),
                Disable3D = true,
                TransparentBg = false,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always
            };

            AddChild(_capVp);

            _capRect = new TextureRect
            {
                Texture = GetViewport().GetTexture(),
                StretchMode = TextureRect.StretchModeEnum.Scale,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
            };

            _capRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _capVp.AddChild(_capRect);
        }

        /// <summary>
        /// If snap is called multiple times in a single frame, it coalesces multiple requests into ONE snap per frame, merging reasons.
        /// </summary>
        public void Snap(SnapReason reason)
        {
            GD.Print($"Snap requested: reason='{reason}'");

            if (!Enabled)
            {
                EmitSignal(SignalName.SnapDropped, "disabled");
                return;
            }

            ulong frame = Engine.GetProcessFrames();

            // Starting a new coalesced snap for this frame?
            if (!_snapPending || frame != _snapPendingFrame)
            {
                _snapPending = true;
                _snapFinalized = false;
                _snapPendingFrame = frame;

                _pendingReasons.Clear();
                _pendingUtcUnixSeconds = Time.GetUnixTimeFromSystem();

                // Allow multiple Snap() calls this frame before finalizing
                CallDeferred(nameof(FinalizePendingSnap));
            }

            // Merge reasons
            _pendingReasons.Add(reason);

            EmitSignal(SignalName.SnapQueued);
        }

        private void FinalizePendingSnap()
        {
            if (!_snapPending || _snapFinalized)
                return;

            _snapFinalized = true;
            var pkt = _capturer.Capture(_pendingReasons, _pendingUtcUnixSeconds);
            _store.Enqueue(pkt);
            // reset snap state for next frame
            ConsumeSnap();
        }

        /// <summary>
        /// Drain the single coalesced snap (max 1).
        /// </summary>
        private void ConsumeSnap()
        {
            _snapPending = false;
            _snapFinalized = false;

            _pendingReasons.Clear();
            _pendingUtcUnixSeconds = 0;
        }
    }
}
