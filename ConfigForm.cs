using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace MultiModelChat
{
    public partial class ConfigForm : Form
    {
        private WebViewConfig config;
        private DataGridView dataGridView;
        private Button saveButton;
        private Button cancelButton;
        private Button addButton;
        private Button deleteButton;

        public ConfigForm(WebViewConfig config)
        {
            this.config = config;
            InitializeComponent();
            LoadConfigData();
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.Get("ConfigTitle");
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            // 设置窗体图标
            string iconPath = Path.Combine(Application.StartupPath, "img", "favicon.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }

            // 创建 DataGridView
            dataGridView = new DataGridView();
            dataGridView.Dock = DockStyle.Top;
            dataGridView.Height = 380;
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView.MultiSelect = false;
            dataGridView.BackgroundColor = Color.White;
            dataGridView.BorderStyle = BorderStyle.None;
            dataGridView.RowHeadersVisible = false;
            dataGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 247, 255);
            dataGridView.DefaultCellStyle.SelectionForeColor = Color.Black;
            dataGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
            dataGridView.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 10, FontStyle.Bold);
            dataGridView.EnableHeadersVisualStyles = false;

            // 添加列
            var selectColumn = new DataGridViewCheckBoxColumn();
            selectColumn.Name = "IsSelected";
            selectColumn.HeaderText = LanguageManager.Get("ColEnabled");
            selectColumn.Width = 50;
            selectColumn.FillWeight = 10;
            dataGridView.Columns.Add(selectColumn);

            var nameColumn = new DataGridViewTextBoxColumn();
            nameColumn.Name = "Name";
            nameColumn.HeaderText = LanguageManager.Get("ColName");
            nameColumn.FillWeight = 30;
            dataGridView.Columns.Add(nameColumn);

            var urlColumn = new DataGridViewTextBoxColumn();
            urlColumn.Name = "Url";
            urlColumn.HeaderText = LanguageManager.Get("ColUrl");
            urlColumn.FillWeight = 60;
            dataGridView.Columns.Add(urlColumn);

            // 按钮面板
            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Fill;
            buttonPanel.BackColor = Color.FromArgb(250, 251, 252);

            addButton = CreateStyledButton(LanguageManager.Get("BtnAdd"), Color.FromArgb(82, 196, 26));
            addButton.Location = new Point(20, 10);
            addButton.Click += (s, e) => {
                dataGridView.Rows.Add(false, LanguageManager.Get("MsgNewSite"), "https://");
            };

            deleteButton = CreateStyledButton(LanguageManager.Get("BtnDelete"), Color.FromArgb(255, 77, 79));
            deleteButton.Location = new Point(130, 10);
            deleteButton.Click += (s, e) => {
                if (dataGridView.SelectedRows.Count > 0)
                {
                    dataGridView.Rows.Remove(dataGridView.SelectedRows[0]);
                }
            };

            saveButton = CreateStyledButton(LanguageManager.Get("BtnSave"), Color.FromArgb(24, 144, 255));
            saveButton.Location = new Point(450, 10);
            saveButton.Click += SaveButton_Click;

            cancelButton = CreateStyledButton(LanguageManager.Get("BtnCancel"), Color.FromArgb(140, 140, 140));
            cancelButton.Location = new Point(560, 10);
            cancelButton.Click += (s, e) => this.Close();

            buttonPanel.Controls.Add(addButton);
            buttonPanel.Controls.Add(deleteButton);
            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);

            this.Controls.Add(buttonPanel);
            this.Controls.Add(dataGridView);
        }
        
        private Button CreateStyledButton(string text, Color backColor)
        {
            var btn = new Button();
            btn.Text = text;
            btn.Size = new Size(100, 32);
            btn.Font = new Font("微软雅黑", 9);
            btn.BackColor = backColor;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private void LoadConfigData()
        {
            dataGridView.Rows.Clear();
            foreach (var site in config.Sites)
            {
                dataGridView.Rows.Add(site.IsSelected, site.Name, site.Url);
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            int selectedCount = 0;
            var newSites = new List<WebSiteConfig>();

            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                if (row.IsNewRow) continue;

                bool isSelected = (bool)(row.Cells["IsSelected"].Value ?? false);
                string name = row.Cells["Name"].Value?.ToString() ?? "";
                string url = row.Cells["Url"].Value?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;

                if (!IsValidUrl(url))
                {
                    MessageBox.Show($"'{name}' - {LanguageManager.Get("MsgUrlInvalid")}", LanguageManager.Get("MsgError"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (isSelected) selectedCount++;

                newSites.Add(new WebSiteConfig
                {
                    IsSelected = isSelected,
                    Name = name,
                    Url = url
                });
            }

            if (selectedCount > 3)
            {
                var result = MessageBox.Show(LanguageManager.Get("MsgTooManySites"), LanguageManager.Get("MsgWarning"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.No) return;
            }

            config.Sites = newSites;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult) 
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }
}