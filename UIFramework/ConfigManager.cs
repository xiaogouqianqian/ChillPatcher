using System;
using System.IO;
using ChillPatcher.UIFramework.Config;
using Newtonsoft.Json;

namespace ChillPatcher.UIFramework
{
    /// <summary>
    /// 配置管理器
    /// </summary>
    public class ConfigManager
    {
        private const string CONFIG_FILENAME = "MusicLibraryConfig.json";
        private string _configPath;
        private MusicLibraryConfig _config;

        public MusicLibraryConfig Config => _config;

        public ConfigManager(string configDirectory)
        {
            _configPath = Path.Combine(configDirectory, CONFIG_FILENAME);
            LoadConfig();
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _config = JsonConvert.DeserializeObject<MusicLibraryConfig>(json);
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"Loaded config from {_configPath}");
                }
                else
                {
                    // 创建默认配置
                    _config = CreateDefaultConfig();
                    SaveConfig();
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"Created default config at {_configPath}");
                }
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"Failed to load config: {ex}");
                _config = CreateDefaultConfig();
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"Saved config to {_configPath}");
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"Failed to save config: {ex}");
            }
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private MusicLibraryConfig CreateDefaultConfig()
        {
            return new MusicLibraryConfig
            {
                Folders = new System.Collections.Generic.List<LibraryFolder>
                {
                    new LibraryFolder
                    {
                        Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "ChillWithYou"),
                        PlaylistName = "My Music",
                        Recursive = false,
                        AutoWatch = true,
                        Enabled = true
                    }
                },
                AutoScan = true,
            };
        }

        /// <summary>
        /// 添加文件夹
        /// </summary>
        public void AddFolder(LibraryFolder folder)
        {
            if (folder == null)
                throw new ArgumentNullException(nameof(folder));

            _config.Folders.Add(folder);
            SaveConfig();
        }

        /// <summary>
        /// 移除文件夹
        /// </summary>
        public void RemoveFolder(string path)
        {
            _config.Folders.RemoveAll(f => f.Path == path);
            SaveConfig();
        }
    }
}

