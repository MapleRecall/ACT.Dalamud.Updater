using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using Monitor.Core.Utilities;
using Serilog;
using XIVLauncher.Common.Dalamud;

namespace ACTDaLaMa;
public partial class DaLaMaPlugin : UserControl, IActPluginV1
{
    public class ProgressEventArgsEx
    {
        public int Percentage { get; set; }

        public string Text { get; set; }
    }

    private bool isThreadRunning = true;

    private double injectDelaySeconds = 10;

    private IContainer components;

    private Button buttonCheckForUpdate;

    private Label labelVersion;

    private Button buttonInject;

    private LinkLabel linkLabel1;

    private CheckBox checkBoxAcce;

    private LinkLabel linkLabel2;

    private CheckBox checkBoxAutoInject;

    private NotifyIcon DalamudUpdaterIcon;

    private ContextMenuStrip contextMenuStrip1;

    private ToolStripMenuItem 显示ToolStripMenuItem;

    private ToolStripMenuItem 退出ToolStripMenuItem;

    private Label labelVer;
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
    private PictureBox pictureBox3;
    private ComboBox comboBoxPs;
    private Label labelPV;
    private GroupBox groupBox1;
    private LinkLabel linkLabel4;
    private Label label4;
    private Button button1;
    private LinkLabel linkLabel5;
    private CheckBox checkBoxXL;
    private ToolTip toolTip1;
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

        if (GetAppSettings("AutoCheckXL", "true") == "true")
        {
            checkBoxXL.Checked = true;
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

    private void ButtonCheckForUpdate_Click(object sender, EventArgs e)
    {
        if (ffxivProcess != null)
        {
            if (isInjected(ffxivProcess))
            {
                if (MessageBox.Show("经检测存在 ffxiv_dx11.exe 进程，更新达拉姆德需要关闭游戏，需要帮您代劳吗？", "关闭游戏", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) != DialogResult.Yes)
                {
                    return;
                }
                ffxivProcess.Kill();
            }
        }

        InitializeUpdate(true);
    }

    private void comboBoxFFXIV_Clicked(object sender, EventArgs e)
    {
    }

    private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        //Process.Start("https://jq.qq.com/?_wv=1027&k=agTNLSBJ");
        Process.Start("https://qun.qq.com/qqweb/qunpro/share?_wv=3&_wwv=128&inviteCode=CZtWN&from=181074&biz=ka&shareSource=5");
    }

    private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        pictureBox2.Visible = true;
        pictureBox1.Visible = false;
    }

    private DalamudStartInfo GeneratingDalamudStartInfo(Process process, string dalamudPath)
    {
        var ffxivDir = Path.GetDirectoryName(process.MainModule.FileName);
        var appDataDir = Path.Combine(rootPath);
        var xivlauncherDir = Path.Combine(appDataDir, "XIVLauncher");

        var gameVerStr = File.ReadAllText(Path.Combine(ffxivDir, "ffxivgame.ver"));

        var startInfo = new DalamudStartInfo
        {
            ConfigurationPath = Path.Combine(xivlauncherDir, "dalamudConfig.json"),
            PluginDirectory = Path.Combine(xivlauncherDir, "installedPlugins"),
            DefaultPluginDirectory = Path.Combine(xivlauncherDir, "devPlugins"),
            AssetDirectory = dalamudUpdater.AssetDirectory.FullName,
            GameVersion = gameVerStr,
            Language = "4",
            OptOutMbCollection = false,
            GlobalAccelerate = this.checkBoxAcce.Checked,
            WorkingDirectory = dalamudPath
        };

        return startInfo;
    }

    private void ButtonInject_Click(object sender, EventArgs e)
    {
        if (int.TryParse((string)this.comboBoxPs.SelectedItem, out var pid))
        {
            _ = InjectAsync(pid);
        }
        else
        {
            MessageBox.Show("游戏未启动", "找不到游戏", MessageBoxButtons.OK, MessageBoxIcon.Hand);
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
            this.buttonInject = new System.Windows.Forms.Button();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.checkBoxAcce = new System.Windows.Forms.CheckBox();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.checkBoxAutoInject = new System.Windows.Forms.CheckBox();
            this.DalamudUpdaterIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.显示ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.退出ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.labelVer = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.comboBoxPs = new System.Windows.Forms.ComboBox();
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
            this.linkLabel3 = new System.Windows.Forms.LinkLabel();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.pictureBox3 = new System.Windows.Forms.PictureBox();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.labelPV = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.checkBoxXL = new System.Windows.Forms.CheckBox();
            this.button1 = new System.Windows.Forms.Button();
            this.linkLabel4 = new System.Windows.Forms.LinkLabel();
            this.label4 = new System.Windows.Forms.Label();
            this.linkLabel5 = new System.Windows.Forms.LinkLabel();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.contextMenuStrip1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.groupBox1.SuspendLayout();
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
            // buttonInject
            // 
            this.buttonInject.Enabled = false;
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
            this.linkLabel1.Location = new System.Drawing.Point(17, 501);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(77, 15);
            this.linkLabel1.TabIndex = 3;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "加入QQ频道";
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
            this.linkLabel2.Location = new System.Drawing.Point(100, 501);
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
            this.checkBoxAutoInject.Location = new System.Drawing.Point(134, 25);
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
            // labelVer
            // 
            this.labelVer.AutoSize = true;
            this.labelVer.Cursor = System.Windows.Forms.Cursors.Hand;
            this.labelVer.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelVer.ForeColor = System.Drawing.Color.DeepPink;
            this.labelVer.Location = new System.Drawing.Point(599, 501);
            this.labelVer.Name = "labelVer";
            this.labelVer.Size = new System.Drawing.Size(210, 15);
            this.labelVer.TabIndex = 8;
            this.labelVer.Text = "\\ (^・ω・^ )(^・ω・^ )( ^・ω・^)";
            this.labelVer.Click += new System.EventHandler(this.labelVer_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.comboBoxPs);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.delayTextBox);
            this.groupBox2.Controls.Add(this.buttonInject);
            this.groupBox2.Controls.Add(this.checkBoxAutoInject);
            this.groupBox2.Location = new System.Drawing.Point(14, 98);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(468, 128);
            this.groupBox2.TabIndex = 10;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "跑路";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(428, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(20, 15);
            this.label3.TabIndex = 7;
            this.label3.Text = "秒";
            // 
            // comboBoxPs
            // 
            this.comboBoxPs.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxPs.FormattingEnabled = true;
            this.comboBoxPs.Location = new System.Drawing.Point(22, 22);
            this.comboBoxPs.Name = "comboBoxPs";
            this.comboBoxPs.Size = new System.Drawing.Size(106, 23);
            this.comboBoxPs.TabIndex = 18;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(343, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(46, 15);
            this.label1.TabIndex = 6;
            this.label1.Text = "慢一点";
            // 
            // delayTextBox
            // 
            this.delayTextBox.Location = new System.Drawing.Point(395, 21);
            this.delayTextBox.Name = "delayTextBox";
            this.delayTextBox.Size = new System.Drawing.Size(32, 23);
            this.delayTextBox.TabIndex = 5;
            this.delayTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.delayTextBox.TextChanged += new System.EventHandler(this.delayTextBox_TextChanged);
            this.delayTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.delayTextBox_KeyPress);
            // 
            // rootTextBox
            // 
            this.rootTextBox.Location = new System.Drawing.Point(19, 26);
            this.rootTextBox.Name = "rootTextBox";
            this.rootTextBox.Size = new System.Drawing.Size(268, 23);
            this.rootTextBox.TabIndex = 11;
            this.rootTextBox.Click += new System.EventHandler(this.choseRootBtn_Click);
            this.rootTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.rootTextBox_KeyPress);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.label2);
            this.groupBox3.Controls.Add(this.openRootBtn);
            this.groupBox3.Controls.Add(this.choseRootBtn);
            this.groupBox3.Controls.Add(this.rootTextBox);
            this.groupBox3.Location = new System.Drawing.Point(14, 232);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(468, 161);
            this.groupBox3.TabIndex = 13;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "达拉姆德工作目录";
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(19, 52);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(433, 95);
            this.label2.TabIndex = 14;
            this.label2.Text = "可以换成你喜欢的地方❤ \r\n\r\n如果之前有使用过更新器（Dalamud.Updater），可以直接选择所在目录以复用配置。 \r\n\r\n点击“打开目录”来备份配置或寻" +
    "找日志文件（Dalamud.Updater.ACT.log）。";
            // 
            // openRootBtn
            // 
            this.openRootBtn.Location = new System.Drawing.Point(374, 26);
            this.openRootBtn.Name = "openRootBtn";
            this.openRootBtn.Size = new System.Drawing.Size(75, 23);
            this.openRootBtn.TabIndex = 13;
            this.openRootBtn.Text = "打开目录";
            this.openRootBtn.UseVisualStyleBackColor = true;
            this.openRootBtn.Click += new System.EventHandler(this.openRootBtn_Click);
            // 
            // choseRootBtn
            // 
            this.choseRootBtn.Location = new System.Drawing.Point(293, 26);
            this.choseRootBtn.Name = "choseRootBtn";
            this.choseRootBtn.Size = new System.Drawing.Size(75, 23);
            this.choseRootBtn.TabIndex = 12;
            this.choseRootBtn.Text = "选择...";
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
            this.logListBox.Size = new System.Drawing.Size(307, 229);
            this.logListBox.TabIndex = 15;
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(6, 26);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(189, 80);
            this.label5.TabIndex = 16;
            this.label5.Text = "达拉姆德为开发人员提供了SE不能改善其功能的自由，同时官方禁止了可以给其他玩家带来不公平优势的插件。";
            this.label5.Click += new System.EventHandler(this.label5_Click);
            // 
            // linkLabel3
            // 
            this.linkLabel3.AutoSize = true;
            this.linkLabel3.LinkColor = System.Drawing.Color.DodgerBlue;
            this.linkLabel3.Location = new System.Drawing.Point(6, 102);
            this.linkLabel3.Name = "linkLabel3";
            this.linkLabel3.Size = new System.Drawing.Size(85, 15);
            this.linkLabel3.TabIndex = 18;
            this.linkLabel3.TabStop = true;
            this.linkLabel3.Text = "点我了解更多";
            this.linkLabel3.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.linkLabel3);
            this.groupBox4.Controls.Add(this.pictureBox3);
            this.groupBox4.Controls.Add(this.label5);
            this.groupBox4.Location = new System.Drawing.Point(496, 267);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(307, 126);
            this.groupBox4.TabIndex = 0;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "关于…";
            // 
            // pictureBox3
            // 
            this.pictureBox3.Image = global::ACTDaLaMa.Properties.Resources.logo;
            this.pictureBox3.Location = new System.Drawing.Point(201, 22);
            this.pictureBox3.Name = "pictureBox3";
            this.pictureBox3.Size = new System.Drawing.Size(100, 95);
            this.pictureBox3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox3.TabIndex = 19;
            this.pictureBox3.TabStop = false;
            // 
            // pictureBox2
            // 
            this.pictureBox2.Image = global::ACTDaLaMa.Properties.Resources.ot;
            this.pictureBox2.Location = new System.Drawing.Point(19, 519);
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
            this.pictureBox1.Location = new System.Drawing.Point(527, 519);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(282, 288);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 14;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Visible = false;
            this.pictureBox1.Click += new System.EventHandler(this.labelVer_Click);
            // 
            // labelPV
            // 
            this.labelPV.AutoSize = true;
            this.labelPV.Location = new System.Drawing.Point(493, 11);
            this.labelPV.Name = "labelPV";
            this.labelPV.Size = new System.Drawing.Size(72, 15);
            this.labelPV.TabIndex = 18;
            this.labelPV.Text = "插件版本：";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.checkBoxXL);
            this.groupBox1.Controls.Add(this.button1);
            this.groupBox1.Controls.Add(this.linkLabel4);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Location = new System.Drawing.Point(14, 399);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(789, 100);
            this.groupBox1.TabIndex = 19;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "XIV 蓝鹊儿 [NEW]";
            // 
            // checkBoxXL
            // 
            this.checkBoxXL.AutoSize = true;
            this.checkBoxXL.Location = new System.Drawing.Point(527, 69);
            this.checkBoxXL.Name = "checkBoxXL";
            this.checkBoxXL.Size = new System.Drawing.Size(104, 19);
            this.checkBoxXL.TabIndex = 3;
            this.checkBoxXL.Text = "自动提示同步";
            this.toolTip1.SetToolTip(this.checkBoxXL, "插件启动时自动检查蓝雀儿");
            this.checkBoxXL.UseVisualStyleBackColor = true;
            this.checkBoxXL.CheckedChanged += new System.EventHandler(this.checkBoxXL_CheckedChanged);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(637, 66);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(146, 23);
            this.button1.TabIndex = 2;
            this.button1.Text = "蓝鹊儿配制同步助手";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // linkLabel4
            // 
            this.linkLabel4.AutoSize = true;
            this.linkLabel4.LinkColor = System.Drawing.Color.DodgerBlue;
            this.linkLabel4.Location = new System.Drawing.Point(16, 70);
            this.linkLabel4.Name = "linkLabel4";
            this.linkLabel4.Size = new System.Drawing.Size(85, 15);
            this.linkLabel4.TabIndex = 1;
            this.linkLabel4.TabStop = true;
            this.linkLabel4.Text = "点我去看看！";
            this.linkLabel4.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel4_LinkClicked);
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.Location = new System.Drawing.Point(16, 28);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(767, 33);
            this.label4.TabIndex = 0;
            this.label4.Text = "XIV 蓝鹊儿（简写为 XL）是用于最终幻想14的一个多功能启动器，包含各种可用的附加组件和游戏增强功能，例如自动启动ACT和更方便的改善生活功能。如果你是盛趣帐" +
    "号的玩家可以试试哦~";
            // 
            // linkLabel5
            // 
            this.linkLabel5.AutoSize = true;
            this.linkLabel5.LinkColor = System.Drawing.Color.DodgerBlue;
            this.linkLabel5.Location = new System.Drawing.Point(743, 11);
            this.linkLabel5.Name = "linkLabel5";
            this.linkLabel5.Size = new System.Drawing.Size(59, 15);
            this.linkLabel5.TabIndex = 20;
            this.linkLabel5.TabStop = true;
            this.linkLabel5.Text = "更新日志";
            this.linkLabel5.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel5_LinkClicked);
            // 
            // DaLaMaPlugin
            // 
            this.AccessibleDescription = "123456";
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Controls.Add(this.linkLabel5);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.labelPV);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.logListBox);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.progressBar3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.labelVer);
            this.Controls.Add(this.checkBoxAcce);
            this.Controls.Add(this.linkLabel2);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.buttonCheckForUpdate);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "DaLaMaPlugin";
            this.Size = new System.Drawing.Size(984, 821);
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.Disposed += new System.EventHandler(this.FormMain_Disposed);
            this.contextMenuStrip1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
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

        _ = CheckPluginVersionAsync();
        InitializeConfig();
        GetFfxivPlugin();
        CheckExistingProcess();
        InitLogging();
        InitEnv();

        InitializePIDCheck();

        pluginStatusText.Text = "达拉姆德，起飞！";
    }


    private void CheckExistingProcess()
    {
        var dalamudProcesses = Process.GetProcessesByName("Dalamud.Updater");
        var dalamudProcess = dalamudProcesses.Length > 0 ? dalamudProcesses[0] : null;

        if (dalamudProcess != null)
        {
            var dlmPath = new FileInfo(dalamudProcess.MainModule.FileName).DirectoryName;
            if (dlmPath != rootPath && MessageBox.Show("检测到已在运行的达拉姆德更新器，是否直接使用该路径以复用配置？", "已发现实例！", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                rootPath = new FileInfo(dalamudProcess.MainModule.FileName).DirectoryName;
                rootTextBox.Text = rootPath;
            }
        }

        if (!checkBoxXL.Checked)
        {
            return;
        }

        var xlProcesses = Process.GetProcessesByName("XIVLauncherCN");
        var xlProcess = xlProcesses.Length > 0 ? xlProcesses[0] : null;

        if (xlProcess != null && MessageBox.Show("检测到已在运行的XIV蓝鹊儿，是否同步配置？", "已发现蓝鹊儿！", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            syncPlugin();
        }
    }

    private void syncPlugin()
    {
        var xlProcesses = Process.GetProcessesByName("XIVLauncherCN");
        var xlProcess = xlProcesses.Length > 0 ? xlProcesses[0] : null;

        if (xlProcess != null)
        {
            var xlPath = new FileInfo(xlProcess.MainModule.FileName).DirectoryName;
            var xlDirectory = new DirectoryInfo(xlPath).Parent.FullName;
            var xlDataPath = Path.Combine(xlDirectory, "Roaming");
            var rootDataPath = Path.Combine(rootPath, "XIVLauncher");

            if (!Directory.Exists(xlDataPath))
            {
                Directory.CreateDirectory(xlDataPath);
            }

            if (!Directory.Exists(rootDataPath))
            {
                Directory.CreateDirectory(rootDataPath);
            }

            if (JunctionPoint.Exists(xlDataPath) || JunctionPoint.Exists(rootDataPath))
            {
                MessageBox.Show("看起来你的配置文件夹已经被关联了，不用操作了~", "Wow!");
                return;
            }

            var result = MessageBox.Show("你想用哪种方法同步呢？\r\n\r\n[是]：传统而安全稳妥的复制粘贴\r\n[否]：快速简单省空间的文件夹链接\r\n[取消]：我再想想……", "命运二选一", MessageBoxButtons.YesNoCancel);

            if (result == DialogResult.Cancel)
            {
                return;
            }

            try
            {
                xlProcess.Kill();
            }
            catch (Exception) { }

            Thread.Sleep(1000);

            if (result == DialogResult.No)
            {
                try
                {
                    var xlConfig = Path.Combine(xlDataPath, "launcherConfigV3.json");
                    var rootConfig = Path.Combine(rootDataPath, "launcherConfigV3.json");
                    File.Copy(xlConfig, rootConfig, true);
                }
                catch (Exception) { }

                var backup = $"{xlDataPath}_backup_{DateTime.Now.Ticks}";

                Directory.Move(xlDataPath, backup);

                try
                {
                    JunctionPoint.Create(xlDataPath, rootDataPath, false);
                    MessageBox.Show($"创建完成！请注意不要删除原来的配制文件！", "");
                }
                catch (Exception ex)
                {
                    Directory.Move(backup, xlDataPath);
                    Log.Error(ex, "创建链接失败！");
                    addLog(ex.ToString());

                    MessageBox.Show($"出错了！试试别的方法吧...\r\n{ex}", "");
                    return;
                }
            }
            else if (result == DialogResult.Yes)
            {
                CreateInfoFile(xlDataPath, "1_这是【启动器】的Dalamud配置目录");
                CreateInfoFile(xlDataPath, "2_你需要把之前的配置【粘贴】进来");
                CreateInfoFile(xlDataPath, "3_遇到重复文件直接替换就好");
                CreateInfoFile(xlDataPath, "4_操作完成后可以把这些指引删除~");

                CreateInfoFile(rootDataPath, "1_这是【更新器】或【ACT插件】的Dalamud配置目录");
                CreateInfoFile(rootDataPath, "2_你需要把这儿的文件【复制】出去");
                CreateInfoFile(rootDataPath, "3_遇到重复文件直接替换就好");
                CreateInfoFile(rootDataPath, "4_操作完成后可以把这些指引删除~");


                MessageBox.Show("请根据两个文件夹内的文件指引来同步文件！", "看我看我！");

                Process.Start("explorer.exe", rootDataPath);
                Process.Start("explorer.exe", xlDataPath);
            }
        }
        else
        {
            MessageBox.Show("请先启动XIV蓝鹊儿（XIVLauncherCN.exe）再试试~");
        }

    }

    private void CreateInfoFile(string path, string name)
    {
        try
        {
            File.Create(Path.Combine(path, name)).Dispose();
        }
        catch (Exception) { }
    }

    private void DalamudUpdater_OnUpdateEvent(DalamudUpdater.DownloadState value, Exception ex = null)
    {
        var verStr = string.Format("卫月版本 : {0}", getVersion());
        if (this.labelVersion.InvokeRequired)
        {
            Action<string> actionDelegate = (x) => { labelVersion.Text = verStr; };
            this.labelVersion.Invoke(actionDelegate, verStr);
        }
        else
        {
            labelVersion.Text = verStr;
        }

        switch (value)
        {
            case DalamudUpdater.DownloadState.Failed:
                buttonInject.Enabled = MessageBox.Show("更新Dalamud失败，要直接用么？", "獭纪委", MessageBoxButtons.YesNo) == DialogResult.Yes;
                setStatus("更新Dalamud失败");
                break;
            case DalamudUpdater.DownloadState.Unknown:
                setStatus("未知错误");
                buttonInject.Enabled = Enabled;
                break;
            case DalamudUpdater.DownloadState.NoIntegrity:
                setStatus("卫月与游戏不兼容");
                buttonInject.Enabled = Enabled;
                break;
            case DalamudUpdater.DownloadState.Done:
                setStatus("更新成功");
                buttonInject.Enabled = Enabled;
                break;
        }
    }

    private void setStatus(string v)
    {
        addLog(v);
    }

    private void setProgressBar(int v)
    {
        this.progressBar3.Value = v;
    }

    private void setVisible(bool v)
    {
        progressBar3.Visible = v;
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

        labelVersion.Text = $"达拉姆德版本 : {getVersion()}";

        InitializeUpdate();
        addLog($"初始化完成！");
    }

    private void InitializeUpdate(bool feedback = false)
    {
        addLog($"检查新玩意儿...");
        try
        {
            InitUpdater();
            dalamudUpdater.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "程序启动版本检查失败", MessageBoxButtons.OK, MessageBoxIcon.Hand);
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
        Process.Start("explorer.exe", rootPath);
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

    private void linkLabel4_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        Process.Start("https://shimo.im/docs/m5kv952DMGtBvLqX/read");
    }

    private void button1_Click(object sender, EventArgs e)
    {
        syncPlugin();
    }

    private void linkLabel5_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        MessageBox.Show(changeLog, "更新日志");
    }

    private void checkBoxXL_CheckedChanged(object sender, EventArgs e)
    {
        AddOrUpdateAppSettings("AutoCheckXL", checkBoxXL.Checked ? "true" : "false");
    }
}
