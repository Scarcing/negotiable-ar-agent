using System;
using System.IO;
using UnityEngine;

namespace NegotiableAR
{
    public sealed class SessionLog : IDisposable
    {
        [Serializable] class Entry { public string utc, sessionId, kind, payload; public double monotonic; }
        readonly StreamWriter writer;
        readonly string sessionId = Guid.NewGuid().ToString("N");
        public string Path { get; }
        public SessionLog()
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "Sessions");
            Directory.CreateDirectory(dir);
            Path = System.IO.Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + sessionId + ".jsonl");
            writer = new StreamWriter(Path) { AutoFlush = true };
        }
        public void Write(string kind, string payload) => writer.WriteLine(JsonUtility.ToJson(new Entry { utc = DateTime.UtcNow.ToString("O"), sessionId = sessionId, monotonic = Time.realtimeSinceStartupAsDouble, kind = kind, payload = payload }));
        public void Dispose() => writer.Dispose();
    }
}
