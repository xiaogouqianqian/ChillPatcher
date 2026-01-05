using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ChillPatcher.SDK.Interfaces;
using ChillPatcher.SDK.Models;

namespace ChillPatcher.Module.Bilibili
{
    public class FFmpegStreamReader : IPcmStreamReader
    {
        private readonly string _url;
        private readonly string _ffmpegPath;
        private Process _process;
        private readonly ulong _totalFrames;

        private RingBuffer _ringBuffer;
        private const int BUFFER_SIZE = 44100 * 2 * 10; // 10秒缓冲

        private Thread _downloadThread;
        private volatile bool _isDisposed;
        private volatile bool _isFFmpegExited;
        private volatile bool _shouldStopThread;
        private ulong _currentFrame;

        public FFmpegStreamReader(string ffmpegPath, string url, float duration)
        {
            _ffmpegPath = ffmpegPath;
            _url = url;
            _totalFrames = (ulong)(44100 * duration);
            _ringBuffer = new RingBuffer(BUFFER_SIZE);
            StartWorker(0);
        }

        private void StartWorker(float startTime)
        {
            StopWorker();
            _shouldStopThread = false;
            _isFFmpegExited = false;
            _ringBuffer.Clear();
            _currentFrame = (ulong)(startTime * 44100);

            _downloadThread = new Thread(() => DownloadLoop(startTime));
            _downloadThread.IsBackground = true;
            _downloadThread.Start();
        }

        private void StopWorker()
        {
            _shouldStopThread = true;
            KillProcess();
            if (_downloadThread != null && _downloadThread.IsAlive) _downloadThread.Join(200);
        }

        private void DownloadLoop(float startTime)
        {
            try
            {
                // 标准化的参数：带 Referer 和 User-Agent (写在 headers 里避免转义问题)
                string headers = $"Referer: https://www.bilibili.com\r\nUser-Agent: {BilibiliBridge.UserAgent}";

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    // -loglevel warning: 只输出警告和错误
                    Arguments = $"-headers \"{headers}\" -ss {startTime} -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i \"{_url}\" -f s16le -ac 2 -ar 44100 -loglevel warning pipe:1",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _process = Process.Start(startInfo);

                // 吃掉 stderr 防止阻塞
                new Thread(() => {
                    try
                    {
                        using (var reader = _process.StandardError)
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                // 关键错误仍然打印出来，但不刷屏
                                if (line.Contains("403") || line.Contains("Error"))
                                    UnityEngine.Debug.LogWarning($"[FFmpeg] {line}");
                            }
                        }
                    }
                    catch { }
                })
                { IsBackground = true }.Start();

                var baseStream = _process.StandardOutput.BaseStream;
                byte[] byteBuffer = new byte[8192];
                float[] floatBuffer = new float[4096];

                while (!_shouldStopThread)
                {
                    if (_process.HasExited) break;

                    if (_ringBuffer.Count >= _ringBuffer.Capacity - floatBuffer.Length)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    int bytesRead = baseStream.Read(byteBuffer, 0, byteBuffer.Length);
                    if (bytesRead == 0) break; // EOF

                    int samplesRead = bytesRead / 2;
                    for (int i = 0; i < samplesRead; i++)
                    {
                        short sample = (short)((byteBuffer[i * 2 + 1] << 8) | byteBuffer[i * 2]);
                        floatBuffer[i] = sample / 32768f;
                    }
                    _ringBuffer.Write(floatBuffer, 0, samplesRead);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FFmpegThread] Error: {ex.Message}");
            }
            finally
            {
                _isFFmpegExited = true;
                KillProcess();
            }
        }

        private void KillProcess()
        {
            if (_process != null)
            {
                try { if (!_process.HasExited) _process.Kill(); _process.Dispose(); } catch { }
                _process = null;
            }
        }

        public PcmStreamInfo Info => new PcmStreamInfo { SampleRate = 44100, Channels = 2, TotalFrames = _totalFrames, Format = "pcm_s16le" };
        public bool IsReady => !_isDisposed;
        public ulong CurrentFrame => _currentFrame;

        // 结束判断：ffmpeg 退出且缓冲空
        public bool IsEndOfStream => _isDisposed || (_isFFmpegExited && _ringBuffer.Count == 0);

        public bool CanSeek => true;
        public double CacheProgress => (double)_ringBuffer.Count / _ringBuffer.Capacity * 100.0;
        public bool IsCacheComplete => _isFFmpegExited;
        public bool HasPendingSeek => false;
        public long PendingSeekFrame => -1;

        public long ReadFrames(float[] buffer, int framesToRead)
        {
            if (_isDisposed) return 0;

            int samplesToRead = framesToRead * 2;
            int samplesRead = _ringBuffer.Read(buffer, samplesToRead);

            // 缓冲不足时填充静音，防止意外 EOF
            if (samplesRead < samplesToRead)
            {
                Array.Clear(buffer, samplesRead, samplesToRead - samplesRead);
                _currentFrame += (ulong)(samplesToRead / 2);
                return framesToRead;
            }

            int framesRead = samplesRead / 2;
            _currentFrame += (ulong)framesRead;
            return framesRead;
        }

        public bool Seek(ulong frameIndex)
        {
            if (_isDisposed) return false;
            float seekTime = frameIndex / 44100f;
            StartWorker(seekTime);
            return true;
        }

        public void CancelPendingSeek() { }
        public void Dispose() { _isDisposed = true; StopWorker(); }
    }
}