using SharpDivert;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static SharpDivert.WinDivert;

namespace PacketMonitor
{
    public class MonitorServer
    {
        private const int PACKET_LATENCY = 0;
        private readonly WinDivert divert = new("outbound and tcp and ((ip.SrcAddr == 127.0.0.1 and tcp.SrcPort == 42447) or (ip.SrcAddr == 127.0.0.1 and tcp.SrcPort == 42372) or (ip.SrcAddr == 127.0.0.1 and tcp.SrcPort == 42371) or (ip.SrcAddr == 127.0.0.1 and tcp.SrcPort == 42368) or (ip.SrcAddr == 127.0.0.1 and tcp.SrcPort == 42367) or (ip.SrcAddr == 127.0.0.1 and tcp.SrcPort == 39574) or (ip.SrcAddr == 127.0.0.1 and tcp.SrcPort == 39572) or (ip.SrcAddr == 45.92.61.3 and tcp.SrcPort == 42396) or (ip.SrcAddr == 0.0.0.0 and tcp.SrcPort == 9264) or (ip.SrcAddr == 0.0.0.0 and tcp.SrcPort == 27036))" +
            "", WinDivert.Layer.Network, 0, Flag.Drop); // (udp.SrcPort > 1000 or tcp.SrcPort > 1000) // and (udp.DstPort == 8443 or tcp.DstPort == 8443 or udp.SrcPort == 8443 or tcp.SrcPort == 8443)

        private CancellationTokenSource tokenSource = new();
        private readonly ConcurrentDictionary<IPAddress, (int, string)> cache = [];
        private readonly ConcurrentDictionary<Stopwatch, (Memory<byte> rec, Memory<WinDivertAddress> snd)> resendBuffer = [];
        private readonly System.Timers.Timer ResendExecutionTimer = new(1) { AutoReset = true };
        private Task? ReadTask;

        public void Start()
        {
            this.ResendExecutionTimer.Elapsed += (_, _) => ResendAsync();
            this.ResendExecutionTimer.Start();
            // this.ReadTask = Task.Run(ReadAync, this.tokenSource.Token);
        }

        public void Stop()
        {
            this.ResendExecutionTimer.Stop();
            resendBuffer.Clear();
            tokenSource.Cancel();

            tokenSource = new();
        }

        private readonly AutoResetEvent newIpEvent = new(false);
        private static readonly IPAddress[] badIps = [IPAddress.Any, IPAddress.Loopback, IPAddress.None];
        
        private void ReadAync()
        {
            Task.Run(LogAsync, tokenSource.Token);

            var recvBuf = new Memory<byte>(new byte[divert.QueueSize]);
            var sendBuf = new Memory<WinDivertAddress>(new WinDivertAddress[1]);

            while (!tokenSource.IsCancellationRequested)
            {
                var (recvLen, addrLen) = divert.RecvEx(recvBuf.Span, sendBuf.Span);

                var recv = recvBuf[..(int)recvLen];
                var send = sendBuf[..(int)addrLen];
                var (_, result) = new WinDivertIndexedPacketParser(recv).First();
                
                {
                    int port = 0;
                    string ip = string.Empty;
                    unsafe
                    {
                        if (result.TCPHdr is not null)
                        {
                            port = result.TCPHdr->DstPort;
                        }
                        else if (result.UDPHdr is not null)
                        {
                            port = result.UDPHdr->DstPort;
                        }
                        if (result.IPv4Hdr is not null)
                        {
                            ip = result.IPv4Hdr->DstAddr.ToString();
                        }
                        else if (result.IPv6Hdr is not null)
                        {
                            ip = result.IPv6Hdr->DstAddr.ToString();
                        }
                    }

                    var smlpkt = result.Packet.Slice(0, Math.Min(result.Packet.Length, 40));
                    var (IpAddress, IpType) = GetDestinationIP(smlpkt);

                    if (cache.TryAdd(IPAddress.Parse(ip), (port, string.Empty)) || cache.TryAdd(IpAddress, (port, string.Empty)))
                        newIpEvent.Set();
                }

                {
                    int port = 0;
                    string ip = string.Empty;
                    unsafe
                    {
                        if (result.TCPHdr is not null)
                        {
                            port = result.TCPHdr->SrcPort;
                        }
                        else if (result.UDPHdr is not null)
                        {
                            port = result.UDPHdr->SrcPort;
                        }
                        if (result.IPv4Hdr is not null)
                        {
                            ip = result.IPv4Hdr->SrcAddr.ToString();
                        }
                        else if (result.IPv6Hdr is not null)
                        {
                            ip = result.IPv6Hdr->SrcAddr.ToString();
                        }
                    }

                    var smlpkt = result.Packet.Slice(0, Math.Min(result.Packet.Length, 40));
                    var (IpAddress, IpType) = GetDestinationIP(smlpkt);

                    if (cache.TryAdd(IPAddress.Parse(ip), (port, string.Empty)) || cache.TryAdd(IpAddress, (port, string.Empty)))
                        newIpEvent.Set();
                }

                //if (IpType != 4)
                //    goto End;
                /*
                 * if (cache.TryGetValue(IpAddress, out _) && PACKET_LATENCY > 0 /*&& hstnm.Contains("valve", StringComparison.InvariantCultureIgnoreCase)*)
                    {
                    this.resendBuffer.TryAdd(Stopwatch.StartNew(), (recv, send));
                    continue;
                }
                */
                goto End;
            End:
                _ = divert.SendEx(recv.Span, send.Span);
            }
        }

        private void ResendAsync()
        {
            foreach(var packet in this.resendBuffer.Where(_ => _.Key.ElapsedMilliseconds > PACKET_LATENCY).ToArray())
                if (this.resendBuffer.TryRemove(packet.Key, out var pck))
                    _ = this.divert.SendEx(pck.rec.Span, pck.snd.Span);
        }

        public static (IPAddress IpAddress, int IpType) GetDestinationIP(Memory<byte> packetData)
        {
            if (packetData.Length < 20) // Minimum length check
            {
                throw new ArgumentException("Insufficient packet data length.");
            }

            Span<byte> packet = packetData.Span;
            byte version = (byte)(packet[0] >> 4); // Get the IP version from the first 4 bits
            IPAddress ip;

            if (version == 4) // IPv4
            {
                if (packet.Length < 20) // Minimum IPv4 header length
                {
                    throw new ArgumentException("Insufficient IPv4 packet length.");
                }
                var destinationIP = new IPAddress(packet.Slice(16, 4).ToArray()); // Bytes 16-19 for IPv4 destination address
                var sourceIP = new IPAddress(packet.Slice(12, 4).ToArray()); // Bytes 12-15 for IPv4 source address
                ip = (destinationIP.Equals(IPAddress.Loopback) || destinationIP.Equals(IPAddress.Any)) ? sourceIP : destinationIP;
            }
            else if (version == 6) // IPv6
            {
                var destinationIP = new IPAddress(packet.Slice(24, 16).ToArray()); // Bytes 24-39 for IPv6 destination address
                var sourceIP = new IPAddress(packet.Slice(8, 16).ToArray()); // Bytes 8-23 for IPv6 source address
                ip = (destinationIP.Equals(IPAddress.IPv6Loopback) || destinationIP.Equals(IPAddress.IPv6None)) ? sourceIP : destinationIP;
            }
            else
            {
                throw new ArgumentException("Unknown IP version.");
            }

            return (ip, version);
        }

        private void LogAsync()
        {
            while(!tokenSource.IsCancellationRequested)
            {
                newIpEvent.WaitOne();
                Console.Clear();

                foreach (var (ip, (port, hstnm)) in cache)
                {
                    if (ip == null || ip == default || badIps.Contains(ip))
                        continue;
                    
                    if (string.IsNullOrWhiteSpace(hstnm))
                        Dns.GetHostEntryAsync(ip).ContinueWith(entry => cache[ip] = (port, $"{entry.Result.HostName}; {string.Join(", ", entry.Result.Aliases)}"));

                    //if (hstnm.Contains("valve", StringComparison.InvariantCultureIgnoreCase))
                    if (hstnm.Length > 0)
                        Console.WriteLine($"{ip}:{port} => {hstnm}");
                }
            }
        }
    }
}
