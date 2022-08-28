using System;
using XIVLauncher.Common.Dalamud;

namespace Dalamud.Updater
{
    internal class DalamudLoadingOverlay : IDalamudLoadingOverlay
    {
        public delegate void progressBar(int value);
        public delegate void statusLabel(string value);
        public delegate void setVisible(bool value);
        public event progressBar OnProgressBar;
        public event statusLabel OnStatusLabel;
        public event setVisible OnSetVisible;
        public DalamudLoadingOverlay()
        {
            //this.progressBar = form.toolStripProgressBar1;
            //this.statusLabel = form.toolStripStatusLabel1;
        }
        public void ReportProgress(long? size, long downloaded, double? progress)
        {
            size = size ?? 0;
            progress = progress ?? 0;
            OnProgressBar?.Invoke((int)progress.Value);
        }

        public void SetInvisible()
        {
            OnSetVisible?.Invoke(false);
        }

        public void SetStep(IDalamudLoadingOverlay.DalamudUpdateStep progress)
        {
            switch (progress)
            {
                // 文本太长会一个字都不显示
                case IDalamudLoadingOverlay.DalamudUpdateStep.Dalamud:
                    OnStatusLabel?.Invoke("正在更新核心");
                    break;

                case IDalamudLoadingOverlay.DalamudUpdateStep.Assets:
                    OnStatusLabel?.Invoke("正在更新资源");
                    break;

                case IDalamudLoadingOverlay.DalamudUpdateStep.Runtime:
                    OnStatusLabel?.Invoke("正在更新运行库");
                    break;

                case IDalamudLoadingOverlay.DalamudUpdateStep.Unavailable:
                    OnStatusLabel?.Invoke("暂时无法使用");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(progress), progress, null);
            }
        }

        public void SetVisible()
        {
            OnSetVisible?.Invoke(true);
        }
    }
}
