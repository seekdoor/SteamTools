using Gameloop.Vdf.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Application.Models;
using System.Application.Models.Settings;
using System.Application.UI;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Application.Services.ISteamService;

namespace System.Application.Services.Implementation
{
    internal sealed class SteamServiceImpl : ISteamService
    {
        const string TAG = "SteamS";

        /// <summary>
        /// <list type="bullet">
        ///   <item>
        ///     Windows：~\Steam\config\loginusers.vdf
        ///   </item>
        ///   <item>
        ///     Linux：~/.steam/steam/config/loginusers.vdf
        ///   </item>
        ///   <item>
        ///     Mac：~/Library/Application Support/Steam/config/loginusers.vdf
        ///   </item>
        /// </list>
        /// </summary>
        readonly string? UserVdfPath;
        readonly string? ConfigVdfPath;
        readonly string? AppInfoPath;
        readonly string? LibrarycacheDirPath;
        const string UserDataDirectory = "userdata";
        readonly IDesktopPlatformService platformService;
        readonly string? mSteamDirPath;
        readonly string? mSteamProgramPath;
        readonly string? mRegistryVdfPath;
        readonly string[] steamProcess = new[] { "steam", "steam_osx", "steamservice", "steamwebhelper" };
        readonly Lazy<IHttpService> _http = new(() => DI.Get<IHttpService>());
        IHttpService Http => _http.Value;

        public SteamServiceImpl(IDesktopPlatformService platformService)
        {
            this.platformService = platformService;
            mSteamDirPath = platformService.GetSteamDirPath();
            mSteamProgramPath = platformService.GetSteamProgramPath();
            UserVdfPath = SteamDirPath == null ? null : Path.Combine(SteamDirPath, "config", "loginusers.vdf");
            AppInfoPath = SteamDirPath == null ? null : Path.Combine(SteamDirPath, "appcache", "appinfo.vdf");
            LibrarycacheDirPath = SteamDirPath == null ? null : Path.Combine(SteamDirPath, "appcache", "librarycache");
            mRegistryVdfPath = platformService.GetRegistryVdfPath();// SteamDirPath == null ? null : Path.Combine(SteamDirPath, "registry.vdf");
            //RegistryVdfPath  = SteamDirPath == null ? null : Path.Combine(SteamDirPath, "registry.vdf");
            ConfigVdfPath = SteamDirPath == null ? null : Path.Combine(SteamDirPath, "config", "config.vdf");

            if (!File.Exists(UserVdfPath)) UserVdfPath = null;
            if (!File.Exists(AppInfoPath)) AppInfoPath = null;
            if (!File.Exists(ConfigVdfPath)) ConfigVdfPath = null;
            if (!Directory.Exists(LibrarycacheDirPath)) LibrarycacheDirPath = null;
        }

        public string? SteamDirPath => mSteamDirPath;
        public string? RegistryVdfPath => mRegistryVdfPath;

        public string? SteamProgramPath => mSteamProgramPath;

        public bool IsRunningSteamProcess
        {
            get
            {
                return Process.GetProcessesByName(steamProcess[0]).Any_Nullable();
            }
        }

        public void KillSteamProcess()
        {
            foreach (var p in steamProcess)
            {
                var process = Process.GetProcessesByName(p);
                foreach (var item in process)
                {
                    if (item.HasExited == false)
                    {
                        item.Kill();
                        item.WaitForExit();
                    }
                }
            }
        }

        public bool TryKillSteamProcess()
        {
            try
            {
                KillSteamProcess();
                return true;
                //if (IsRunningSteamProcess)
                //{
                //    Process closeProc = Process.Start(new ProcessStartInfo(SteamProgramPath, "-shutdown"));
                //    bool closeProcSuccess = closeProc != null && closeProc.WaitForExit(3000);
                //    return closeProcSuccess;
                //}
                //return false;
            }
            catch (Exception e)
            {
                Log.Error(TAG, e, "KillSteamProcess Fail.");
                return false;
            }
            finally
            {
                SteamConnectService.Current.IsConnectToSteam = false;
            }
        }

        public int? GetSteamProcessPid()
        {
            var processes = Process.GetProcessesByName(steamProcess[0]);
            if (processes.Any_Nullable())
                return processes.First().Id;
            return default;
        }

        public void StartSteam(string? arguments = null)
        {
            if (!string.IsNullOrEmpty(SteamProgramPath))
            {
                //if (OperatingSystem2.IsWindows && IDesktopPlatformService.Instance.IsAdministrator)
                //{
                //    //Process2.Start("explorer.exe", $"\"{SteamProgramPath}\" \"{arguments}\"");
                //    Process2.Start("runas.exe", $"/trustlevel:0x20000 \"{SteamProgramPath} \\\"{arguments}\\\"\"", useShellExecute: true);
                //}
                //else
                //{
                Process2.Start(SteamProgramPath, arguments, workingDirectory: SteamDirPath);
                //}
            }
        }

        public string GetLastLoginUserName() => platformService.GetLastSteamLoginUserName();

        public List<SteamUser> GetRememberUserList()
        {
            var users = new List<SteamUser>();
            try
            {
                if (!string.IsNullOrWhiteSpace(UserVdfPath) && File.Exists(UserVdfPath))
                {
                    // 注意：动态类型在移动端受限，且运行时可能抛出异常
                    dynamic v = VdfHelper.Read(UserVdfPath);
                    foreach (var item in v.Value)
                    {
                        try
                        {
                            var i = item.Value;
                            var user = new SteamUser(item.ToString())
                            {
                                SteamId64 = Convert.ToInt64(item.Key.ToString()),
                                AccountName = i.AccountName?.ToString(),
                                SteamID = i.PersonaName?.ToString(),
                                PersonaName = i.PersonaName?.ToString(),
                                RememberPassword = Convert.ToBoolean(Convert.ToInt64(i.RememberPassword?.ToString())),
                                Timestamp = Convert.ToInt64(i.Timestamp?.ToString())
                            };
                            user.LastLoginTime = user.Timestamp.ToDateTimeS();

                            // 老版本 Steam 数据 小写 mostrecent 支持
                            user.MostRecent = i.mostrecent != null ?
                                Convert.ToBoolean(Convert.ToByte(i.mostrecent.ToString())) :
                                Convert.ToBoolean(Convert.ToByte(i.MostRecent.ToString()));

                            user.WantsOfflineMode = i.WantsOfflineMode != null ?
                                Convert.ToBoolean(Convert.ToByte(i.WantsOfflineMode.ToString())) : false;

                            // 因为警告这个东西应该都不需要所以直接默认跳过好了
                            user.SkipOfflineModeWarning = true;
                            //user.SkipOfflineModeWarning = i.SkipOfflineModeWarning != null ?
                            //    Convert.ToBoolean(Convert.ToByte(i.SkipOfflineModeWarning.ToString())) : false;

                            users.Add(user);
                        }
                        catch (Exception e)
                        {
                            Log.Error(TAG, e, "GetRememberUserList Fail(0).");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(TAG, e, "GetRememberUserList Fail(1).");
            }
            return users;
        }

        public bool UpdateAuthorizedDeviceList(IEnumerable<AuthorizedDevice> model)
        {
            var authorizeds = new List<AuthorizedDevice>();
            try
            {
                if (!string.IsNullOrWhiteSpace(ConfigVdfPath) && File.Exists(ConfigVdfPath))
                {
                    dynamic v = VdfHelper.Read(ConfigVdfPath);
                    var authorizedDevice = v.Value.AuthorizedDevice;
                    if (authorizedDevice != null)
                    {
                        foreach (var item in authorizedDevice)
                        {
                            try
                            {
                                var i = item.Value;
                                authorizeds.Add(new AuthorizedDevice(item.ToString())
                                {
                                    SteamId3_Int = Convert.ToInt64(item.Key.ToString()),
                                    Timeused = Convert.ToInt64(i.timeused.ToString()),
                                    Description = i.description.ToString(),
                                    Tokenid = i.tokenid.ToString(),
                                });
                            }
                            catch (Exception e)
                            {
                                Log.Error(TAG, e, "GetAuthorizedDeviceList Fail(0).");
                            }
                        }
                        var oldStr = $"\t{{\n{string.Join("\n", authorizeds.Select(x => x.CurrentVdfString))}\n\t}}".TrimEnd("\n");
                        //authorizedDevice.Select(x => x.ToString());
                        var newStr = $"\t{{\n{string.Join("\n", model.OrderBy(x => x.Index).Select(x => x.CurrentVdfString))}\n\t}}".TrimEnd("\n");
                        VdfHelper.UpdateValueByReplaceNoPattern(ConfigVdfPath, oldStr, newStr);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(TAG, e, "UpdateAuthorizedDeviceList Fail(0).");
                return false;
            }
            return false;
        }
        public bool RemoveAuthorizedDeviceList(AuthorizedDevice model)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ConfigVdfPath) && File.Exists(ConfigVdfPath))
                {
                    VdfHelper.DeleteValueByKey(ConfigVdfPath, model.OriginVdfString.ThrowIsNull(nameof(model.OriginVdfString)));
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Error(TAG, e, "RemoveAuthorizedDeviceList Fail(0).");
                return false;
            }
            return false;
        }
        public List<AuthorizedDevice> GetAuthorizedDeviceList()
        {
            var authorizeds = new List<AuthorizedDevice>();
            try
            {
                if (!string.IsNullOrWhiteSpace(ConfigVdfPath) && File.Exists(ConfigVdfPath))
                {
                    // 注意：动态类型在移动端受限，且运行时可能抛出异常
                    dynamic v = VdfHelper.Read(ConfigVdfPath);
                    var authorizedDevice = v.Value.AuthorizedDevice;
                    if (authorizedDevice != null)
                    {
                        var index = 0;
                        foreach (var item in authorizedDevice)
                        {
                            try
                            {
                                var i = item.Value;
                                authorizeds.Add(new AuthorizedDevice(item.ToString())
                                {
                                    Index = index,
                                    SteamId3_Int = Convert.ToInt64(item.Key.ToString()),
                                    Timeused = Convert.ToInt64(i.timeused.ToString()),
                                    Description = i.description.ToString(),
                                    Tokenid = i.tokenid.ToString(),
                                });
                                index++;
                            }
                            catch (Exception e)
                            {
                                Log.Error(TAG, e, "GetAuthorizedDeviceList Fail(0).");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(TAG, e, "GetAuthorizedDeviceList Fail(1).");
            }
            return authorizeds;
        }

        public void SetCurrentUser(string userName) => platformService.SetCurrentUser(userName);

        public List<SteamApp>? GetAppListJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var lastChanged = File.GetLastWriteTime(filePath);
            int daysSinceChanged = (int)(DateTime.Now - lastChanged).TotalDays;
            if (daysSinceChanged > 10)
            {
                return null;
            }

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var apps = Serializable.DJSON<SteamApps?>(json);
            return apps?.AppList?.Apps;
        }

        public bool UpdateAppListJson(List<SteamApp> apps, string filePath)
        {
            try
            {
                var json_str = Serializable.SJSON(apps);
                File.WriteAllText(filePath, json_str, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(TAG, ex, "UpdateAppListJson(obj) Fail.");
                return false;
            }
        }

        public bool UpdateAppListJson(string appsJsonStr, string filePath)
        {
            try
            {
                File.WriteAllText(filePath, appsJsonStr, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(TAG, ex, "UpdateAppListJson(str) Fail.");
                return false;
            }
        }

        public void DeleteLocalUserData(SteamUser user, bool IsDeleteUserData = false)
        {
            if (string.IsNullOrWhiteSpace(UserVdfPath) || string.IsNullOrWhiteSpace(SteamDirPath))
            {
                return;
            }
            else
            {
                VdfHelper.DeleteValueByKey(UserVdfPath, user.SteamId64.ToString());
                if (IsDeleteUserData)
                {
                    var temp = Path.Combine(SteamDirPath, UserDataDirectory, user.SteamId3_Int.ToString());
                    if (Directory.Exists(temp))
                    {
                        Directory.Delete(temp, true);
                    }
                }
            }
        }

        public void UpdateLocalUserData(SteamUser user)
        {
            if (string.IsNullOrWhiteSpace(UserVdfPath))
            {
                return;
            }
            else
            {
                var originVdfStr = user.OriginVdfString;
                VdfHelper.UpdateValueByReplace(
                    UserVdfPath,
                    originVdfStr.ThrowIsNull(nameof(originVdfStr)),
                    user.CurrentVdfString);
            }
        }

        private uint unknownValueAtStart;
        private const uint MagicNumber = 123094055U;

        /// <summary>
        /// 从steam本地客户端缓存文件中读取游戏数据
        /// </summary>
        public /*async*/ Task<List<SteamApp>> GetAppInfos()
        {
            return Task.FromResult(GetAppInfos_());
            List<SteamApp> GetAppInfos_()
            {
                var apps = new List<SteamApp>();
                try
                {
                    if (string.IsNullOrEmpty(AppInfoPath) && !File.Exists(AppInfoPath))
                        return apps;
                    using var stream = IOPath.OpenRead(AppInfoPath);
                    if (stream == null)
                    {
                        return apps;
                    }
                    using (BinaryReader binaryReader = new(stream))
                    {
                        uint num = binaryReader.ReadUInt32();
                        if (num != MagicNumber)
                        {
                            Log.Error(nameof(GetAppInfos), string.Format("\"{0}\" magic code is not supported: 0x{1:X8}", Path.GetFileName(AppInfoPath), num));
                            return apps;
                        }
                        SteamApp? app = new();
                        unknownValueAtStart = binaryReader.ReadUInt32();
                        while ((app = SteamApp.FromReader(binaryReader)) != null)
                        {
                            if (app.AppId > 0)
                            {
                                //if (GameLibrarySettings.DefaultIgnoreList.Value.Contains(app.AppId))
                                //    continue;
                                if (GameLibrarySettings.HideGameList.Value!.ContainsKey(app.AppId))
                                    continue;
                                //if (app.ParentId > 0)
                                //{
                                //    var parentApp = apps.FirstOrDefault(f => f.AppId == app.ParentId);
                                //    if (parentApp != null)
                                //        parentApp.ChildApp.Add(app.AppId);
                                //    //continue;
                                //}
                                apps.Add(app);
                                //app.Modified += (s, e) =>
                                //{
                                //};
                            }
                        }
                    }
                    return apps;
                }
                catch (Exception ex)
                {
                    Log.Error(nameof(SteamServiceImpl), ex, nameof(GetAppInfos));
                    GC.Collect();
                    return apps;
                }
            }
        }

        public string? GetAppLibCacheFilePath(uint appId, SteamApp.LibCacheType type)
        {
            if (LibrarycacheDirPath == null) return null;
            var fileName = type switch
            {
                SteamApp.LibCacheType.Header => $"{appId}_header.jpg",
                SteamApp.LibCacheType.Icon => $"{appId}_icon.jpg",
                SteamApp.LibCacheType.Library_600x900 => $"{appId}_library_600x900.jpg",
                SteamApp.LibCacheType.Library_Hero => $"{appId}_library_hero.jpg",
                SteamApp.LibCacheType.Library_Hero_Blur => $"{appId}_library_hero_blur.jpg",
                SteamApp.LibCacheType.Logo => $"{appId}_logo.png",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
            var filePath = Path.Combine(LibrarycacheDirPath, fileName);
            return filePath;
        }

        public async Task<string> GetAppImageAsync(SteamApp app, SteamApp.LibCacheType type)
        {
            var cacheFilePath = GetAppLibCacheFilePath(app.AppId, type);
            if (File.Exists(cacheFilePath)) return cacheFilePath;
            var url = type switch
            {
                SteamApp.LibCacheType.Header => app.HeaderLogoUrl,
                SteamApp.LibCacheType.Icon => app.IconUrl,
                SteamApp.LibCacheType.Library_600x900 => app.LibraryLogoUrl,
                SteamApp.LibCacheType.Library_Hero => app.LibraryHeaderUrl,
                SteamApp.LibCacheType.Library_Hero_Blur => app.LibraryHeaderBlurStream,
                SteamApp.LibCacheType.Logo => app.LibraryNameUrl,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
            if (url == null) return string.Empty;
            var value = await Http.GetImageAsync(url, ImageChannelType.SteamGames);
            return value ?? string.Empty;
        }

        public /*async*/ ValueTask LoadAppImageAsync(SteamApp app)
        {
            return ValueTask.CompletedTask;
            //if (app.LibraryLogoStream == null)
            //{
            //    app.LibraryLogoStream = await GetAppImageAsync(app, SteamApp.LibCacheType.Library_600x900);
            //}
            //if (app.LibraryHeaderStream == null)
            //{
            //    app.LibraryHeaderStream = await GetAppImageAsync(app, SteamApp.LibCacheType.Library_Hero);
            //}
            //if (app.LibraryHeaderBlurStream == null)
            //{
            //    app.LibraryHeaderBlurStream = await GetAppImageAsync(app, SteamApp.LibCacheType.Library_Hero_Blur);
            //}
            //if (app.LibraryNameStream == null)
            //{
            //    app.LibraryNameStream = await GetAppImageAsync(app, SteamApp.LibCacheType.Logo);
            //}
            //if (app.HeaderLogoStream == null)
            //{
            //    app.HeaderLogoStream = await GetAppImageAsync(app, SteamApp.LibCacheType.Header);
            //}
        }


        string[]? GetLibraryPaths()
        {
            if (string.IsNullOrEmpty(SteamDirPath) || !Directory.Exists(SteamDirPath))
            {
                return null;
            }

            List<string> paths = new()
            {
                Path.Combine(SteamDirPath, "SteamApps"),
            };

            try
            {

                string libraryFoldersPath = Path.Combine(SteamDirPath, "SteamApps", "libraryfolders.vdf");

                dynamic v = VdfHelper.Read(libraryFoldersPath);

                for (int i = 1; ; i++)
                {
                    try
                    {
                        dynamic pathNode = v.Value[i.ToString()];

                        if (pathNode == null) break;

                        if (pathNode.path != null)
                        {
                            // New format
                            // Valve introduced a new format for the "libraryfolders.vdf" file
                            // In the new format, the node "1" not only contains a single value (the path),
                            // but multiple values: path, label, mounted, contentid

                            // If a library folder is removed in the Steam settings, the path persists, but its 'mounted' value is set to 0 (disabled)
                            // We consider only the value '1' as that the path is actually enabled.
                            if (pathNode.mounted != null && pathNode.mounted.ToString() != "1")
                                continue;
                            pathNode = pathNode.path;
                        }

                        string path = Path.Combine(pathNode.ToString(), "SteamApps");

                        if (Directory.Exists(path))
                            paths.Add(path);
                    }
                    catch (Exception e)
                    {
                        Log.Error(TAG, e, "GetLibraryPaths for catch");
                    }
                }

            }
            catch (Exception e)
            {
                Log.Error(TAG, e, "GetLibraryPaths Read libraryFoldersPath catch");
            }

            return paths.ToArray();
        }

        public SteamApp? FileToAppInfo(string filename)
        {
            try
            {
                string[] content = File.ReadAllLines(filename);
                // Skip if file contains only NULL bytes (this can happen sometimes, example: download crashes, resulting in a corrupted file)
                if (content.Length == 1 && string.IsNullOrWhiteSpace(content[0].TrimStart('\0'))) return null;

                dynamic v = VdfHelper.Read(filename);

                if (v.Value == null)
                {
                    Toast.Show(
                        $"{filename}{Environment.NewLine}contains unexpected content.{Environment.NewLine}This game will be ignored.");
                    return null;
                }
                v = v.Value;

                SteamApp newInfo = new SteamApp
                {
                    AppId = uint.Parse((v.appid ?? v.appID ?? v.AppID).ToString()),
                    Name = v.name.ToString() ?? v.installdir.ToString(),
                    InstalledDir = Path.Combine(Path.GetDirectoryName(filename), "common", v.installdir.ToString()),
                    State = int.Parse(v.StateFlags.ToString()),
                    SizeOnDisk = long.Parse(v.SizeOnDisk.ToString()),
                    LastOwner = long.Parse(v.LastOwner.ToString()),
                    BytesToDownload = long.Parse(v.BytesToDownload.ToString()),
                    BytesDownloaded = long.Parse(v.BytesDownloaded.ToString()),
                    lastUpdatedTicks = long.Parse(v.LastUpdated.ToString()),
                };
                newInfo.LastUpdated = newInfo.lastUpdatedTicks.ToDateTimeS();
                return newInfo;
            }
            catch (Exception ex)
            {
                Log.Error(nameof(FileToAppInfo), ex, filename);
                return null;
            }
        }

        /// <summary>
        /// 获取正在下载的SteamApp列表
        /// </summary>
        public List<SteamApp> GetDownloadingAppList()
        {
            var appInfos = new List<SteamApp>();
            try
            {
                var libraryPaths = GetLibraryPaths();
                if (!libraryPaths.Any_Nullable())
                {
                    Toast.Show("No game library found." + Environment.NewLine + "This might appear if Steam has been installed on this machine but was uninstalled.");
                }

                foreach (string path in libraryPaths!)
                {
                    DirectoryInfo di = new DirectoryInfo(path);

                    foreach (FileInfo fileInfo in di.EnumerateFiles("*.acf"))
                    {
                        // Skip if file is empty
                        if (fileInfo.Length == 0) continue;

                        SteamApp? ai = FileToAppInfo(fileInfo.FullName);
                        if (ai == null) continue;

                        appInfos.Add(ai);
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error(nameof(GetDownloadingAppList), ex, "GetDownloadApp Error");
            }
            return appInfos.OrderBy(x => x.Name).ToList();
        }

        static uint IdFromAcfFilename(string filename)
        {
            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

            int loc = filenameWithoutExtension.IndexOf('_');
            return uint.Parse(filenameWithoutExtension.Substring(loc + 1));
        }

        /// <summary>
        /// 监听Steam下载
        /// </summary>
        public void InitWatchSteamDownloading(Action<uint> changedAction, Action<uint> deleteAction)
        {
            var libraryPaths = GetLibraryPaths();
            if (!libraryPaths.Any_Nullable())
            {
                Toast.Show("No game library found." + Environment.NewLine + "This might appear if Steam has been installed on this machine but was uninstalled.");
            }

            foreach (string libraryFolder in libraryPaths!)
            {
                var fsw = new FileSystemWatcher(libraryFolder, "*.acf")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                };
                fsw.Changed += Fsw_Changed;
                fsw.Deleted += Fsw_Deleted;
                fsw.EnableRaisingEvents = true;
            }

            void Fsw_Deleted(object sender, FileSystemEventArgs e)
            {
                uint id = IdFromAcfFilename(e.FullPath);

                //SteamApp info = Apps.FirstOrDefault(x => x.ID == id);
                //if (info == null) return;

                //var eventArgs = new AppInfoEventArgs(info);
                deleteAction.Invoke(id);
            }

            void Fsw_Changed(object sender, FileSystemEventArgs e)
            {
                dynamic? v = null;
                try
                {
                    // This is necessary because sometimes the file is still accessed by steam, so let's wait for 10 ms and try again.
                    // Maximum 5 times
                    int counter = 1;
                    do
                    {
                        try
                        {
                            v = VdfHelper.Read(e.FullPath);
                            break;
                        }
                        catch (IOException)
                        {
                            System.Threading.Thread.Sleep(50);
                        }
                    }
                    while (counter++ <= 5);
                }
                catch
                {
                    return;
                }

                // Shouldn't happen, but might occur if Steam holds the acf file too long
                if (v == null) return;

                // Search for changed app, if null it's a new app
                //SteamApp info = Apps.FirstOrDefault(x => x.ID == newID);
                changedAction.Invoke((v.appid ?? v.appID ?? v.AppID));

                //if (info != null) // Download state changed
                //{
                //    eventArgs = new AppInfoChangedEventArgs(info, info.State);
                //    // Only update existing AppInfo
                //    info.State = int.Parse(v.StateFlags.ToString());
                //}
                //else // New download started
                //{
                //    // Add new AppInfo
                //    info = JsonToAppInfo(newJson);
                //    Apps.Add(info);
                //    eventArgs = new AppInfoChangedEventArgs(info, -1);
                //}

                //OnAppInfoChanged(info, eventArgs);
            }
        }
    }
}