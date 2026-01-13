using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace MultiModelChat
{
    public class WebSiteConfig
    {
        public bool IsSelected { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
    }

    // 配置类
    public class WebViewConfig
    {
        public System.Collections.Generic.List<WebSiteConfig> Sites { get; set; } = new System.Collections.Generic.List<WebSiteConfig>();
    }

    public partial class MainWindow : Form
    {
        private TextBox searchTextBox;
        private MenuStrip menuStrip;
        private ToolStrip toolStrip;
        private ToolTip toolTip;
        private Panel inputPanel; // 输入框面板
        private Panel bottomPanel;
        private System.Collections.Generic.List<Microsoft.Web.WebView2.WinForms.WebView2> activeWebViews = new System.Collections.Generic.List<Microsoft.Web.WebView2.WinForms.WebView2>();
        private System.Collections.Generic.List<ToolStripButton> expandToolButtons = new System.Collections.Generic.List<ToolStripButton>();
        private int currentExpandedIndex = -1; // -1 表示没有展开，0, 1, 2... 表示展开第几个

        
        // 配置信息
        private WebViewConfig config = new WebViewConfig();
        private string configFilePath = Path.Combine(Application.StartupPath, "webview_config.json");
        private string historyFilePath = Path.Combine(Application.StartupPath, "history.txt");

        public MainWindow()
        {
            LoadConfig(); // 先加载配置
            InitializeComponent();
            
            // 订阅语言变更事件
            LanguageManager.LanguageChanged += OnLanguageChanged;
        }
        
        private void OnLanguageChanged()
        {
            // 重新创建菜单和工具栏以应用新语言
            this.Controls.Remove(menuStrip);
            this.Controls.Remove(toolStrip);
            CreateMenuStrip();
            CreateToolStrip();
            this.Controls.Add(toolStrip);
            this.Controls.Add(menuStrip);
            this.MainMenuStrip = menuStrip;
            
            // 更新窗体标题
            this.Text = LanguageManager.Get("AppTitle");
            
            // 更新输入框占位符
            searchTextBox.PlaceholderText = LanguageManager.Get("InputPlaceholder");
            
            // 更新展开按钮
            UpdateExpandToolButtons();
        }

        private void InitializeComponent()
        {
            // 设置窗体属性
            this.Text = LanguageManager.Get("AppTitle");
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1200, 800);
            this.BackColor = Color.FromArgb(245, 247, 250); // 浅灰蓝背景
            
            // 设置窗体图标
            string iconPath = Path.Combine(Application.StartupPath, "img", "favicon.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
            
            // 创建 ToolTip
            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 500;
            toolTip.ReshowDelay = 200;
            
            // 创建菜单栏
            CreateMenuStrip();
            
            // 创建工具栏
            CreateToolStrip();
            
            // 创建输入框面板
            CreateInputPanel();
            
            // 创建下面板
            CreateBottomPanel();
            
            // 添加控件到窗体（注意顺序，从上到下）
            this.Controls.Add(bottomPanel);
            this.Controls.Add(inputPanel);
            this.Controls.Add(toolStrip);
            this.Controls.Add(menuStrip);
            this.MainMenuStrip = menuStrip;
        }

        private void CreateMenuStrip()
        {
            menuStrip = new MenuStrip();
            menuStrip.Font = new Font("微软雅黑", 10);
            menuStrip.BackColor = Color.White;
            menuStrip.ForeColor = Color.FromArgb(51, 51, 51);
            
            // 文件菜单
            var fileMenu = new ToolStripMenuItem(LanguageManager.Get("MenuFile"));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem(LanguageManager.Get("MenuConfig"), null, (s, e) => ConfigButton_Click(s, e), Keys.Control | Keys.P));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem(LanguageManager.Get("MenuHistory"), null, (s, e) => HistoryButton_Click(s, e), Keys.Control | Keys.H));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem(LanguageManager.Get("MenuExit"), null, (s, e) => this.Close(), Keys.Alt | Keys.F4));
            
            // 操作菜单
            var actionMenu = new ToolStripMenuItem(LanguageManager.Get("MenuAction"));
            actionMenu.DropDownItems.Add(new ToolStripMenuItem(LanguageManager.Get("MenuSendQuery"), null, (s, e) => SearchButton_Click(s, e), Keys.Control | Keys.Enter));
            actionMenu.DropDownItems.Add(new ToolStripMenuItem(LanguageManager.Get("MenuClearInput"), null, (s, e) => ClearButton_Click(s, e), Keys.Control | Keys.L));
            actionMenu.DropDownItems.Add(new ToolStripSeparator());
            actionMenu.DropDownItems.Add(new ToolStripMenuItem(LanguageManager.Get("MenuRestoreView"), null, (s, e) => { currentExpandedIndex = -1; UpdateLayout(); UpdateExpandToolButtons(); }));
            
            // 视图菜单 (展开各个模型)
            var viewMenu = new ToolStripMenuItem(LanguageManager.Get("MenuView"));
            viewMenu.Tag = "viewMenu"; // 标记方便后续更新
            // 展开项会在 UpdateExpandToolButtons 中动态添加
            
            // 帮助菜单
            var helpMenu = new ToolStripMenuItem(LanguageManager.Get("MenuHelp"));
            
            // 语言子菜单
            var langMenu = new ToolStripMenuItem(LanguageManager.Get("MenuLanguage"));
            var chineseItem = new ToolStripMenuItem("简体中文", null, (s, e) => { LanguageManager.CurrentLanguage = Language.Chinese; });
            var englishItem = new ToolStripMenuItem("English", null, (s, e) => { LanguageManager.CurrentLanguage = Language.English; });
            chineseItem.Checked = LanguageManager.CurrentLanguage == Language.Chinese;
            englishItem.Checked = LanguageManager.CurrentLanguage == Language.English;
            langMenu.DropDownItems.Add(chineseItem);
            langMenu.DropDownItems.Add(englishItem);
            
            helpMenu.DropDownItems.Add(langMenu);
            helpMenu.DropDownItems.Add(new ToolStripSeparator());
            helpMenu.DropDownItems.Add(new ToolStripMenuItem(LanguageManager.Get("MenuAbout"), null, (s, e) => AboutButton_Click(s, e)));
            
            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(actionMenu);
            menuStrip.Items.Add(viewMenu);
            menuStrip.Items.Add(helpMenu);
        }

        private void CreateToolStrip()
        {
            toolStrip = new ToolStrip();
            toolStrip.ImageScalingSize = new Size(20, 20);
            toolStrip.Font = new Font("微软雅黑", 9);
            toolStrip.BackColor = Color.White;
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.Padding = new Padding(8, 4, 8, 4);
            toolStrip.RenderMode = ToolStripRenderMode.Professional;
            
            // 查询按钮
            var searchBtn = CreateStyledToolButton(LanguageManager.Get("ToolSearch"), LanguageManager.Get("ToolSearchTip"), "▶", Color.FromArgb(82, 196, 26));
            searchBtn.Click += SearchButton_Click;
            
            // 清除按钮
            var clearBtn = CreateStyledToolButton(LanguageManager.Get("ToolClear"), LanguageManager.Get("ToolClearTip"), "✕", Color.FromArgb(255, 77, 79));
            clearBtn.Click += ClearButton_Click;
            
            // 配置按钮
            var configBtn = CreateStyledToolButton(LanguageManager.Get("ToolConfig"), LanguageManager.Get("ToolConfigTip"), "⚙", Color.FromArgb(24, 144, 255));
            configBtn.Click += ConfigButton_Click;
            
            // 历史按钮
            var historyBtn = CreateStyledToolButton(LanguageManager.Get("ToolHistory"), LanguageManager.Get("ToolHistoryTip"), "☰", Color.FromArgb(114, 46, 209));
            historyBtn.Click += HistoryButton_Click;
            
            // 恢复分栏按钮
            var restoreBtn = CreateStyledToolButton(LanguageManager.Get("ToolSplit"), LanguageManager.Get("ToolSplitTip"), "▦", Color.FromArgb(250, 173, 20));
            restoreBtn.Click += (s, e) => { currentExpandedIndex = -1; UpdateLayout(); UpdateExpandToolButtons(); };
            
            toolStrip.Items.Add(searchBtn);
            toolStrip.Items.Add(clearBtn);
            toolStrip.Items.Add(CreateToolStripSeparator());
            toolStrip.Items.Add(configBtn);
            toolStrip.Items.Add(historyBtn);
            toolStrip.Items.Add(CreateToolStripSeparator());
            toolStrip.Items.Add(restoreBtn);
            toolStrip.Items.Add(CreateToolStripSeparator());
            // 展开按钮会在 UpdateExpandToolButtons 中动态添加
        }
        
        private ToolStripButton CreateStyledToolButton(string text, string tooltip, string icon, Color iconColor)
        {
            var btn = new ToolStripButton();
            btn.Text = text;
            btn.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            btn.ToolTipText = tooltip;
            btn.Margin = new Padding(2, 0, 2, 0);
            btn.Padding = new Padding(6, 2, 6, 2);
            btn.AutoSize = true;
            
            // 创建简洁的图标
            using (var bmp = new Bitmap(20, 20))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(iconColor))
                {
                    g.DrawString(icon, new Font("Segoe UI Symbol", 11, FontStyle.Bold), brush, 0, 0);
                }
                btn.Image = (Image)bmp.Clone();
            }
            
            return btn;
        }
        
        private ToolStripSeparator CreateToolStripSeparator()
        {
            var sep = new ToolStripSeparator();
            sep.Margin = new Padding(4, 0, 4, 0);
            return sep;
        }

        private void UpdateExpandToolButtons()
        {
            // 清除旧的展开按钮
            foreach (var btn in expandToolButtons)
            {
                toolStrip.Items.Remove(btn);
            }
            expandToolButtons.Clear();

            var selectedSites = config.Sites.FindAll(s => s.IsSelected);
            for (int i = 0; i < selectedSites.Count; i++)
            {
                var site = selectedSites[i];
                bool isExpanded = (i == currentExpandedIndex);
                string prefix = isExpanded ? LanguageManager.Get("ToolCollapse") : LanguageManager.Get("ToolExpand");
                string tooltip = isExpanded ? LanguageManager.Get("ToolCollapseTip") : LanguageManager.Get("ToolExpandTip");
                Color iconColor = isExpanded ? Color.FromArgb(255, 77, 79) : Color.FromArgb(82, 196, 26);
                
                var expandBtn = CreateStyledToolButton(prefix + " " + site.Name, tooltip + " " + site.Name, isExpanded ? "◀" : "▷", iconColor);
                
                int index = i;
                expandBtn.Click += (sender, e) => ToggleExpand(index);
                
                toolStrip.Items.Add(expandBtn);
                expandToolButtons.Add(expandBtn);
            }
            
            // 更新视图菜单
            UpdateViewMenu(selectedSites);
        }
        
        private void UpdateViewMenu(System.Collections.Generic.List<WebSiteConfig> selectedSites)
        {
            foreach (ToolStripMenuItem item in menuStrip.Items)
            {
                if (item.Tag != null && item.Tag.ToString() == "viewMenu")
                {
                    item.DropDownItems.Clear();
                    for (int i = 0; i < selectedSites.Count; i++)
                    {
                        var site = selectedSites[i];
                        int index = i;
                        string prefix = (i == currentExpandedIndex) ? "✓ " : "";
                        var menuItem = new ToolStripMenuItem(prefix + LanguageManager.Get("MenuExpand") + " " + site.Name, null, (s, e) => ToggleExpand(index));
                        item.DropDownItems.Add(menuItem);
                    }
                    if (selectedSites.Count > 0)
                    {
                        item.DropDownItems.Add(new ToolStripSeparator());
                    }
                    item.DropDownItems.Add(new ToolStripMenuItem(LanguageManager.Get("MenuRestoreView"), null, (s, e) => { currentExpandedIndex = -1; UpdateLayout(); UpdateExpandToolButtons(); }));
                    break;
                }
            }
        }

        private void CreateInputPanel()
        {
            inputPanel = new Panel();
            inputPanel.Dock = DockStyle.Top;
            inputPanel.Height = 80;
            inputPanel.BackColor = Color.FromArgb(250, 251, 252);
            inputPanel.Padding = new Padding(10);
            
            // 创建多行搜索输入框
            searchTextBox = new TextBox();
            searchTextBox.Location = new Point(10, 10);
            searchTextBox.Size = new Size(this.Width - 30, 60); // 占据面板宽度
            searchTextBox.Font = new Font("微软雅黑", 11);
            searchTextBox.PlaceholderText = LanguageManager.Get("InputPlaceholder");
            searchTextBox.Multiline = true; // 设置为多行
            searchTextBox.ScrollBars = ScrollBars.Vertical; // 添加垂直滚动条
            searchTextBox.AcceptsReturn = false; // 禁止回车换行，确保回车触发查询
            searchTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; // 响应式调整
            searchTextBox.BorderStyle = BorderStyle.FixedSingle;
            
            // 添加键盘事件支持
            searchTextBox.KeyDown += (sender, e) => {
                // Ctrl+Enter 触发查询
                if (e.Control && e.KeyCode == Keys.Enter)
                {
                    SearchButton_Click(sender, e);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                // 单独按Enter时也触发查询（如需换行可改为Ctrl+Enter）
                else if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    SearchButton_Click(sender, e);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            
            // 添加控件到输入框面板
            inputPanel.Controls.Add(searchTextBox);
        }

        private void CreateBottomPanel()
        {
            bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Fill;
            
            UpdateWebViews();
        }

        private async void UpdateWebViews()
        {
            // 清除旧的WebView
            foreach (var wv in activeWebViews)
            {
                bottomPanel.Controls.Remove(wv);
                wv.Dispose();
            }
            activeWebViews.Clear();

            var selectedSites = config.Sites.FindAll(s => s.IsSelected);
            if (selectedSites.Count == 0) return;

            // 限制最多显示3个，但实际可以多开，只是布局会挤
            // 用户要求“一般最多允许同时打开3个”，这里不对添加做限制，但显示上我们按选中的来
            
            for (int i = 0; i < selectedSites.Count; i++)
            {
                var site = selectedSites[i];
                var wv = new Microsoft.Web.WebView2.WinForms.WebView2();
                wv.Tag = site.Name;
                activeWebViews.Add(wv);
                bottomPanel.Controls.Add(wv);
                
                try {
                    await wv.EnsureCoreWebView2Async(null);
                    wv.CoreWebView2.Navigate(site.Url);
                } catch (Exception ex) {
                    MessageBox.Show($"初始化 {site.Name} 时出错: {ex.Message}");
                }
            }

            UpdateLayout();
            UpdateExpandToolButtons();
        }

        private void UpdateLayout()
        {
            int count = activeWebViews.Count;
            if (count == 0) return;

            if (currentExpandedIndex >= 0 && currentExpandedIndex < count)
            {
                // 展开模式
                for (int i = 0; i < count; i++)
                {
                    activeWebViews[i].Visible = (i == currentExpandedIndex);
                    if (i == currentExpandedIndex)
                    {
                        activeWebViews[i].Dock = DockStyle.Fill;
                    }
                }
            }
            else
            {
                // 分栏模式
                for (int i = 0; i < count; i++)
                {
                    activeWebViews[i].Visible = true;
                    if (i == count - 1)
                    {
                        activeWebViews[i].Dock = DockStyle.Fill;
                    }
                    else
                    {
                        activeWebViews[i].Dock = DockStyle.Left;
                        activeWebViews[i].Width = bottomPanel.Width / count;
                    }
                }
            }
        }

        // InitializeWebViews 被 UpdateWebViews 取代了

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            string question = searchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(question))
            {
                MessageBox.Show(LanguageManager.Get("MsgEnterQuestion"), LanguageManager.Get("MsgTip"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            try
            {
                // 保存历史记录
                SaveHistory(question);
                
                // 遍历所有激活的WebView并注入问题
                foreach (var wv in activeWebViews)
                {
                    await SimulateInputToWebView(wv, question);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LanguageManager.Get("MsgError")}: {ex.Message}", LanguageManager.Get("MsgError"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    UpdateWebViews();
                }
            }
        }

        // 关于按钮点击事件
        private void AboutButton_Click(object sender, EventArgs e)
        {
            using (var aboutForm = new Form())
            {
                aboutForm.Text = LanguageManager.Get("AboutTitle");
                aboutForm.Size = new Size(400, 250);
                aboutForm.StartPosition = FormStartPosition.CenterParent;
                aboutForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                aboutForm.MaximizeBox = false;
                aboutForm.MinimizeBox = false;
                aboutForm.BackColor = Color.White;
                
                // 创建标签显示信息
                var infoLabel = new Label();
                infoLabel.AutoSize = false;
                infoLabel.Size = new Size(360, 150);
                infoLabel.Location = new Point(20, 20);
                infoLabel.Font = new Font("微软雅黑", 12);
                infoLabel.Text = LanguageManager.Get("AboutAuthor") + "\n\n" +
                               LanguageManager.Get("AboutEmail") + "\n\n" +
                               LanguageManager.Get("AboutVersion");
                
                // 创建确定按钮
                var okButton = new Button();
                okButton.Text = LanguageManager.Get("BtnOK");
                okButton.Size = new Size(100, 35);
                okButton.Location = new Point(150, 170);
                okButton.DialogResult = DialogResult.OK;
                okButton.Font = new Font("微软雅黑", 10);
                okButton.BackColor = Color.FromArgb(24, 144, 255);
                okButton.ForeColor = Color.White;
                okButton.FlatStyle = FlatStyle.Flat;
                okButton.FlatAppearance.BorderSize = 0;
                
                aboutForm.Controls.Add(infoLabel);
                aboutForm.Controls.Add(okButton);
                aboutForm.AcceptButton = okButton;
                
                aboutForm.ShowDialog(this);
            }
        }

        // 保存历史记录
        private void SaveHistory(string question)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string record = $"[{timestamp}] {question}";
                File.AppendAllText(historyFilePath, record + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存历史记录时出错: {ex.Message}");
            }
        }

        // 历史记录按钮点击事件
        private void HistoryButton_Click(object sender, EventArgs e)
        {
            var historyForm = new HistoryForm(historyFilePath);
            historyForm.ShowDialog(this);
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
                
                // 如果配置为空或者Sites为空，初始化默认值
                if (config == null || config.Sites == null || config.Sites.Count == 0)
                {
                    config = new WebViewConfig();
                    config.Sites.Add(new WebSiteConfig { IsSelected = true, Name = "通义千问", Url = "https://tongyi.aliyun.com/qianwen/" });
                    config.Sites.Add(new WebSiteConfig { IsSelected = true, Name = "豆包", Url = "https://www.doubao.com/chat/" });
                    config.Sites.Add(new WebSiteConfig { IsSelected = true, Name = "DeepSeek", Url = "https://chat.deepseek.com/" });
                    config.Sites.Add(new WebSiteConfig { IsSelected = false, Name = "Gemini", Url = "https://gemini.google.com/" });
                    config.Sites.Add(new WebSiteConfig { IsSelected = false, Name = "ChatGPT", Url = "https://chatgpt.com/" });
                    config.Sites.Add(new WebSiteConfig { IsSelected = false, Name = "Kimi", Url = "https://www.kimi.com/" });
                    config.Sites.Add(new WebSiteConfig { IsSelected = false, Name = "元宝", Url = "https://yuanbao.tencent.com/" });
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

        // LoadWebViews 被 UpdateWebViews 取代了

        // 清除按钮点击事件
        private void ClearButton_Click(object sender, EventArgs e)
        {
            searchTextBox.Clear();
            searchTextBox.Focus();
        }

        // 切换展开状态
        private void ToggleExpand(int index)
        {
            if (currentExpandedIndex == index)
            {
                currentExpandedIndex = -1;
            }
            else
            {
                currentExpandedIndex = index;
            }

            UpdateLayout();
            UpdateExpandToolButtons();
        }

        // ExpandSingleWindow 和 RestoreThreeColumnLayout 被 UpdateLayout 取代了

        private async Task SimulateInputToWebView(Microsoft.Web.WebView2.WinForms.WebView2 wv, string question)
        {
            try
            {
                wv.Focus();
                await Task.Delay(100);
                
                string clickScript = @"
                    (function() {
                        var inputElements = document.querySelectorAll('textarea[placeholder*=""提问""], textarea[aria-label*=""输入""], textarea, div[contenteditable=""true""]');
                        for (var i = 0; i < inputElements.length; i++) {
                            var element = inputElements[i];
                            if (element.offsetHeight > 0 && element.offsetWidth > 0) {
                                element.focus();
                                element.click();
                                return;
                            }
                        }
                        var x = window.innerWidth / 2;
                        var y = window.innerHeight - 100;
                        var element = document.elementFromPoint(x, y);
                        if (element) {
                            element.click();
                        }
                    })();
                ";
                
                await wv.ExecuteScriptAsync(clickScript);
                await Task.Delay(200);
                
                Clipboard.SetText(question);
                SendKeys.SendWait("^{v}");
                await Task.Delay(200);
                SendKeys.SendWait("{ENTER}");
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"模拟输入到 {wv.Tag} 时出错: {ex.Message}");
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // 窗口大小改变时重新调整布局
            if (bottomPanel != null && activeWebViews != null)
            {
                UpdateLayout();
            }
        }
    }
}