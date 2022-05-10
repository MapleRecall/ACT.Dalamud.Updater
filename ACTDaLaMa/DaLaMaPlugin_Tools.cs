using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;

namespace ACTDaLaMa;

public partial class DaLaMaPlugin
{
    private FFXIV_ACT_Plugin.FFXIV_ACT_Plugin _ffxivPlugin;

    private Process ffxivProcess => _ffxivPlugin.DataRepository.GetCurrentFFXIVProcess();

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
}