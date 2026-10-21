using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OptimizationTool.Services
{
    public static class DiscordRpc
    {
        private const string ClientId = "1561484591292879069";
        private static NamedPipeClientStream? pipeClient;
        private static Thread? rpcThread;
        private static volatile bool running;
        private static volatile string currentDetails = "Optimizing Windows..";
        private static volatile string currentState = "Loading..";
        private static long startTimestamp;

        public static void Initialize()
        {
            if (rpcThread != null && rpcThread.IsAlive) return;

            startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            running = true;
            rpcThread = new Thread(RpcLoop) { IsBackground = true };
            rpcThread.Start();
        }

        public static void UpdatePresence(string details, string state)
        {
            currentDetails = details;
            currentState = state;
        }

        private static void RpcLoop()
        {
            while (running)
            {
                try
                {
                    if (pipeClient == null || !pipeClient.IsConnected)
                    {
                        if (!Connect())
                        {
                            Disconnect();
                            WaitInterval();
                            continue;
                        }
                    }

                    SendPresence();

                    if (ReadPacket(out int op, out string json))
                    {
                        Debug.WriteLine($"[DiscordRpc] op={op} {json}");
                        if (op == 2) Disconnect(); 
                    }
                    else
                    {
                        Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DiscordRpc] Exception: {ex.Message}");
                    Disconnect();
                }

                WaitInterval();
            }
        }

        private static void WaitInterval()
        {
            for (int i = 0; i < 15 && running; i++)
                Thread.Sleep(1000);
        }

        private static bool Connect()
        {
            for (int i = 0; i < 10 && running; i++)
            {
                try
                {
                    pipeClient = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
                    pipeClient.Connect(500);
                    break;
                }
                catch
                {
                    pipeClient?.Dispose();
                    pipeClient = null;
                }
            }

            if (pipeClient == null || !pipeClient.IsConnected) return false;

            SendPacket(0, JsonSerializer.Serialize(new { v = 1, client_id = ClientId }));
            if (!ReadPacket(out int op, out string json))
                return false;

            Debug.WriteLine($"[DiscordRpc] Handshake reply op={op} {json}");
            return op == 1 && json.Contains("READY");
        }

        private static void SendPresence()
        {
            var payload = new
            {
                cmd = "SET_ACTIVITY",
                args = new
                {
                    pid = Environment.ProcessId,
                    activity = new
                    {
                        details = currentDetails,
                        state = currentState,
                        timestamps = new { start = startTimestamp }
                    }
                },
                nonce = Guid.NewGuid().ToString()
            };

            SendPacket(1, JsonSerializer.Serialize(payload));
        }

        private static void SendPacket(int opCode, string jsonPayload)
        {
            if (pipeClient == null || !pipeClient.IsConnected) return;

            byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(opCode);
            writer.Write(payloadBytes.Length);
            writer.Write(payloadBytes);

            byte[] packet = ms.ToArray();
            pipeClient.Write(packet, 0, packet.Length);
            pipeClient.Flush();
        }

        private static bool ReadPacket(out int op, out string json, int timeoutMs = 3000)
        {
            op = -1;
            json = string.Empty;

            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);

                while (true)
                {
                    byte[] header = new byte[8];
                    if (!ReadExact(header, cts.Token)) return false;

                    op = BitConverter.ToInt32(header, 0);
                    int len = BitConverter.ToInt32(header, 4);

                    byte[] body = new byte[len];
                    if (!ReadExact(body, cts.Token)) return false;
                    json = Encoding.UTF8.GetString(body);

                    if (op == 3) 
                    {
                        SendPacket(4, json);
                        continue;
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool ReadExact(byte[] buffer, CancellationToken ct)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int n = pipeClient!.ReadAsync(buffer, offset, buffer.Length - offset, ct).GetAwaiter().GetResult();
                if (n == 0) return false;
                offset += n;
            }
            return true;
        }

        private static void Disconnect()
        {
            try { pipeClient?.Dispose(); } catch { }
            pipeClient = null;
        }

        public static void Shutdown()
        {
            running = false;
            Disconnect();
        }
    }
}