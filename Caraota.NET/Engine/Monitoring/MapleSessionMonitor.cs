using System.Diagnostics;

using Caraota.NET.Engine.Session;

namespace Caraota.NET.Engine.Monitoring
{
    public class MapleSessionMonitor : IDisposable
    {
        public event Action? Disconnected;

        public long LastPacketInterceptedTime;

        private ISessionState? _session;

        private CancellationTokenSource? _cts;

        public void Start(ISessionState session)
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

                    if (_session == null || !_session.Success)
                        continue;

                    long idleTime = Environment.TickCount64 - LastPacketInterceptedTime;

                    if (idleTime >= 12000)
                    {
                        Debug.WriteLine($"[Monitor] Timeout detectado ({idleTime}ms). Disparando OnDisconnected.");

                        if (Disconnected != null)
                        {
                            Disconnected.Invoke();
                        }

                        // TODO: Manejar la desconexion
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
        }
    }
}
