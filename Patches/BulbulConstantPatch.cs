using HarmonyLib;
using Bulbul;

namespace ChillPatcher.Patches
{
    /// <summary>
    /// Patch 5: 修复路径 - BulbulConstant.CreateSaveDirectoryPath (重载1)
    /// 使用配置文件中的用户ID替换 SteamUser.GetSteamID().ToString()
    /// 壁纸引擎模式强制使用配置存档；多存档选项开启时也使用配置存档
    /// </summary>
    [HarmonyPatch(typeof(BulbulConstant), "CreateSaveDirectoryPath", new System.Type[] { typeof(bool), typeof(string) })]
    public class BulbulConstant_CreateSaveDirectoryPath1_Patch
    {
        static bool Prefix(bool isDemo, string version, ref string __result)
        {
            // 壁纸引擎模式或多存档模式启用时，使用配置的用户ID
            if (!PluginConfig.EnableWallpaperEngineMode.Value && !PluginConfig.UseMultipleSaveSlots.Value)
                return true; // 不屏蔽，执行原方法
                
            string userID = PluginConfig.OfflineUserId.Value;
            __result = System.IO.Path.Combine("SaveData", isDemo ? "Demo" : "Release", version, userID);
            return false; // 阻止原方法执行
        }
    }

    /// <summary>
    /// Patch 6: 修复路径 - BulbulConstant.CreateSaveDirectoryPath (重载2)
    /// 使用配置文件中的用户ID替换 SteamUser.GetSteamID().ToString()
    /// 壁纸引擎模式强制使用配置存档；多存档选项开启时也使用配置存档
    /// </summary>
    [HarmonyPatch(typeof(BulbulConstant), "CreateSaveDirectoryPath", new System.Type[] { typeof(string) })]
    public class BulbulConstant_CreateSaveDirectoryPath2_Patch
    {
        static bool Prefix(string versionDirectory, ref string __result)
        {
            // 壁纸引擎模式或多存档模式启用时，使用配置的用户ID
            if (!PluginConfig.EnableWallpaperEngineMode.Value && !PluginConfig.UseMultipleSaveSlots.Value)
                return true; // 不屏蔽，执行原方法
                
            string userID = PluginConfig.OfflineUserId.Value;
            __result = System.IO.Path.Combine("SaveData", versionDirectory, userID);
            return false; // 阻止原方法执行
        }
    }
}
