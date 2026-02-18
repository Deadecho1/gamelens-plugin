namespace GameLensAnalytics.Runtime
{
    public sealed class CapturePacket
    {
        public string CaptureId;
        public double UtcUnixSeconds;
        public byte[] ImageBytes;
        public string ImageExt;     // ".png"
        public string PayloadJson;  // metadata+input+reasons 
    }
}
