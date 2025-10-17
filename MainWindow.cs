using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace MultiModelChat
{
    // 配置类
    public class WebViewConfig
    {
        public string TongyiUrl { get; set; } = "https://tongyi.aliyun.com/qianwen/";
        public string DoubaoUrl { get; set; } = "https://www.doubao.com/chat/";
        public string DeepSeekUrl { get; set; } = "https://chat.deepseek.com/";
    }

    public partial class MainWindow : Form
    {
        private TextBox searchTextBox;
        private Button searchButton;
        private Button clearButton;
        private Button configButton; // 新增配置按钮
        private Panel topPanel;
        private Panel bottomPanel;
        private Microsoft.Web.WebView2.WinForms.WebView2 tongyiWebView;
        private Microsoft.Web.WebView2.WinForms.WebView2 doubaoWebView;
        private Microsoft.Web.WebView2.WinForms.WebView2 deepseekWebView;
        
        // 为每个WebView添加独立显示按钮
        private Button tongyiExpandButton;
        private Button doubaoExpandButton;
        private Button deepseekExpandButton;
        
        // 记录当前展开状态
        private enum ExpandState { None, Tongyi, Doubao, Deepseek }
        private ExpandState currentExpandState = ExpandState.None;
        
        // 配置信息
        private WebViewConfig config = new WebViewConfig();
        private string configFilePath = Path.Combine(Application.StartupPath, "webview_config.json");

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig(); // 加载配置
        }

        private void InitializeComponent()
        {
            // 设置窗体属性
            this.Text = "多模型问答系统";
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1200, 800);
            
            // 设置窗体图标
            string iconPath = Path.Combine(Application.StartupPath, "img", "favicon.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
            
            // 创建上方面板
            CreateTopPanel();
            
            // 创建下面板
            CreateBottomPanel();
            
            // 添加面板到窗体
            this.Controls.Add(topPanel);
            this.Controls.Add(bottomPanel);
        }

        private void CreateTopPanel()
        {
            topPanel = new Panel();
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 100;
            topPanel.BackColor = Color.LightGray;
            
            // 创建搜索输入框，调整为窗口宽度的80%
            searchTextBox = new TextBox();
            searchTextBox.Location = new Point(20, 30);
            searchTextBox.Size = new Size((int)(this.Width * 0.8) - 150, 30); // 80%宽度减去按钮宽度
            searchTextBox.Font = new Font("微软雅黑", 12);
            searchTextBox.PlaceholderText = "请输入您的问题...";
            
            // 创建查询按钮
            searchButton = new Button();
            searchButton.Location = new Point(searchTextBox.Right + 10, 30);
            searchButton.Size = new Size(80, 30);
            searchButton.Text = "查询";
            searchButton.Font = new Font("微软雅黑", 12);
            searchButton.Click += SearchButton_Click;
            
            // 创建清除按钮
            clearButton = new Button();
            clearButton.Location = new Point(searchButton.Right + 10, 30);
            clearButton.Size = new Size(80, 30);
            clearButton.Text = "清除";
            clearButton.Font = new Font("微软雅黑", 12);
            clearButton.Click += ClearButton_Click;
            
            // 创建配置按钮
            configButton = new Button();
            configButton.Location = new Point(clearButton.Right + 10, 30);
            configButton.Size = new Size(80, 30);
            configButton.Text = "配置";
            configButton.Font = new Font("微软雅黑", 12);
            configButton.Click += ConfigButton_Click;
            
            // 创建展开按钮
            tongyiExpandButton = new Button();
            tongyiExpandButton.Text = "展开通义";
            tongyiExpandButton.Size = new Size(80, 25);
            tongyiExpandButton.Location = new Point(20, 65);
            tongyiExpandButton.Click += (sender, e) => ToggleExpand(ExpandState.Tongyi);
            
            doubaoExpandButton = new Button();
            doubaoExpandButton.Text = "展开豆包";
            doubaoExpandButton.Size = new Size(80, 25);
            doubaoExpandButton.Location = new Point(tongyiExpandButton.Right + 10, 65);
            doubaoExpandButton.Click += (sender, e) => ToggleExpand(ExpandState.Doubao);
            
            deepseekExpandButton = new Button();
            deepseekExpandButton.Text = "展开DeepSeek";
            deepseekExpandButton.Size = new Size(100, 25);
            deepseekExpandButton.Location = new Point(doubaoExpandButton.Right + 10, 65);
            deepseekExpandButton.Click += (sender, e) => ToggleExpand(ExpandState.Deepseek);
            
            // 添加回车键支持
            searchTextBox.KeyDown += (sender, e) => {
                if (e.KeyCode == Keys.Enter)
                {
                    SearchButton_Click(sender, e);
                }
            };
            
            // 添加控件到上方面板
            topPanel.Controls.Add(searchTextBox);
            topPanel.Controls.Add(searchButton);
            topPanel.Controls.Add(clearButton);
            topPanel.Controls.Add(configButton);
            topPanel.Controls.Add(tongyiExpandButton);
            topPanel.Controls.Add(doubaoExpandButton);
            topPanel.Controls.Add(deepseekExpandButton);
        }

        private void CreateBottomPanel()
        {
            bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Fill;
            bottomPanel.Padding = new Padding(0, 100, 0, 0); // 为上方面板留出空间
            
            // 创建三个WebView控件
            tongyiWebView = new Microsoft.Web.WebView2.WinForms.WebView2();
            doubaoWebView = new Microsoft.Web.WebView2.WinForms.WebView2();
            deepseekWebView = new Microsoft.Web.WebView2.WinForms.WebView2();
            
            // 设置WebView属性
            tongyiWebView.Dock = DockStyle.Left;
            tongyiWebView.Width = (this.Width / 3) - 10;
            
            doubaoWebView.Dock = DockStyle.Left;
            doubaoWebView.Width = (this.Width / 3) - 10;
            
            deepseekWebView.Dock = DockStyle.Fill;
            
            // 初始化WebView
            InitializeWebViews();
            
            // 添加WebView到下面板
            bottomPanel.Controls.Add(deepseekWebView);
            bottomPanel.Controls.Add(doubaoWebView);
            bottomPanel.Controls.Add(tongyiWebView);
        }

        private async void InitializeWebViews()
        {
            try
            {
                // 初始化WebView2控件
                await tongyiWebView.EnsureCoreWebView2Async(null);
                await doubaoWebView.EnsureCoreWebView2Async(null);
                await deepseekWebView.EnsureCoreWebView2Async(null);
                
                // 导航到配置中的网站
                tongyiWebView.CoreWebView2.Navigate(config.TongyiUrl);
                doubaoWebView.CoreWebView2.Navigate(config.DoubaoUrl);
                deepseekWebView.CoreWebView2.Navigate(config.DeepSeekUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化WebView时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            string question = searchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(question))
            {
                MessageBox.Show("请输入问题内容", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            try
            {
                // 使用键盘模拟方式向三个模型注入问题
                await SimulateInputToTongyi(question);
                await SimulateInputToDoubao(question);
                await SimulateInputToDeepSeek(question);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送问题时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 配置按钮点击事件
        private void ConfigButton_Click(object sender, EventArgs e)
        {
            using (var configForm = new ConfigForm(config))
            {
                if (configForm.ShowDialog() == DialogResult.OK)
                {
                    // 保存配置
                    SaveConfig();
                    // 重新加载WebView页面
                    LoadWebViews();
                }
            }
        }

        // 加载配置
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    config = JsonSerializer.Deserialize<WebViewConfig>(json);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置文件时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 保存配置
        private void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置文件时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 重新加载WebView页面
        private void LoadWebViews()
        {
            try
            {
                tongyiWebView.CoreWebView2.Navigate(config.TongyiUrl);
                doubaoWebView.CoreWebView2.Navigate(config.DoubaoUrl);
                deepseekWebView.CoreWebView2.Navigate(config.DeepSeekUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重新加载WebView时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 清除按钮点击事件
        private void ClearButton_Click(object sender, EventArgs e)
        {
            searchTextBox.Clear();
            searchTextBox.Focus();
        }

        // 切换展开状态
        private void ToggleExpand(ExpandState targetState)
        {
            // 如果点击的是当前已展开的窗口，则恢复为三列布局
            if (currentExpandState == targetState)
            {
                RestoreThreeColumnLayout();
                currentExpandState = ExpandState.None;
                
                // 恢复按钮文字
                switch (targetState)
                {
                    case ExpandState.Tongyi:
                        tongyiExpandButton.Text = "展开通义";
                        break;
                    case ExpandState.Doubao:
                        doubaoExpandButton.Text = "展开豆包";
                        break;
                    case ExpandState.Deepseek:
                        deepseekExpandButton.Text = "展开DeepSeek";
                        break;
                }
            }
            else
            {
                // 展开指定窗口
                ExpandSingleWindow(targetState);
                currentExpandState = targetState;
                
                // 更新按钮文字
                switch (targetState)
                {
                    case ExpandState.Tongyi:
                        tongyiExpandButton.Text = "收缩通义";
                        break;
                    case ExpandState.Doubao:
                        doubaoExpandButton.Text = "收缩豆包";
                        break;
                    case ExpandState.Deepseek:
                        deepseekExpandButton.Text = "收缩DeepSeek";
                        break;
                }
            }
        }

        // 展开单个窗口
        private void ExpandSingleWindow(ExpandState state)
        {
            // 隐藏所有WebView
            tongyiWebView.Visible = false;
            doubaoWebView.Visible = false;
            deepseekWebView.Visible = false;
            
            // 根据状态显示对应的WebView并设置为全屏
            switch (state)
            {
                case ExpandState.Tongyi:
                    tongyiWebView.Visible = true;
                    tongyiWebView.Dock = DockStyle.Fill;
                    break;
                case ExpandState.Doubao:
                    doubaoWebView.Visible = true;
                    doubaoWebView.Dock = DockStyle.Fill;
                    break;
                case ExpandState.Deepseek:
                    deepseekWebView.Visible = true;
                    deepseekWebView.Dock = DockStyle.Fill;
                    break;
            }
        }

        // 恢复三列布局
        private void RestoreThreeColumnLayout()
        {
            // 显示所有WebView
            tongyiWebView.Visible = true;
            doubaoWebView.Visible = true;
            deepseekWebView.Visible = true;
            
            // 恢复原始布局
            tongyiWebView.Dock = DockStyle.Left;
            tongyiWebView.Width = (this.Width / 3) - 10;
            
            doubaoWebView.Dock = DockStyle.Left;
            doubaoWebView.Width = (this.Width / 3) - 10;
            
            deepseekWebView.Dock = DockStyle.Fill;
        }

        private async Task SimulateInputToTongyi(string question)
        {
            try
            {
                // 切换到通义千问窗口
                tongyiWebView.Focus();
                
                // 等待窗口获得焦点
                await Task.Delay(100);
                
                // 通过JavaScript点击输入框区域（假设输入框在页面中央偏下位置）
                string clickScript = @"
                    (function() {
                        // 尝试找到输入框并点击
                        var inputElements = document.querySelectorAll('textarea[placeholder*=""提问""], textarea[aria-label*=""输入""], textarea');
                        for (var i = 0; i < inputElements.length; i++) {
                            var element = inputElements[i];
                            if (element.offsetHeight > 0 && element.offsetWidth > 0) {
                                element.focus();
                                element.click();
                                return;
                            }
                        }
                        // 如果找不到特定输入框，点击页面中央偏下位置
                        var x = window.innerWidth / 2;
                        var y = window.innerHeight - 100;
                        var element = document.elementFromPoint(x, y);
                        if (element) {
                            element.click();
                        }
                    })();
                ";
                
                await tongyiWebView.ExecuteScriptAsync(clickScript);
                
                // 等待点击生效
                await Task.Delay(200);
                
                // 清空剪贴板并设置新内容
                Clipboard.SetText(question);
                
                // 发送粘贴命令 (Ctrl+V)
                SendKeys.SendWait("^{v}");
                
                // 等待粘贴完成
                await Task.Delay(100);
                
                // 增加延迟确保粘贴完成后再发送回车
                await Task.Delay(200);
                
                // 发送回车键
                SendKeys.SendWait("{ENTER}");
                
                // 再增加一个短延迟，确保回车被处理
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"模拟输入到通义千问时出错: {ex.Message}");
            }
        }

        private async Task SimulateInputToDoubao(string question)
        {
            try
            {
                // 切换到豆包窗口
                doubaoWebView.Focus();
                
                // 等待窗口获得焦点
                await Task.Delay(100);
                
                // 通过JavaScript点击输入框区域
                string clickScript = @"
                    (function() {
                        // 尝试找到输入框并点击
                        var inputElements = document.querySelectorAll('textarea[placeholder*=""提问""], textarea[contenteditable=""true""], div[contenteditable=""true""]');
                        for (var i = 0; i < inputElements.length; i++) {
                            var element = inputElements[i];
                            if (element.offsetHeight > 0 && element.offsetWidth > 0) {
                                element.focus();
                                element.click();
                                return;
                            }
                        }
                        // 如果找不到特定输入框，点击页面中央偏下位置
                        var x = window.innerWidth / 2;
                        var y = window.innerHeight - 100;
                        var element = document.elementFromPoint(x, y);
                        if (element) {
                            element.click();
                        }
                    })();
                ";
                
                await doubaoWebView.ExecuteScriptAsync(clickScript);
                
                // 等待点击生效
                await Task.Delay(200);
                
                // 清空剪贴板并设置新内容
                Clipboard.SetText(question);
                
                // 发送粘贴命令 (Ctrl+V)
                SendKeys.SendWait("^{v}");
                
                // 等待粘贴完成
                await Task.Delay(100);
                
                // 增加延迟确保粘贴完成后再发送回车
                await Task.Delay(200);
                
                // 发送回车键
                SendKeys.SendWait("{ENTER}");
                
                // 再增加一个短延迟，确保回车被处理
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"模拟输入到豆包时出错: {ex.Message}");
            }
        }

        private async Task SimulateInputToDeepSeek(string question)
        {
            try
            {
                // 切换到DeepSeek窗口
                deepseekWebView.Focus();
                
                // 等待窗口获得焦点
                await Task.Delay(100);
                
                // 通过JavaScript点击输入框区域
                string clickScript = @"
                    (function() {
                        // 尝试找到输入框并点击
                        var inputElements = document.querySelectorAll('textarea[placeholder*=""提问""], div[contenteditable=""true""], textarea');
                        for (var i = 0; i < inputElements.length; i++) {
                            var element = inputElements[i];
                            if (element.offsetHeight > 0 && element.offsetWidth > 0) {
                                element.focus();
                                element.click();
                                return;
                            }
                        }
                        // 如果找不到特定输入框，点击页面中央偏下位置
                        var x = window.innerWidth / 2;
                        var y = window.innerHeight - 100;
                        var element = document.elementFromPoint(x, y);
                        if (element) {
                            element.click();
                        }
                    })();
                ";
                
                await deepseekWebView.ExecuteScriptAsync(clickScript);
                
                // 等待点击生效
                await Task.Delay(200);
                
                // 清空剪贴板并设置新内容
                Clipboard.SetText(question);
                
                // 发送粘贴命令 (Ctrl+V)
                SendKeys.SendWait("^{v}");
                
                // 等待粘贴完成
                await Task.Delay(100);
                
                // 发送回车键
                SendKeys.SendWait("{ENTER}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"模拟输入到DeepSeek时出错: {ex.Message}");
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // 窗口大小改变时重新调整布局
            if (bottomPanel != null && tongyiWebView != null && doubaoWebView != null)
            {
                // 调整输入框大小为窗口宽度的80%
                searchTextBox.Size = new Size((int)(this.Width * 0.8) - 150, 30);
                
                // 调整按钮位置
                searchButton.Location = new Point(searchTextBox.Right + 10, 30);
                clearButton.Location = new Point(searchButton.Right + 10, 30);
                configButton.Location = new Point(clearButton.Right + 10, 30);
                
                // 调整展开按钮位置
                doubaoExpandButton.Location = new Point(tongyiExpandButton.Right + 10, 65);
                deepseekExpandButton.Location = new Point(doubaoExpandButton.Right + 10, 65);
                
                // 如果当前不是展开状态，则调整WebView宽度
                if (currentExpandState == ExpandState.None)
                {
                    tongyiWebView.Width = (this.Width / 3) - 10;
                    doubaoWebView.Width = (this.Width / 3) - 10;
                }
            }
        }
    }
}