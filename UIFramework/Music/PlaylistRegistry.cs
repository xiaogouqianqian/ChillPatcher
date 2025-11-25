using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChillPatcher.UIFramework.Core;

namespace ChillPatcher.UIFramework.Music
{
    /// <summary>
    /// 歌单注册表实现
    /// </summary>
    public class PlaylistRegistry : IPlaylistRegistry, IDisposable
    {
        private readonly Dictionary<string, IPlaylistProvider> _playlists;
        private bool _disposed = false;

        public event Action<PlaylistChangedEventArgs> OnPlaylistChanged;

        public PlaylistRegistry()
        {
            _playlists = new Dictionary<string, IPlaylistProvider>();
        }

        public void RegisterPlaylist(string id, IPlaylistProvider provider)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Playlist ID cannot be null or empty", nameof(id));

            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (_playlists.ContainsKey(id))
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"Playlist '{id}' already registered, replacing...");
                UnregisterPlaylist(id);
            }

            _playlists[id] = provider;

            // 订阅歌单更新事件
            provider.OnPlaylistUpdated += () => OnProviderUpdated(id, provider);

            OnPlaylistChanged?.Invoke(new PlaylistChangedEventArgs
            {
                PlaylistId = id,
                ChangeType = PlaylistChangeType.Added,
                Provider = provider
            });

            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"Registered playlist: {id} ({provider.DisplayName})");
        }

        public void UnregisterPlaylist(string id)
        {
            if (!_playlists.TryGetValue(id, out var provider))
                return;

            // 取消订阅事件
            provider.OnPlaylistUpdated -= () => OnProviderUpdated(id, provider);

            _playlists.Remove(id);

            OnPlaylistChanged?.Invoke(new PlaylistChangedEventArgs
            {
                PlaylistId = id,
                ChangeType = PlaylistChangeType.Removed,
                Provider = provider
            });

            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"Unregistered playlist: {id}");
        }

        public IPlaylistProvider GetPlaylist(string id)
        {
            return _playlists.TryGetValue(id, out var provider) ? provider : null;
        }

        public IReadOnlyDictionary<string, IPlaylistProvider> GetAllPlaylists()
        {
            return _playlists;
        }

        public async void RefreshPlaylist(string id)
        {
            if (!_playlists.TryGetValue(id, out var provider))
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"Playlist '{id}' not found");
                return;
            }

            try
            {
                await provider.BuildPlaylist();
                OnPlaylistChanged?.Invoke(new PlaylistChangedEventArgs
                {
                    PlaylistId = id,
                    ChangeType = PlaylistChangeType.Updated,
                    Provider = provider
                });
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"Failed to refresh playlist '{id}': {ex}");
            }
        }

        public async void RefreshAll()
        {
            var tasks = _playlists.Values
                .Select(async provider =>
                {
                    try
                    {
                        await provider.BuildPlaylist();
                    }
                    catch (Exception ex)
                    {
                        BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"Failed to refresh playlist '{provider.Id}': {ex}");
                    }
                });

            await Task.WhenAll(tasks);

            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo("All playlists refreshed");
        }

        private void OnProviderUpdated(string id, IPlaylistProvider provider)
        {
            OnPlaylistChanged?.Invoke(new PlaylistChangedEventArgs
            {
                PlaylistId = id,
                ChangeType = PlaylistChangeType.Updated,
                Provider = provider
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            foreach (var provider in _playlists.Values)
            {
                provider.OnPlaylistUpdated -= () => OnProviderUpdated(provider.Id, provider);
            }

            _playlists.Clear();
            _disposed = true;
        }
    }
}

