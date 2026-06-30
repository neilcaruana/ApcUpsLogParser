using Microsoft.AspNetCore.SignalR;

namespace ApcUpsLogParser.Web.Hubs;

public class VoltageHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", DateTime.UtcNow);
        await base.OnConnectedAsync();
    }
}
