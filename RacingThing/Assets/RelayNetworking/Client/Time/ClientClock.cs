using System;
using System.Diagnostics;

namespace RelayNetworking.Client.Time
{
    public sealed class ClientClock
    {
        private readonly object _lock = new();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private double _offset;
        private double _smoothedRtt = -1.0;
        private int _tickRate = 60;
        private double _tickDuration = 1.0 / 60.0;
        public double SnapThresholdSeconds { get; set; } = 0.25;
        public double Smoothing { get; set; } = 0.1;
        public bool SimulationSynced { get; private set; }

        public double SmoothedRtt
        {
            get { lock (_lock) return _smoothedRtt; }
        }

        public double LocalTime => _stopwatch.Elapsed.TotalSeconds;

        public double ServerTime
        {
            get { lock (_lock) return LocalTime + _offset; }
        }

        public ulong CurrentTick
        {
            get
            {
                lock (_lock)
                {
                    double serverTime = LocalTime + _offset;
                    if (serverTime <= 0)
                        return 0;
                    return (ulong)(serverTime / _tickDuration);
                }
            }
        }

        public int TickRate
        {
            get { lock (_lock) return _tickRate; }
        }

        public double TickDuration
        {
            get { lock (_lock) return _tickDuration; }
        }

        public void OnSyncResponse(double requestSentLocalTime, double responseReceivedLocalTime, double serverTimeAtSend, int tickRate)
        {
            double rtt = responseReceivedLocalTime - requestSentLocalTime;
            if (rtt < 0)
                rtt = 0;
            double oneWayLatency = rtt * 0.5;
            double estimatedServerTimeNow = serverTimeAtSend + oneWayLatency;
            double newOffset = estimatedServerTimeNow - responseReceivedLocalTime;
            lock (_lock)
            {
                if (tickRate > 0)
                {
                    _tickRate = tickRate;
                    _tickDuration = 1.0 / tickRate;
                }
                _smoothedRtt = _smoothedRtt < 0 ? rtt : (_smoothedRtt * 0.8) + (rtt * 0.2);
                if (!SimulationSynced)
                {
                    _offset = newOffset;
                    SimulationSynced = true;
                    return;
                }
                double error = newOffset - _offset;
                if (Math.Abs(error) > SnapThresholdSeconds)
                {
                    _offset = newOffset;
                }
                else
                {
                    _offset += error * Smoothing;
                }
            }
        }
    }
}