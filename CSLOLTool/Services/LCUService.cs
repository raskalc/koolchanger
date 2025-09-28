using LCUSharp;
using LCUSharp.Websocket;

namespace CSLOLTool.Services;

public class LCUService
{
    public LeagueClientApi? Api;
    public event EventHandler<LeagueEvent>? GameFlowChanged;

    public async Task ConnectAsync()
    {
        Api = await LeagueClientApi.ConnectAsync();
    }

    public void SubscrbeLobbyEvent()
    {
        Api?.EventHandler.Subscribe("/lol-gameflow/v1/gameflow-phase", GameFlowChanged);
    }
}