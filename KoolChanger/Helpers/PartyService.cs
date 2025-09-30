
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CSLOLTool.Models;
using CSLOLTool.Services;
using LCUSharp.Websocket;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;

namespace KoolChanger.Helpers
{
    public class PartyService
    {
        // Services
        private readonly LCUService _lcuService;
        private readonly LobbyService _lobbyService;
        private readonly List<Champion> _champions;

        // State
        private HubConnection? _lobbyConnection;
        private LobbyData? _currentLobby;
        private Dictionary<Champion, Skin> _savedSelectedSkins = new();
        public Dictionary<Champion, Skin> SelectedSkins { get; private set; } = new();
        public bool IsPartyModeEnabled { get; private set; } = false;

        // Events for UI updates
        public event Action<string>? OnLog;
        public event Action<string>? OnLobbyStatusUpdate;
        public event Action<string>? OnLobbyIdUpdate;
        public event Action<string>? OnMembersUpdate;
        public event Action<Dictionary<Champion, Skin>>? OnSkinsUpdated;
        public event Action? OnPartyModeEnabled;
        public event Action? OnPartyModeDisabled;
        public event Action<string, string>? OnError;


        public PartyService(LCUService lcuService, LobbyService lobbyService, List<Champion> champions)
        {
            _lcuService = lcuService;
            _lobbyService = lobbyService;
            _champions = champions;
        }

        public async Task EnableAsync(Dictionary<Champion, Skin> currentSkins)
        {
            if (!Process.GetProcessesByName("LeagueClient").Any())
            {
                OnError?.Invoke("Attention!", "Please launch league before enabling party mode");
                return;
            }

            IsPartyModeEnabled = true;
            BackupSelectedSkins(currentSkins);
            
            await _lcuService.ConnectAsync();

            if (_lcuService.Api != null)
            {
                var gameflowPhase = await _lcuService.Api.RequestHandler.GetJsonResponseAsync(HttpMethod.Get, "/lol-gameflow/v1/gameflow-phase");
                if (gameflowPhase != "\"None\"")
                {
                    try
                    {
                        _currentLobby = await _lobbyService.ExtractLobbyInfoAsync();
                        await ConnectToLobby(_currentLobby);
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"Error connecting to initial lobby: {ex.Message}");
                    }
                }
            }

            _lcuService.GameFlowChanged += OnGameFlowChanged;
            _lcuService.SubscrbeLobbyEvent();
            OnPartyModeEnabled?.Invoke();
        }

        public async Task DisableAsync()
        {
            IsPartyModeEnabled = false;
            RestoreSelectedSkins();

            if (_lobbyConnection != null)
            {
                if (_lobbyConnection.State == HubConnectionState.Connected)
                {
                    await _lobbyConnection.InvokeAsync("LeaveLobby");
                }
                await _lobbyConnection.DisposeAsync();
                _lobbyConnection = null;
            }

            if (_lcuService.Api != null)
            {
                _lcuService.GameFlowChanged -= OnGameFlowChanged;
                _lcuService.Api.Disconnect();
            }
            
            OnLobbyStatusUpdate?.Invoke("");
            OnLobbyIdUpdate?.Invoke("");
            OnMembersUpdate?.Invoke("");
            OnPartyModeDisabled?.Invoke();
        }

        private void BackupSelectedSkins(Dictionary<Champion, Skin> currentSkins)
        {
            _savedSelectedSkins = new Dictionary<Champion, Skin>(currentSkins);
            SelectedSkins = new Dictionary<Champion, Skin>();
            OnSkinsUpdated?.Invoke(SelectedSkins);
        }

        private void RestoreSelectedSkins()
        {
            SelectedSkins = _savedSelectedSkins;
            _savedSelectedSkins = new Dictionary<Champion, Skin>();
            OnSkinsUpdated?.Invoke(SelectedSkins);
        }

        private async void OnGameFlowChanged(object? sender, LeagueEvent e)
        {
            var data = e.Data.ToString();
            OnLog?.Invoke($"GameFlowStatus: {data}");

            if (string.IsNullOrEmpty(data)) return;

            if (data == "Lobby")
            {
                SelectedSkins = new Dictionary<Champion, Skin>();
                OnSkinsUpdated?.Invoke(SelectedSkins);
                _currentLobby = await _lobbyService.ExtractLobbyInfoAsync();
                await ConnectToLobby(_currentLobby);
            }
            else if (data == "None")
            {
                try
                {
                    if (_lobbyConnection != null)
                    {
                        await _lobbyConnection.InvokeAsync("LeaveLobby");
                        await _lobbyConnection.StopAsync();
                        OnLobbyStatusUpdate?.Invoke("Lobby status: disconnected");
                        OnLobbyIdUpdate?.Invoke("");
                        OnMembersUpdate?.Invoke("");
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Failed to leave SignalR lobby: {ex.Message}");
                }
            }
        }

        private async Task ConnectToLobby(LobbyData lobby)
        {
            try
            {
                _lobbyConnection = _lobbyService.CreateConnection();
                RegisterLobbyHandlers();
                await _lobbyConnection.StartAsync();
                await JoinOrCreateLobby(lobby);
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Error!", $"Failed to connect to lobby: {ex.Message}");
                OnLobbyStatusUpdate?.Invoke("Lobby status: disconnected");
                OnLobbyIdUpdate?.Invoke("");
                if (_lobbyConnection != null)
                {
                    await _lobbyConnection.StopAsync();
                }
            }
        }

        private void RegisterLobbyHandlers()
        {
            if (_lobbyConnection == null) return;

            _lobbyConnection.On<LobbyMember>("MemberJoined", async member =>
            {
                OnLog?.Invoke($"Member {member.Puuid} joined lobby");
                var members = await _lobbyConnection.InvokeAsync<List<LobbyMember>>("GetLobbyMembers", _currentLobby!.LobbyId);
                OnMembersUpdate?.Invoke($"Members count: {members.Count}");

                await SendSkinsAsync(SelectedSkins);
            });

            _lobbyConnection.On<string, string, string>("ReceiveMessage", (lobbyId, puuid, msg) =>
            {
                if (_currentLobby == null || puuid == _currentLobby.LocalMember.Puuid) return;

                var data = JsonConvert.DeserializeObject<Dictionary<int, Skin>>(msg);
                if (data == null) return;

                var skins = data.ToDictionary(
                    kvp => _champions.First(c => c.Id == kvp.Key),
                    kvp => kvp.Value
                );

                var merged = new Dictionary<Champion, Skin>(SelectedSkins);
                foreach (var pair in skins)
                {
                    merged[pair.Key] = pair.Value;
                }
                SelectedSkins = merged;
                OnSkinsUpdated?.Invoke(SelectedSkins);
            });

            _lobbyConnection.Closed += async error =>
            {
                OnLog?.Invoke("Lobby connection closed. Reconnecting...");
                await Task.Delay(new Random().Next(0, 5) * 1000);
                try
                {
                    await _lobbyConnection.StartAsync();
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Failed to reconnect: {ex.Message}");
                }
            };
        }

        private async Task JoinOrCreateLobby(LobbyData lobby)
        {
            if (_lobbyConnection == null) return;

            var lobbyFound = false;
            foreach (var member in lobby.Members)
            {
                OnLog?.Invoke($"Trying to connect to lobby {member.Puuid}");
                var result = await _lobbyConnection.InvokeAsync<bool>("JoinLobby", member.Puuid, lobby.LocalMember.Puuid);
                if (result)
                {
                    _currentLobby!.LobbyId = member.Puuid;
                    var members = await _lobbyConnection.InvokeAsync<List<LobbyMember>>("GetLobbyMembers", _currentLobby.LobbyId);

                    OnLobbyStatusUpdate?.Invoke("Lobby status: connected");
                    OnLobbyIdUpdate?.Invoke($"Lobby id: {member.Puuid}");
                    OnMembersUpdate?.Invoke($"Members count: {members.Count}");
                    OnLog?.Invoke($"Lobby found! Id: {member.Puuid}");
                    lobbyFound = true;
                    break; 
                }
            }

            if (!lobbyFound)
            {
                OnLog?.Invoke("Lobby not found, creating...");
                await _lobbyConnection.InvokeAsync("CreateLobby", lobby.LocalMember.Puuid, lobby.LocalMember.Puuid);
                _currentLobby!.LobbyId = lobby.LocalMember.Puuid;
                
                OnLobbyStatusUpdate?.Invoke("Lobby status: created");
                OnLobbyIdUpdate?.Invoke($"Lobby id: {lobby.LocalMember.Puuid}");
                OnMembersUpdate?.Invoke("Members count: 1");
            }
        }

        public async Task SendSkinsAsync(Dictionary<Champion, Skin> skins)
        {
            // Update the service's state first
            SelectedSkins = skins;

            if (_lobbyConnection == null || _lobbyConnection.State != HubConnectionState.Connected || _currentLobby == null)
            {
                return;
            }

            var data = SelectedSkins.ToDictionary(kvp => kvp.Key.Id, kvp => kvp.Value);
            try
            {
                await _lobbyConnection.InvokeAsync("SendMessage", _currentLobby.LobbyId, JsonConvert.SerializeObject(data));
            }
            catch (Exception ex)
            {
                OnError?.Invoke("Error!", "Error sending skins: " + ex.Message);
            }
        }
    }
}
