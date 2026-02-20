using System;
using System.Threading.Tasks;
using SocketIOClient;

namespace GameLensAnalytics.Runtime
{
    public sealed class SocketCaptureClient : IDisposable
    {
        private SocketIOClient.SocketIO _io;

        public bool IsConnected => _io != null && _io.Connected;

        // Events
        public event Action Connected;
        public event Action<string> Disconnected;
        public event Action<string> ResponseReceived;
        public event Action<string> ErrorReceived;

        public async Task ConnectAsync(string endpointBase)
        {
            var url = endpointBase.TrimEnd('/');

            _io = new SocketIOClient.SocketIO(new Uri(url), new SocketIOOptions
            {
                Path = "/socket.io"
            });

            _io.OnConnected += (_, __) => Connected?.Invoke();

            _io.OnDisconnected += (_, reason) =>
                Disconnected?.Invoke(reason);

            _io.On("response", resp =>
                ResponseReceived?.Invoke(resp?.ToString() ?? "<null>"));

            _io.On("error", resp =>
                ErrorReceived?.Invoke(resp?.ToString() ?? "<null>"));

            await _io.ConnectAsync().ConfigureAwait(false);
        }

        public async Task EmitCaptureEventAsync(object payload)
        {
            if (_io == null || !_io.Connected)
                return;

            await _io.EmitAsync("capture_event", payload)
                     .ConfigureAwait(false);
        }

        public void Dispose()
        {
            try { _io?.DisconnectAsync().GetAwaiter().GetResult(); }
            catch { }

            _io?.Dispose();
            _io = null;
        }
    }
}