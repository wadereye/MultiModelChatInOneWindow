using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MultiModelChat
{
    public class HistoryForm : Form
    {
        private string historyDirPath;
        private string currentHistoryFilePath = "";
        private Panel historyListPanel;
        private Button clearAllButton;
        private Button closeButton;
        private ComboBox dateSelector;
        private Label dateLabel;
        private List<TextBox> historyTextBoxes = new List<TextBox>();

        public HistoryForm(string dirPath)
        {
            historyDirPath = dirPath;
            InitializeComponent();
            LoadAvailableDates();
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.Get("HistoryTitle");
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(600, 400);
            this.BackColor = Color.FromArgb(245, 247, 250);

            // 顶部面板 - 包含日期选择
            Panel topPanel = new Panel();
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 50;
            topPanel.BackColor = Color.FromArgb(240, 242, 245);
            topPanel.Padding = new Padding(10, 8, 10, 8);

            // 日期选择标签
            dateLabel = new Label();
            dateLabel.Text = LanguageManager.CurrentLanguage == Language.Chinese ? "选择日期:" : "Select Date:";
            dateLabel.AutoSize = true;
            dateLabel.Location = new Point(10, 15);
            dateLabel.Font = new Font("微软雅黑", 10);
            dateLabel.ForeColor = Color.FromArgb(60, 60, 60);

            // 日期选择下拉框
            dateSelector = new ComboBox();
            dateSelector.Location = new Point(100, 12);
            dateSelector.Size = new Size(200, 28);
            dateSelector.Font = new Font("微软雅黑", 10);
            dateSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            dateSelector.SelectedIndexChanged += DateSelector_SelectedIndexChanged;

            topPanel.Controls.Add(dateLabel);
            topPanel.Controls.Add(dateSelector);

            // 提示标签
            Label tipLabel = new Label();
            tipLabel.Text = LanguageManager.CurrentLanguage == Language.Chinese ? "历史记录 (您可以直接在框内编辑并复制文字):" : "History (You can edit and copy text directly):";
            tipLabel.Dock = DockStyle.Top;
            tipLabel.Height = 30;
            tipLabel.TextAlign = ContentAlignment.MiddleLeft;
            tipLabel.Padding = new Padding(10, 0, 0, 0);
            tipLabel.Font = new Font("微软雅黑", 9, FontStyle.Italic);
            tipLabel.ForeColor = Color.FromArgb(100, 100, 100);

            // 创建历史记录容器面板
            historyListPanel = new Panel();
            historyListPanel.Dock = DockStyle.Fill;
            historyListPanel.AutoScroll = true;
            historyListPanel.Padding = new Padding(10);
            historyListPanel.BackColor = Color.White;

            // 底部按钮面板
            Panel buttonPanel = new Panel();
            buttonPanel.Dock = DockStyle.Bottom;
            buttonPanel.Height = 60;
            buttonPanel.Padding = new Padding(10);
            buttonPanel.BackColor = Color.FromArgb(240, 242, 245);

            // 创建清空全部按钮
            clearAllButton = new Button();
            clearAllButton.Text = LanguageManager.CurrentLanguage == Language.Chinese ? "清空当日" : "Clear Day";
            clearAllButton.Size = new Size(120, 35);
            clearAllButton.Location = new Point(10, 12);
            clearAllButton.FlatStyle = FlatStyle.Flat;
            clearAllButton.FlatAppearance.BorderSize = 1;
            clearAllButton.FlatAppearance.BorderColor = Color.FromArgb(255, 77, 79);
            clearAllButton.ForeColor = Color.FromArgb(255, 77, 79);
            clearAllButton.Font = new Font("微软雅黑", 10);
            clearAllButton.Cursor = Cursors.Hand;
            clearAllButton.Click += ClearAllButton_Click;

            // 创建关闭按钮
            closeButton = new Button();
            closeButton.Text = LanguageManager.Get("BtnOK");
            closeButton.Size = new Size(100, 35);
            closeButton.Location = new Point(this.ClientSize.Width - 110, 12);
            closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            closeButton.BackColor = Color.FromArgb(24, 144, 255);
            closeButton.ForeColor = Color.White;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Font = new Font("微软雅黑", 10);
            closeButton.Cursor = Cursors.Hand;
            closeButton.Click += (sender, e) => this.Close();

            buttonPanel.Controls.Add(clearAllButton);
            buttonPanel.Controls.Add(closeButton);

            // 添加控件到窗体
            this.Controls.Add(historyListPanel);
            this.Controls.Add(tipLabel);
            this.Controls.Add(topPanel);
            this.Controls.Add(buttonPanel);
        }

        private void LoadAvailableDates()
        {
            dateSelector.Items.Clear();
            
            try
            {
                if (!Directory.Exists(historyDirPath))
                {
                    Directory.CreateDirectory(historyDirPath);
                }

                // 获取所有历史文件
                var files = Directory.GetFiles(historyDirPath, "history_*.txt")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Select(f => f.Replace("history_", ""))
                    .Where(f => !string.IsNullOrEmpty(f))
                    .OrderByDescending(f => f)
                    .ToList();

                if (files.Count == 0)
                {
                    dateSelector.Items.Add(LanguageManager.CurrentLanguage == Language.Chinese ? "暂无历史记录" : "No history");
                    dateSelector.Enabled = false;
                    ShowEmptyMessage();
                }
                else
                {
                    foreach (var date in files)
                    {
                        dateSelector.Items.Add(date);
                    }
                    dateSelector.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载日期列表时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DateSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (dateSelector.SelectedItem == null) return;
            
            string selectedDate = dateSelector.SelectedItem.ToString() ?? "";
            if (string.IsNullOrEmpty(selectedDate) || selectedDate.Contains("暂无") || selectedDate.Contains("No"))
            {
                ShowEmptyMessage();
                return;
            }

            currentHistoryFilePath = Path.Combine(historyDirPath, $"history_{selectedDate}.txt");
            LoadHistoryForDate(currentHistoryFilePath);
        }

        private void LoadHistoryForDate(string filePath)
        {
            historyListPanel.Controls.Clear();
            historyTextBoxes.Clear();

            try
            {
                if (File.Exists(filePath))
                {
                    var lines = File.ReadAllLines(filePath).ToList();
                    lines.Reverse();
                    
                    if (lines.Count == 0 || (lines.Count == 1 && string.IsNullOrWhiteSpace(lines[0])))
                    {
                        ShowEmptyMessage();
                        return;
                    }

                    historyListPanel.SuspendLayout();
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            AddHistoryItem(line);
                        }
                    }
                    historyListPanel.ResumeLayout();
                }
                else
                {
                    ShowEmptyMessage();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载历史记录时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowEmptyMessage()
        {
            historyListPanel.Controls.Clear();
            Label emptyLabel = new Label();
            emptyLabel.Text = LanguageManager.CurrentLanguage == Language.Chinese ? "该日期暂无历史记录" : "No history records for this date";
            emptyLabel.Dock = DockStyle.Fill;
            emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            emptyLabel.Font = new Font("微软雅黑", 12);
            emptyLabel.ForeColor = Color.Gray;
            historyListPanel.Controls.Add(emptyLabel);
        }

        private void AddHistoryItem(string text)
        {
            Panel itemPanel = new Panel();
            itemPanel.Dock = DockStyle.Top;
            itemPanel.Padding = new Padding(0, 0, 0, 10);
            
            int lineCount = text.Split('\n').Length;
            int estimatedHeight = Math.Max(45, Math.Min(250, lineCount * 22 + 20));
            itemPanel.Height = estimatedHeight + 10;

            TextBox tb = new TextBox();
            tb.Multiline = true;
            tb.Text = text;
            tb.Font = new Font("微软雅黑", 10);
            tb.Dock = DockStyle.Fill;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.BackColor = Color.FromArgb(250, 251, 252);
            tb.ScrollBars = ScrollBars.Vertical;
            
            Button delBtn = new Button();
            delBtn.Text = "×";
            delBtn.Font = new Font("Arial", 12, FontStyle.Bold);
            delBtn.Width = 35;
            delBtn.Dock = DockStyle.Right;
            delBtn.FlatStyle = FlatStyle.Flat;
            delBtn.FlatAppearance.BorderSize = 0;
            delBtn.ForeColor = Color.FromArgb(200, 200, 200);
            delBtn.Cursor = Cursors.Hand;
            delBtn.MouseEnter += (s, e) => delBtn.ForeColor = Color.FromArgb(255, 77, 79);
            delBtn.MouseLeave += (s, e) => delBtn.ForeColor = Color.FromArgb(200, 200, 200);
            delBtn.Click += (s, e) => {
                RemoveHistoryItem(text, itemPanel);
            };

            itemPanel.Controls.Add(tb);
            itemPanel.Controls.Add(delBtn);
            
            historyListPanel.Controls.Add(itemPanel);
            historyTextBoxes.Add(tb);
        }

        private void RemoveHistoryItem(string text, Panel panel)
        {
            if (string.IsNullOrEmpty(currentHistoryFilePath) || !File.Exists(currentHistoryFilePath))
            {
                MessageBox.Show("无法删除：未找到历史文件");
                return;
            }

            string msg = LanguageManager.CurrentLanguage == Language.Chinese ? "确定删除这条记录吗？" : "Are you sure you want to delete this record?";
            string title = LanguageManager.Get("MsgTip");
            
            if (MessageBox.Show(msg, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    var lines = File.ReadAllLines(currentHistoryFilePath).ToList();
                    var index = lines.LastIndexOf(text);
                    if (index >= 0)
                    {
                        lines.RemoveAt(index);
                        File.WriteAllLines(currentHistoryFilePath, lines);
                        historyListPanel.Controls.Remove(panel);
                        
                        if (historyListPanel.Controls.Count == 0)
                        {
                            ShowEmptyMessage();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("删除失败: " + ex.Message);
                }
            }
        }

        private void ClearAllButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentHistoryFilePath))
            {
                MessageBox.Show("没有可清空的历史记录");
                return;
            }

            string msg = LanguageManager.CurrentLanguage == Language.Chinese ? "确定要清空这天的所有历史记录吗？" : "Are you sure you want to clear all history for this day?";
            string title = LanguageManager.CurrentLanguage == Language.Chinese ? "确认清空" : "Confirm Clear";

            if (MessageBox.Show(msg, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    if (File.Exists(currentHistoryFilePath))
                    {
                        File.WriteAllText(currentHistoryFilePath, string.Empty);
                    }
                    
                    if (dateSelector.SelectedItem != null)
                    {
                        LoadHistoryForDate(currentHistoryFilePath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清空时出错: {ex.Message}");
                }
            }
        }
    }
}
