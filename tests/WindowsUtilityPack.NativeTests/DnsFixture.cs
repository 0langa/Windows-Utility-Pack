using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WindowsUtilityPack.NativeTests;

internal sealed class DnsFixture : IDisposable
{
    internal System.Collections.Concurrent.ConcurrentQueue<string> Queries { get; } = new();
    private readonly UdpClient socket = new(new IPEndPoint(IPAddress.Loopback, 53));
    private readonly CancellationTokenSource stop = new();
    private readonly Task loop;
    internal DnsFixture()
    {
        loop = Task.Run(async () => {
            while (!stop.IsCancellationRequested)
            {
                UdpReceiveResult incoming;
                try { incoming = await socket.ReceiveAsync(stop.Token); }
                catch (OperationCanceledException) { break; }
                var query = incoming.Buffer;
                if (query.Length < 17) continue;
                var end = 12; var labels = new List<string>();
                while (end < query.Length && query[end] != 0) { var length = query[end++]; if (end + length >= query.Length) break; labels.Add(Encoding.ASCII.GetString(query, end, length)); end += length; }
                end++;
                if (end + 4 > query.Length) continue;
                var type = BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(end)); var found = string.Join('.', labels).Equals("native.wup.test", StringComparison.OrdinalIgnoreCase);
                Queries.Enqueue($"{string.Join('.', labels)} type={type}");
                byte[] value = type switch {
                    1 => [127, 0, 0, 42], 28 => IPAddress.IPv6Loopback.GetAddressBytes(),
                    5 => Name("alias.wup.test"), 15 => [0, 10, .. Name("mail.wup.test")],
                    16 => Text("native-dns-fixture!"), _ => [] };
                using var output = new MemoryStream();
                output.Write(query.AsSpan(0, 2)); output.Write([0x81, found ? (byte)0x80 : (byte)0x83, 0, 1, 0, found ? (byte)1 : (byte)0, 0, 0, 0, 0]);
                output.Write(query.AsSpan(12, end + 4 - 12));
                if (found) { output.Write([0xc0, 0x0c]); output.Write(query.AsSpan(end, 2)); output.Write([0, 1, 0, 0, 0, 60, (byte)(value.Length >> 8), (byte)value.Length]); output.Write(value); }
                await socket.SendAsync(output.ToArray(), incoming.RemoteEndPoint, stop.Token);
            }
        });
    }
    private static byte[] Name(string name) => name.Split('.').SelectMany(label => new[] { (byte)label.Length }.Concat(Encoding.ASCII.GetBytes(label))).Append((byte)0).ToArray();
    private static byte[] Text(string value) { var bytes = Encoding.UTF8.GetBytes(value); return [(byte)bytes.Length, .. bytes]; }
    public void Dispose() { stop.Cancel(); socket.Dispose(); try { loop.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { } stop.Dispose(); }
}
