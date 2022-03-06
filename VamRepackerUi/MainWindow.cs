using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autofac;
using MoreLinq;
using Newtonsoft.Json;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;
using VamRepacker.Operations.Destructive;
using VamRepacker.Operations.NotDestructive;
using VamRepacker.Operations.Repo;

namespace VamRepackerUi
{
    public partial class MainWindow : Form, IProgressTracker
    {
        public const long ReportEveryTicks = 500 * TimeSpan.TicksPerMillisecond;
        private readonly Stopwatch _stopwatch = new();
        private long _nextReport;
        private int _stage, _totalStages;

        private readonly ILifetimeScope _ctx;
        private Dictionary<string, bool> _buttonsState = new();
        private List<ProfileModel> _profiles = new();

        private List<VarPackage> _vars;
        private IList<FreeFile> _freeFiles;
        private bool _working;
        private Stopwatch _sw = new();

        public MainWindow(ILifetimeScope ctx)
        {
            _ctx = ctx;
            InitializeComponent();

            Text += Assembly.GetExecutingAssembly().GetName().Version;
            comboThreads.Items.AddRange(Enumerable.Range(1, Environment.ProcessorCount).Cast<object>().ToArray());
            operationStatusLabel.Text = string.Empty;

            LoadSettings();
        }

        private void selectVamDirBtn_Click(object sender, EventArgs e)
        {
            var (selected, vamDir) = AskFirDirectory();
            if (!selected)
                return;
            if (!Directory.Exists(Path.Combine(vamDir, "AddonPackages")))
            {
                MessageBox.Show("VaM dir doesn't contain AddonPackages");
                vamDirTxt.Text = string.Empty;
            }
            else
            {
                vamDirTxt.Text = vamDir;
            }
        }

        private void additionalVarsBtn_Click(object sender, EventArgs e)
        {
            var (selected, dir) = AskFirDirectory();
            if (selected)
                additionalVarsDir.Text = dir;
        }

        private async void copyMissingDepsFromRepoBtn_Click(object sender, EventArgs e)
        {
            var ctx = GetContext(stages: 5);

            await RunIndexing(ctx);
            await using var scope = _ctx.BeginLifetimeScope();
            await scope.Resolve<IScanJsonFilesOperation>()
                .ExecuteAsync(ctx, _freeFiles, _vars);
            await RemoveOldLinks(scope, ctx);
            await scope.Resolve<ICopyMissingVarDependenciesFromRepo>()
                .ExecuteAsync(ctx, _vars, _freeFiles, moveMissingDepsChk.Checked, shallowChk.Checked);

            if (MessageBox.Show("Do you want to try to download missing vars from HUB?", "Hub",
                    MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                _totalStages++;
                await scope.Resolve<IDownloadMissingVars>()
                    .ExecuteAsync(ctx, _vars, _freeFiles);
            }

            SwitchUI(false);
        }

        private async void softLinkVarsBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(additionalVarsDir.Text))
            {
                MessageBox.Show("Missing additional directory for REPO vars");
                return;
            }

            if (profilesListBox.CheckedItems.Count == 0 &&
                MessageBox.Show("Nothing was selected, everything from repo will be linked. Continue?", "", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }

            var ctx = GetContext(stages: 5);
            await RunIndexing(ctx);
            await using var scope = _ctx.BeginLifetimeScope();
            await scope.Resolve<IScanJsonFilesOperation>()
                .ExecuteAsync(ctx, _freeFiles, _vars, BuildFilters());
            await RemoveOldLinks(scope, ctx);
            await scope.Resolve<ICopySelectedVarsWithDependenciesFromRepo>()
                .ExecuteAsync(ctx, _vars, BuildFilters());

            SwitchUI(false);
        }

        private (bool, string) AskFirDirectory(string root = null)
        {
            using var odf = new FolderBrowserDialog();
            if (root != null)
                odf.SelectedPath = root;
            var result = odf.ShowDialog();
            return (result == DialogResult.OK, odf.SelectedPath?.NormalizePathSeparators());
        }

        public void InitProgress(string message) => RunInvokedInvoke(() =>
           {
               _stopwatch.Start();
               progressBar.Value = 0;
               progressBar.Style = ProgressBarStyle.Blocks;
               MoveToStage(message);
               SwitchUI(true);
           });

        public void Report(ProgressInfo progress)
        {
            if (_stopwatch.ElapsedTicks <= _nextReport)
            {
                return;
            }

            _nextReport = _stopwatch.ElapsedTicks + ReportEveryTicks;
            RunInvokedInvoke(() =>
            {
                operationStatusLabel.Text = progress.Current;
                if (progress.Total == 0)
                {
                    progressBar.Style = ProgressBarStyle.Marquee;
                    return;
                }
                else if (progressBar.Style != ProgressBarStyle.Blocks)
                {
                    progressBar.Style = ProgressBarStyle.Blocks;
                }

                if (progressBar.Maximum != progress.Total)
                    progressBar.Maximum = progress.Total;
                progressBar.Value = progress.Processed;
            });
        }

        public void Report(string message) => Report(new ProgressInfo(message));

        public void Complete(string resultMessage) => RunInvokedInvoke(() =>
        {
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Value = progressBar.Maximum;
            operationStatusLabel.Text = resultMessage;
        });


        void SwitchUI(bool working)
        {
            if (_working == working) return;
            if(working) _sw.Restart();

            _working = working;
            var controls = Controls
                .OfType<Control>()
                .Where(t => t is not Label)
                .ToList();

            if (_buttonsState.Count > 0 && working)
                throw new InvalidOperationException();

            if (working)
            {
                _buttonsState = controls
                    .ToDictionary(t => t.Name, t => t.Enabled);
                controls.ForEach(t => t.Enabled = false);
            }
            else
            {
                controls.ForEach(t => t.Enabled = _buttonsState[t.Name]);
                _buttonsState.Clear();
            }

            if (!working)
            {
                _sw.Stop();
                stageTxt.Text = $"Finished in {_sw.Elapsed.Minutes}min and {_sw.Elapsed.Seconds}s";
                operationStatusLabel.Text = string.Empty;
            }
        }

        private OperationContext GetContext(int stages)
        {
            _totalStages = stages;
            _stage = 0;

            var ctx = new OperationContext
            {
                DryRun = dryRunCheckbox.Checked,
                Threads = (int)comboThreads.SelectedItem,
                RepoDir = additionalVarsDir.Text,
                VamDir = vamDirTxt.Text,
                ShallowDeps = shallowChk.Checked
            };
            return ctx;
        }

        private void RunInvokedInvoke(Action action)
        {
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.additionalVars = additionalVarsDir.Text;
            Properties.Settings.Default.vamDir = vamDirTxt.Text;
            Properties.Settings.Default.numberOfThreads = (int)comboThreads.SelectedItem;
            Properties.Settings.Default.removeSoftLinksBefore = removeAllSoftLinkBeforeChk.Checked;
            Properties.Settings.Default.shallow = shallowChk.Checked;
            Properties.Settings.Default.profiles = JsonConvert.SerializeObject(_profiles);
            Properties.Settings.Default.Save();
            _ctx.Dispose();
        }

        private void LoadSettings()
        {
            additionalVarsDir.Text = Properties.Settings.Default.additionalVars;
            vamDirTxt.Text = Properties.Settings.Default.vamDir;
            comboThreads.SelectedItem = Properties.Settings.Default.numberOfThreads == 0 ? Environment.ProcessorCount : Properties.Settings.Default.numberOfThreads;
            removeAllSoftLinkBeforeChk.Checked = Properties.Settings.Default.removeSoftLinksBefore;
            shallowChk.Checked = Properties.Settings.Default.shallow;

            if (!string.IsNullOrEmpty(Properties.Settings.Default.profiles))
            {
                _profiles = JsonConvert.DeserializeObject<List<ProfileModel>>(Properties.Settings.Default.profiles);
            }

            ReloadProfiles();
        }

        private async Task RunIndexing(OperationContext operationContext)
        {
            await using var scope = _ctx.BeginLifetimeScope();
            _freeFiles = await scope.Resolve<IScanFilesOperation>()
                .ExecuteAsync(operationContext);
            _vars = await scope.Resolve<IScanVarPackagesOperation>()
                .ExecuteAsync(operationContext, _freeFiles);
        }

        private async void scanInvalidVars_Btn_Click(object sender, EventArgs e)
        {
            await RunIndexing(GetContext(stages: 2));
            SwitchUI(false);
        }

        private async void scanJsonFilesBtn_Click(object sender, EventArgs e)
        {
            await using var scope = _ctx.BeginLifetimeScope();
            var ctx = GetContext(stages: 3);
            await RunIndexing(ctx);
            await scope.Resolve<IScanJsonFilesOperation>()
                .ExecuteAsync(ctx, _freeFiles, _vars);
            SwitchUI(false);
        }

        private void manageProfilesBtn_Click(object sender, EventArgs e)
        {
            if(string.IsNullOrEmpty(additionalVarsDir.Text))
            {
                MessageBox.Show("Select repo dir virs");
                return;
            }

            using var manageProfiles = new ManageProfiles(_profiles, additionalVarsDir.Text);
            if (manageProfiles.ShowDialog() == DialogResult.OK)
            {
                _profiles = manageProfiles.Profiles;
                ReloadProfiles();
            }
        }

        private void ReloadProfiles()
        {
            profilesListBox.Items.Clear();
            _profiles.ForEach(t => profilesListBox.Items.Add(t));
        }

        private IVarFilters BuildFilters()
        {
            var filters = new VarFilters();
            profilesListBox.CheckedItems
                .Cast<ProfileModel>()
                .ForEach(filters.FromProfile);

            return filters;
        }

        private async void deduplicateAssetsBtn_Click(object sender, EventArgs e)
        {
            var ctx = GetContext(stages: 5);
            await RunIndexing(ctx);
            await using var scope = _ctx.BeginLifetimeScope();
            await scope.Resolve<IScanJsonFilesOperation>()
                .ExecuteAsync(ctx, _freeFiles, _vars);
            await scope.Resolve<IHashFilesOperation>()
                .ExecuteAsync(ctx, _vars, _freeFiles);
            await scope.Resolve<IDeduplicateOperation>()
                .ExecuteAsync(ctx, _vars, _freeFiles);

            SwitchUI(false);
        }

        private void fixReferencesJsonBtn_Click(object sender, EventArgs e) => MessageBox.Show("Not implemented");

        private void clearRepoDirBtn_Click(object sender, EventArgs e) => additionalVarsDir.Text = string.Empty;

        private async void trustAllVarsBtn_Click(object sender, EventArgs e)
        {
            await using var scope = _ctx.BeginLifetimeScope();
            var ctx = GetContext(stages: 3);

            await RunIndexing(ctx);
            await scope.Resolve<ITrustAllVarsOperation>()
                .ExecuteAsync(ctx, _vars);

            SwitchUI(false);
        }

        private Task RemoveOldLinks(ILifetimeScope scope, OperationContext ctx)
        {
            if (!removeAllSoftLinkBeforeChk.Checked)
            {
                _stage++;
                return Task.CompletedTask;
            }
            return scope.Resolve<IRemoveSoftLinks>()
                .ExecuteAsync(ctx);
        }

        private void MoveToStage(string text) => stageTxt.Text = $"{(_stage++) + 1}/{_totalStages} {text}";
    }
}
