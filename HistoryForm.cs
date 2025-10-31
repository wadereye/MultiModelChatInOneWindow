using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Text.Json;
using System.Linq;

namespace MultiModelChat
{
    public class HistoryItem
    {
        public string Question { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public partial class HistoryForm : Form
    {
        private List<HistoryItem> historyItems;
        private string historyFilePath;
        private ListBox historyListBox;
        private Button selectButton;
        private Button clearAllButton;
        private TextBox questionPreviewTextBox;
        
        public string SelectedQuestion { get; private set; }

        public HistoryForm()
        {
            InitializeComponent();
            historyFilePath = Path.Combine(Application.StartupPath, "history.txt");
            historyItems = new List<HistoryItem>();
            LoadHistory();
        }

        private void InitializeComponent()
        {
            this.Text = "历史查询记录";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // 创建列表框
            historyListBox = new ListBox();
            historyListBox.Location = new Point(20, 20);
            historyListBox.Size = new Size(540, 350);
            historyListBox.Font = new System.Drawing.Font("微软雅黑", 10);
            historyListBox.SelectionMode = SelectionMode.One;
            historyListBox.SelectedIndexChanged += HistoryListBox_SelectedIndexChanged;
            
            // 创建预览文本框
            questionPreviewTextBox = new TextBox();
            questionPreviewTextBox.Location = new Point(20, 380);
            questionPreviewTextBox.Size = new Size(540, 60);
            questionPreviewTextBox.Font = new System.Drawing.Font("微软雅黑", 10);
            questionPreviewTextBox.Multiline = true;
            questionPreviewTextBox.ReadOnly = true;
            questionPreviewTextBox.ScrollBars = ScrollBars.Vertical;
            
            // 创建选择按钮
            selectButton = new Button();
            selectButton.Location = new Point(380, 445);
            selectButton.Size = new Size(80, 30);
            selectButton.Text = "选择";
            selectButton.Font = new System.Drawing.Font("微软雅黑", 10);
            selectButton.Click += SelectButton_Click;
            
            // 创建清空按钮
            clearAllButton = new Button();
            clearAllButton.Location = new Point(480, 445);
            clearAllButton.Size = new Size(80, 30);
            clearAllButton.Text = "清空全部";
            clearAllButton.Font = new System.Drawing.Font("微软雅黑", 10);
            clearAllButton.Click += ClearAllButton_Click;
            
            // 添加控件
            this.Controls.Add(historyListBox);
            this.Controls.Add(questionPreviewTextBox);
            this.Controls.Add(selectButton);
            this.Controls.Add(clearAllButton);
            
            // 添加双击事件
            historyListBox.DoubleClick += SelectButton_Click;
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(historyFilePath))
                {
                    string json = File.ReadAllText(historyFilePath);
                    historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(json) ?? new List<HistoryItem>();
                    // 按时间倒序排列
                    historyItems.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                    UpdateListBox();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载历史记录时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateListBox()
        {
            historyListBox.Items.Clear();
            foreach (var item in historyItems)
            {
                string displayText = $"[{item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")}] {TruncateText(item.Question, 50)}";
                historyListBox.Items.Add(displayText);
            }
        }

        private string TruncateText(string text, int maxLength)
        {
            if (text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength) + "...";
        }

        private void HistoryListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (historyListBox.SelectedIndex >= 0 && historyListBox.SelectedIndex < historyItems.Count)
            {
                questionPreviewTextBox.Text = historyItems[historyListBox.SelectedIndex].Question;
            }
        }

        private void SelectButton_Click(object sender, EventArgs e)
        {
            if (historyListBox.SelectedIndex >= 0 && historyListBox.SelectedIndex < historyItems.Count)
            {
                SelectedQuestion = historyItems[historyListBox.SelectedIndex].Question;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("请先选择一条历史记录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ClearAllButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要清空所有历史记录吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                historyItems.Clear();
                try
                {
                    File.WriteAllText(historyFilePath, JsonSerializer.Serialize(historyItems, new JsonSerializerOptions { WriteIndented = true }));
                    UpdateListBox();
                    questionPreviewTextBox.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清空历史记录时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}