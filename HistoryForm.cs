using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MultiModelChat
{
    public class HistoryForm : Form
    {
        private string historyFilePath;
        private ListBox historyListBox;
        private Button deleteButton;
        private Button clearAllButton;
        private Button closeButton;

        public HistoryForm(string filePath)
        {
            historyFilePath = filePath;
            InitializeComponent();
            LoadHistory();
        }

        private void InitializeComponent()
        {
            this.Text = "历史记录";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(600, 400);

            // 创建历史记录列表框
            historyListBox = new ListBox();
            historyListBox.Location = new Point(10, 10);
            historyListBox.Size = new Size(this.ClientSize.Width - 20, this.ClientSize.Height - 60);
            historyListBox.Font = new Font("微软雅黑", 10);
            historyListBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            historyListBox.HorizontalScrollbar = true;

            // 创建删除按钮
            deleteButton = new Button();
            deleteButton.Text = "删除选中";
            deleteButton.Size = new Size(100, 35);
            deleteButton.Location = new Point(10, this.ClientSize.Height - 45);
            deleteButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            deleteButton.Font = new Font("微软雅黑", 10);
            deleteButton.Click += DeleteButton_Click;

            // 创建清空全部按钮
            clearAllButton = new Button();
            clearAllButton.Text = "清空全部";
            clearAllButton.Size = new Size(100, 35);
            clearAllButton.Location = new Point(120, this.ClientSize.Height - 45);
            clearAllButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            clearAllButton.Font = new Font("微软雅黑", 10);
            clearAllButton.Click += ClearAllButton_Click;

            // 创建关闭按钮
            closeButton = new Button();
            closeButton.Text = "关闭";
            closeButton.Size = new Size(100, 35);
            closeButton.Location = new Point(this.ClientSize.Width - 110, this.ClientSize.Height - 45);
            closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            closeButton.Font = new Font("微软雅黑", 10);
            closeButton.Click += (sender, e) => this.Close();

            // 添加控件到窗体
            this.Controls.Add(historyListBox);
            this.Controls.Add(deleteButton);
            this.Controls.Add(clearAllButton);
            this.Controls.Add(closeButton);
        }

        // 加载历史记录
        private void LoadHistory()
        {
            historyListBox.Items.Clear();

            try
            {
                if (File.Exists(historyFilePath))
                {
                    var lines = File.ReadAllLines(historyFilePath);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            historyListBox.Items.Add(line);
                        }
                    }
                }
                else
                {
                    historyListBox.Items.Add("暂无历史记录");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载历史记录时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 删除选中的记录
        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (historyListBox.SelectedIndex == -1)
            {
                MessageBox.Show("请先选择要删除的记录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show("确定要删除选中的记录吗？", "确认删除", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    int selectedIndex = historyListBox.SelectedIndex;
                    var lines = File.ReadAllLines(historyFilePath).ToList();
                    
                    if (selectedIndex >= 0 && selectedIndex < lines.Count)
                    {
                        lines.RemoveAt(selectedIndex);
                        File.WriteAllLines(historyFilePath, lines);
                        LoadHistory();
                        MessageBox.Show("删除成功", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除记录时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // 清空全部记录
        private void ClearAllButton_Click(object sender, EventArgs e)
        {
            if (!File.Exists(historyFilePath) || new FileInfo(historyFilePath).Length == 0)
            {
                MessageBox.Show("历史记录已经为空", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show("确定要清空所有历史记录吗？此操作不可恢复！", "确认清空", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    File.WriteAllText(historyFilePath, string.Empty);
                    LoadHistory();
                    MessageBox.Show("已清空所有历史记录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清空历史记录时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
