using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZipToFolder
{
    public partial class MainForm : Form
    {
        protected class WorkerParam
        {
            public string FolderPath { get; set; } = "";

            public override string ToString()
            {
                return $"{FolderPath}";
            }
        }

        public MainForm()
        {
            InitializeComponent();
        }

        private void mainForm_Load(object sender, EventArgs e)
        {
            Log.Info("アプリケーション起動");
        }

        private void mainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Log.Info("アプリケーション終了");
        }

        private void folderPathRefButton_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                folderPathTextBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            if (bgWorker.IsBusy)
            {
                return;
            }

            var folderPath = folderPathTextBox.Text;
            if (string.IsNullOrEmpty(folderPath))
            {
                MessageBox.Show("フォルダパスが指定されていません", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!Directory.Exists(folderPath))
            {
                MessageBox.Show("フォルダパスが存在しません", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            startButton.Enabled = false;
            bgWorker.RunWorkerAsync(new WorkerParam()
            {
                FolderPath = folderPath,
            });
        }

        private void bgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (sender is not BackgroundWorker worker)
            {
                Log.Error("sender is not BackgroundWorker");
                e.Result = "処理パラメーターエラー";
                return;
            }
            if (e.Argument is not WorkerParam param)
            {
                Log.Error("e.Argument is not WorkerParam");
                e.Result = "処理パラメーターエラー";
                return;
            }

            Log.Info($"処理開始 param={param}");
            var zipFileList = Directory.GetFiles(param.FolderPath, "*.zip");
            if (zipFileList.Length == 0)
            {
                Log.Warn("フォルダ内にZIPファイルが見つからない");
                e.Result = "指定されたフォルダ内にZIPファイルが見つかりません";
                return;
            }

            worker.ReportProgress(zipFileList.Length);
            var pos = 0;
            var successCount = 0;
            var errorCount = 0;
            foreach (var zipFile in zipFileList)
            {
                pos++;
                worker.ReportProgress(pos, Path.GetFileName(zipFile));
                if (worker.CancellationPending)
                {
                    Log.Info("処理キャンセル検知");
                    e.Cancel = true;
                    return;
                }

                Log.Info($"処理ファイル={zipFile}");
                if (unzipFolder(zipFile))
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                }
            }

            Log.Info("処理完了");
            e.Result = $"処理結果 成功={successCount} 失敗={errorCount}";
        }

        private static bool unzipFolder(string zipFile)
        {
            var dirPath = Path.GetDirectoryName(zipFile);
            if (dirPath == null)
            {
                Log.Warn($"展開ZIPファイルパスはフルパスで指定してください file={zipFile}");
                return false;
            }
            var outputPath = Path.Combine(dirPath, Path.GetFileNameWithoutExtension(zipFile));
            if (Directory.Exists(outputPath))
            {
                Log.Warn($"フォルダが既に存在しています file={outputPath}");
                return false;
            }

            try
            {
                Directory.CreateDirectory(outputPath);
                ZipFile.ExtractToDirectory(zipFile, outputPath);
            }
            catch (Exception e)
            {
                Log.Error(e, "例外発生");
                return false;
            }

            return true;
        }

        private void bgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var message = "";
            if (e.UserState != null)
            {
                message = e.UserState.ToString();
            }

            if (message == "")
            {
                progressBar.Maximum = e.ProgressPercentage;
                progressBar.Value = 0;
                progressStatusLabel.Text = "";
            }
            else
            {
                progressBar.Value = e.ProgressPercentage;
                progressStatusLabel.Text = message;
            }
        }

        private void bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            startButton.Enabled = true;
            if (e.Cancelled)
            {
                MessageBox.Show("キャンセルされました", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                progressStatusLabel.Text = "キャンセルされました";
            }
            else if (e.Result == null)
            {
                MessageBox.Show("想定外のエラーが発生しました", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                progressStatusLabel.Text = "想定外のエラーが発生しました";
            }
            else
            {
                MessageBox.Show(e.Result.ToString(), "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                progressStatusLabel.Text = e.Result.ToString();
            }
        }
    }
}
