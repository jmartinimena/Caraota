using System.Diagnostics;

namespace Caraota.NET.Models
{
    public class MapleSessionMonitor
    {
        public event DisconnectedEventDelegate? OnDisconnected;

        public delegate Task DisconnectedEventDelegate();

        public long LastPacketInterceptedTime;

        private MapleSession? _session;

        private CancellationTokenSource? _cts;

        public void Start(MapleSession session)
        {
            _session = session;

            _cts = new CancellationTokenSource();

            Task.Factory.StartNew(CheckAlive, _cts.Token,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private async Task CheckAlive()
        {
            Debug.WriteLine("[Monitor] Hilo de vigilancia iniciado.");

            while (_cts != null && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(500, _cts.Token);

                    if (_session == null || !_session.SessionSuccess)
                        continue;

                    long idleTime = Environment.TickCount64 - LastPacketInterceptedTime;

                    if (idleTime >= 8000)
                    {
                        Debug.WriteLine($"[Monitor] Timeout detectado ({idleTime}ms). Disparando OnDisconnected.");

                        if (OnDisconnected != null)
                        {
                            await OnDisconnected.Invoke();
                        }

                        _session.SessionSuccess = false;
                        break;
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Monitor] Error crítico: {ex.Message}");
                }
            }

            Debug.WriteLine("[Monitor] Hilo de vigilancia finalizado.");
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _session = null;
        }
    }
}
