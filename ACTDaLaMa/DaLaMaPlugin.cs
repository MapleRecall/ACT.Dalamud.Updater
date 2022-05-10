using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using System.Xml;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Windows.UI.Notifications;

namespace ACTDaLaMa;

public partial class DaLaMaPlugin : UserControl, IActPluginV1
{
    public class ProgressEventArgsEx
    {
        public int Percentage { get; set; }

        public string Text { get; set; }
    }

    private string updateUrl = "https://dalamud-1253720819.cos.ap-nanjing.myqcloud.com/update.xml";

    private bool isThreadRunning = true;

    private bool dotnetDownloadFinished;

    private bool desktopDownloadFinished;

    private string dotnetDownloadPath;

    private string desktopDownloadPath;

    private DirectoryInfo runtimePath;

    private DirectoryInfo[] runtimePaths;

    private string RuntimeVersion = "5.0.6";

    private double injectDelaySeconds = 10;

    private IContainer components;

    private Button buttonCheckForUpdate;

    private Label labelVersion;

    private Button buttonCheckRuntime;

    private ComboBox comboBoxFFXIV;

    private Button buttonInject;

    private LinkLabel linkLabel1;

    private CheckBox checkBoxAcce;

    private LinkLabel linkLabel2;

    private CheckBox checkBoxAutoInject;

    private NotifyIcon DalamudUpdaterIcon;

    private ContextMenuStrip contextMenuStrip1;

    private ToolStripMenuItem 显示ToolStripMenuItem;

    private ToolStripMenuItem 退出ToolStripMenuItem;

    private ProgressBar progressBar1;

    private ProgressBar progressBar2;

    private Label labelVer;
    private GroupBox groupBox1;
    private GroupBox groupBox2;
    private Label label1;
    private TextBox delayTextBox;
    private TextBox rootTextBox;
    private GroupBox groupBox3;
    private Button openRootBtn;
    private Button choseRootBtn;
    private Label label3;
    private Label label2;
    private ProgressBar progressBar3;
    private PictureBox pictureBox1;
    private ListBox logListBox;
    private Label label5;
    private PictureBox pictureBox2;
    private LinkLabel linkLabel3;
    private GroupBox groupBox4;
    private string rootPath = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "Dalamud");
    private string backupPath => Path.Combine(rootPath, "backup");

    public static string GetAppSettings(string key, string def = null)
    {
        try
        {
            KeyValueConfigurationElement keyValueConfigurationElement = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings.Settings[key];
            if (keyValueConfigurationElement == null)
            {
                return def;
            }
            return keyValueConfigurationElement.Value;
        }
        catch (ConfigurationErrorsException)
        {
            Console.WriteLine("Error reading app settings");
            return def;
        }
    }

    public static void AddOrUpdateAppSettings(string key, string value)
    {
        try
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            KeyValueConfigurationCollection settings = configuration.AppSettings.Settings;
            if (settings[key] == null)
            {
                settings.Add(key, value);
            }
            else
            {
                settings[key].Value = value;
            }
            configuration.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(configuration.AppSettings.SectionInformation.Name);
        }
        catch (ConfigurationErrorsException)
        {
            Console.WriteLine("Error writing app settings");
        }
    }

    private Version getVersion()
    {
        Regex rgx = new Regex("^\\d+\\.\\d+\\.\\d+\\.\\d+$");
        DirectoryInfo[] array = (from dir in new DirectoryInfo(rootPath).GetDirectories("*", SearchOption.TopDirectoryOnly)
                                 where rgx.IsMatch(dir.Name)
                                 select dir).ToArray();
        Version version = new Version("0.0.0.0");
        DirectoryInfo[] array2 = array;
        for (int i = 0; i < array2.Length; i++)
        {
            Version version2 = new Version(array2[i].Name);
            if (version2 > version)
            {
                version = version2;
            }
        }
        return version;
    }

    public DaLaMaPlugin()
    {
        InitializeComponent();
    }

    private void InitializeConfig()
    {
        if (GetAppSettings("AutoInject", "false") == "true")
        {
            checkBoxAutoInject.Checked = true;
        }

        if (GetAppSettings("Accelerate", "false") == "true")
        {
            checkBoxAcce.Checked = true;
        }

        string delay = GetAppSettings("InjectDelaySeconds", "-1");
        if (!double.TryParse(delay, out injectDelaySeconds) || injectDelaySeconds < 0)
        {
            injectDelaySeconds = 10;
        }

        delayTextBox.Text = injectDelaySeconds.ToString();


        var root = GetAppSettings("rootPath", null);
        if (!string.IsNullOrWhiteSpace(root))
        {
            rootTextBox.Text = rootPath = root;
        }
    }

    private void InitializePIDCheck()
    {
        string[] newPidList;
        Thread thread = new Thread((ThreadStart)async delegate
        {
            while (isThreadRunning)
            {
                try
                {
                    newPidList = (from process in Process.GetProcessesByName("ffxiv_dx11")
                                  where !process.MainWindowTitle.Contains("FINAL FANTASY XIV")
                                  select process).ToList().ConvertAll((Process process) => process.Id.ToString()).ToArray();
                    int hashCode = string.Join(", ", newPidList).GetHashCode();
                    string[] value = (from object item in comboBoxFFXIV.Items
                                      select item.ToString()).ToArray();
                    if (string.Join(", ", value).GetHashCode() != hashCode)
                    {
                        _ = comboBoxFFXIV.Invoke((MethodInvoker)async delegate
                          {
                              comboBoxFFXIV.Items.Clear();
                              ComboBox.ObjectCollection items = comboBoxFFXIV.Items;
                              object[] items2 = newPidList;
                              items.AddRange(items2);
                              if (newPidList.Length != 0)
                              {
                                  if (!comboBoxFFXIV.DroppedDown)
                                  {
                                      comboBoxFFXIV.SelectedIndex = 0;
                                  }
                                  if (checkBoxAutoInject.Checked)
                                  {
                                      string[] array = newPidList;
                                      foreach (string pid in array)
                                      {
                                          addLog($"将在{injectDelaySeconds}秒后装载游戏：{pid}");
                                          SendNotification($"检测到游戏启动，将在{injectDelaySeconds}秒后装载", $"PID {pid}");
                                          await Task.Delay((int)injectDelaySeconds * 1000);
                                          int num = int.Parse(pid);
                                          Inject(num);
                                      }
                                  }
                              }
                          });
                    }
                }
                catch
                {
                }
                await Task.Delay(1000);
            }
        })
        {
            IsBackground = true
        };
        thread.Start();
    }

    private void FormMain_Load(object sender, EventArgs e)
    {
        //AutoUpdater.ApplicationExitEvent += AutoUpdater_ApplicationExitEvent;
        //AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
        //AutoUpdater.InstalledVersion = getVersion();
    }

    private void FormMain_Disposed(object sender, EventArgs e)
    {
        isThreadRunning = false;
    }

    private void DalamudUpdaterIcon_MouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (!base.Visible)
            {
                base.Visible = true;
            }
        }
    }

    private void 显示ToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (!base.Visible)
        {
            base.Visible = true;
        }
    }

    private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Dispose();
        DalamudUpdaterIcon.Dispose();
        Application.Exit();
    }

    private void AutoUpdater_ApplicationExitEvent()
    {
        Text = "Closing application...";
        Thread.Sleep(5000);
        Application.Exit();
    }

    private void TryDownloadRuntime(DirectoryInfo runtimePath, string RuntimeVersion)
    {
        addLog($"开始拖库...");
        new Thread((ThreadStart)delegate
        {
            try
            {
                DownloadRuntime(runtimePath, RuntimeVersion);
            }
            catch (Exception)
            {
                MessageBox.Show("运行库下载失败 :(", "下载运行库", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }).Start();
    }

    private void client1_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        double num = double.Parse(e.BytesReceived.ToString());
        double num2 = double.Parse(e.TotalBytesToReceive.ToString());
        double percentage = num / num2 * 100.0;
        progressBar1.Invoke((MethodInvoker)delegate
        {
            progressBar1.Value = int.Parse(Math.Truncate(percentage).ToString());
        });
    }

    private void client1_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            MessageBox.Show($"dotnet运行库下载失败\n{e.Error}", "下载运行库", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            buttonCheckRuntime.Invoke((MethodInvoker)delegate
            {
                buttonCheckRuntime.Text = "重试下载";
                buttonCheckRuntime.Enabled = true;
            });
            return;
        }
        ZipFile.ExtractToDirectory(dotnetDownloadPath, runtimePath.FullName);
        File.Delete(dotnetDownloadPath);
        dotnetDownloadFinished = true;
        if (dotnetDownloadFinished && desktopDownloadFinished)
        {
            buttonCheckRuntime.Invoke((MethodInvoker)delegate
            {
                buttonCheckRuntime.Text = "下载完毕";
                buttonCheckRuntime.Enabled = true;
            });
            MessageBox.Show("运行库已下载 :)", "下载运行库", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }
    }

    private void client2_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        double num = double.Parse(e.BytesReceived.ToString());
        double num2 = double.Parse(e.TotalBytesToReceive.ToString());
        double percentage = num / num2 * 100.0;
        progressBar2.Invoke((MethodInvoker)delegate
        {
            progressBar2.Value = int.Parse(Math.Truncate(percentage).ToString());
        });
    }

    private void client2_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            MessageBox.Show($"desktop运行库下载失败\n{e.Error}", "下载运行库", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            buttonCheckRuntime.Invoke((MethodInvoker)delegate
            {
                buttonCheckRuntime.Text = "重试下载";
                buttonCheckRuntime.Enabled = true;
            });
            return;
        }
        ZipFile.ExtractToDirectory(desktopDownloadPath, runtimePath.FullName);
        File.Delete(desktopDownloadPath);
        desktopDownloadFinished = true;
        if (dotnetDownloadFinished && desktopDownloadFinished)
        {
            buttonCheckRuntime.Invoke((MethodInvoker)delegate
            {
                buttonCheckRuntime.Text = "下载完毕";
                buttonCheckRuntime.Enabled = true;
            });
            MessageBox.Show("运行库已下载 :)", "下载运行库", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }
    }

    private void DownloadRuntime(DirectoryInfo runtimePath, string version)
    {
        if (!runtimePath.Exists)
        {
            runtimePath.Create();
        }
        else
        {
            runtimePath.Delete(recursive: true);
            runtimePath.Create();
        }
        WebClient client1 = new WebClient();
        client1.DownloadProgressChanged += client1_DownloadProgressChanged;
        client1.DownloadFileCompleted += client1_DownloadFileCompleted;
        WebClient client2 = new WebClient();
        client2.DownloadProgressChanged += client2_DownloadProgressChanged;
        client2.DownloadFileCompleted += client2_DownloadFileCompleted;
        string text = (checkBoxAcce.Checked ? "https://dotnetcli.azureedge.net" : "https://dotnetcli.blob.core.windows.net");
        string dotnetUrl = text + "/dotnet/Runtime/" + version + "/dotnet-runtime-" + version + "-win-x64.zip";
        string desktopUrl = text + "/dotnet/WindowsDesktop/" + version + "/windowsdesktop-runtime-" + version + "-win-x64.zip";
        dotnetDownloadPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        desktopDownloadPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        if (File.Exists(dotnetDownloadPath))
        {
            File.Delete(dotnetDownloadPath);
        }
        if (File.Exists(desktopDownloadPath))
        {
            File.Delete(desktopDownloadPath);
        }
        buttonCheckRuntime.Invoke((MethodInvoker)delegate
        {
            buttonCheckRuntime.Text = "正在下载";
            buttonCheckRuntime.Enabled = false;
        });
        dotnetDownloadFinished = false;
        new Thread((ThreadStart)delegate
        {
            client1.DownloadFileAsync(new Uri(dotnetUrl), dotnetDownloadPath);
        }).Start();
        desktopDownloadFinished = false;
        new Thread((ThreadStart)delegate
        {
            client2.DownloadFileAsync(new Uri(desktopUrl), desktopDownloadPath);
        }).Start();
    }

    private void UpdateRuntimePaths()
    {

        runtimePath = new DirectoryInfo(Path.Combine(rootPath, "XIVLauncher", "runtime"));
        runtimePaths = new DirectoryInfo[3]
        {
            new DirectoryInfo(Path.Combine(runtimePath.FullName, "host", "fxr", RuntimeVersion)),
            new DirectoryInfo(Path.Combine(runtimePath.FullName, "shared", "Microsoft.NETCore.App", RuntimeVersion)),
            new DirectoryInfo(Path.Combine(runtimePath.FullName, "shared", "Microsoft.WindowsDesktop.App", RuntimeVersion))
        };
    }

    private void ButtonCheckRuntime_Click(object sender, EventArgs e)
    {
        addLog($"检查底裤...");
        UpdateRuntimePaths();
        if (runtimePaths.Any((DirectoryInfo p) => !p.Exists))
        {
            if (MessageBox.Show("运行达拉姆德需要下载所需运行库，是否下载？", "下载运行库", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
            {
                TryDownloadRuntime(runtimePath, RuntimeVersion);
            }
        }
        else if (MessageBox.Show("运行库已存在，是否强制下载？", "下载运行库", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
        {
            TryDownloadRuntime(runtimePath, RuntimeVersion);
        }
    }

    private string getUpdateUrl()
    {
        string result = updateUrl;
        if (checkBoxAcce.Checked)
        {
            result = updateUrl.Replace("/update", "/acce_update").Replace("ap-nanjing", "accelerate");
        }
        return result;
    }

    private void ButtonCheckForUpdate_Click(object sender, EventArgs e)
    {
        if (comboBoxFFXIV.SelectedItem != null)
        {
            Process processById = Process.GetProcessById(int.Parse((string)comboBoxFFXIV.SelectedItem));
            if (isInjected(processById))
            {
                if (MessageBox.Show("经检测存在 ffxiv_dx11.exe 进程，更新达拉姆德需要关闭游戏，需要帮您代劳吗？", "关闭游戏", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) != DialogResult.Yes)
                {
                    return;
                }
                processById.Kill();
            }
        }

        InitializeUpdate(true);
    }

    private void comboBoxFFXIV_Clicked(object sender, EventArgs e)
    {
    }

    private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        Process.Start("https://jq.qq.com/?_wv=1027&k=agTNLSBJ");
    }

    private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        pictureBox2.Visible = true;
        pictureBox1.Visible = false;
    }

    private string GeneratingDalamudStartInfo(Process process)
    {
        string directoryName = Path.GetDirectoryName(process.MainModule.FileName);
        string path = Path.Combine(Path.Combine(rootPath), "XIVLauncher");
        string gameVersion = File.ReadAllText(Path.Combine(directoryName, "ffxivgame.ver"));
        return JObject.FromObject(new
        {
            ConfigurationPath = Path.Combine(path, "dalamudConfig.json"),
            PluginDirectory = Path.Combine(path, "installedPlugins"),
            DefaultPluginDirectory = Path.Combine(path, "devPlugins"),
            AssetDirectory = Path.Combine(path, "dalamudAssets"),
            GameVersion = gameVersion,
            Language = "ChineseSimplified",
            OptOutMbCollection = false,
            GlobalAccelerate = checkBoxAcce.Checked
        }).ToString();
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
        catch
        {
        }
        return false;
    }

    private bool Inject(int pid)
    {
        addLog($"开始装载游戏：{pid}");
        try
        {
            Process processById = Process.GetProcessById(pid);
            if (isInjected(processById))
            {
                addLog($"已经装载，不用再装了：{pid}");
                SendNotification($"已经装载过了!", $"PID {pid}");
                return false;
            }
            Version version = getVersion();
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(rootPath, $"{version}"));
            Process.Start(new ProcessStartInfo(Path.Combine(directoryInfo.FullName, "Dalamud.Injector.exe"), string.Format(arg1: Convert.ToBase64String(Encoding.UTF8.GetBytes(GeneratingDalamudStartInfo(processById))), format: "{0} {1}", arg0: pid))
            {
                WorkingDirectory = directoryInfo.FullName
            });
            addLog($"装载完成：{pid}");
            SendNotification($"帮你装载了，可能会卡一会儿，不用谢。", $"PID {pid}");

            return true;
        }
        catch (Exception)
        {
            addLog($"装载失败，是不是崩了？：{pid}");
            return false;
        }
    }

    private void ButtonInject_Click(object sender, EventArgs e)
    {
        if (comboBoxFFXIV.SelectedItem != null && comboBoxFFXIV.SelectedItem.ToString().Length > 0)
        {
            if (int.TryParse(comboBoxFFXIV.SelectedItem.ToString(), out var result))
            {
                Inject(result);
            }
            else
            {
                MessageBox.Show("未能解析游戏进程ID", "找不到游戏", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
        }
        else
        {
            MessageBox.Show("未选择游戏进程", "找不到游戏", MessageBoxButtons.OK, MessageBoxIcon.Hand);
        }
    }

    private void checkBoxAutoInject_CheckedChanged(object sender, EventArgs e)
    {
        AddOrUpdateAppSettings("AutoInject", checkBoxAutoInject.Checked ? "true" : "false");
    }

    private void checkBoxAcce_CheckedChanged(object sender, EventArgs e)
    {
        AddOrUpdateAppSettings("Accelerate", checkBoxAcce.Checked ? "true" : "false");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
            this.components = new System.ComponentModel.Container();
            this.buttonCheckForUpdate = new System.Windows.Forms.Button();
            this.labelVersion = new System.Windows.Forms.Label();
            this.buttonCheckRuntime = new System.Windows.Forms.Button();
            this.comboBoxFFXIV = new System.Windows.Forms.ComboBox();
            this.buttonInject = new System.Windows.Forms.Button();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.checkBoxAcce = new System.Windows.Forms.CheckBox();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.checkBoxAutoInject = new System.Windows.Forms.CheckBox();
            this.DalamudUpdaterIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.显示ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.退出ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.progressBar2 = new System.Windows.Forms.ProgressBar();
            this.labelVer = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.delayTextBox = new System.Windows.Forms.TextBox();
            this.rootTextBox = new System.Windows.Forms.TextBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.openRootBtn = new System.Windows.Forms.Button();
            this.choseRootBtn = new System.Windows.Forms.Button();
            this.progressBar3 = new System.Windows.Forms.ProgressBar();
            this.logListBox = new System.Windows.Forms.ListBox();
            this.label5 = new System.Windows.Forms.Label();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.linkLabel3 = new System.Windows.Forms.LinkLabel();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.contextMenuStrip1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.groupBox4.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonCheckForUpdate
            // 
            this.buttonCheckForUpdate.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonCheckForUpdate.Location = new System.Drawing.Point(29, 36);
            this.buttonCheckForUpdate.Name = "buttonCheckForUpdate";
            this.buttonCheckForUpdate.Size = new System.Drawing.Size(433, 40);
            this.buttonCheckForUpdate.TabIndex = 0;
            this.buttonCheckForUpdate.Text = "检查更新，检了又检，新了又新";
            this.buttonCheckForUpdate.UseVisualStyleBackColor = true;
            this.buttonCheckForUpdate.Click += new System.EventHandler(this.ButtonCheckForUpdate_Click);
            // 
            // labelVersion
            // 
            this.labelVersion.AutoSize = true;
            this.labelVersion.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelVersion.Location = new System.Drawing.Point(10, 12);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(119, 15);
            this.labelVersion.TabIndex = 1;
            this.labelVersion.Text = "当前版本 : Unknown";
            // 
            // buttonCheckRuntime
            // 
            this.buttonCheckRuntime.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonCheckRuntime.Location = new System.Drawing.Point(19, 37);
            this.buttonCheckRuntime.Name = "buttonCheckRuntime";
            this.buttonCheckRuntime.Size = new System.Drawing.Size(221, 40);
            this.buttonCheckRuntime.TabIndex = 0;
            this.buttonCheckRuntime.Text = "下载运行库";
            this.buttonCheckRuntime.UseVisualStyleBackColor = true;
            this.buttonCheckRuntime.Click += new System.EventHandler(this.ButtonCheckRuntime_Click);
            // 
            // comboBoxFFXIV
            // 
            this.comboBoxFFXIV.Cursor = System.Windows.Forms.Cursors.Default;
            this.comboBoxFFXIV.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFFXIV.FormattingEnabled = true;
            this.comboBoxFFXIV.ImeMode = System.Windows.Forms.ImeMode.On;
            this.comboBoxFFXIV.Location = new System.Drawing.Point(19, 22);
            this.comboBoxFFXIV.Name = "comboBoxFFXIV";
            this.comboBoxFFXIV.Size = new System.Drawing.Size(141, 23);
            this.comboBoxFFXIV.TabIndex = 2;
            this.comboBoxFFXIV.Click += new System.EventHandler(this.comboBoxFFXIV_Clicked);
            // 
            // buttonInject
            // 
            this.buttonInject.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonInject.Location = new System.Drawing.Point(19, 51);
            this.buttonInject.Name = "buttonInject";
            this.buttonInject.Size = new System.Drawing.Size(433, 60);
            this.buttonInject.TabIndex = 0;
            this.buttonInject.Text = "点我立刻改善生活质量";
            this.buttonInject.UseVisualStyleBackColor = true;
            this.buttonInject.Click += new System.EventHandler(this.ButtonInject_Click);
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.LinkColor = System.Drawing.Color.DodgerBlue;
            this.linkLabel1.Location = new System.Drawing.Point(11, 492);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(64, 15);
            this.linkLabel1.TabIndex = 3;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "加入QQ群";
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // checkBoxAcce
            // 
            this.checkBoxAcce.AutoSize = true;
            this.checkBoxAcce.Location = new System.Drawing.Point(400, 11);
            this.checkBoxAcce.Name = "checkBoxAcce";
            this.checkBoxAcce.Size = new System.Drawing.Size(78, 19);
            this.checkBoxAcce.TabIndex = 4;
            this.checkBoxAcce.Text = "国际加速";
            this.checkBoxAcce.UseVisualStyleBackColor = true;
            this.checkBoxAcce.CheckedChanged += new System.EventHandler(this.checkBoxAcce_CheckedChanged);
            // 
            // linkLabel2
            // 
            this.linkLabel2.AutoSize = true;
            this.linkLabel2.LinkColor = System.Drawing.Color.LightCoral;
            this.linkLabel2.Location = new System.Drawing.Point(81, 492);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(83, 15);
            this.linkLabel2.TabIndex = 3;
            this.linkLabel2.TabStop = true;
            this.linkLabel2.Text = "❤爱抚小獭❤";
            this.linkLabel2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel2_LinkClicked);
            // 
            // checkBoxAutoInject
            // 
            this.checkBoxAutoInject.AutoSize = true;
            this.checkBoxAutoInject.Location = new System.Drawing.Point(166, 26);
            this.checkBoxAutoInject.Name = "checkBoxAutoInject";
            this.checkBoxAutoInject.Size = new System.Drawing.Size(104, 19);
            this.checkBoxAutoInject.TabIndex = 4;
            this.checkBoxAutoInject.Text = "自动改善生活";
            this.checkBoxAutoInject.UseVisualStyleBackColor = true;
            this.checkBoxAutoInject.CheckedChanged += new System.EventHandler(this.checkBoxAutoInject_CheckedChanged);
            // 
            // DalamudUpdaterIcon
            // 
            this.DalamudUpdaterIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            this.DalamudUpdaterIcon.BalloonTipText = "123";
            this.DalamudUpdaterIcon.BalloonTipTitle = "达拉姆德更新器";
            this.DalamudUpdaterIcon.ContextMenuStrip = this.contextMenuStrip1;
            this.DalamudUpdaterIcon.Text = "DalamudUpdater";
            this.DalamudUpdaterIcon.Visible = true;
            this.DalamudUpdaterIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(this.DalamudUpdaterIcon_MouseClick);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.显示ToolStripMenuItem,
            this.退出ToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(101, 48);
            // 
            // 显示ToolStripMenuItem
            // 
            this.显示ToolStripMenuItem.Name = "显示ToolStripMenuItem";
            this.显示ToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.显示ToolStripMenuItem.Text = "显示";
            this.显示ToolStripMenuItem.Click += new System.EventHandler(this.显示ToolStripMenuItem_Click);
            // 
            // 退出ToolStripMenuItem
            // 
            this.退出ToolStripMenuItem.Name = "退出ToolStripMenuItem";
            this.退出ToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.退出ToolStripMenuItem.Text = "退出";
            this.退出ToolStripMenuItem.Click += new System.EventHandler(this.退出ToolStripMenuItem_Click);
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(257, 67);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(195, 10);
            this.progressBar1.TabIndex = 6;
            // 
            // progressBar2
            // 
            this.progressBar2.Location = new System.Drawing.Point(257, 37);
            this.progressBar2.Name = "progressBar2";
            this.progressBar2.Size = new System.Drawing.Size(195, 10);
            this.progressBar2.TabIndex = 7;
            // 
            // labelVer
            // 
            this.labelVer.AutoSize = true;
            this.labelVer.Cursor = System.Windows.Forms.Cursors.Hand;
            this.labelVer.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelVer.ForeColor = System.Drawing.Color.DeepPink;
            this.labelVer.Location = new System.Drawing.Point(593, 492);
            this.labelVer.Name = "labelVer";
            this.labelVer.Size = new System.Drawing.Size(210, 15);
            this.labelVer.TabIndex = 8;
            this.labelVer.Text = "\\ (^・ω・^ )(^・ω・^ )( ^・ω・^)";
            this.labelVer.Click += new System.EventHandler(this.labelVer_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.buttonCheckRuntime);
            this.groupBox1.Controls.Add(this.progressBar2);
            this.groupBox1.Controls.Add(this.progressBar1);
            this.groupBox1.Location = new System.Drawing.Point(13, 102);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(468, 111);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "拖库";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.delayTextBox);
            this.groupBox2.Controls.Add(this.buttonInject);
            this.groupBox2.Controls.Add(this.comboBoxFFXIV);
            this.groupBox2.Controls.Add(this.checkBoxAutoInject);
            this.groupBox2.Location = new System.Drawing.Point(13, 219);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(468, 132);
            this.groupBox2.TabIndex = 10;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "跑路";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(437, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(12, 15);
            this.label3.TabIndex = 7;
            this.label3.Text = "s";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(338, 27);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(46, 15);
            this.label1.TabIndex = 6;
            this.label1.Text = "慢一点";
            // 
            // delayTextBox
            // 
            this.delayTextBox.Location = new System.Drawing.Point(384, 22);
            this.delayTextBox.Name = "delayTextBox";
            this.delayTextBox.Size = new System.Drawing.Size(52, 23);
            this.delayTextBox.TabIndex = 5;
            this.delayTextBox.TextChanged += new System.EventHandler(this.delayTextBox_TextChanged);
            this.delayTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.delayTextBox_KeyPress);
            // 
            // rootTextBox
            // 
            this.rootTextBox.Location = new System.Drawing.Point(19, 26);
            this.rootTextBox.Name = "rootTextBox";
            this.rootTextBox.Size = new System.Drawing.Size(268, 23);
            this.rootTextBox.TabIndex = 11;
            this.rootTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.rootTextBox_KeyPress);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label2);
            this.groupBox3.Controls.Add(this.openRootBtn);
            this.groupBox3.Controls.Add(this.choseRootBtn);
            this.groupBox3.Controls.Add(this.rootTextBox);
            this.groupBox3.Location = new System.Drawing.Point(13, 360);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(468, 126);
            this.groupBox3.TabIndex = 13;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "达拉姆德之家";
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(19, 52);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(433, 65);
            this.label2.TabIndex = 14;
            this.label2.Text = "可以换成你喜欢的地方❤ \r\n\r\n如果之前有使用过独立更新器（Dalamud.Updater），可以直接选择所在文件夹以复用配置。打开文件夹可以备份配置。";
            // 
            // openRootBtn
            // 
            this.openRootBtn.Location = new System.Drawing.Point(374, 26);
            this.openRootBtn.Name = "openRootBtn";
            this.openRootBtn.Size = new System.Drawing.Size(75, 23);
            this.openRootBtn.TabIndex = 13;
            this.openRootBtn.Text = "打开它";
            this.openRootBtn.UseVisualStyleBackColor = true;
            this.openRootBtn.Click += new System.EventHandler(this.openRootBtn_Click);
            // 
            // choseRootBtn
            // 
            this.choseRootBtn.Location = new System.Drawing.Point(293, 26);
            this.choseRootBtn.Name = "choseRootBtn";
            this.choseRootBtn.Size = new System.Drawing.Size(75, 23);
            this.choseRootBtn.TabIndex = 12;
            this.choseRootBtn.Text = "选择它...";
            this.choseRootBtn.UseVisualStyleBackColor = true;
            this.choseRootBtn.Click += new System.EventHandler(this.choseRootBtn_Click);
            // 
            // progressBar3
            // 
            this.progressBar3.Location = new System.Drawing.Point(30, 82);
            this.progressBar3.Name = "progressBar3";
            this.progressBar3.Size = new System.Drawing.Size(432, 10);
            this.progressBar3.TabIndex = 7;
            // 
            // logListBox
            // 
            this.logListBox.FormattingEnabled = true;
            this.logListBox.ItemHeight = 15;
            this.logListBox.Location = new System.Drawing.Point(496, 32);
            this.logListBox.Name = "logListBox";
            this.logListBox.Size = new System.Drawing.Size(307, 319);
            this.logListBox.TabIndex = 15;
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(16, 26);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(285, 49);
            this.label5.TabIndex = 16;
            this.label5.Text = "达拉姆德为开发人员提供了SE不能改善其功能的自由，同时官方禁止了可以给其他玩家带来不公平优势的插件。";
            this.label5.Click += new System.EventHandler(this.label5_Click);
            // 
            // pictureBox2
            // 
            this.pictureBox2.Image = global::ACTDaLaMa.Properties.Resources.ot;
            this.pictureBox2.Location = new System.Drawing.Point(281, 492);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(282, 288);
            this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox2.TabIndex = 17;
            this.pictureBox2.TabStop = false;
            this.pictureBox2.Visible = false;
            this.pictureBox2.Click += new System.EventHandler(this.pictureBox2_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::ACTDaLaMa.Properties.Resources.dog;
            this.pictureBox1.Location = new System.Drawing.Point(281, 492);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(282, 288);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 14;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Visible = false;
            this.pictureBox1.Click += new System.EventHandler(this.labelVer_Click);
            // 
            // linkLabel3
            // 
            this.linkLabel3.AutoSize = true;
            this.linkLabel3.LinkColor = System.Drawing.Color.DodgerBlue;
            this.linkLabel3.Location = new System.Drawing.Point(16, 94);
            this.linkLabel3.Name = "linkLabel3";
            this.linkLabel3.Size = new System.Drawing.Size(85, 15);
            this.linkLabel3.TabIndex = 18;
            this.linkLabel3.TabStop = true;
            this.linkLabel3.Text = "点我了解更多";
            this.linkLabel3.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.label5);
            this.groupBox4.Controls.Add(this.linkLabel3);
            this.groupBox4.Location = new System.Drawing.Point(496, 360);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(307, 126);
            this.groupBox4.TabIndex = 0;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "关于…";
            // 
            // DaLaMaPlugin
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.logListBox);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.progressBar3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.labelVer);
            this.Controls.Add(this.checkBoxAcce);
            this.Controls.Add(this.linkLabel2);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.buttonCheckForUpdate);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "DaLaMaPlugin";
            this.Size = new System.Drawing.Size(984, 612);
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.Disposed += new System.EventHandler(this.FormMain_Disposed);
            this.contextMenuStrip1.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

    }

    private Label lblStatus;


    public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
    {
        pluginScreenSpace.Text = "达拉姆德";
        lblStatus = pluginStatusText;   // Hand the status label's reference to our local var
        pluginScreenSpace.Controls.Add(this);   // Add this UserControl to the tab ACT provides
        this.Dock = DockStyle.Fill; // Expand the UserControl to fill the tab's client space
        InitializeConfig();
        GetFfxivPlugin();
        CheckExistingProcess();
        InitializePIDCheck();
        InitEnv();
        pluginStatusText.Text = "达拉姆德，起飞！";
    }


    private void CheckExistingProcess()
    {
        var dalamudProcesses = Process.GetProcessesByName("Dalamud.Updater");
        var dalamudProcess = dalamudProcesses.Length > 0 ? dalamudProcesses[0] : null;

        if (dalamudProcess != null)
        {
            var dlmPath = new FileInfo(dalamudProcess.MainModule.FileName).DirectoryName;
            if (dlmPath != rootPath && MessageBox.Show("检测到已在运行的达拉姆德，是否直接使用该路径以复用配置？", "已发现实例！", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                rootPath = new FileInfo(dalamudProcess.MainModule.FileName).DirectoryName;
                rootTextBox.Text = rootPath;
            }
        }
    }

    private void InitEnv()
    {
        addLog($"打扫场地...");
        try
        {
            if (!Directory.Exists(rootPath))
            {
                addLog("未找到当前路径，尝试创一下...");
                Directory.CreateDirectory(rootPath);
            }

            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }

            AddOrUpdateAppSettings("RootPath", rootPath);
        }
        catch (Exception)
        {
            addLog("路径检查失败...");
            MessageBox.Show("已经滚回以前了", "路径不对劲", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            InitializeConfig();
            return;
        }

        buttonInject.Enabled = false;

        addLog("装载配置...");
        InitializeConfig();
        UpdateRuntimePaths();
        new Thread((ThreadStart)delegate
        {
            Thread.Sleep(1000);
            if (runtimePaths.Any((DirectoryInfo p) => !p.Exists) && MessageBox.Show("运行达拉姆德需要下载所需运行库，是否下载？", "下载运行库", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
            {
                TryDownloadRuntime(runtimePath, RuntimeVersion);
            }

            buttonCheckRuntime.Text = "运行库已存在，不用下载了";
        }).Start();

        labelVersion.Text = $"达拉姆德版本 : {getVersion()}";

        InitializeUpdate();
        addLog($"初始化完成！");
    }

    private void InitializeUpdate(bool fallback = false)
    {
        addLog($"检查新玩意儿...");
        try
        {
            Version version = getVersion();
            string text = getUpdateUrl();
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load(text);
            foreach (XmlNode item in xmlDocument.SelectNodes("/item/version"))
            {
                if (item.InnerText != version.ToString())
                {
                    var url = xmlDocument.SelectSingleNode("/item/url");
                    var zipFilePath = Path.Combine(rootPath, "Dalamud.Updater.zip");

                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                        wc.DownloadFileCompleted += (o, de) =>
                        {
                            InitializeDeleteShit();
                            System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, rootPath);
                            MessageBox.Show("更新成功", "已经是最新的了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            labelVersion.Text = $"达拉姆德版本 : {getVersion()}";
                            buttonInject.Enabled = true;
                        };
                        wc.DownloadFileAsync(
                            // Param1 = Link of file
                            new System.Uri(url.InnerText),
                            // Param2 = Path to save
                            zipFilePath
                        );
                    }
                }
                else
                {
                    if (fallback)
                    {
                        MessageBox.Show("不用更了", "已经是最新的了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    buttonInject.Enabled = true;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "程序启动版本检查失败", MessageBoxButtons.OK, MessageBoxIcon.Hand);
        }
    }

    private void InitializeDeleteShit()
    {
        string path = Path.Combine(rootPath, "Dalamud.Updater.exe");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        progressBar3.Value = e.ProgressPercentage;
    }

    private void addLog(string s)
    {
        logListBox.Items.Add($"[{DateTime.Now.ToString("H:mm:ss")}] {s}");
    }

    public void DeInitPlugin()
    {
        throw new NotImplementedException();
    }

    private void labelVer_Click(object sender, EventArgs e)
    {
        MessageBox.Show("(^・ω・^ )", "(^・ω・^ )", MessageBoxButtons.OK, MessageBoxIcon.Question);
        MessageBox.Show("(^・ω・^ )( ^・ω・^)", "(^・ω・^ )( ^・ω・^)", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        MessageBox.Show("(^・ω・^ )( ^・ω・^)(^・ω・^ )", "(^・ω・^ )( ^・ω・^)(^・ω・^ )", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Question);
        MessageBox.Show("(^・ω・^ )( ^・ω・^)(^・ω・^ )( ^・ω・^)", "(^・ω・^ )( ^・ω・^)(^・ω・^ )( ^・ω・^)", MessageBoxButtons.OK, MessageBoxIcon.Information);
        pictureBox2.Visible = false;
        pictureBox1.Visible = !pictureBox1.Visible;
    }

    private void delayTextBox_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (!char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
        {
            e.Handled = true;
        }

        if ((e.KeyChar == '.') && ((sender as TextBox).Text.IndexOf('.') > -1))
        {
            e.Handled = true;
        }
    }

    private void delayTextBox_TextChanged(object sender, EventArgs e)
    {
        if (double.TryParse(delayTextBox.Text, out injectDelaySeconds))
        {
            AddOrUpdateAppSettings("InjectDelaySeconds", injectDelaySeconds.ToString());
        }
    }

    private void choseRootBtn_Click(object sender, EventArgs e)
    {
        FolderBrowserDialog folderDlg = new FolderBrowserDialog();
        folderDlg.ShowNewFolderButton = true;

        DialogResult result = folderDlg.ShowDialog();
        if (result == DialogResult.OK)
        {
            if (folderDlg.SelectedPath != rootPath)
            {
                rootPath = folderDlg.SelectedPath;
                rootTextBox.Text = rootPath;
                InitEnv();
            }
        }
    }

    private void rootTextBox_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)Keys.Return)
        {
            if (rootTextBox.Text != rootPath)
            {
                rootPath = rootTextBox.Text;
                InitEnv();
            }
        }
    }

    private void openRootBtn_Click(object sender, EventArgs e)
    {
        Process.Start(rootPath);
    }

    private void label5_Click(object sender, EventArgs e)
    {

    }

    private void pictureBox2_Click(object sender, EventArgs e)
    {
        pictureBox2.Visible = false;
    }

    private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        Process.Start("https://bbs.tggfl.com/topic/32/dalamud-%E5%8D%AB%E6%9C%88%E6%A1%86%E6%9E%B6");
    }
}
