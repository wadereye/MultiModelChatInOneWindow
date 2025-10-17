using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace MultiModelChat
{
    public partial class ConfigForm : Form
    {
        private WebViewConfig config;
        private TextBox tongyiUrlTextBox;
        private TextBox doubaoUrlTextBox;
        private TextBox deepseekUrlTextBox;
        private Button saveButton;
        private Button cancelButton;

        public ConfigForm(WebViewConfig config)
        {
            this.config = config;
            InitializeComponent();
            LoadConfig();
        }

        private void InitializeComponent()
        {
            // 设置窗体属性
            this.Text = "配置 WebView URL";
            this.Size = new Size(500, 250);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // 设置窗体图标
            string iconPath = Path.Combine(Application.StartupPath, "img", "favicon.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }

            // 创建标签和文本框
            Label tongyiLabel = new Label();
            tongyiLabel.Text = "通义千问 URL:";
            tongyiLabel.Location = new Point(20, 20);
            tongyiLabel.Size = new Size(100, 20);

            tongyiUrlTextBox = new TextBox();
            tongyiUrlTextBox.Location = new Point(130, 20);
            tongyiUrlTextBox.Size = new Size(330, 20);

            Label doubaoLabel = new Label();
            doubaoLabel.Text = "豆包 URL:";
            doubaoLabel.Location = new Point(20, 60);
            doubaoLabel.Size = new Size(100, 20);

            doubaoUrlTextBox = new TextBox();
            doubaoUrlTextBox.Location = new Point(130, 60);
            doubaoUrlTextBox.Size = new Size(330, 20);

            Label deepseekLabel = new Label();
            deepseekLabel.Text = "DeepSeek URL:";
            deepseekLabel.Location = new Point(20, 100);
            deepseekLabel.Size = new Size(100, 20);

            deepseekUrlTextBox = new TextBox();
            deepseekUrlTextBox.Location = new Point(130, 100);
            deepseekUrlTextBox.Size = new Size(330, 20);

            // 创建按钮
            saveButton = new Button();
            saveButton.Text = "保存";
            saveButton.Location = new Point(150, 150);
            saveButton.Size = new Size(75, 30);
            saveButton.Click += SaveButton_Click;

            cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.Location = new Point(250, 150);
            cancelButton.Size = new Size(75, 30);
            cancelButton.Click += CancelButton_Click;

            // 添加控件到窗体
            this.Controls.Add(tongyiLabel);
            this.Controls.Add(tongyiUrlTextBox);
            this.Controls.Add(doubaoLabel);
            this.Controls.Add(doubaoUrlTextBox);
            this.Controls.Add(deepseekLabel);
            this.Controls.Add(deepseekUrlTextBox);
            this.Controls.Add(saveButton);
            this.Controls.Add(cancelButton);
        }

        private void LoadConfig()
        {
            tongyiUrlTextBox.Text = config.TongyiUrl;
            doubaoUrlTextBox.Text = config.DoubaoUrl;
            deepseekUrlTextBox.Text = config.DeepSeekUrl;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            // 验证URL格式
            if (!IsValidUrl(tongyiUrlTextBox.Text))
            {
                MessageBox.Show("通义千问 URL 格式不正确", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!IsValidUrl(doubaoUrlTextBox.Text))
            {
                MessageBox.Show("豆包 URL 格式不正确", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!IsValidUrl(deepseekUrlTextBox.Text))
            {
                MessageBox.Show("DeepSeek URL 格式不正确", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 保存配置
            config.TongyiUrl = tongyiUrlTextBox.Text;
            config.DoubaoUrl = doubaoUrlTextBox.Text;
            config.DeepSeekUrl = deepseekUrlTextBox.Text;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
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