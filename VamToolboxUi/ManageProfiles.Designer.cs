
namespace VamToolboxUi
{
    partial class ManageProfiles
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.saveBtn = new System.Windows.Forms.Button();
            this.cancelBtn = new System.Windows.Forms.Button();
            this.profileList = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.addNewBtn = new System.Windows.Forms.Button();
            this.deleteBtn = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.addDirBtn = new System.Windows.Forms.Button();
            this.addFileBtn = new System.Windows.Forms.Button();
            this.dirList = new System.Windows.Forms.ListBox();
            this.filesList = new System.Windows.Forms.ListBox();
            this.removeDirsBtn = new System.Windows.Forms.Button();
            this.removeFilesBtn = new System.Windows.Forms.Button();
            this.renameBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // saveBtn
            // 
            this.saveBtn.Location = new System.Drawing.Point(344, 331);
            this.saveBtn.Name = "saveBtn";
            this.saveBtn.Size = new System.Drawing.Size(75, 23);
            this.saveBtn.TabIndex = 0;
            this.saveBtn.Text = "Save";
            this.saveBtn.UseVisualStyleBackColor = true;
            this.saveBtn.Click += new System.EventHandler(this.saveBtn_Click);
            // 
            // cancelBtn
            // 
            this.cancelBtn.Location = new System.Drawing.Point(440, 331);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(75, 23);
            this.cancelBtn.TabIndex = 1;
            this.cancelBtn.Text = "Cancel";
            this.cancelBtn.UseVisualStyleBackColor = true;
            this.cancelBtn.Click += new System.EventHandler(this.cancelBtn_Click);
            // 
            // profileList
            // 
            this.profileList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.profileList.FormattingEnabled = true;
            this.profileList.Location = new System.Drawing.Point(12, 12);
            this.profileList.Name = "profileList";
            this.profileList.Size = new System.Drawing.Size(218, 23);
            this.profileList.TabIndex = 2;
            this.profileList.SelectedIndexChanged += new System.EventHandler(this.profileList_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 58);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(63, 15);
            this.label1.TabIndex = 4;
            this.label1.Text = "Directories";
            // 
            // addNewBtn
            // 
            this.addNewBtn.Location = new System.Drawing.Point(236, 11);
            this.addNewBtn.Name = "addNewBtn";
            this.addNewBtn.Size = new System.Drawing.Size(75, 23);
            this.addNewBtn.TabIndex = 5;
            this.addNewBtn.Text = "Add";
            this.addNewBtn.UseVisualStyleBackColor = true;
            this.addNewBtn.Click += new System.EventHandler(this.addNewBtn_Click);
            // 
            // deleteBtn
            // 
            this.deleteBtn.Location = new System.Drawing.Point(317, 11);
            this.deleteBtn.Name = "deleteBtn";
            this.deleteBtn.Size = new System.Drawing.Size(75, 23);
            this.deleteBtn.TabIndex = 6;
            this.deleteBtn.Text = "Delete";
            this.deleteBtn.UseVisualStyleBackColor = true;
            this.deleteBtn.Click += new System.EventHandler(this.deleteBtn_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(440, 58);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(30, 15);
            this.label2.TabIndex = 8;
            this.label2.Text = "Files";
            // 
            // addDirBtn
            // 
            this.addDirBtn.Location = new System.Drawing.Point(81, 54);
            this.addDirBtn.Name = "addDirBtn";
            this.addDirBtn.Size = new System.Drawing.Size(75, 23);
            this.addDirBtn.TabIndex = 9;
            this.addDirBtn.Text = "Add";
            this.addDirBtn.UseVisualStyleBackColor = true;
            this.addDirBtn.Click += new System.EventHandler(this.addDirBtn_Click);
            // 
            // addFileBtn
            // 
            this.addFileBtn.Location = new System.Drawing.Point(476, 54);
            this.addFileBtn.Name = "addFileBtn";
            this.addFileBtn.Size = new System.Drawing.Size(75, 23);
            this.addFileBtn.TabIndex = 10;
            this.addFileBtn.Text = "Add";
            this.addFileBtn.UseVisualStyleBackColor = true;
            this.addFileBtn.Click += new System.EventHandler(this.addFileBtn_Click);
            // 
            // dirList
            // 
            this.dirList.FormattingEnabled = true;
            this.dirList.HorizontalScrollbar = true;
            this.dirList.ItemHeight = 15;
            this.dirList.Location = new System.Drawing.Point(12, 85);
            this.dirList.Name = "dirList";
            this.dirList.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.dirList.Size = new System.Drawing.Size(407, 229);
            this.dirList.TabIndex = 11;
            // 
            // filesList
            // 
            this.filesList.FormattingEnabled = true;
            this.filesList.HorizontalScrollbar = true;
            this.filesList.ItemHeight = 15;
            this.filesList.Location = new System.Drawing.Point(425, 85);
            this.filesList.Name = "filesList";
            this.filesList.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.filesList.Size = new System.Drawing.Size(416, 229);
            this.filesList.TabIndex = 12;
            // 
            // removeDirsBtn
            // 
            this.removeDirsBtn.Location = new System.Drawing.Point(162, 54);
            this.removeDirsBtn.Name = "removeDirsBtn";
            this.removeDirsBtn.Size = new System.Drawing.Size(115, 23);
            this.removeDirsBtn.TabIndex = 13;
            this.removeDirsBtn.Text = "Remove selected";
            this.removeDirsBtn.UseVisualStyleBackColor = true;
            this.removeDirsBtn.Click += new System.EventHandler(this.removeDirsBtn_Click);
            // 
            // removeFilesBtn
            // 
            this.removeFilesBtn.Location = new System.Drawing.Point(557, 54);
            this.removeFilesBtn.Name = "removeFilesBtn";
            this.removeFilesBtn.Size = new System.Drawing.Size(115, 23);
            this.removeFilesBtn.TabIndex = 14;
            this.removeFilesBtn.Text = "Remove selected";
            this.removeFilesBtn.UseVisualStyleBackColor = true;
            this.removeFilesBtn.Click += new System.EventHandler(this.removeFilesBtn_Click);
            // 
            // renameBtn
            // 
            this.renameBtn.Location = new System.Drawing.Point(398, 11);
            this.renameBtn.Name = "renameBtn";
            this.renameBtn.Size = new System.Drawing.Size(75, 23);
            this.renameBtn.TabIndex = 15;
            this.renameBtn.Text = "Rename";
            this.renameBtn.UseVisualStyleBackColor = true;
            this.renameBtn.Click += new System.EventHandler(this.renameBtn_Click);
            // 
            // ManageProfiles
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(853, 366);
            this.Controls.Add(this.renameBtn);
            this.Controls.Add(this.removeFilesBtn);
            this.Controls.Add(this.removeDirsBtn);
            this.Controls.Add(this.filesList);
            this.Controls.Add(this.dirList);
            this.Controls.Add(this.addFileBtn);
            this.Controls.Add(this.addDirBtn);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.deleteBtn);
            this.Controls.Add(this.addNewBtn);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.profileList);
            this.Controls.Add(this.cancelBtn);
            this.Controls.Add(this.saveBtn);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "ManageProfiles";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Manage Profiles";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button saveBtn;
        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.ComboBox profileList;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button addNewBtn;
        private System.Windows.Forms.Button deleteBtn;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button addDirBtn;
        private System.Windows.Forms.Button addFileBtn;
        private System.Windows.Forms.ListBox dirList;
        private System.Windows.Forms.ListBox filesList;
        private System.Windows.Forms.Button removeDirsBtn;
        private System.Windows.Forms.Button removeFilesBtn;
        private System.Windows.Forms.Button renameBtn;
    }
}