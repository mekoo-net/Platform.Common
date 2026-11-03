using System.Net;
using System.Net.Sockets;

namespace Platform.Common.Discovery;

/// <summary>注册时对外发布的主机：本机通往 Consul 的那块网卡的地址。</summary>
public static class ConsulRoute
{
    /// <summary>
    /// 本机通往 <paramref name="consul"/> 的那块网卡的 IPv4。UDP 的 Connect 只让内核选路由、填本端地址，不发包，
    /// Consul 此刻不在线也取得到。不取「第一块非回环网卡」：容器挂多张网、宿主机有 docker0 / wg0 时，
    /// 枚举顺序和 Consul 能拨到哪张没有关系。
    /// </summary>
    /// <remarks>
    /// 注册地址必须是 Consul 拨得回来的地址，hostname / localhost 一律不用，也不留退路——取不到路由
    /// （Consul 主机名在本机解析不了）就抛。退回 hostname 注册出去的是一个注定被摘掉的地址，服务看着在跑、
    /// 调用方却找不到它，比起不来难查得多。回环只在 Consul 自己也在本机回环上（单机开发）时出现，那时它拨得回来。
    /// </remarks>
    public static IPAddress SourceAddress(Uri consul)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(consul.Host, consul.Port);
            return ((IPEndPoint)socket.LocalEndPoint!).Address;
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException(
                $"取不到本机连 Consul（{consul.Authority}）时用的源 IP，推不出注册地址。检查 Consul 的主机名在本机能否解析。", ex);
        }
    }
}
