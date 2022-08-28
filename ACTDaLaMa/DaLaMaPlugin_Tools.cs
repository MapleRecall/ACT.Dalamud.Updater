using Advanced_Combat_Tracker;
using Dalamud.Updater;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.UI.Notifications;
using XIVLauncher.Common.Dalamud;

namespace ACTDaLaMa;

public partial class DaLaMaPlugin
{

    private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin _ffxivPlugin;

    private Process ffxivProcess => _ffxivPlugin?.DataRepository?.GetCurrentFFXIVProcess();

    private List<string> pidList = new();

    private HashSet<int> waidList = new();

    private DalamudUpdater dalamudUpdater;

    private DalamudLoadingOverlay dalamudLoadingOverlay;

    private DirectoryInfo addonDirectory => new DirectoryInfo(Path.Combine(rootPath, "addon"));
    private DirectoryInfo runtimeDirectory => new DirectoryInfo(Path.Combine(rootPath, "runtime"));
    private DirectoryInfo assetDirectory => new DirectoryInfo(Path.Combine(rootPath, "XIVLauncher", "dalamudAssets"));
    private DirectoryInfo configDirectory => new DirectoryInfo(Path.Combine(rootPath, "XIVLauncher", "pluginConfigs"));

    public string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString();

    private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin GetFfxivPlugin()
    {
        foreach (ActPluginData actPlugin in ActGlobals.oFormActMain.ActPlugins)
        {
            if (actPlugin.pluginFile.Name.ToUpper().Contains("FFXIV_ACT_Plugin".ToUpper()) && (actPlugin.lblPluginStatus.Text.ToUpper().Contains("FFXIV Plugin Started.".ToUpper()) || actPlugin.lblPluginStatus.Text.ToUpper().Contains("FFXIV_ACT_Plugin Started.".ToUpper())))
            {
                _ffxivPlugin = (global::FFXIV_ACT_Plugin.FFXIV_ACT_Plugin)(object)actPlugin.pluginObj;
            }
        }

        return _ffxivPlugin ?? throw new Exception("找不到FFXIV解析插件。");
    }

    private void SendNotification(string content1, string content2)
    {
        try
        {
            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText03);
            var text = toastXml.GetElementsByTagName("text");
            text[0].AppendChild(toastXml.CreateTextNode(content1));
            text[1].AppendChild(toastXml.CreateTextNode(content2));
            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier("ACT 达拉姆德").Show(toast);
        }
        catch (Exception) { }

    }

    private void InitLogging()
    {
        var logPath = Path.Combine(rootPath, "Dalamud.Updater.ACT.log");

        var levelSwitch = new LoggingLevelSwitch();

#if DEBUG
        levelSwitch.MinimumLevel = LogEventLevel.Verbose;
#else
        levelSwitch.MinimumLevel = LogEventLevel.Information;
#endif


        //Log.Logger = new LoggerConfiguration()
        //    //.WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
        //    .WriteTo.Async(a => a.File(logPath))
        //    .MinimumLevel.ControlledBy(levelSwitch)
        //    .CreateLogger();

        Log.Logger = new LoggerConfiguration()
            //.WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
            .WriteTo.Async(a => a.File(logPath))
            .MinimumLevel.ControlledBy(levelSwitch)
            .CreateLogger();
    }

    private void InitUpdater()
    {
        dalamudLoadingOverlay = new DalamudLoadingOverlay();
        dalamudLoadingOverlay.OnProgressBar += setProgressBar;
        dalamudLoadingOverlay.OnSetVisible += setVisible;
        dalamudLoadingOverlay.OnStatusLabel += setStatus;
        dalamudUpdater = new DalamudUpdater(addonDirectory, runtimeDirectory, assetDirectory, configDirectory);
        dalamudUpdater.Overlay = dalamudLoadingOverlay;
        dalamudUpdater.OnUpdateEvent += DalamudUpdater_OnUpdateEvent;
    }

    private Version getVersion()
    {
        var rgx = new Regex(@"^\d+\.\d+\.\d+\.\d+$");
        var di = new DirectoryInfo(Path.Combine(rootPath, "addon", "Hooks"));
        var version = new Version("0.0.0.0");
        if (!di.Exists)
            return version;
        var dirs = di.GetDirectories("*", SearchOption.TopDirectoryOnly).Where(dir => rgx.IsMatch(dir.Name)).ToArray();
        foreach (var dir in dirs)
        {
            var newVersion = new Version(dir.Name);
            if (newVersion > version)
            {
                version = newVersion;
            }
        }
        return version;
    }

    private void InitializePIDCheck()
    {
        Thread thread = new Thread((ThreadStart)async delegate
        {
            while (isThreadRunning)
            {
                await Task.Delay(1000);

                if (!buttonInject.Enabled) continue;

                try
                {
                    var processList = Process.GetProcessesByName("ffxiv_dx11").Where(process => !process.MainWindowTitle.Contains("FINAL FANTASY XIV")).ToList();
                    var newPidList = processList.ConvertAll(process => process.Id.ToString()).ToList();

                    if (Enumerable.SequenceEqual(newPidList, pidList))
                    {
                        continue;
                    }

                    pidList = newPidList;
                    comboBoxPs.Items.Clear();
                    comboBoxPs.Items.AddRange(pidList.ToArray());

                    if (!comboBoxPs.DroppedDown)
                    {
                        this.comboBoxPs.SelectedIndex = 0;
                    }

                    if (this.checkBoxAutoInject.Checked)
                    {
                        foreach (var pc in processList)
                        {
                            if (!isInjected(pc)) _ = InjectAsync(pc.Id, injectDelaySeconds);
                        }
                    }
                }
                catch
                {
                }
            }
        })
        {
            IsBackground = true
        };
        thread.Start();
    }

    private async Task<bool> InjectAsync(int pid, double delay = 0)
    {
        if (delay > 0)
        {
            if (waidList.Contains(pid))
            {
                return false;
            }

            waidList.Add(pid);
            addLog($"将在{delay}秒后装载游戏：{pid}");
            SendNotification($"检测到游戏启动，将在{delay}秒后装载", $"PID {pid}");

            await Task.Delay((int)delay * 1000);

            if (!this.checkBoxAutoInject.Checked)
            {
                return false;
            }
        }

        waidList.Remove(pid);

        addLog($"开始装载游戏：{pid}");
        try
        {
            Process process = Process.GetProcessById(pid);
            if (isInjected(process))
            {
                addLog($"已经装载，不用再装了：{pid}");
                //SendNotification($"已经装载过了!", $"PID {pid}");
                return false;
            }

            Log.Information($"[Updater] dalamudUpdater.State:{dalamudUpdater.State}");
            if (dalamudUpdater.State == DalamudUpdater.DownloadState.NoIntegrity)
            {
                if (MessageBox.Show("当前Dalamud版本可能与游戏不兼容,确定注入吗？", "獭纪委", MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    return false;
                }
            }

            var dalamudStartInfo = GeneratingDalamudStartInfo(process, Directory.GetParent(dalamudUpdater.Runner.FullName).FullName);
            var environment = new Dictionary<string, string>();
            // No use cuz we're injecting instead of launching, the Dalamud.Boot.dll is reading environment variables from ffxiv_dx11.exe
            /*
            var prevDalamudRuntime = Environment.GetEnvironmentVariable("DALAMUD_RUNTIME");
            if (string.IsNullOrWhiteSpace(prevDalamudRuntime))
                environment.Add("DALAMUD_RUNTIME", runtimeDirectory.FullName);
            */
            WindowsDalamudRunner.Inject(dalamudUpdater.Runner, process.Id, environment, DalamudLoadMethod.DllInject, dalamudStartInfo);
            addLog($"装载完成：{pid}");
            SendNotification($"帮你装载了，可能会卡一会儿，不用谢。", $"PID {pid}");

            return true;
        }
        catch (Exception ex)
        {
            addLog($"装载失败，是不是崩了？：{pid}");
            Log.Error(ex.ToString());
            return false;
        }
    }

    private bool isInjected(Process process)
    {
        try
        {
            for (int i = 0; i < process.Modules.Count; i++)
            {
                if (process.Modules[i].ModuleName == "Dalamud.dll")
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private string pluginVersionJsonURL = "https://raw.githubusercontent.com/MapleRecall/ACT.Dalamud.Updater/main/version.json";

    internal class PluginVersionInfo
    {
        public string Version { get; set; }
        public string[] ChangeLog { get; set; }
    }

    public string changeLog = "";

    private async Task CheckPluginVersionAsync()
    {
        labelPV.Text = $"插件版本：{CurrentVersion}";

        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(25),
            };

            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
            };

            var res = await client.GetStringAsync(pluginVersionJsonURL).ConfigureAwait(false);
            var versionJson = JsonConvert.DeserializeObject<PluginVersionInfo>(res);
            var version = versionJson.Version;
            changeLog = string.Join("\r\n", versionJson.ChangeLog);

            if (string.IsNullOrEmpty(version))
            {
                throw (new Exception());
            }
            else if (new Version(version) > new Version(CurrentVersion))
            {
                addLog($"最新版本：{version}");

                if (MessageBox.Show($"当前版本：{CurrentVersion}\r\n最新版本：{version}\r\n\r\n想试试新版么？", "新玩意儿？", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Process.Start("https://github.com/MapleRecall/ACT.Dalamud.Updater/releases");
                };
            }
            else
            {
                addLog($"服务器版本：{version}");
            }
        }
        catch (Exception)
        {

            addLog("检查插件版本失败！");
        }
    }
}