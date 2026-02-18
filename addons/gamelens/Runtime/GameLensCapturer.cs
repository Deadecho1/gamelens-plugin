using Godot;

namespace GameLensAnalytics.Runtime
{
    public partial class GameLensCapturer : Node
    {
        private const int _width = 640;
        private const int _height = 360;
        private SubViewport _capVp;
        private TextureRect _capRect;

        // TODO: flesh out
        public CapturePacket Capture(Godot.Collections.Array<SnapReason> reasons, double captureTime)
        {
            var img = _capVp.GetTexture().GetImage();
            var png = img.SavePngToBuffer();

            return new CapturePacket
            {
                CaptureId = System.Guid.NewGuid().ToString(),
                UtcUnixSeconds = captureTime,
                ImageBytes = png,
                ImageExt = ".png",
                PayloadJson ="{}"
            };
        }

        public override void _Ready()
        {
            SetupCaptureViewport(_width, _height);
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
                Texture = GetViewport().GetTexture(), // mirror main viewport into our SubViewport
                StretchMode = TextureRect.StretchModeEnum.Scale,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
            };
            _capRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _capVp.AddChild(_capRect);
        }
    }
}