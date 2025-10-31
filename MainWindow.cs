using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using CefSharp;
using CefSharp.WinForms;

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
        private void Log(string msg)
        {
            try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_startup.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n"); } catch { }
        }

        private TextBox searchTextBox;
        private Button searchButton;
        private Button clearButton;
        private Button configButton; // 新增配置按钮
        private Button historyButton; // 新增查看历史按钮
        private Panel topPanel;
        private Panel bottomPanel;
        private ChromiumWebBrowser tongyiBrowser;
        private ChromiumWebBrowser doubaoBrowser;
        private ChromiumWebBrowser deepseekBrowser;
        private string historyFilePath; // 历史记录文件路径
        
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
            Log("MainWindow ctor start");
            InitializeComponent();
            Log("InitializeComponent done");
            try
            {
                LoadConfig(); // 加载配置
                historyFilePath = Path.Combine(Application.StartupPath, "history.txt");
                Log("LoadConfig done");
            }
            catch (Exception ex)
            {
                Log("LoadConfig exception: " + ex.Message);
            }
        }

        private void InitializeComponent()
        {
            Log("InitializeComponent enter");
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
            this.Load += (s, e) => Log("MainWindow Load event fired");
            Log("InitializeComponent exit");
        }

        private void CreateTopPanel()
        {
            Log("CreateTopPanel enter");
            topPanel = new Panel();
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 120; // 调整高度，因为展开按钮移到顶部了
            topPanel.BackColor = Color.LightGray;
            
            // 创建按钮面板 - 顶部的所有按钮都放在这里
            Panel buttonPanel = new Panel();
            buttonPanel.Size = new Size(this.Width - 40, 35);
            buttonPanel.Location = new Point(20, 10);
            
            // 创建搜索输入框，调整为多行输入，高度为60（约三行）
            searchTextBox = new TextBox();
            searchTextBox.Location = new Point(20, 55);
            searchTextBox.Size = new Size(this.Width - 40, 50);
            searchTextBox.Font = new Font("微软雅黑", 12);
            searchTextBox.Multiline = true;
            searchTextBox.ScrollBars = ScrollBars.Vertical;
            
            // 创建查询按钮
            searchButton = new Button();
            searchButton.Size = new Size(80, 30);
            searchButton.Text = "查询";
            searchButton.Font = new Font("微软雅黑", 12);
            searchButton.Click += SearchButton_Click;
            
            // 创建清除按钮
            clearButton = new Button();
            clearButton.Size = new Size(80, 30);
            clearButton.Text = "清除";
            clearButton.Font = new Font("微软雅黑", 12);
            clearButton.Click += ClearButton_Click;
            
            // 创建配置按钮
            configButton = new Button();
            configButton.Size = new Size(80, 30);
            configButton.Text = "配置";
            configButton.Font = new Font("微软雅黑", 12);
            configButton.Click += ConfigButton_Click;
            
            // 创建历史按钮
            historyButton = new Button();
            historyButton.Size = new Size(80, 30);
            historyButton.Text = "历史";
            historyButton.Font = new Font("微软雅黑", 12);
            historyButton.Click += HistoryButton_Click;
            
            // 创建展开按钮
            tongyiExpandButton = new Button();
            tongyiExpandButton.Text = "展开通义";
            tongyiExpandButton.Size = new Size(80, 25);
            tongyiExpandButton.Click += (sender, e) => ToggleExpand(ExpandState.Tongyi);
            
            doubaoExpandButton = new Button();
            doubaoExpandButton.Text = "展开豆包";
            doubaoExpandButton.Size = new Size(80, 25);
            doubaoExpandButton.Click += (sender, e) => ToggleExpand(ExpandState.Doubao);
            
            deepseekExpandButton = new Button();
            deepseekExpandButton.Text = "展开DeepSeek";
            deepseekExpandButton.Size = new Size(100, 25);
            deepseekExpandButton.Click += (sender, e) => ToggleExpand(ExpandState.Deepseek);
            
            // 添加所有按钮到同一个按钮面板（查询、清除、配置、历史、展开按钮）
            buttonPanel.Controls.Add(searchButton);
            buttonPanel.Controls.Add(clearButton);
            buttonPanel.Controls.Add(configButton);
            buttonPanel.Controls.Add(historyButton);
            buttonPanel.Controls.Add(tongyiExpandButton);
            buttonPanel.Controls.Add(doubaoExpandButton);
            buttonPanel.Controls.Add(deepseekExpandButton);
            
            // 添加回车键支持
            searchTextBox.KeyDown += (sender, e) => {
                if (e.KeyCode == Keys.Enter && e.Control) // Ctrl+Enter 发送
                {
                    SearchButton_Click(sender, e);
                }
            };
            
            // 添加控件到上方面板
            topPanel.Controls.Add(buttonPanel);
            topPanel.Controls.Add(searchTextBox);
            Log("CreateTopPanel exit");
        }

        private void CreateBottomPanel()
        {
            Log("CreateBottomPanel enter");
            bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Fill;
            bottomPanel.Padding = new Padding(0, 120, 0, 0); // 调整上边距以适应新的顶部面板高度
            try
            {
                tongyiBrowser = new ChromiumWebBrowser(config.TongyiUrl);
                doubaoBrowser = new ChromiumWebBrowser(config.DoubaoUrl);
                deepseekBrowser = new ChromiumWebBrowser(config.DeepSeekUrl);
                Log("ChromiumWebBrowser instances created");
                // 设置浏览器属性
                tongyiBrowser.Dock = DockStyle.Left;
                tongyiBrowser.Width = (this.Width / 3) - 10;
                
                doubaoBrowser.Dock = DockStyle.Left;
                doubaoBrowser.Width = (this.Width / 3) - 10;
                
                deepseekBrowser.Dock = DockStyle.Fill;
                
                // 添加浏览器到下面板
                bottomPanel.Controls.Add(deepseekBrowser);
                bottomPanel.Controls.Add(doubaoBrowser);
                bottomPanel.Controls.Add(tongyiBrowser);
                Log("Browsers added to bottomPanel");
            }
            catch (Exception ex)
            {
                Log("CreateBottomPanel exception: " + ex.Message);
                MessageBox.Show("创建浏览器控件失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            Log("CreateBottomPanel exit");
        }

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            string question = searchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(question))
            {
                MessageBox.Show("请输入问题内容", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // 保存到历史记录
            SaveToHistory(question);
            
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
                    // 重新加载页面
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

        // 重新加载页面
        private void LoadWebViews()
        {
            try
            {
                tongyiBrowser.Load(config.TongyiUrl);
                doubaoBrowser.Load(config.DoubaoUrl);
                deepseekBrowser.Load(config.DeepSeekUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重新加载浏览器时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 历史按钮点击事件
        private void HistoryButton_Click(object sender, EventArgs e)
        {
            using (var historyForm = new HistoryForm())
            {
                if (historyForm.ShowDialog() == DialogResult.OK)
                {
                    searchTextBox.Text = historyForm.SelectedQuestion;
                    searchTextBox.Focus();
                }
            }
        }
        
        // 清除按钮点击事件
        private void ClearButton_Click(object sender, EventArgs e)
        {
            searchTextBox.Clear();
            searchTextBox.Focus();
        }
        
        // 保存到历史记录
        private void SaveToHistory(string question)
        {
            try
            {
                List<HistoryItem> historyItems = new List<HistoryItem>();
                
                // 读取现有历史记录
                if (File.Exists(historyFilePath))
                {
                    string json = File.ReadAllText(historyFilePath);
                    historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(json) ?? new List<HistoryItem>();
                }
                
                // 添加新记录
                historyItems.Add(new HistoryItem
                {
                    Question = question,
                    Timestamp = DateTime.Now
                });
                
                // 限制历史记录数量，只保留最近100条
                if (historyItems.Count > 100)
                {
                    historyItems = historyItems.Skip(historyItems.Count - 100).ToList();
                }
                
                // 保存到文件
                string jsonToSave = JsonSerializer.Serialize(historyItems, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(historyFilePath, jsonToSave);
            }
            catch (Exception ex)
            {
                // 历史记录保存失败不影响主要功能，只记录日志
                Console.WriteLine($"保存历史记录失败: {ex.Message}");
            }
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
            // 隐藏所有浏览器
            tongyiBrowser.Visible = false;
            doubaoBrowser.Visible = false;
            deepseekBrowser.Visible = false;
            
            // 根据状态显示对应的浏览器并设置为全屏
            switch (state)
            {
                case ExpandState.Tongyi:
                    tongyiBrowser.Visible = true;
                    tongyiBrowser.Dock = DockStyle.Fill;
                    break;
                case ExpandState.Doubao:
                    doubaoBrowser.Visible = true;
                    doubaoBrowser.Dock = DockStyle.Fill;
                    break;
                case ExpandState.Deepseek:
                    deepseekBrowser.Visible = true;
                    deepseekBrowser.Dock = DockStyle.Fill;
                    break;
            }
        }

        // 恢复三列布局
        private void RestoreThreeColumnLayout()
        {
            // 显示所有浏览器
            tongyiBrowser.Visible = true;
            doubaoBrowser.Visible = true;
            deepseekBrowser.Visible = true;
            
            // 恢复原始布局
            tongyiBrowser.Dock = DockStyle.Left;
            tongyiBrowser.Width = (this.Width / 3) - 10;
            
            doubaoBrowser.Dock = DockStyle.Left;
            doubaoBrowser.Width = (this.Width / 3) - 10;
            
            deepseekBrowser.Dock = DockStyle.Fill;
        }

        private async Task SimulateInputToTongyi(string question)
        {
            try
            {
                // 切换到通义千问窗口
                tongyiBrowser.Focus();
                
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
                
                await tongyiBrowser.EvaluateScriptAsync(clickScript);
                
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
                doubaoBrowser.Focus();
                
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
                
                await doubaoBrowser.EvaluateScriptAsync(clickScript);
                
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
                deepseekBrowser.Focus();
                
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
                
                await deepseekBrowser.EvaluateScriptAsync(clickScript);
                
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
            if (bottomPanel != null && tongyiBrowser != null && doubaoBrowser != null && topPanel != null)
            {
                // 调整输入框大小
                searchTextBox.Width = this.Width - 40;
                
                // 调整按钮面板大小和位置
                foreach (Control control in topPanel.Controls)
                {
                    if (control is Panel)
                    {
                        control.Width = this.Width - 40;
                    }
                }
                
                // 调整所有按钮位置 - 全部放在同一个面板中
                if (topPanel.Controls.Count > 0 && topPanel.Controls[0] is Panel buttonPanel)
                {
                    Button[] allButtons = { searchButton, clearButton, configButton, historyButton, 
                                           tongyiExpandButton, doubaoExpandButton, deepseekExpandButton };
                    int x = 0;
                    foreach (var button in allButtons)
                    {
                        button.Location = new Point(x, 5);
                        x += button.Width + 10;
                    }
                }
                
                // 如果当前不是展开状态，则调整浏览器宽度
                if (currentExpandState == ExpandState.None)
                {
                    tongyiBrowser.Width = (this.Width / 3) - 10;
                    doubaoBrowser.Width = (this.Width / 3) - 10;
                }
            }
        }
    }
}
