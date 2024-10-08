using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Grenache.Interfaces;
using Grenache.Models.PeerRPC;

namespace Grenache
{
  public delegate Task<bool> RpcResponseHandler(RpcServerResponse response);

  public delegate Task RpcRequestHandler(RpcServerRequest request, RpcResponseHandler response);


  public abstract class PeerRPCServer : IRPCServer
  {
    protected Link Link { get; }
    public string Service { get; protected set; }
    public int Port { get; protected set; }

    protected event RpcRequestHandler RequestReceived;
    protected List<RpcRequestHandler> RequestHandler { get; }
    public Task ListenerTask { get; protected set; }

    protected Timer AnnounceInterval { get; set; }
    protected int AnnouncePeriod { get; }

    public PeerRPCServer(Link link, int announcePeriod = 120 * 1000)
    {
      Link = link;
      AnnouncePeriod = announcePeriod;
      RequestHandler = new List<RpcRequestHandler>();
    }

    public async Task<bool> Listen(string service, int port)
    {
      Service = service;
      Port = port;

      var started = await StartServer();
      if (!started) return false;

      AnnounceInterval = new Timer(async _ => { await Link.Announce(Service, Port); }, null, 0, AnnouncePeriod);

      return true;
    }

    public async Task Close()
    {
      if (AnnounceInterval != null) await AnnounceInterval.DisposeAsync();
      await StopServer();
      foreach (var handler in RequestHandler)
      {
        RequestReceived -= handler;
      }

      RequestHandler.Clear();
    }

    public void AddRequestHandler(RpcRequestHandler handler)
    {
      RequestReceived += handler;
      RequestHandler.Add(handler);
    }

    public void RemoveRequestHandler(RpcRequestHandler handler)
    {
      RequestReceived -= handler;
      RequestHandler.Remove(handler);
    }

    protected virtual async Task OnRequestReceived(RpcServerRequest request)
    {
      if (RequestReceived != null)
      {
        var invocationList = RequestReceived.GetInvocationList();

        foreach (var handler in invocationList)
        {
            var rpcHandler = (RpcRequestHandler)handler;
            await rpcHandler(request, SendResponse);
        }
      }
    }

    protected abstract Task<bool> StartServer();
    protected abstract Task StopServer();
    protected abstract Task<bool> SendResponse(RpcServerResponse response);
  }
}
