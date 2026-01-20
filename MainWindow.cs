using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using MultiModelChat.LocalFileSearch;
using MultiModelChat.LocalFileSearch.Models;

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

    // 支持双缓冲的 Panel，用于减少拖动时的闪烁
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer | 
                ControlStyles.AllPaintingInWmPaint | 
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw, 
                true);
            this.UpdateStyles();
        }
        
        // 重写 WndProc 来进一步减少闪烁
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 不绘制背景，由 OnPaint 统一处理
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(this.BackColor))
            {
                e.Graphics.FillRectangle(brush, this.ClientRectangle);
            }
            base.OnPaint(e);
        }
    }

    public partial class MainWindow : Form
    {
        // 主要控件
        private MenuStrip menuStrip;
        private Panel mainContentPanel;  // 主内容面板（全屏）
        private Panel floatingSidebarPanel;  // 浮动侧边栏
        private Button toggleSidebarBtn;  // 浮动的切换按钮
        
        // 左侧面板 - 网络查询区域
        private Panel networkSectionPanel;
        private Button networkSectionHeader;
        private Panel networkSectionContent;
        private ToolStrip networkToolStrip;
        private TextBox searchTextBox;
        private System.Collections.Generic.List<ToolStripButton> expandToolButtons = new System.Collections.Generic.List<ToolStripButton>();
        
        // 左侧面板 - 本地搜索区域
        private Panel localSectionPanel;
        private Button localSectionHeader;
        private Panel localSectionContent;
        private TextBox localSearchTextBox;
        private ComboBox fileTypeComboBox;
        private Button searchByNameBtn;
        private Button searchRecentBtn;
        private Button searchByTypeBtn;
        private Button searchAIBtn;
        
        // 右侧内容区域
        private Panel bottomPanel; // WebView容器
        private Panel localContentPanel; // 本地搜索结果面板
        private DataGridView localResultsGrid;
        private Label localStatusLabel;
        
        // WebView相关
        private System.Collections.Generic.List<Microsoft.Web.WebView2.WinForms.WebView2> activeWebViews = new System.Collections.Generic.List<Microsoft.Web.WebView2.WinForms.WebView2>();
        private int currentExpandedIndex = -1;
        
        // 状态变量
        private bool isLocalMode = false;
        private bool isNetworkSectionExpanded = true;
        private bool isLocalSectionExpanded = false;
        private List<FileAndDirectoryEntry> allFilesCache = null;
        
        // 拖拽相关变量
        private bool isDraggingBtn = false;
        private bool isDraggingPanel = false;
        private bool isResizingPanel = false;
        private Point dragOffset;
        private Size resizeStartSize;
        private Point resizeStartPos;
        private Panel sidebarTitleBar;  // 侧边栏标题栏（用于拖拽）
        private Panel resizeGrip;  // 调整大小的手柄
        
        // 配置信息
        private WebViewConfig config = new WebViewConfig();
        private string configFilePath = Path.Combine(Application.StartupPath, "webview_config.json");
        private string historyFilePath = Path.Combine(Application.StartupPath, "history.txt");
        
        // 常量
        private const int SIDEBAR_WIDTH = 320;
        private const int SIDEBAR_HEIGHT = 500;

        public MainWindow()
        {
            LoadConfig();
            InitializeComponent();
            LanguageManager.LanguageChanged += OnLanguageChanged;
        }
        
        private void OnLanguageChanged()
        {
            this.Text = LanguageManager.Get("AppTitle");
            
            // 更新菜单
            this.Controls.Remove(menuStrip);
            CreateMenuStrip();
            this.Controls.Add(menuStrip);
            this.MainMenuStrip = menuStrip;
            
            // 更新左侧面板标题
            if (networkSectionHeader != null)
                networkSectionHeader.Text = (isNetworkSectionExpanded ? "▼ " : "▶ ") + LanguageManager.Get("SectionNetwork");
            if (localSectionHeader != null)
                localSectionHeader.Text = (isLocalSectionExpanded ? "▼ " : "▶ ") + LanguageManager.Get("SectionLocal");
            
            // 更新输入框
            if (searchTextBox != null)
                searchTextBox.PlaceholderText = LanguageManager.Get("InputPlaceholder");
            if (localSearchTextBox != null)
                localSearchTextBox.PlaceholderText = LanguageManager.Get("LocalSearchPlaceholder");
            
            // 更新工具栏
            UpdateNetworkToolStrip();
            UpdateExpandToolButtons();
        }

        private void InitializeComponent()
        {
            this.Text = LanguageManager.Get("AppTitle");
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1200, 800);
            this.BackColor = Color.FromArgb(245, 247, 250);
            
            // 开启双缓冲减少闪烁
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            
            string iconPath = Path.Combine(Application.StartupPath, "img", "favicon.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
            
            // 创建菜单栏
            CreateMenuStrip();
            
            // 创建主布局（内容区域 + 浮动侧边栏 + 浮动按钮）
            CreateMainLayout();
            
            // 添加控件（注意顺序，后添加的在上层）
            this.Controls.Add(floatingSidebarPanel);
            this.Controls.Add(toggleSidebarBtn);
            this.Controls.Add(mainContentPanel);
            this.Controls.Add(menuStrip);
            this.MainMenuStrip = menuStrip;
            
            // 确保浮动元素在最上层
            toggleSidebarBtn.BringToFront();
            floatingSidebarPanel.BringToFront();
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
            actionMenu.DropDownItems.Add(new ToolStripMenuItem(LanguageManager.Get("MenuRestoreView"), null, (s, e) => { currentExpandedIndex = -1; UpdateWebViewLayout(); UpdateExpandToolButtons(); }));
            
            // 视图菜单
            var viewMenu = new ToolStripMenuItem(LanguageManager.Get("MenuView"));
            viewMenu.Tag = "viewMenu";
            
            // 帮助菜单
            var helpMenu = new ToolStripMenuItem(LanguageManager.Get("MenuHelp"));
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

        private void CreateMainLayout()
        {
            // 1. 创建主内容面板（全屏，在最底层）
            mainContentPanel = new Panel();
            mainContentPanel.Dock = DockStyle.Fill;
            mainContentPanel.BackColor = Color.FromArgb(245, 247, 250);
            
            // WebView容器
            bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Fill;
            bottomPanel.BackColor = Color.White;
            
            // 本地搜索结果面板
            CreateLocalResultsPanel();
            
            mainContentPanel.Controls.Add(localContentPanel);
            mainContentPanel.Controls.Add(bottomPanel);
            
            // 初始化WebViews
            UpdateWebViews();
            
            // 2. 创建浮动切换按钮（可拖动）
            toggleSidebarBtn = new Button();
            toggleSidebarBtn.Size = new Size(40, 40);
            toggleSidebarBtn.Location = new Point(15, 40);
            toggleSidebarBtn.Text = "☰";
            toggleSidebarBtn.TextAlign = ContentAlignment.MiddleCenter;
            toggleSidebarBtn.FlatStyle = FlatStyle.Flat;
            toggleSidebarBtn.FlatAppearance.BorderSize = 0;
            toggleSidebarBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 24, 144, 255);
            toggleSidebarBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(240, 24, 144, 255);
            toggleSidebarBtn.BackColor = Color.FromArgb(180, 24, 144, 255);
            toggleSidebarBtn.ForeColor = Color.White;
            toggleSidebarBtn.Font = new Font("Segoe UI Symbol", 16, FontStyle.Bold);
            toggleSidebarBtn.Cursor = Cursors.SizeAll;  // 显示拖动光标
            
            // 按钮拖拽事件
            Point panelOffset = new Point(0, 0);  // 面板相对于按钮的偏移
            toggleSidebarBtn.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDraggingBtn = true;
                    dragOffset = e.Location;
                    // 记录面板相对于按钮的位置
                    if (floatingSidebarPanel != null && floatingSidebarPanel.Visible)
                    {
                        panelOffset = new Point(
                            floatingSidebarPanel.Location.X - toggleSidebarBtn.Location.X,
                            floatingSidebarPanel.Location.Y - toggleSidebarBtn.Location.Y
                        );
                    }
                }
            };
            toggleSidebarBtn.MouseMove += (s, e) =>
            {
                if (isDraggingBtn)
                {
                    var newLocation = toggleSidebarBtn.Location;
                    newLocation.X += e.X - dragOffset.X;
                    newLocation.Y += e.Y - dragOffset.Y;
                    // 限制在窗口范围内
                    newLocation.X = Math.Max(0, Math.Min(this.ClientSize.Width - toggleSidebarBtn.Width, newLocation.X));
                    newLocation.Y = Math.Max(25, Math.Min(this.ClientSize.Height - toggleSidebarBtn.Height, newLocation.Y));
                    
                    // 使用 SuspendLayout 减少闪烁
                    this.SuspendLayout();
                    toggleSidebarBtn.Location = newLocation;
                    
                    // 如果面板可见，同时移动面板
                    if (floatingSidebarPanel != null && floatingSidebarPanel.Visible)
                    {
                        var panelLocation = new Point(
                            newLocation.X + panelOffset.X,
                            newLocation.Y + panelOffset.Y
                        );
                        // 面板也限制在窗口范围内
                        panelLocation.X = Math.Max(0, Math.Min(this.ClientSize.Width - 100, panelLocation.X));
                        panelLocation.Y = Math.Max(25, Math.Min(this.ClientSize.Height - 100, panelLocation.Y));
                        floatingSidebarPanel.Location = panelLocation;
                    }
                    this.ResumeLayout();
                }
            };
            toggleSidebarBtn.MouseUp += (s, e) =>
            {
                if (isDraggingBtn && e.Button == MouseButtons.Left)
                {
                    isDraggingBtn = false;
                    // 检查是否是点击（而不是拖动）
                    if (Math.Abs(e.X - dragOffset.X) < 5 && Math.Abs(e.Y - dragOffset.Y) < 5)
                    {
                        ToggleSidebar_Click(s, e);
                    }
                }
            };
            
            var toolTip = new ToolTip();
            toolTip.SetToolTip(toggleSidebarBtn, LanguageManager.Get("ToggleSidebar") + " (\u62d6\u52a8\u53ef\u79fb\u52a8)");
            
            // 3. 创建浮动侧边栏面板
            CreateFloatingSidebar();
        }

        private void CreateFloatingSidebar()
        {
            // 使用双缓冲面板减少闪烁
            floatingSidebarPanel = new DoubleBufferedPanel();
            floatingSidebarPanel.Size = new Size(SIDEBAR_WIDTH, SIDEBAR_HEIGHT);
            floatingSidebarPanel.Location = new Point(60, 40);
            floatingSidebarPanel.BackColor = Color.FromArgb(250, 251, 253);
            floatingSidebarPanel.BorderStyle = BorderStyle.None;
            floatingSidebarPanel.Visible = false;
            floatingSidebarPanel.Padding = new Padding(0);
            
            // 添加明显的边框效果
            floatingSidebarPanel.Paint += (s, e) =>
            {
                var rect = floatingSidebarPanel.ClientRectangle;
                var g = e.Graphics;
                
                // 绘制外层阴影效果
                using (var shadowPen = new Pen(Color.FromArgb(30, 0, 0, 0), 4))
                {
                    g.DrawRectangle(shadowPen, new Rectangle(2, 2, rect.Width - 4, rect.Height - 4));
                }
                
                // 绘制主边框
                using (var borderPen = new Pen(Color.FromArgb(24, 144, 255), 2))
                {
                    g.DrawRectangle(borderPen, new Rectangle(0, 0, rect.Width - 1, rect.Height - 1));
                }
            };
            
            // 创建标题栏（可拖拽区域）- 使用双缓冲面板
            sidebarTitleBar = new DoubleBufferedPanel();
            sidebarTitleBar.Dock = DockStyle.Top;
            sidebarTitleBar.Height = 32;
            sidebarTitleBar.BackColor = Color.FromArgb(24, 144, 255);
            sidebarTitleBar.Cursor = Cursors.SizeAll;
            
            // 标题文字
            var titleLabel = new Label();
            titleLabel.Text = "⚡ " + LanguageManager.Get("ToggleSidebar");
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = new Font("微软雅黑", 10, FontStyle.Bold);
            titleLabel.AutoSize = false;
            titleLabel.Size = new Size(200, 32);
            titleLabel.Location = new Point(10, 0);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Cursor = Cursors.SizeAll;
            
            // 标题栏拖拽事件
            Point btnOffset = new Point(0, 0);  // 图标相对于面板的偏移
            EventHandler<MouseEventArgs> titleMouseDown = (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDraggingPanel = true;
                    dragOffset = e.Location;
                    if (s == titleLabel)
                    {
                        dragOffset.X += titleLabel.Location.X;
                        dragOffset.Y += titleLabel.Location.Y;
                    }
                    // 记录图标相对于面板的位置
                    btnOffset = new Point(
                        toggleSidebarBtn.Location.X - floatingSidebarPanel.Location.X,
                        toggleSidebarBtn.Location.Y - floatingSidebarPanel.Location.Y
                    );
                }
            };
            EventHandler<MouseEventArgs> titleMouseMove = (s, e) =>
            {
                if (isDraggingPanel)
                {
                    var mousePos = e.Location;
                    if (s == titleLabel)
                    {
                        mousePos.X += titleLabel.Location.X;
                        mousePos.Y += titleLabel.Location.Y;
                    }
                    var newLocation = floatingSidebarPanel.Location;
                    newLocation.X += mousePos.X - dragOffset.X;
                    newLocation.Y += mousePos.Y - dragOffset.Y;
                    // 限制在窗口范围内
                    newLocation.X = Math.Max(0, Math.Min(this.ClientSize.Width - 100, newLocation.X));
                    newLocation.Y = Math.Max(25, Math.Min(this.ClientSize.Height - 100, newLocation.Y));
                    
                    // 使用 SuspendLayout 减少闪烁
                    this.SuspendLayout();
                    floatingSidebarPanel.Location = newLocation;
                    
                    // 同时移动图标
                    var btnLocation = new Point(
                        newLocation.X + btnOffset.X,
                        newLocation.Y + btnOffset.Y
                    );
                    btnLocation.X = Math.Max(0, Math.Min(this.ClientSize.Width - toggleSidebarBtn.Width, btnLocation.X));
                    btnLocation.Y = Math.Max(25, Math.Min(this.ClientSize.Height - toggleSidebarBtn.Height, btnLocation.Y));
                    toggleSidebarBtn.Location = btnLocation;
                    this.ResumeLayout();
                }
            };
            EventHandler<MouseEventArgs> titleMouseUp = (s, e) =>
            {
                isDraggingPanel = false;
            };
            
            sidebarTitleBar.MouseDown += (s, e) => titleMouseDown(s, e);
            sidebarTitleBar.MouseMove += (s, e) => titleMouseMove(s, e);
            sidebarTitleBar.MouseUp += (s, e) => titleMouseUp(s, e);
            titleLabel.MouseDown += (s, e) => titleMouseDown(s, e);
            titleLabel.MouseMove += (s, e) => titleMouseMove(s, e);
            titleLabel.MouseUp += (s, e) => titleMouseUp(s, e);
            
            // 关闭按钮（标题栏右侧）
            var closeBtn = new Button();
            closeBtn.Size = new Size(28, 28);
            closeBtn.Location = new Point(SIDEBAR_WIDTH - 32, 2);
            closeBtn.Text = "✕";
            closeBtn.TextAlign = ContentAlignment.MiddleCenter;
            closeBtn.FlatStyle = FlatStyle.Flat;
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 77, 79);
            closeBtn.BackColor = Color.Transparent;
            closeBtn.ForeColor = Color.White;
            closeBtn.Font = new Font("Segoe UI Symbol", 10);
            closeBtn.Cursor = Cursors.Hand;
            closeBtn.Click += (s, e) => { floatingSidebarPanel.Visible = false; };
            
            sidebarTitleBar.Controls.Add(titleLabel);
            sidebarTitleBar.Controls.Add(closeBtn);
            closeBtn.BringToFront();
            
            // 创建调整大小的手柄（右下角）
            resizeGrip = new Panel();
            resizeGrip.Size = new Size(16, 16);
            resizeGrip.BackColor = Color.Transparent;
            resizeGrip.Cursor = Cursors.SizeNWSE;
            resizeGrip.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            
            // 绘制调整大小的图案
            resizeGrip.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using (var brush = new SolidBrush(Color.FromArgb(150, 120, 120, 120)))
                {
                    // 绘制斜线图案
                    g.FillPolygon(brush, new Point[] {
                        new Point(16, 4),
                        new Point(16, 16),
                        new Point(4, 16)
                    });
                }
            };
            
            // 调整大小事件
            resizeGrip.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isResizingPanel = true;
                    resizeStartSize = floatingSidebarPanel.Size;
                    resizeStartPos = floatingSidebarPanel.PointToScreen(e.Location);
                    resizeStartPos.X += resizeGrip.Location.X;
                    resizeStartPos.Y += resizeGrip.Location.Y;
                }
            };
            resizeGrip.MouseMove += (s, e) =>
            {
                if (isResizingPanel)
                {
                    var currentPos = floatingSidebarPanel.PointToScreen(e.Location);
                    currentPos.X += resizeGrip.Location.X;
                    currentPos.Y += resizeGrip.Location.Y;
                    
                    int newWidth = resizeStartSize.Width + (currentPos.X - resizeStartPos.X);
                    int newHeight = resizeStartSize.Height + (currentPos.Y - resizeStartPos.Y);
                    
                    // 限制最小和最大尺寸
                    newWidth = Math.Max(280, Math.Min(600, newWidth));
                    newHeight = Math.Max(300, Math.Min(800, newHeight));
                    
                    floatingSidebarPanel.Size = new Size(newWidth, newHeight);
                    
                    // 更新关闭按钮位置
                    closeBtn.Location = new Point(floatingSidebarPanel.Width - 32, 2);
                }
            };
            resizeGrip.MouseUp += (s, e) =>
            {
                isResizingPanel = false;
            };
            
            // 内容面板（放置网络和本地搜索区域）- 使用双缓冲面板
            var contentPanel = new DoubleBufferedPanel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = Color.FromArgb(250, 251, 253);
            contentPanel.AutoScroll = true;
            contentPanel.Padding = new Padding(5);
            
            // 网络查询区域
            CreateNetworkSection();
            
            // 本地搜索区域
            CreateLocalSection();
            
            // 添加控件到内容面板
            contentPanel.Controls.Add(localSectionPanel);
            contentPanel.Controls.Add(networkSectionPanel);
            
            // 添加所有控件到侧边栏
            floatingSidebarPanel.Controls.Add(resizeGrip);
            floatingSidebarPanel.Controls.Add(contentPanel);
            floatingSidebarPanel.Controls.Add(sidebarTitleBar);
            
            // 更新调整手柄位置
            floatingSidebarPanel.Resize += (s, e) =>
            {
                resizeGrip.Location = new Point(floatingSidebarPanel.Width - 16, floatingSidebarPanel.Height - 16);
            };
            resizeGrip.Location = new Point(floatingSidebarPanel.Width - 16, floatingSidebarPanel.Height - 16);
            resizeGrip.BringToFront();
        }

        private void CreateNetworkSection()
        {
            networkSectionPanel = new Panel();
            networkSectionPanel.Dock = DockStyle.Top;
            networkSectionPanel.Height = 260;
            networkSectionPanel.BackColor = Color.FromArgb(250, 251, 252);
            
            // 区域标题
            networkSectionHeader = new Button();
            networkSectionHeader.Dock = DockStyle.Top;
            networkSectionHeader.Height = 36;
            networkSectionHeader.Text = "▼ " + LanguageManager.Get("SectionNetwork");
            networkSectionHeader.TextAlign = ContentAlignment.MiddleLeft;
            networkSectionHeader.Padding = new Padding(8, 0, 0, 0);
            networkSectionHeader.FlatStyle = FlatStyle.Flat;
            networkSectionHeader.FlatAppearance.BorderSize = 0;
            networkSectionHeader.BackColor = Color.FromArgb(24, 144, 255);
            networkSectionHeader.ForeColor = Color.White;
            networkSectionHeader.Font = new Font("微软雅黑", 10, FontStyle.Bold);
            networkSectionHeader.Cursor = Cursors.Hand;
            networkSectionHeader.Click += (s, e) => ToggleNetworkSection();
            
            // 区域内容
            networkSectionContent = new Panel();
            networkSectionContent.Dock = DockStyle.Fill;
            networkSectionContent.Padding = new Padding(8);
            networkSectionContent.BackColor = Color.FromArgb(250, 251, 252);
            
            // 工具栏
            CreateNetworkToolStrip();
            
            // 搜索输入框
            searchTextBox = new TextBox();
            searchTextBox.Dock = DockStyle.Fill;
            searchTextBox.Font = new Font("微软雅黑", 11);
            searchTextBox.PlaceholderText = LanguageManager.Get("InputPlaceholder");
            searchTextBox.Multiline = true;
            searchTextBox.ScrollBars = ScrollBars.Vertical;
            searchTextBox.AcceptsReturn = false;
            searchTextBox.BorderStyle = BorderStyle.FixedSingle;
            searchTextBox.KeyDown += (sender, e) => {
                if ((e.Control && e.KeyCode == Keys.Enter) || (e.KeyCode == Keys.Enter && !e.Shift))
                {
                    SearchButton_Click(sender, e);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            
            networkSectionContent.Controls.Add(searchTextBox);
            networkSectionContent.Controls.Add(networkToolStrip);
            
            networkSectionPanel.Controls.Add(networkSectionContent);
            networkSectionPanel.Controls.Add(networkSectionHeader);
        }

        private void CreateNetworkToolStrip()
        {
            networkToolStrip = new ToolStrip();
            networkToolStrip.Dock = DockStyle.Top;
            networkToolStrip.ImageScalingSize = new Size(16, 16);
            networkToolStrip.Font = new Font("微软雅黑", 9);
            networkToolStrip.BackColor = Color.FromArgb(250, 251, 252);
            networkToolStrip.GripStyle = ToolStripGripStyle.Hidden;
            networkToolStrip.Padding = new Padding(0, 2, 0, 2);
            networkToolStrip.RenderMode = ToolStripRenderMode.System;
            networkToolStrip.LayoutStyle = ToolStripLayoutStyle.Flow;
            
            UpdateNetworkToolStrip();
        }

        private void UpdateNetworkToolStrip()
        {
            if (networkToolStrip == null) return;
            
            networkToolStrip.Items.Clear();
            expandToolButtons.Clear();
            
            // 查询按钮
            var searchBtn = CreateCompactToolButton(LanguageManager.Get("ToolSearch"), "▶", Color.FromArgb(82, 196, 26));
            searchBtn.Click += SearchButton_Click;
            
            // 清除按钮
            var clearBtn = CreateCompactToolButton(LanguageManager.Get("ToolClear"), "✕", Color.FromArgb(255, 77, 79));
            clearBtn.Click += ClearButton_Click;
            
            // 配置按钮
            var configBtn = CreateCompactToolButton(LanguageManager.Get("ToolConfig"), "⚙", Color.FromArgb(24, 144, 255));
            configBtn.Click += ConfigButton_Click;
            
            // 历史按钮
            var historyBtn = CreateCompactToolButton(LanguageManager.Get("ToolHistory"), "☰", Color.FromArgb(114, 46, 209));
            historyBtn.Click += HistoryButton_Click;
            
            // 恢复分栏按钮
            var restoreBtn = CreateCompactToolButton(LanguageManager.Get("ToolSplit"), "▦", Color.FromArgb(250, 173, 20));
            restoreBtn.Click += (s, e) => { currentExpandedIndex = -1; UpdateWebViewLayout(); UpdateExpandToolButtons(); };
            
            networkToolStrip.Items.Add(searchBtn);
            networkToolStrip.Items.Add(clearBtn);
            networkToolStrip.Items.Add(new ToolStripSeparator());
            networkToolStrip.Items.Add(configBtn);
            networkToolStrip.Items.Add(historyBtn);
            networkToolStrip.Items.Add(restoreBtn);
        }

        private ToolStripButton CreateCompactToolButton(string text, string icon, Color iconColor)
        {
            var btn = new ToolStripButton();
            btn.Text = text;
            btn.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            btn.ToolTipText = text;
            btn.Margin = new Padding(1, 0, 1, 0);
            btn.Padding = new Padding(2, 1, 2, 1);
            btn.AutoSize = true;
            
            using (var bmp = new Bitmap(16, 16))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(iconColor))
                {
                    g.DrawString(icon, new Font("Segoe UI Symbol", 9, FontStyle.Bold), brush, 0, 0);
                }
                btn.Image = (Image)bmp.Clone();
            }
            
            return btn;
        }

        private void CreateLocalSection()
        {
            localSectionPanel = new Panel();
            localSectionPanel.Dock = DockStyle.Top;
            localSectionPanel.Height = 36; // 折叠状态只显示标题
            localSectionPanel.BackColor = Color.FromArgb(250, 251, 252);
            
            // 区域标题
            localSectionHeader = new Button();
            localSectionHeader.Dock = DockStyle.Top;
            localSectionHeader.Height = 36;
            localSectionHeader.Text = "▶ " + LanguageManager.Get("SectionLocal");
            localSectionHeader.TextAlign = ContentAlignment.MiddleLeft;
            localSectionHeader.Padding = new Padding(8, 0, 0, 0);
            localSectionHeader.FlatStyle = FlatStyle.Flat;
            localSectionHeader.FlatAppearance.BorderSize = 0;
            localSectionHeader.BackColor = Color.FromArgb(19, 194, 194);
            localSectionHeader.ForeColor = Color.White;
            localSectionHeader.Font = new Font("微软雅黑", 10, FontStyle.Bold);
            localSectionHeader.Cursor = Cursors.Hand;
            localSectionHeader.Click += (s, e) => ToggleLocalSection();
            
            // 区域内容
            localSectionContent = new Panel();
            localSectionContent.Dock = DockStyle.Fill;
            localSectionContent.Padding = new Padding(8);
            localSectionContent.BackColor = Color.FromArgb(250, 251, 252);
            localSectionContent.Visible = false;
            
            // 本地搜索输入框
            localSearchTextBox = new TextBox();
            localSearchTextBox.Dock = DockStyle.Top;
            localSearchTextBox.Height = 28;
            localSearchTextBox.Font = new Font("微软雅黑", 10);
            localSearchTextBox.PlaceholderText = LanguageManager.Get("LocalSearchPlaceholder");
            localSearchTextBox.BorderStyle = BorderStyle.FixedSingle;
            localSearchTextBox.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter)
                {
                    PerformLocalSearchByName(localSearchTextBox.Text.Trim());
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            
            // 文件类型选择
            var typePanel = new Panel();
            typePanel.Dock = DockStyle.Top;
            typePanel.Height = 32;
            typePanel.Padding = new Padding(0, 4, 0, 0);
            
            var typeLabel = new Label();
            typeLabel.Text = LanguageManager.Get("LocalFileTypes") + ":";
            typeLabel.AutoSize = true;
            typeLabel.Location = new Point(0, 8);
            typeLabel.Font = new Font("微软雅黑", 9);
            
            fileTypeComboBox = new ComboBox();
            fileTypeComboBox.Location = new Point(70, 4);
            fileTypeComboBox.Size = new Size(180, 24);
            fileTypeComboBox.Font = new Font("微软雅黑", 9);
            fileTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            fileTypeComboBox.Items.AddRange(new string[] {
                LanguageManager.Get("TypeAll"),
                LanguageManager.Get("TypeDocuments"),
                LanguageManager.Get("TypeImages"),
                LanguageManager.Get("TypeVideos"),
                LanguageManager.Get("TypeAudio"),
                LanguageManager.Get("TypeArchives"),
                LanguageManager.Get("TypeCode")
            });
            fileTypeComboBox.SelectedIndex = 0;
            
            typePanel.Controls.Add(fileTypeComboBox);
            typePanel.Controls.Add(typeLabel);
            
            // 搜索按钮面板
            var buttonsPanel = new FlowLayoutPanel();
            buttonsPanel.Dock = DockStyle.Top;
            buttonsPanel.Height = 120;
            buttonsPanel.FlowDirection = FlowDirection.TopDown;
            buttonsPanel.Padding = new Padding(0, 8, 0, 0);
            buttonsPanel.WrapContents = false;
            
            searchByNameBtn = CreateLocalActionButton(LanguageManager.Get("LocalSearchByName"), "🔍");
            searchByNameBtn.Click += (s, e) => PerformLocalSearchByName(localSearchTextBox.Text.Trim());
            
            searchRecentBtn = CreateLocalActionButton(LanguageManager.Get("LocalSearchRecent"), "🕐");
            searchRecentBtn.Click += (s, e) => PerformLocalSearchRecent();
            
            searchByTypeBtn = CreateLocalActionButton(LanguageManager.Get("LocalSearchByType"), "📂");
            searchByTypeBtn.Click += (s, e) => PerformLocalSearchByType();
            
            searchAIBtn = CreateLocalActionButton(LanguageManager.Get("LocalSearchAI"), "🤖");
            searchAIBtn.Enabled = false; // 即将推出
            searchAIBtn.ForeColor = Color.Gray;
            
            buttonsPanel.Controls.Add(searchByNameBtn);
            buttonsPanel.Controls.Add(searchRecentBtn);
            buttonsPanel.Controls.Add(searchByTypeBtn);
            buttonsPanel.Controls.Add(searchAIBtn);
            
            // 添加控件（注意顺序）
            localSectionContent.Controls.Add(buttonsPanel);
            localSectionContent.Controls.Add(typePanel);
            localSectionContent.Controls.Add(localSearchTextBox);
            
            localSectionPanel.Controls.Add(localSectionContent);
            localSectionPanel.Controls.Add(localSectionHeader);
        }

        private Button CreateLocalActionButton(string text, string icon)
        {
            var btn = new Button();
            btn.Size = new Size(240, 26);
            btn.Text = icon + " " + text;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(8, 0, 0, 0);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 220);
            btn.BackColor = Color.White;
            btn.ForeColor = Color.FromArgb(51, 51, 51);
            btn.Font = new Font("微软雅黑", 9);
            btn.Cursor = Cursors.Hand;
            btn.Margin = new Padding(0, 0, 0, 4);
            return btn;
        }

        private void ToggleNetworkSection()
        {
            isNetworkSectionExpanded = !isNetworkSectionExpanded;
            int heightDiff = 0;
            if (isNetworkSectionExpanded)
            {
                heightDiff = 260 - networkSectionPanel.Height;
                networkSectionPanel.Height = 260;
                networkSectionContent.Visible = true;
                networkSectionHeader.Text = "▼ " + LanguageManager.Get("SectionNetwork");
            }
            else
            {
                heightDiff = 36 - networkSectionPanel.Height;
                networkSectionPanel.Height = 36;
                networkSectionContent.Visible = false;
                networkSectionHeader.Text = "▶ " + LanguageManager.Get("SectionNetwork");
            }
            // 调整外部面板高度
            AdjustSidebarPanelHeight(heightDiff);
        }

        private void ToggleLocalSection()
        {
            isLocalSectionExpanded = !isLocalSectionExpanded;
            int heightDiff = 0;
            if (isLocalSectionExpanded)
            {
                heightDiff = 240 - localSectionPanel.Height;
                localSectionPanel.Height = 240;
                localSectionContent.Visible = true;
                localSectionHeader.Text = "▼ " + LanguageManager.Get("SectionLocal");
            }
            else
            {
                heightDiff = 36 - localSectionPanel.Height;
                localSectionPanel.Height = 36;
                localSectionContent.Visible = false;
                localSectionHeader.Text = "▶ " + LanguageManager.Get("SectionLocal");
            }
            // 调整外部面板高度
            AdjustSidebarPanelHeight(heightDiff);
        }
        
        private void AdjustSidebarPanelHeight(int heightDiff)
        {
            if (floatingSidebarPanel == null) return;
            
            // 计算新高度
            int newHeight = floatingSidebarPanel.Height + heightDiff;
            // 限制最小和最大高度
            newHeight = Math.Max(150, Math.Min(800, newHeight));
            
            // 确保不超出窗口底部
            if (floatingSidebarPanel.Location.Y + newHeight > this.ClientSize.Height - 10)
            {
                newHeight = this.ClientSize.Height - floatingSidebarPanel.Location.Y - 10;
            }
            
            floatingSidebarPanel.Height = newHeight;
        }

        private void ToggleSidebar_Click(object sender, EventArgs e)
        {
            // 切换浮动侧边栏的显示状态
            floatingSidebarPanel.Visible = !floatingSidebarPanel.Visible;
            if (floatingSidebarPanel.Visible)
            {
                // 将面板放置在图标旁边
                var panelX = toggleSidebarBtn.Location.X + toggleSidebarBtn.Width + 5;
                var panelY = toggleSidebarBtn.Location.Y;
                
                // 如果面板超出右边界，放到图标左边
                if (panelX + floatingSidebarPanel.Width > this.ClientSize.Width)
                {
                    panelX = toggleSidebarBtn.Location.X - floatingSidebarPanel.Width - 5;
                }
                
                // 确保不超出底部
                if (panelY + floatingSidebarPanel.Height > this.ClientSize.Height)
                {
                    panelY = this.ClientSize.Height - floatingSidebarPanel.Height - 10;
                }
                
                // 确保不超出顶部
                panelY = Math.Max(25, panelY);
                panelX = Math.Max(0, panelX);
                
                floatingSidebarPanel.Location = new Point(panelX, panelY);
                floatingSidebarPanel.BringToFront();
            }
        }


        private void CreateLocalResultsPanel()
        {
            localContentPanel = new Panel();
            localContentPanel.Dock = DockStyle.Fill;
            localContentPanel.BackColor = Color.FromArgb(245, 247, 250);
            localContentPanel.Visible = false;
            
            localStatusLabel = new Label();
            localStatusLabel.Dock = DockStyle.Top;
            localStatusLabel.Height = 30;
            localStatusLabel.Font = new Font("微软雅黑", 10);
            localStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            localStatusLabel.Padding = new Padding(10, 0, 0, 0);
            localStatusLabel.BackColor = Color.FromArgb(250, 251, 252);
            localStatusLabel.Text = LanguageManager.Get("MsgNoResults");
            
            localResultsGrid = new DataGridView();
            localResultsGrid.Dock = DockStyle.Fill;
            localResultsGrid.Font = new Font("微软雅黑", 10);
            localResultsGrid.BackgroundColor = Color.White;
            localResultsGrid.BorderStyle = BorderStyle.None;
            localResultsGrid.RowHeadersVisible = false;
            localResultsGrid.AllowUserToAddRows = false;
            localResultsGrid.AllowUserToDeleteRows = false;
            localResultsGrid.ReadOnly = true;
            localResultsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            localResultsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            localResultsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 10, FontStyle.Bold);
            localResultsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 242, 245);
            localResultsGrid.ColumnHeadersHeight = 35;
            localResultsGrid.RowTemplate.Height = 28;
            localResultsGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 252);
            
            localResultsGrid.Columns.Add("FileName", LanguageManager.Get("ColFileName"));
            localResultsGrid.Columns.Add("FileType", LanguageManager.Get("ColFileType"));
            localResultsGrid.Columns.Add("FilePath", LanguageManager.Get("ColFilePath"));
            localResultsGrid.Columns["FileName"].FillWeight = 30;
            localResultsGrid.Columns["FileType"].FillWeight = 10;
            localResultsGrid.Columns["FilePath"].FillWeight = 60;
            
            localResultsGrid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    var path = localResultsGrid.Rows[e.RowIndex].Cells["FilePath"].Value?.ToString();
                    if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path)))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                        }
                        catch { }
                    }
                }
            };
            
            localContentPanel.Controls.Add(localResultsGrid);
            localContentPanel.Controls.Add(localStatusLabel);
        }

        private void UpdateExpandToolButtons()
        {
            foreach (var btn in expandToolButtons)
            {
                networkToolStrip.Items.Remove(btn);
            }
            expandToolButtons.Clear();

            var selectedSites = config.Sites.FindAll(s => s.IsSelected);
            for (int i = 0; i < selectedSites.Count; i++)
            {
                var site = selectedSites[i];
                bool isExpanded = (i == currentExpandedIndex);
                string prefix = isExpanded ? "◀" : "▷";
                string tooltip = isExpanded ? LanguageManager.Get("ToolCollapseTip") : LanguageManager.Get("ToolExpandTip");
                Color iconColor = isExpanded ? Color.FromArgb(255, 77, 79) : Color.FromArgb(82, 196, 26);
                
                var expandBtn = CreateCompactToolButton(site.Name, prefix, iconColor);
                expandBtn.ToolTipText = tooltip + " " + site.Name;
                
                int index = i;
                expandBtn.Click += (sender, e) => ToggleExpand(index);
                
                networkToolStrip.Items.Add(expandBtn);
                expandToolButtons.Add(expandBtn);
            }
            
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
                    item.DropDownItems.Add(new ToolStripMenuItem(LanguageManager.Get("MenuRestoreView"), null, (s, e) => { currentExpandedIndex = -1; UpdateWebViewLayout(); UpdateExpandToolButtons(); }));
                    break;
                }
            }
        }

        private async void UpdateWebViews()
        {
            foreach (var wv in activeWebViews)
            {
                bottomPanel.Controls.Remove(wv);
                wv.Dispose();
            }
            activeWebViews.Clear();

            var selectedSites = config.Sites.FindAll(s => s.IsSelected);
            if (selectedSites.Count == 0) return;

            for (int i = 0; i < selectedSites.Count; i++)
            {
                var site = selectedSites[i];
                var wv = new Microsoft.Web.WebView2.WinForms.WebView2();
                wv.Tag = site.Name;
                activeWebViews.Add(wv);
            }
            
            for (int i = selectedSites.Count - 1; i >= 0; i--)
            {
                bottomPanel.Controls.Add(activeWebViews[i]);
            }
            
            for (int i = 0; i < selectedSites.Count; i++)
            {
                var site = selectedSites[i];
                var wv = activeWebViews[i];
                try {
                    await wv.EnsureCoreWebView2Async(null);
                    wv.CoreWebView2.Navigate(site.Url);
                } catch (Exception ex) {
                    MessageBox.Show($"初始化 {site.Name} 时出错: {ex.Message}");
                }
            }

            UpdateWebViewLayout();
            UpdateExpandToolButtons();
        }

        private void UpdateWebViewLayout()
        {
            int count = activeWebViews.Count;
            if (count == 0) return;

            if (currentExpandedIndex >= 0 && currentExpandedIndex < count)
            {
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

        private async void SearchButton_Click(object sender, EventArgs e)
        {
            string question = searchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(question))
            {
                MessageBox.Show(LanguageManager.Get("MsgEnterQuestion"), LanguageManager.Get("MsgTip"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // 切换到网络模式
            isLocalMode = false;
            bottomPanel.Visible = true;
            localContentPanel.Visible = false;
            
            try
            {
                SaveHistory(question);
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
        
        private void LocalSearchButton_Click(object sender, EventArgs e)
        {
            // 展开本地搜索区域
            if (!isLocalSectionExpanded)
            {
                ToggleLocalSection();
            }
            
            // 切换到本地模式
            isLocalMode = true;
            bottomPanel.Visible = false;
            localContentPanel.Visible = true;
            
            // 聚焦到本地搜索输入框
            localSearchTextBox.Focus();
        }
        
        private async void PerformLocalSearchByName(string keyword)
        {
            isLocalMode = true;
            bottomPanel.Visible = false;
            localContentPanel.Visible = true;
            
            if (string.IsNullOrEmpty(keyword))
            {
                localStatusLabel.Text = LanguageManager.Get("MsgEnterQuestion");
                localResultsGrid.Rows.Clear();
                return;
            }
            
            await PerformLocalSearch(keyword, null);
        }
        
        private async void PerformLocalSearchRecent()
        {
            isLocalMode = true;
            bottomPanel.Visible = false;
            localContentPanel.Visible = true;
            
            localStatusLabel.Text = LanguageManager.Get("MsgSearching");
            localResultsGrid.Rows.Clear();
            this.Cursor = Cursors.WaitCursor;
            
            try
            {
                if (allFilesCache == null)
                {
                    localStatusLabel.Text = LanguageManager.Get("MsgIndexing");
                    Application.DoEvents();
                    
                    await Task.Run(() =>
                    {
                        allFilesCache = SearchEngine.GetAllFilesAndDirectories();
                    });
                }
                
                if (allFilesCache == null || allFilesCache.Count == 0)
                {
                    localStatusLabel.Text = "未能获取文件索引";
                    this.Cursor = Cursors.Default;
                    return;
                }
                
                // 获取最近的文件（简化实现，实际应该根据修改时间排序）
                var results = allFilesCache
                    .Where(f => !f.IsFolder)
                    .Take(100)
                    .ToList();
                
                DisplaySearchResults(results);
            }
            catch (Exception ex)
            {
                localStatusLabel.Text = $"{LanguageManager.Get("MsgError")}: {ex.Message}";
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }
        
        private async void PerformLocalSearchByType()
        {
            isLocalMode = true;
            bottomPanel.Visible = false;
            localContentPanel.Visible = true;
            
            int typeIndex = fileTypeComboBox.SelectedIndex;
            string[] extensions = typeIndex switch
            {
                1 => new[] { ".doc", ".docx", ".pdf", ".txt", ".xls", ".xlsx", ".ppt", ".pptx" }, // 文档
                2 => new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" }, // 图片
                3 => new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv" }, // 视频
                4 => new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma" }, // 音频
                5 => new[] { ".zip", ".rar", ".7z", ".tar", ".gz" }, // 压缩包
                6 => new[] { ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".c", ".h", ".html", ".css" }, // 代码
                _ => null
            };
            
            await PerformLocalSearch(localSearchTextBox.Text.Trim(), extensions);
        }
        
        private async Task PerformLocalSearch(string keyword, string[] extensions)
        {
            localStatusLabel.Text = LanguageManager.Get("MsgSearching");
            localResultsGrid.Rows.Clear();
            this.Cursor = Cursors.WaitCursor;
            Application.DoEvents();
            
            try
            {
                if (allFilesCache == null)
                {
                    localStatusLabel.Text = LanguageManager.Get("MsgIndexing");
                    Application.DoEvents();
                    
                    try
                    {
                        await Task.Run(() =>
                        {
                            allFilesCache = SearchEngine.GetAllFilesAndDirectories();
                        });
                    }
                    catch (Exception indexEx)
                    {
                        string errorDetails = indexEx.Message;
                        if (indexEx.InnerException != null)
                        {
                            errorDetails += $"\n内部错误: {indexEx.InnerException.Message}";
                        }
                        
                        localStatusLabel.Text = $"{LanguageManager.Get("MsgError")}: {errorDetails}";
                        this.Cursor = Cursors.Default;
                        
                        var result = MessageBox.Show(
                            $"索引文件系统失败，需要以管理员身份运行程序。\n\n是否立即以管理员身份重新启动？\n\n错误详情: {errorDetails}",
                            LanguageManager.Get("MsgError"),
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                        
                        if (result == DialogResult.Yes)
                        {
                            RestartAsAdmin();
                        }
                        return;
                    }
                }
                
                if (allFilesCache == null || allFilesCache.Count == 0)
                {
                    localStatusLabel.Text = "未能获取文件索引，请以管理员身份运行程序";
                    this.Cursor = Cursors.Default;
                    return;
                }
                
                localStatusLabel.Text = LanguageManager.Get("MsgSearching");
                Application.DoEvents();
                
                var query = allFilesCache.AsEnumerable();
                
                if (!string.IsNullOrEmpty(keyword))
                {
                    query = query.Where(f => f.FileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
                }
                
                if (extensions != null && extensions.Length > 0)
                {
                    query = query.Where(f => !f.IsFolder && extensions.Any(ext => 
                        f.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
                }
                
                var results = query.Take(1000).ToList();
                DisplaySearchResults(results);
            }
            catch (Exception ex)
            {
                localStatusLabel.Text = $"{LanguageManager.Get("MsgError")}: {ex.Message}";
                MessageBox.Show($"搜索出错: {ex.Message}", LanguageManager.Get("MsgError"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }
        
        private void DisplaySearchResults(List<FileAndDirectoryEntry> results)
        {
            localResultsGrid.Rows.Clear();
            foreach (var file in results)
            {
                string fileType = file.IsFolder ? "📁" : "📄";
                localResultsGrid.Rows.Add(file.FileName, fileType, file.FullFileName);
            }
            
            if (results.Count > 0)
            {
                localStatusLabel.Text = string.Format(LanguageManager.Get("MsgResultCount"), results.Count);
            }
            else
            {
                localStatusLabel.Text = LanguageManager.Get("MsgNoResults");
            }
        }

        private void ConfigButton_Click(object sender, EventArgs e)
        {
            using (var configForm = new ConfigForm(config))
            {
                if (configForm.ShowDialog() == DialogResult.OK)
                {
                    SaveConfig();
                    UpdateWebViews();
                }
            }
        }

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
                
                var infoLabel = new Label();
                infoLabel.AutoSize = false;
                infoLabel.Size = new Size(360, 150);
                infoLabel.Location = new Point(20, 20);
                infoLabel.Font = new Font("微软雅黑", 12);
                infoLabel.Text = LanguageManager.Get("AboutAuthor") + "\n\n" +
                               LanguageManager.Get("AboutEmail") + "\n\n" +
                               LanguageManager.Get("AboutVersion");
                
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

        private void HistoryButton_Click(object sender, EventArgs e)
        {
            var historyForm = new HistoryForm(historyFilePath);
            historyForm.ShowDialog(this);
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    config = JsonSerializer.Deserialize<WebViewConfig>(json);
                }
                
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

        private void ClearButton_Click(object sender, EventArgs e)
        {
            searchTextBox.Clear();
            searchTextBox.Focus();
        }

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

            UpdateWebViewLayout();
            UpdateExpandToolButtons();
        }

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
            if (bottomPanel != null && activeWebViews != null)
            {
                UpdateWebViewLayout();
            }
        }
        
        private void RestartAsAdmin()
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                
                System.Diagnostics.Process.Start(startInfo);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"无法以管理员身份启动程序: {ex.Message}",
                    LanguageManager.Get("MsgError"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// 创建圆角区域
        /// </summary>
        private System.Drawing.Region CreateRoundedRegion(int width, int height, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, width, height);
            return new System.Drawing.Region(path);
        }
    }
}