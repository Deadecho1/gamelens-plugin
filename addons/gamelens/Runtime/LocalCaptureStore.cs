using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace GameLensAnalytics.Runtime
{
    public sealed class LocalCaptureStore : IDisposable
    {
        public int MaxFilesOnDisk { get; set; } = 5000;

        // Fired AFTER files are written successfully (runs on the store worker thread)
        public event Action<string, string, string> CaptureSaved; 
        // (imgPath, jsonPath, captureId)

        private readonly string _rootDirGlobal;
        private readonly BlockingCollection<CapturePacket> _queue = new(new ConcurrentQueue<CapturePacket>());
        private readonly Thread _thread;
        private volatile bool _running = true;

        public LocalCaptureStore(string rootDirGlobal)
        {
            _rootDirGlobal = rootDirGlobal;
            Directory.CreateDirectory(_rootDirGlobal);

            _thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "GameLens.LocalCaptureStore"
            };
            _thread.Start();
        }

        public void Enqueue(CapturePacket packet)
        {
            if (!_running) return;
            _queue.Add(packet);
        }

        private void WorkerLoop()
        {
            foreach (CapturePacket pkt in _queue.GetConsumingEnumerable())
            {
                try
                {
                    var dayDir = Path.Combine(_rootDirGlobal, "captures", DateTime.UtcNow.ToString("yyyy-MM-dd"));
                    Directory.CreateDirectory(dayDir);

                    var baseName = $"{pkt.UtcUnixSeconds:0.000}_{pkt.CaptureId}";
                    var imgPath = Path.Combine(dayDir, baseName + pkt.ImageExt);
                    var jsonPath = Path.Combine(dayDir, baseName + ".json");

                    File.WriteAllBytes(imgPath, pkt.ImageBytes);
                    File.WriteAllText(jsonPath, pkt.PayloadJson);

                    EnforceRetention(dayDir);

                    // Notify orchestrator (or anyone) that this capture is ready for upload
                    try { CaptureSaved?.Invoke(imgPath, jsonPath, pkt.CaptureId); } catch { }
                }
                catch
                {
                    // swallow or log somewhere
                }
            }
        }

        private void EnforceRetention(string dayDir)
        {
            // Simple: cap file count in that folder (you can upgrade to size-based + LRU later)
            try
            {
                var files = Directory.GetFiles(dayDir, "*.png");
                if (files.Length <= MaxFilesOnDisk) return;

                Array.Sort(files, StringComparer.Ordinal); // oldest first if name starts with timestamp
                int toDelete = files.Length - MaxFilesOnDisk;

                for (int i = 0; i < toDelete; i++)
                {
                    var img = files[i];
                    var json = Path.ChangeExtension(img, ".json");

                    TryDelete(img);
                    TryDelete(json);
                }
            }
            catch { }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
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
