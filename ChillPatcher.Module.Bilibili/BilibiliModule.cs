using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ChillPatcher.SDK;
using ChillPatcher.SDK.Attributes;
using ChillPatcher.SDK.Events;
using ChillPatcher.SDK.Interfaces;
using ChillPatcher.SDK.Models;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx;
using BepInEx.Configuration;

namespace ChillPatcher.Module.Bilibili
{
    [MusicModule("com.chillpatcher.bilibili", "Bilibili Music",
        Version = "1.0.0",
        Author = "xgqq",
        Description = "Streaming via FFmpeg")]
    public class BilibiliModule : IMusicModule, IStreamingMusicSourceProvider, ICoverProvider
    {
        public string ModuleId => "com.chillpatcher.bilibili";
        public string DisplayName => "Bilibili Music";
        public string Version => "1.0.0";
        public int Priority => 10;
        public ModuleCapabilities Capabilities => new ModuleCapabilities { CanFavorite = true, ProvidesCover = true };
        public MusicSourceType SourceType => MusicSourceType.Stream;

        public bool IsReady => true;
        public event Action<bool> OnReadyStateChanged;

        private IModuleContext _context;
        private BilibiliBridge _bridge;
        private QRLoginManager _qrManager;
        private BilibiliSongRegistry _registry;
        private string _currentLoginUuid;
        private string _ffmpegPath;

        private Dictionary<string, string> _albumCoverUrls = new Dictionary<string, string>();
        private Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        public async Task InitializeAsync(IModuleContext context)
        {
            _context = context;
            string dataPath = Path.Combine(Application.persistentDataPath, "ChillPatcher", ModuleId);
            Directory.CreateDirectory(dataPath);

            string dllFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _ffmpegPath = Path.Combine(dllFolder, "ffmpeg.exe");

            // === 读取配置 ===
            int pageDelay = 300;
            try
            {
                string configPath = Path.Combine(Paths.ConfigPath, "com.chillpatcher.plugin.cfg");
                var configFile = new ConfigFile(configPath, true);

                var delayEntry = configFile.Bind(
                    "Module:com.chillpatcher.bilibili",
                    "PageLoadDelay",
                    300,
                    "翻页加载延迟(毫秒)。过低可能导致412错误，建议保持在300以上。"
                );
                configFile.Save();

                pageDelay = delayEntry.Value;
                context.Logger.LogInfo($"[{DisplayName}] 读取配置: 翻页延迟 = {pageDelay}ms");
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning($"[{DisplayName}] 配置文件读写失败，使用默认值 300ms: {ex.Message}");
            }

            if (!File.Exists(_ffmpegPath))
                context.Logger.LogError($"[{DisplayName}] ❌ 缺少 ffmpeg.exe！请放入: {dllFolder}");
            else
                context.Logger.LogInfo($"[{DisplayName}] FFmpeg 就绪");

            _bridge = new BilibiliBridge(context.Logger, dataPath, pageDelay);
            _registry = new BilibiliSongRegistry(context, ModuleId);
            _qrManager = new QRLoginManager(_bridge, context.Logger);

            _qrManager.OnLoginSuccess += async () => {
                _registry.UpdateLoginSongTitle("登录成功！正在同步...");
                await RefreshAsync();
            };
            _qrManager.OnStatusChanged += (msg) => _registry.UpdateLoginSongTitle(msg);
            _qrManager.OnQRCodeReady += () => {
                if (!string.IsNullOrEmpty(_currentLoginUuid))
                    _context.EventBus.Publish(new CoverInvalidatedEvent { MusicUuid = _currentLoginUuid, Reason = "QR" });
            };

            if (_bridge.IsLoggedIn)
            {
                context.Logger.LogInfo($"Bilibili 已登录: {_bridge.CurrentUserId}");
                await RefreshAsync();
            }
            else
            {
                RefreshLoginSong();
            }
            OnReadyStateChanged?.Invoke(true);
        }

        public async Task<PlayableSource> ResolveAsync(string uuid, AudioQuality quality, CancellationToken token = default)
        {
            if (uuid == _currentLoginUuid || uuid.Contains("bili_login_action"))
            {
                _context.Logger.LogInfo("触发登录流程...");
                _qrManager.StartLogin();
                return PlayableSource.FromPcmStream(uuid, new SilentPcmReader(), AudioFormat.Mp3);
            }

            if (!File.Exists(_ffmpegPath))
            {
                _context.Logger.LogError("FFmpeg 丢失，无法播放");
                return null;
            }

            var music = _context.MusicRegistry.GetMusic(uuid);
            if (music == null) return null;

            var url = await _bridge.GetPlayUrlAsync(music.SourcePath);
            if (string.IsNullOrEmpty(url)) return null;

            _context.Logger.LogInfo($"[Stream] 启动 FFmpeg 流: {music.Title}");
            return PlayableSource.FromPcmStream(uuid, new FFmpegStreamReader(_ffmpegPath, url, music.Duration), AudioFormat.Mp3);
        }

        public Task<PlayableSource> RefreshUrlAsync(string u, AudioQuality q, CancellationToken t) => ResolveAsync(u, q, t);
        private void RefreshLoginSong()
        {
            _registry.RegisterLoginSong(">>> 点击播放以扫码 <<<");
            _currentLoginUuid = BilibiliSongRegistry.UUID_LOGIN;
        }

        public async Task<List<MusicInfo>> GetMusicListAsync()
        {
            var list = new List<MusicInfo>();
            if (!_bridge.IsLoggedIn) return list;

            _albumCoverUrls.Clear();
            _spriteCache.Clear();

            var folders = await _bridge.GetMyFoldersAsync();
            foreach (var f in folders)
            {
                var videos = await _bridge.GetFolderVideosAsync(f.Id);

                if (videos.Count > 0 && !string.IsNullOrEmpty(videos[0].CoverUrl))
                {
                    string albumId = $"bili_album_{f.Id}";
                    string coverUrl = videos[0].CoverUrl;
                    if (coverUrl.StartsWith("http://")) coverUrl = coverUrl.Replace("http://", "https://");
                    _albumCoverUrls[albumId] = coverUrl;

                    _context.EventBus.Publish(new CoverInvalidatedEvent { AlbumId = albumId, Reason = "FolderLoaded" });
                }

                _registry.RegisterFolder(f, videos);

                foreach (var v in videos)
                {
                    string uuid = MusicInfo.GenerateUUID("bili_" + v.Bvid);
                    var registeredMusic = _context.MusicRegistry.GetMusic(uuid);
                    if (registeredMusic != null) list.Add(registeredMusic);
                }
            }
            return list;
        }

        public async Task<Sprite> GetAlbumCoverAsync(string albumId)
        {
            if (_albumCoverUrls.TryGetValue(albumId, out string url))
                return await DownloadSpriteAsync(url);
            return _context.DefaultCover.DefaultAlbumCover;
        }

        public async Task<Sprite> GetMusicCoverAsync(string uuid)
        {
            if (uuid == _currentLoginUuid) return _qrManager?.QRCodeSprite ?? _context.DefaultCover.DefaultMusicCover;
            var music = _context.MusicRegistry.GetMusic(uuid);
            if (music?.ExtendedData is string url && !string.IsNullOrEmpty(url))
                return await DownloadSpriteAsync(url);
            return _context.DefaultCover.DefaultMusicCover;
        }

        private async Task<Sprite> DownloadSpriteAsync(string url)
        {
            if (_spriteCache.TryGetValue(url, out var cachedSprite) && cachedSprite != null)
                return cachedSprite;

            try
            {
                if (url.StartsWith("http://")) url = url.Replace("http://", "https://");
                using (var req = UnityWebRequestTexture.GetTexture(url))
                {
                    req.SendWebRequest();
                    while (!req.isDone) await Task.Delay(10);
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var tex = DownloadHandlerTexture.GetContent(req);
                        if (tex != null)
                        {
                            // === [核心修复] 中心裁切逻辑 ===
                            // 1. 取宽和高中的最小值，作为正方形的边长
                            int size = Math.Min(tex.width, tex.height);

                            // 2. 计算居中偏移量
                            int offsetX = (tex.width - size) / 2;
                            int offsetY = (tex.height - size) / 2;

                            // 3. 创建裁切区域
                            Rect cropRect = new Rect(offsetX, offsetY, size, size);

                            // 4. 创建 Sprite
                            var sprite = Sprite.Create(tex, cropRect, new Vector2(0.5f, 0.5f));
                            _spriteCache[url] = sprite;
                            return sprite;
                        }
                    }
                }
            }
            catch { }
            return _context.DefaultCover.DefaultMusicCover;
        }

        public void OnEnable() { }
        public void OnDisable() { }
        public void OnUnload() { _qrManager?.Stop(); _spriteCache.Clear(); }
        public void RemoveMusicCoverCache(string u) { }
        public void RemoveAlbumCoverCache(string a) { }
        public Task<(byte[], string)> GetMusicCoverBytesAsync(string u) => Task.FromResult<(byte[], string)>((null, null));
        public void ClearCache() { _spriteCache.Clear(); }
        public Task<AudioClip> LoadAudioAsync(string u) => Task.FromResult<AudioClip>(null);
        public Task<AudioClip> LoadAudioAsync(string u, CancellationToken c) => Task.FromResult<AudioClip>(null);
        public void UnloadAudio(string u) { }
        public async Task RefreshAsync() => await GetMusicListAsync();
    }
}