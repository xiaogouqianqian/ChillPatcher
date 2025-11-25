using System;
using ChillPatcher.UIFramework.Core;
using ChillPatcher.UIFramework.Audio;

namespace ChillPatcher.UIFramework.Music
{
    /// <summary>
    /// 音乐UI管理器实现
    /// </summary>
    public class MusicUIManager : IMusicUIManager
    {
        private VirtualScrollController _virtualScroll;
        private PlaylistRegistry _playlistRegistry;
        private AudioLoader _audioLoader;
        private TagDropdownManager _tagDropdown;

        public IVirtualScrollController VirtualScroll => _virtualScroll;
        public IPlaylistRegistry PlaylistRegistry => _playlistRegistry;
        public IAudioLoader AudioLoader => _audioLoader;
        public ITagDropdownManager TagDropdown => _tagDropdown;

        /// <summary>
        /// 初始化音乐管理器
        /// </summary>
        public void Initialize()
        {
            _virtualScroll = new VirtualScrollController();
            _playlistRegistry = new PlaylistRegistry();
            _audioLoader = new AudioLoader();
            _tagDropdown = new TagDropdownManager();

            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo("MusicUIManager initialized");
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            _virtualScroll?.Dispose();
            _playlistRegistry?.Dispose();
            _tagDropdown?.Dispose();

            _virtualScroll = null;
            _playlistRegistry = null;
            _audioLoader = null;
            _tagDropdown = null;

            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo("MusicUIManager cleaned up");
        }
    }
}

