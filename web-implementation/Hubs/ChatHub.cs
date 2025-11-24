using Microsoft.AspNetCore.SignalR;

namespace GrapheneTrace.Web.Hubs;

public class ChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    public async Task SendPrivateMessage(string receiverUserId, string message)
    {
        await Clients.User(receiverUserId).SendAsync("ReceiveMessage", Context.UserIdentifier, message);
    }
}
