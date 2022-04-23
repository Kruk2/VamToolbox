﻿using System;
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

namespace VamRepackerUi;

public partial class MainWindow : Form, IProgressTracker
{
    public const long ReportEveryTicks = 500 * TimeSpan.TicksPerMillisecond;
    private readonly Stopwatch _stopwatch = new();
    private long _nextReport;
    private int _stage, _totalStages;

    private readonly ILifetimeScope _ctx;
    private Dictionary<string, bool> _buttonsState = new();
    private List<ProfileModel> _profiles = new();

    private bool _working;
    private readonly Stopwatch _sw = new();

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
        {
            additionalVarsDir.Text = dir;
        }
    }

    private async void copyMissingDepsFromRepoBtn_Click(object sender, EventArgs e)
    {
        var ctx = GetContext(stages: 5);

        await using var scope = _ctx.BeginLifetimeScope();
        await RemoveOldLinks(scope, ctx);
        var (vars, freeFiles) = await ScanJsonFiles(scope, ctx);
        await scope.Resolve<ICopyMissingVarDependenciesFromRepo>()
            .ExecuteAsync(ctx, vars, freeFiles, moveMissingDepsChk.Checked, shallowChk.Checked);

        if (MessageBox.Show("Do you want to try to download missing vars from HUB?", "Hub",
                MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            _totalStages++;
            await scope.Resolve<IDownloadMissingVars>().ExecuteAsync(ctx, vars, freeFiles);
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
        await using var scope = _ctx.BeginLifetimeScope();
        await RemoveOldLinks(scope, ctx);
        var (vars, _) = await ScanJsonFiles(scope, ctx, BuildFilters());
        await scope.Resolve<ICopySelectedVarsWithDependenciesFromRepo>()
            .ExecuteAsync(ctx, vars, BuildFilters());

        SwitchUI(false);
    }

    private static (bool, string) AskFirDirectory(string? root = null)
    {
        using var odf = new FolderBrowserDialog();
        if (root != null)
            odf.SelectedPath = root;
        var result = odf.ShowDialog();
        return (result == DialogResult.OK, odf.SelectedPath.NormalizePathSeparators());
    }

    public void InitProgress(string startingMessage) => RunInvokedInvoke(() =>
    {
        _stopwatch.Start();
        progressBar.Value = 0;
        progressBar.Style = ProgressBarStyle.Blocks;
        MoveToStage(startingMessage);
        SwitchUI(true);
    });

    public void Report(ProgressInfo progress)
    {
        if (_stopwatch.ElapsedTicks <= _nextReport && !progress.ForceShow)
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

    public void Report(string message, bool forceShow) => Report(new ProgressInfo(message, forceShow));

    public void Complete(string endingMessage) => RunInvokedInvoke(() =>
    {
        progressBar.Style = ProgressBarStyle.Blocks;
        progressBar.Value = progressBar.Maximum;
        operationStatusLabel.Text = endingMessage;
    });


    void SwitchUI(bool working)
    {
        if (_working == working) return;
        if (working)
        {
            operationStatusLabel.Text = string.Empty;
            _sw.Restart();
        }

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
            _profiles = JsonConvert.DeserializeObject<List<ProfileModel>>(Properties.Settings.Default.profiles)!;
        }

        ReloadProfiles();
    }

    private static async Task<(List<VarPackage> vars, List<FreeFile> freeFiles)> RunIndexing(ILifetimeScope scope, OperationContext operationContext)
    {
        var freeFiles = await scope.Resolve<IScanFilesOperation>()
            .ExecuteAsync(operationContext);
        var vars = await scope.Resolve<IScanVarPackagesOperation>()
            .ExecuteAsync(operationContext, freeFiles);

        return (vars, freeFiles);
    }

    private async void scanInvalidVars_Btn_Click(object sender, EventArgs e)
    {
        await using var scope = _ctx.BeginLifetimeScope();
        await RunIndexing(scope, GetContext(stages: 2));
        SwitchUI(false);
    }

    private async void scanJsonFilesBtn_Click(object sender, EventArgs e)
    {
        await using var scope = _ctx.BeginLifetimeScope();
        var ctx = GetContext(stages: 3);
        await ScanJsonFiles(scope, ctx);
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

    private async void fixMissingMorphsBtn_Click(object sender, EventArgs e)
    {
        var ctx = GetContext(stages: 3);
        await using var scope = _ctx.BeginLifetimeScope();
        var (vars, freeFiles) = await RunIndexing(scope, ctx);
        await scope.Resolve<IFixMissingMorphsOperation>().ExecuteAsync(ctx, freeFiles, vars);

        SwitchUI(false);
    }

    private void fixReferencesJsonBtn_Click(object sender, EventArgs e) => MessageBox.Show("Not implemented");

    private void clearRepoDirBtn_Click(object sender, EventArgs e) => additionalVarsDir.Text = string.Empty;

    private async void trustAllVarsBtn_Click(object sender, EventArgs e)
    {
        await using var scope = _ctx.BeginLifetimeScope();
        var ctx = GetContext(stages: 3);

        var (vars, _) = await RunIndexing(scope, ctx);
        await scope.Resolve<ITrustAllVarsOperation>().ExecuteAsync(ctx, vars);

        SwitchUI(false);
    }

    private Task RemoveOldLinks(ILifetimeScope scope, OperationContext ctx)
    {
        if (!removeAllSoftLinkBeforeChk.Checked)
        {
            _stage++;
            return Task.CompletedTask;
        }
        return scope.Resolve<IRemoveSoftLinks>().ExecuteAsync(ctx);
    }

    private async void downloadFromHubBtn_Click(object sender, EventArgs e)
    {
        var ctx = GetContext(stages: 4);

        await using var scope = _ctx.BeginLifetimeScope();
        var (vars, freeFiles) =  await ScanJsonFiles(scope, ctx);
        await scope.Resolve<IDownloadMissingVars>() .ExecuteAsync(ctx, vars, freeFiles);
        SwitchUI(false);
    }

    private static async Task<(List<VarPackage> vars, List<FreeFile> freeFiles)> ScanJsonFiles(ILifetimeScope scope, OperationContext ctx, IVarFilters? filters = null)
    {
        var (vars, freeFiles) = await RunIndexing(scope, ctx);
        await scope.Resolve<IScanJsonFilesOperation>().ExecuteAsync(ctx, freeFiles, vars, filters);

        return (vars, freeFiles);
    }

    private void MoveToStage(string text) => stageTxt.Text = $"{(_stage++) + 1}/{_totalStages} {text}";
}