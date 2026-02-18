using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GameLensAnalytics.Runtime
{
    public sealed class UploadQueueWorker : IDisposable
    {
        public sealed class UploadItem
        {
            public string ImagePath;
            public string JsonPath;
            public string CaptureId; // optional, useful for logs
        }

        private readonly BlockingCollection<UploadItem> _queue = new(new ConcurrentQueue<UploadItem>());
        private readonly Thread _thread;
        private volatile bool _running = true;

        public UploadQueueWorker()
        {
            _thread = new Thread(WorkerLoop) { IsBackground = true, Name = "GameLens.Uploader" };
            _thread.Start();
        }

        public void Enqueue(string imagePath, string jsonPath, string captureId = null)
        {
            if (!_running) return;
            _queue.Add(new UploadItem { ImagePath = imagePath, JsonPath = jsonPath, CaptureId = captureId });
        }

        private void WorkerLoop()
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                try
                {
                    // TODO upload item.ImagePath + item.JsonPath
                }
                catch { }
            }
        }

        public void Dispose()
        {
            _running = false;
            _queue.CompleteAdding();
            try { _thread.Join(500); } catch { }
            _queue.Dispose();
        }
    }
}

