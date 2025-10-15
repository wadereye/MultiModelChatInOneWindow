using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using System.Threading.Tasks;

namespace MultiModelChat
{
    public partial class MainWindow : Form
    {
        private TextBox searchTextBox;
        private Button searchButton;
        private Panel topPanel;
        private Panel bottomPanel;
        private Microsoft.Web.WebView2.WinForms.WebView2 tongyiWebView;
        private Microsoft.Web.WebView2.WinForms.WebView2 doubaoWebView;
        private Microsoft.Web.WebView2.WinForms.WebView2 deepseekWebView;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // 设置窗体属性
            this.Text = "多模型问答系统";
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1200, 800);
            
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
            
            // 创建搜索输入框
            searchTextBox = new TextBox();
            searchTextBox.Location = new Point(20, 30);
            searchTextBox.Size = new Size(800, 30);
            searchTextBox.Font = new Font("微软雅黑", 12);
            searchTextBox.PlaceholderText = "请输入您的问题...";
            
            // 创建查询按钮
            searchButton = new Button();
            searchButton.Location = new Point(840, 30);
            searchButton.Size = new Size(100, 30);
            searchButton.Text = "查询";
            searchButton.Font = new Font("微软雅黑", 12);
            searchButton.Click += SearchButton_Click;
            
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
            tongyiWebView.Width = (Screen.PrimaryScreen.WorkingArea.Width / 3) - 10;
            
            doubaoWebView.Dock = DockStyle.Left;
            doubaoWebView.Width = (Screen.PrimaryScreen.WorkingArea.Width / 3) - 10;
            
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
                
                // 导航到相应网站
                tongyiWebView.CoreWebView2.Navigate("https://tongyi.aliyun.com/qianwen/");
                doubaoWebView.CoreWebView2.Navigate("https://www.doubao.com/chat/");
                deepseekWebView.CoreWebView2.Navigate("https://chat.deepseek.com/");
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
                
                // 发送回车键
                SendKeys.SendWait("{ENTER}");
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
                
                // 发送回车键
                SendKeys.SendWait("{ENTER}");
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
                tongyiWebView.Width = (this.Width / 3) - 10;
                doubaoWebView.Width = (this.Width / 3) - 10;
            }
        }
    }
}