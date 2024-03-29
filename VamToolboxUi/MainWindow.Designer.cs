﻿
namespace VamToolboxUi
{
    partial class MainWindow
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.vamDirTxt = new System.Windows.Forms.TextBox();
            this.selectVamDirBtn = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.additionalVarsDir = new System.Windows.Forms.TextBox();
            this.additionalVarsBtn = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.operationStatusLabel = new System.Windows.Forms.Label();
            this.copyVarsFromRepoBtn = new System.Windows.Forms.Button();
            this.dryRunCheckbox = new System.Windows.Forms.CheckBox();
            this.copyMissingDepsFromRepoBtn = new System.Windows.Forms.Button();
            this.scanJsonFilesBtn = new System.Windows.Forms.Button();
            this.clearCacheBtn = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.restoreMetaJsonBtn = new System.Windows.Forms.Button();
            this.downloadFromHubBtn = new System.Windows.Forms.Button();
            this.trustAllVarsBtn = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.profilesListBox = new System.Windows.Forms.CheckedListBox();
            this.manageProfilesBtn = new System.Windows.Forms.Button();
            this.removeAllSoftLinkBeforeChk = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.comboThreads = new System.Windows.Forms.ComboBox();
            this.clearRepoDirBtn = new System.Windows.Forms.Button();
            this.moveMissingDepsChk = new System.Windows.Forms.CheckBox();
            this.stageTxt = new System.Windows.Forms.Label();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.removeDsfMorphsChk = new System.Windows.Forms.CheckBox();
            this.removeVirusMorphsChk = new System.Windows.Forms.CheckBox();
            this.runVarFixersBtn = new System.Windows.Forms.Button();
            this.removeDependenciesFromMetaChk = new System.Windows.Forms.CheckBox();
            this.disableMorphPreloadChk = new System.Windows.Forms.CheckBox();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(88, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "VAM directory: ";
            // 
            // vamDirTxt
            // 
            this.vamDirTxt.Location = new System.Drawing.Point(106, 6);
            this.vamDirTxt.Name = "vamDirTxt";
            this.vamDirTxt.ReadOnly = true;
            this.vamDirTxt.Size = new System.Drawing.Size(301, 23);
            this.vamDirTxt.TabIndex = 1;
            // 
            // selectVamDirBtn
            // 
            this.selectVamDirBtn.Location = new System.Drawing.Point(413, 5);
            this.selectVamDirBtn.Name = "selectVamDirBtn";
            this.selectVamDirBtn.Size = new System.Drawing.Size(121, 23);
            this.selectVamDirBtn.TabIndex = 2;
            this.selectVamDirBtn.Text = "Select";
            this.selectVamDirBtn.UseVisualStyleBackColor = true;
            this.selectVamDirBtn.Click += new System.EventHandler(this.selectVamDirBtn_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 40);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(68, 15);
            this.label2.TabIndex = 3;
            this.label2.Text = "VARs REPO:";
            // 
            // additionalVarsDir
            // 
            this.additionalVarsDir.Location = new System.Drawing.Point(106, 36);
            this.additionalVarsDir.Name = "additionalVarsDir";
            this.additionalVarsDir.ReadOnly = true;
            this.additionalVarsDir.Size = new System.Drawing.Size(301, 23);
            this.additionalVarsDir.TabIndex = 4;
            // 
            // additionalVarsBtn
            // 
            this.additionalVarsBtn.Location = new System.Drawing.Point(413, 36);
            this.additionalVarsBtn.Name = "additionalVarsBtn";
            this.additionalVarsBtn.Size = new System.Drawing.Size(61, 23);
            this.additionalVarsBtn.TabIndex = 5;
            this.additionalVarsBtn.Text = "Select";
            this.additionalVarsBtn.UseVisualStyleBackColor = true;
            this.additionalVarsBtn.Click += new System.EventHandler(this.additionalVarsBtn_Click);
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(3, 604);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(622, 42);
            this.progressBar.TabIndex = 7;
            // 
            // operationStatusLabel
            // 
            this.operationStatusLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.operationStatusLabel.AutoSize = true;
            this.operationStatusLabel.Location = new System.Drawing.Point(3, 571);
            this.operationStatusLabel.Name = "operationStatusLabel";
            this.operationStatusLabel.Size = new System.Drawing.Size(38, 30);
            this.operationStatusLabel.TabIndex = 8;
            this.operationStatusLabel.Text = "status\r\nstatus";
            this.operationStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // copyVarsFromRepoBtn
            // 
            this.copyVarsFromRepoBtn.Location = new System.Drawing.Point(481, 19);
            this.copyVarsFromRepoBtn.Name = "copyVarsFromRepoBtn";
            this.copyVarsFromRepoBtn.Size = new System.Drawing.Size(135, 44);
            this.copyVarsFromRepoBtn.TabIndex = 9;
            this.copyVarsFromRepoBtn.Text = "Soft-link selected profile(s) to VAM";
            this.copyVarsFromRepoBtn.UseVisualStyleBackColor = true;
            this.copyVarsFromRepoBtn.Click += new System.EventHandler(this.softLinkVarsBtn_Click);
            // 
            // dryRunCheckbox
            // 
            this.dryRunCheckbox.AutoSize = true;
            this.dryRunCheckbox.Location = new System.Drawing.Point(502, 22);
            this.dryRunCheckbox.Name = "dryRunCheckbox";
            this.dryRunCheckbox.Size = new System.Drawing.Size(65, 19);
            this.dryRunCheckbox.TabIndex = 12;
            this.dryRunCheckbox.Text = "Dry run";
            this.dryRunCheckbox.UseVisualStyleBackColor = true;
            // 
            // copyMissingDepsFromRepoBtn
            // 
            this.copyMissingDepsFromRepoBtn.Location = new System.Drawing.Point(477, 20);
            this.copyMissingDepsFromRepoBtn.Name = "copyMissingDepsFromRepoBtn";
            this.copyMissingDepsFromRepoBtn.Size = new System.Drawing.Size(135, 75);
            this.copyMissingDepsFromRepoBtn.TabIndex = 13;
            this.copyMissingDepsFromRepoBtn.Text = "Search for missing dependencies in VAM and soft-link them from REPO";
            this.copyMissingDepsFromRepoBtn.UseVisualStyleBackColor = true;
            this.copyMissingDepsFromRepoBtn.Click += new System.EventHandler(this.copyMissingDepsFromRepoBtn_Click);
            // 
            // scanJsonFilesBtn
            // 
            this.scanJsonFilesBtn.Location = new System.Drawing.Point(540, 5);
            this.scanJsonFilesBtn.Name = "scanJsonFilesBtn";
            this.scanJsonFilesBtn.Size = new System.Drawing.Size(85, 54);
            this.scanJsonFilesBtn.TabIndex = 15;
            this.scanJsonFilesBtn.Text = "Scan";
            this.scanJsonFilesBtn.UseVisualStyleBackColor = true;
            this.scanJsonFilesBtn.Click += new System.EventHandler(this.scanJsonFilesBtn_Click);
            // 
            // clearCacheBtn
            // 
            this.clearCacheBtn.Location = new System.Drawing.Point(208, 52);
            this.clearCacheBtn.Name = "clearCacheBtn";
            this.clearCacheBtn.Size = new System.Drawing.Size(129, 23);
            this.clearCacheBtn.TabIndex = 17;
            this.clearCacheBtn.Text = "Clear internal cache";
            this.clearCacheBtn.UseVisualStyleBackColor = true;
            this.clearCacheBtn.Click += new System.EventHandler(this.clearCache_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.restoreMetaJsonBtn);
            this.groupBox2.Controls.Add(this.downloadFromHubBtn);
            this.groupBox2.Controls.Add(this.copyMissingDepsFromRepoBtn);
            this.groupBox2.Controls.Add(this.trustAllVarsBtn);
            this.groupBox2.Location = new System.Drawing.Point(3, 265);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(622, 102);
            this.groupBox2.TabIndex = 19;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Tools";
            // 
            // restoreMetaJsonBtn
            // 
            this.restoreMetaJsonBtn.Location = new System.Drawing.Point(125, 22);
            this.restoreMetaJsonBtn.Name = "restoreMetaJsonBtn";
            this.restoreMetaJsonBtn.Size = new System.Drawing.Size(111, 49);
            this.restoreMetaJsonBtn.TabIndex = 21;
            this.restoreMetaJsonBtn.Text = "Restore meta.json";
            this.restoreMetaJsonBtn.UseVisualStyleBackColor = true;
            this.restoreMetaJsonBtn.Click += new System.EventHandler(this.restoreMetaJsonBtn_Click);
            // 
            // downloadFromHubBtn
            // 
            this.downloadFromHubBtn.Location = new System.Drawing.Point(353, 20);
            this.downloadFromHubBtn.Name = "downloadFromHubBtn";
            this.downloadFromHubBtn.Size = new System.Drawing.Size(118, 75);
            this.downloadFromHubBtn.TabIndex = 28;
            this.downloadFromHubBtn.Text = "Download missing and updated VARs from Virt-a HUB";
            this.downloadFromHubBtn.UseVisualStyleBackColor = true;
            this.downloadFromHubBtn.Click += new System.EventHandler(this.downloadFromHubBtn_Click);
            // 
            // trustAllVarsBtn
            // 
            this.trustAllVarsBtn.Location = new System.Drawing.Point(6, 22);
            this.trustAllVarsBtn.Name = "trustAllVarsBtn";
            this.trustAllVarsBtn.Size = new System.Drawing.Size(113, 49);
            this.trustAllVarsBtn.TabIndex = 18;
            this.trustAllVarsBtn.Text = "Trust scripts for all VARs in .prefs files";
            this.trustAllVarsBtn.UseVisualStyleBackColor = true;
            this.trustAllVarsBtn.Click += new System.EventHandler(this.trustAllVarsBtn_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.profilesListBox);
            this.groupBox3.Controls.Add(this.manageProfilesBtn);
            this.groupBox3.Controls.Add(this.copyVarsFromRepoBtn);
            this.groupBox3.Location = new System.Drawing.Point(3, 373);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(622, 155);
            this.groupBox3.TabIndex = 20;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Profiles";
            // 
            // profilesListBox
            // 
            this.profilesListBox.FormattingEnabled = true;
            this.profilesListBox.Items.AddRange(new object[] {
            "test1",
            "test1",
            "test1",
            "test1",
            "test1",
            "test1",
            "test1",
            "test1",
            "test1",
            "test1",
            "test1"});
            this.profilesListBox.Location = new System.Drawing.Point(6, 19);
            this.profilesListBox.MultiColumn = true;
            this.profilesListBox.Name = "profilesListBox";
            this.profilesListBox.Size = new System.Drawing.Size(469, 130);
            this.profilesListBox.TabIndex = 17;
            // 
            // manageProfilesBtn
            // 
            this.manageProfilesBtn.Location = new System.Drawing.Point(481, 69);
            this.manageProfilesBtn.Name = "manageProfilesBtn";
            this.manageProfilesBtn.Size = new System.Drawing.Size(135, 24);
            this.manageProfilesBtn.TabIndex = 16;
            this.manageProfilesBtn.Text = "Manage profiles";
            this.manageProfilesBtn.UseVisualStyleBackColor = true;
            this.manageProfilesBtn.Click += new System.EventHandler(this.manageProfilesBtn_Click);
            // 
            // removeAllSoftLinkBeforeChk
            // 
            this.removeAllSoftLinkBeforeChk.AutoSize = true;
            this.removeAllSoftLinkBeforeChk.Location = new System.Drawing.Point(237, 22);
            this.removeAllSoftLinkBeforeChk.Name = "removeAllSoftLinkBeforeChk";
            this.removeAllSoftLinkBeforeChk.Size = new System.Drawing.Size(259, 19);
            this.removeAllSoftLinkBeforeChk.TabIndex = 18;
            this.removeAllSoftLinkBeforeChk.Text = "Remove all soft-links before applying profile";
            this.removeAllSoftLinkBeforeChk.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(9, 56);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(110, 15);
            this.label4.TabIndex = 21;
            this.label4.Text = "Number of threads:";
            // 
            // comboThreads
            // 
            this.comboThreads.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboThreads.FormattingEnabled = true;
            this.comboThreads.Location = new System.Drawing.Point(125, 53);
            this.comboThreads.Name = "comboThreads";
            this.comboThreads.Size = new System.Drawing.Size(68, 23);
            this.comboThreads.TabIndex = 22;
            // 
            // clearRepoDirBtn
            // 
            this.clearRepoDirBtn.Location = new System.Drawing.Point(480, 36);
            this.clearRepoDirBtn.Name = "clearRepoDirBtn";
            this.clearRepoDirBtn.Size = new System.Drawing.Size(54, 23);
            this.clearRepoDirBtn.TabIndex = 23;
            this.clearRepoDirBtn.Text = "Clear";
            this.clearRepoDirBtn.UseVisualStyleBackColor = true;
            this.clearRepoDirBtn.Click += new System.EventHandler(this.clearRepoDirBtn_Click);
            // 
            // moveMissingDepsChk
            // 
            this.moveMissingDepsChk.AutoSize = true;
            this.moveMissingDepsChk.Location = new System.Drawing.Point(12, 22);
            this.moveMissingDepsChk.Name = "moveMissingDepsChk";
            this.moveMissingDepsChk.Size = new System.Drawing.Size(216, 19);
            this.moveMissingDepsChk.TabIndex = 24;
            this.moveMissingDepsChk.Text = "Move files instead of doing soft-link";
            this.moveMissingDepsChk.UseVisualStyleBackColor = true;
            // 
            // stageTxt
            // 
            this.stageTxt.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.stageTxt.AutoSize = true;
            this.stageTxt.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.stageTxt.Location = new System.Drawing.Point(3, 541);
            this.stageTxt.Name = "stageTxt";
            this.stageTxt.Size = new System.Drawing.Size(28, 15);
            this.stageTxt.TabIndex = 26;
            this.stageTxt.Text = "Idle";
            this.stageTxt.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.moveMissingDepsChk);
            this.groupBox4.Controls.Add(this.clearCacheBtn);
            this.groupBox4.Controls.Add(this.removeAllSoftLinkBeforeChk);
            this.groupBox4.Controls.Add(this.dryRunCheckbox);
            this.groupBox4.Controls.Add(this.label4);
            this.groupBox4.Controls.Add(this.comboThreads);
            this.groupBox4.Location = new System.Drawing.Point(3, 65);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(622, 98);
            this.groupBox4.TabIndex = 27;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "General Options";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.removeDsfMorphsChk);
            this.groupBox1.Controls.Add(this.removeVirusMorphsChk);
            this.groupBox1.Controls.Add(this.runVarFixersBtn);
            this.groupBox1.Controls.Add(this.removeDependenciesFromMetaChk);
            this.groupBox1.Controls.Add(this.disableMorphPreloadChk);
            this.groupBox1.Location = new System.Drawing.Point(3, 169);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(622, 99);
            this.groupBox1.TabIndex = 31;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Var Fixers";
            // 
            // removeDsfMorphsChk
            // 
            this.removeDsfMorphsChk.AutoSize = true;
            this.removeDsfMorphsChk.Location = new System.Drawing.Point(298, 41);
            this.removeDsfMorphsChk.Name = "removeDsfMorphsChk";
            this.removeDsfMorphsChk.Size = new System.Drawing.Size(132, 19);
            this.removeDsfMorphsChk.TabIndex = 32;
            this.removeDsfMorphsChk.Text = "Remove dsf morphs";
            this.removeDsfMorphsChk.UseVisualStyleBackColor = true;
            // 
            // removeVirusMorphsChk
            // 
            this.removeVirusMorphsChk.AutoSize = true;
            this.removeVirusMorphsChk.Location = new System.Drawing.Point(298, 22);
            this.removeVirusMorphsChk.Name = "removeVirusMorphsChk";
            this.removeVirusMorphsChk.Size = new System.Drawing.Size(213, 19);
            this.removeVirusMorphsChk.TabIndex = 31;
            this.removeVirusMorphsChk.Text = "Remove virus-morphs (RG morphs)";
            this.removeVirusMorphsChk.UseVisualStyleBackColor = true;
            // 
            // runVarFixersBtn
            // 
            this.runVarFixersBtn.Location = new System.Drawing.Point(246, 66);
            this.runVarFixersBtn.Name = "runVarFixersBtn";
            this.runVarFixersBtn.Size = new System.Drawing.Size(114, 24);
            this.runVarFixersBtn.TabIndex = 30;
            this.runVarFixersBtn.Text = "Run var fixers";
            this.runVarFixersBtn.UseVisualStyleBackColor = true;
            this.runVarFixersBtn.Click += new System.EventHandler(this.runVarFixersBtn_Click);
            // 
            // removeDependenciesFromMetaChk
            // 
            this.removeDependenciesFromMetaChk.AutoSize = true;
            this.removeDependenciesFromMetaChk.Location = new System.Drawing.Point(6, 47);
            this.removeDependenciesFromMetaChk.Name = "removeDependenciesFromMetaChk";
            this.removeDependenciesFromMetaChk.Size = new System.Drawing.Size(268, 19);
            this.removeDependenciesFromMetaChk.TabIndex = 1;
            this.removeDependenciesFromMetaChk.Text = "Remove dependencies from all meta.json files";
            this.removeDependenciesFromMetaChk.UseVisualStyleBackColor = true;
            // 
            // disableMorphPreloadChk
            // 
            this.disableMorphPreloadChk.AutoSize = true;
            this.disableMorphPreloadChk.Location = new System.Drawing.Point(6, 22);
            this.disableMorphPreloadChk.Name = "disableMorphPreloadChk";
            this.disableMorphPreloadChk.Size = new System.Drawing.Size(271, 19);
            this.disableMorphPreloadChk.TabIndex = 0;
            this.disableMorphPreloadChk.Text = "Disable morphPreload for all non-morphpacks";
            this.disableMorphPreloadChk.UseVisualStyleBackColor = true;
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(641, 658);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.scanJsonFilesBtn);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.stageTxt);
            this.Controls.Add(this.clearRepoDirBtn);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.additionalVarsDir);
            this.Controls.Add(this.operationStatusLabel);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.additionalVarsBtn);
            this.Controls.Add(this.selectVamDirBtn);
            this.Controls.Add(this.vamDirTxt);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "MainWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "VamToolbox ";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainWindow_FormClosing);
            this.groupBox2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox vamDirTxt;
        private System.Windows.Forms.Button selectVamDirBtn;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox additionalVarsDir;
        private System.Windows.Forms.Button additionalVarsBtn;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label operationStatusLabel;
        private System.Windows.Forms.Button copyVarsFromRepoBtn;
        private System.Windows.Forms.CheckBox dryRunCheckbox;
        private System.Windows.Forms.Button copyMissingDepsFromRepoBtn;
        private System.Windows.Forms.Button scanJsonFilesBtn;
        private System.Windows.Forms.Button clearCacheBtn;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button manageProfilesBtn;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboThreads;
        private System.Windows.Forms.CheckedListBox profilesListBox;
        private System.Windows.Forms.CheckBox removeAllSoftLinkBeforeChk;
        private System.Windows.Forms.Button clearRepoDirBtn;
        private System.Windows.Forms.CheckBox moveMissingDepsChk;
        private System.Windows.Forms.Button trustAllVarsBtn;
        private System.Windows.Forms.Label stageTxt;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Button downloadFromHubBtn;
        private Button restoreMetaJsonBtn;
        private GroupBox groupBox1;
        private Button runVarFixersBtn;
        private CheckBox removeDependenciesFromMetaChk;
        private CheckBox disableMorphPreloadChk;
        private CheckBox removeVirusMorphsChk;
        private CheckBox removeDsfMorphsChk;
    }
}

