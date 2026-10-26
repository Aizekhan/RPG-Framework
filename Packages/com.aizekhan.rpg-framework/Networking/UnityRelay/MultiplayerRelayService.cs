using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using MpsRole = Unity.Services.Multiplayer.NetworkRole;
using MpsSessionOptions = Unity.Services.Multiplayer.SessionOptions;

namespace RPGFramework.Networking.UnityRelay
{
    /// <summary>
    /// Rooms and relay allocations from Unity Multiplayer Services: private sessions joined by code, Relay over DTLS,
    /// players signed in anonymously. The SDK's own netcode integration is replaced by a network handler that hands
    /// the relay data to the game's transport, so Netcode for GameObjects is not needed.
    /// </summary>
    public sealed class MultiplayerRelayService : IRelayService
    {
        private ISession _session;

        public string SetupProblem => string.IsNullOrEmpty(Application.cloudProjectId) ? RelayErrors.NotSetUp : null;

        public async UniTask<string> CreateAsync(int maxPlayers, string name, Func<RelayServerData, UniTask> startNetwork,
            CancellationToken cancellation)
        {
            await Run(async () =>
            {
                await SignInAsync();
                var options = new MpsSessionOptions
                {
                    MaxPlayers = maxPlayers,
                    Name = string.IsNullOrWhiteSpace(name) ? "Co-op" : name,
                    IsPrivate = true
                }.WithRelayNetwork().WithNetworkHandler(new Handler(startNetwork));
                _session = await MultiplayerService.Instance.CreateSessionAsync(options, cancellation);
            }, null);
            return _session.Code;
        }

        public UniTask JoinAsync(string code, Func<RelayServerData, UniTask> startNetwork, CancellationToken cancellation) =>
            Run(async () =>
            {
                await SignInAsync();
                var options = new JoinSessionOptions().WithNetworkHandler(new Handler(startNetwork));
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options, cancellation);
            }, code);

        public async UniTask LeaveAsync()
        {
            var session = _session;
            _session = null;
            if (session == null)
                return;
            try
            {
                if (session.IsHost)
                    await session.AsHost().DeleteAsync();
                else
                    await session.LeaveAsync();
            }
            catch (Exception exception) when (exception is SessionException || exception is RequestFailedException)
            {
                // The room expires on its own; leaving is best effort
                Debug.LogWarning($"Unity Relay: leaving the room failed: {exception.Message}");
            }
        }

        private static async Task SignInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static async UniTask Run(Func<Task> call, string code)
        {
            try
            {
                await call();
            }
            catch (NetworkSessionException)
            {
                throw;
            }
            catch (Exception exception) when (FromTheGame(exception) != null)
            {
                // The game's own transport failed inside the SDK's network start: its message says what happened
                throw FromTheGame(exception);
            }
            catch (SessionException exception)
            {
                throw new NetworkSessionException(RelayErrors.Explain(exception.Error, code), exception);
            }
            catch (AuthenticationException exception)
            {
                throw new NetworkSessionException($"Could not sign in to Unity's online services: {exception.Message}", exception);
            }
            catch (RequestFailedException exception)
            {
                throw new NetworkSessionException(RelayErrors.Offline, exception);
            }
            catch (ServicesInitializationException exception)
            {
                throw new NetworkSessionException(RelayErrors.NotSetUp, exception);
            }
        }

        private static NetworkSessionException FromTheGame(Exception exception)
        {
            for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
            {
                if (inner is NetworkSessionException problem)
                    return problem;
            }
            return null;
        }

        /// <summary>
        /// Receives the relay data when the SDK starts the session's network and opens the game's transport with it.
        /// </summary>
        private sealed class Handler : INetworkHandler
        {
            private readonly Func<RelayServerData, UniTask> _startNetwork;

            public Handler(Func<RelayServerData, UniTask> startNetwork)
            {
                _startNetwork = startNetwork;
            }

            public Task StartAsync(NetworkConfiguration configuration) => StartAsync(configuration, CancellationToken.None);

            public Task StartAsync(NetworkConfiguration configuration, CancellationToken cancellationToken) =>
                _startNetwork(configuration.Role == MpsRole.Client ? configuration.RelayClientData : configuration.RelayServerData)
                    .AsTask();

            // The game owns the transport and closes it itself
            public Task StopAsync() => Task.CompletedTask;
        }
    }
}
