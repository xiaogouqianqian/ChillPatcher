using System;
using System.Collections.Generic;
using System.Reflection;
using Bulbul;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using R3;
using ChillPatcher.UIFramework.Audio;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// 将播放列表切换按钮的图标替换为当前播放音乐的封面
    /// 保持按钮的所有功能不变
    /// </summary>
    [HarmonyPatch(typeof(MusicUI))]
    public static class MusicUI_AlbumArt_Patch
    {
        private static FieldInfo _facilityOpenButtonField;
        private static FieldInfo _facilityMusicField;
        
        // 存储原始图标引用
        private static Sprite _originalDeactiveIcon;
        private static Sprite _originalActiveIcon;
        
        // 存储当前的封面 Sprite
        private static Sprite _currentAlbumArtSprite;
        private static string _currentAudioPath;
        
        // 存储 Image 组件引用
        private static Image _iconDeactiveImage;
        private static Image _iconActiveImage;
        
        // 订阅管理
        private static IDisposable _musicPlaySubscription;

        static MusicUI_AlbumArt_Patch()
        {
            // 使用反射获取私有字段
            var type = typeof(MusicUI);
            _facilityOpenButtonField = type.GetField("_facilityOpenButton", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            _facilityMusicField = type.GetField("_facilityMusic", 
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (_facilityOpenButtonField == null)
            {
                Plugin.Logger.LogError("[MusicUI_AlbumArt_Patch] Failed to find _facilityOpenButton field");
            }
            
            if (_facilityMusicField == null)
            {
                Plugin.Logger.LogError("[MusicUI_AlbumArt_Patch] Failed to find _facilityMusic field");
            }
        }

        /// <summary>
        /// 在 Setup 方法执行后初始化封面显示
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch("Setup")]
        public static void Setup_Postfix(MusicUI __instance)
        {
            // 检查配置是否启用
            if (!UIFrameworkConfig.EnableAlbumArtDisplay.Value)
            {
                Plugin.Logger.LogDebug("[MusicUI_AlbumArt_Patch] Album art display is disabled");
                return;
            }

            try
            {
                // 获取 _facilityOpenButton
                var facilityOpenButton = _facilityOpenButtonField?.GetValue(__instance) as Component;
                if (facilityOpenButton == null)
                {
                    Plugin.Logger.LogWarning("[MusicUI_AlbumArt_Patch] Failed to get _facilityOpenButton");
                    return;
                }

                // 获取 FacilityMusic
                var facilityMusic = _facilityMusicField?.GetValue(__instance) as FacilityMusic;
                if (facilityMusic == null)
                {
                    Plugin.Logger.LogWarning("[MusicUI_AlbumArt_Patch] Failed to get _facilityMusic");
                    return;
                }

                // 查找 IconDeactivemage 和 IconActiveImage
                var buttonTransform = facilityOpenButton.transform;
                var deactiveImageTransform = buttonTransform.Find("IconDeactivemage");
                var activeImageTransform = buttonTransform.Find("IconActiveImage");

                if (deactiveImageTransform == null || activeImageTransform == null)
                {
                    Plugin.Logger.LogWarning("[MusicUI_AlbumArt_Patch] Failed to find icon images");
                    return;
                }

                _iconDeactiveImage = deactiveImageTransform.GetComponent<Image>();
                _iconActiveImage = activeImageTransform.GetComponent<Image>();

                if (_iconDeactiveImage == null || _iconActiveImage == null)
                {
                    Plugin.Logger.LogWarning("[MusicUI_AlbumArt_Patch] Failed to get Image components");
                    return;
                }

                // 保存原始图标
                if (_originalDeactiveIcon == null)
                {
                    _originalDeactiveIcon = _iconDeactiveImage.sprite;
                    Plugin.Logger.LogInfo($"[MusicUI_AlbumArt_Patch] Saved original deactive icon: {_originalDeactiveIcon?.name}");
                }
                
                if (_originalActiveIcon == null)
                {
                    _originalActiveIcon = _iconActiveImage.sprite;
                    Plugin.Logger.LogInfo($"[MusicUI_AlbumArt_Patch] Saved original active icon: {_originalActiveIcon?.name}");
                }

                // 监听音乐播放事件 - 使用静态字段保存订阅，避免 AddTo 参数问题
                if (_musicPlaySubscription != null)
                {
                    _musicPlaySubscription.Dispose();
                }
                
                _musicPlaySubscription = facilityMusic.MusicService.OnPlayMusic.Subscribe(music =>
                {
                    UpdateAlbumArt(music);
                });

                // 立即更新当前播放的音乐封面
                if (facilityMusic.PlayingMusic != null)
                {
                    UpdateAlbumArt(facilityMusic.PlayingMusic);
                }

                Plugin.Logger.LogInfo("[MusicUI_AlbumArt_Patch] Album art display initialized");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MusicUI_AlbumArt_Patch] Error in Setup_Postfix: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 更新封面图标
        /// </summary>
        private static void UpdateAlbumArt(GameAudioInfo audioInfo)
        {
            // 检查配置是否启用
            if (!UIFrameworkConfig.EnableAlbumArtDisplay.Value)
                return;

            if (_iconDeactiveImage == null || _iconActiveImage == null)
                return;

            try
            {
                // 如果是本地文件且路径存在
                if (audioInfo.PathType == AudioMode.LocalPc && !string.IsNullOrEmpty(audioInfo.LocalPath))
                {
                    // 如果已经加载过相同的文件，不需要重新加载
                    if (_currentAudioPath == audioInfo.LocalPath && _currentAlbumArtSprite != null)
                    {
                        Plugin.Logger.LogDebug($"[MusicUI_AlbumArt_Patch] Using cached album art for: {audioInfo.Title}");
                        return;
                    }

                    // 读取封面
                    var albumArtTexture = AlbumArtReader.GetAlbumArt(audioInfo.LocalPath);
                    
                    if (albumArtTexture != null)
                    {
                        // 创建圆形 Sprite
                        var albumArtSprite = AlbumArtReader.CreateCircularSprite(albumArtTexture, 88);
                        
                        if (albumArtSprite != null)
                        {
                            // 销毁旧的封面 Sprite
                            if (_currentAlbumArtSprite != null)
                            {
                                UnityEngine.Object.Destroy(_currentAlbumArtSprite.texture);
                                UnityEngine.Object.Destroy(_currentAlbumArtSprite);
                            }

                            // 应用新的封面
                            _currentAlbumArtSprite = albumArtSprite;
                            _currentAudioPath = audioInfo.LocalPath;
                            
                            _iconDeactiveImage.sprite = albumArtSprite;
                            _iconActiveImage.sprite = albumArtSprite;
                            
                            Plugin.Logger.LogInfo($"[MusicUI_AlbumArt_Patch] Updated album art for: {audioInfo.Title}");
                            return;
                        }
                        else
                        {
                            // 创建 Sprite 失败，销毁纹理
                            UnityEngine.Object.Destroy(albumArtTexture);
                        }
                    }
                }

                // 如果没有封面或不是本地文件，使用原始图标
                RestoreOriginalIcon();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MusicUI_AlbumArt_Patch] Error updating album art: {ex.Message}");
                RestoreOriginalIcon();
            }
        }

        /// <summary>
        /// 恢复原始图标
        /// </summary>
        private static void RestoreOriginalIcon()
        {
            if (_iconDeactiveImage != null && _originalDeactiveIcon != null)
            {
                _iconDeactiveImage.sprite = _originalDeactiveIcon;
            }
            
            if (_iconActiveImage != null && _originalActiveIcon != null)
            {
                _iconActiveImage.sprite = _originalActiveIcon;
            }
            
            // 清理当前封面
            if (_currentAlbumArtSprite != null)
            {
                UnityEngine.Object.Destroy(_currentAlbumArtSprite.texture);
                UnityEngine.Object.Destroy(_currentAlbumArtSprite);
                _currentAlbumArtSprite = null;
            }
            
            _currentAudioPath = null;
        }
    }
}
