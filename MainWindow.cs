using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

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

    // 支持双缓冲的 Panel，用于减少拖动时闪烁
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
        
        // 重写 WndProc 进一步减少闪烁
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
        private Button toggleSidebarBtn;  // 侧边栏切换按钮
        
        // 侧边栏 - 网络查询区域
        private Panel networkSectionPanel;
        private Button networkSectionHeader;
        private Panel networkSectionContent;
        private ToolStrip networkToolStrip;
        private TextBox searchTextBox;
        private System.Collections.Generic.List<ToolStripButton> expandToolButtons = new System.Collections.Generic.List<ToolStripButton>();
        
        // 右侧主内容区
        private Panel bottomPanel; // WebView容器
        
        // WebView相关
        private System.Collections.Generic.List<Microsoft.Web.WebView2.WinForms.WebView2> activeWebViews = new System.Collections.Generic.List<Microsoft.Web.WebView2.WinForms.WebView2>();
        private HashSet<int> initializedWebViewIndices = new HashSet<int>(); // 追踪已初始化的WebView索引
        private int currentExpandedIndex = -1;
        
        // 状态变量
        private bool isNetworkSectionExpanded = true;
        
        // 拖拽相关变量
        private bool isDraggingBtn = false;
        private bool isDraggingPanel = false;
        private bool isResizingPanel = false;
        private Point dragOffset;
        private Size resizeStartSize;
        private Point resizeStartPos;
        private Panel sidebarTitleBar;  // 侧边栏标题栏（用于拖拽）
        private Panel resizeGrip;  // 调整大小手柄
        
        // 配置信息
        private WebViewConfig config = new WebViewConfig();
        private string configFilePath = Path.Combine(Application.StartupPath, "webview_config.json");
        private string historyFilePath = Path.Combine(Application.StartupPath, "history.txt");
        
        // 常量
        private const int SIDEBAR_WIDTH = 320;
        private const int SIDEBAR_HEIGHT = 400;

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
            
            // 更新区块标题
            if (networkSectionHeader != null)
                networkSectionHeader.Text = (isNetworkSectionExpanded ? "\u25BC " : "\u25B6 ") + LanguageManager.Get("SectionNetwork");
            
            // 更新输入框
            if (searchTextBox != null)
                searchTextBox.PlaceholderText = LanguageManager.Get("InputPlaceholder");
            
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
            
            // 启用双缓冲减少闪烁
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            
            string iconPath = Path.Combine(Application.StartupPath, "img", "favicon.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
            
            // 创建菜单栏
            CreateMenuStrip();
            
            // 创建主要布局（主内容区 + 浮动侧边栏 + 切换按钮）
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
            var chineseItem = new ToolStripMenuItem("中文", null, (s, e) => { LanguageManager.CurrentLanguage = Language.Chinese; });
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
            // 1. 创建主内容面板（全屏，包含底部）
            mainContentPanel = new Panel();
            mainContentPanel.Dock = DockStyle.Fill;
            mainContentPanel.BackColor = Color.FromArgb(245, 247, 250);
            
            // WebView容器
            bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Fill;
            bottomPanel.BackColor = Color.White;
            
            mainContentPanel.Controls.Add(bottomPanel);
            
            // 2. 创建侧边栏切换按钮（可拖动）
            toggleSidebarBtn = new Button();
            toggleSidebarBtn.Size = new Size(48, 48);
            toggleSidebarBtn.Location = new Point(15, 40);
            toggleSidebarBtn.Text = "";
            toggleSidebarBtn.FlatStyle = FlatStyle.Flat;
            toggleSidebarBtn.FlatAppearance.BorderSize = 0;
            toggleSidebarBtn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            toggleSidebarBtn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            toggleSidebarBtn.BackColor = Color.Transparent;
            toggleSidebarBtn.Cursor = Cursors.SizeAll;
            
            // 创建圆形区域
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, 48, 48);
            toggleSidebarBtn.Region = new Region(path);

            // 自定义绘制浮动图标
            toggleSidebarBtn.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                
                // 绘制渐变背景
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    toggleSidebarBtn.ClientRectangle,
                    Color.FromArgb(24, 144, 255),
                    Color.FromArgb(0, 100, 200),
                    45f))
                {
                    g.FillEllipse(brush, 0, 0, 47, 47);
                }
                
                // 绘制图标 (三条横线，类似汉堡菜单)
                using (var pen = new Pen(Color.White, 3))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    
                    int centerX = toggleSidebarBtn.Width / 2;
                    int centerY = toggleSidebarBtn.Height / 2;
                    
                    g.DrawLine(pen, centerX - 10, centerY - 6, centerX + 10, centerY - 6);
                    g.DrawLine(pen, centerX - 10, centerY, centerX + 10, centerY);
                    g.DrawLine(pen, centerX - 10, centerY + 6, centerX + 10, centerY + 6);
                }
            };
            
            // 按钮拖拽事件
            Point panelOffset = new Point(0, 0);  // 面板相对按钮的偏移
            toggleSidebarBtn.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDraggingBtn = true;
                    dragOffset = e.Location;
                    // 记录面板相对按钮的位置
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
                    
                    toggleSidebarBtn.Location = newLocation;
                    
                    // 如果面板可见，同时移动面板
                    if (floatingSidebarPanel != null && floatingSidebarPanel.Visible)
                    {
                        var panelLocation = new Point(
                            newLocation.X + panelOffset.X,
                            newLocation.Y + panelOffset.Y
                        );
                        // 确保面板不会超出窗口范围
                        panelLocation.X = Math.Max(0, Math.Min(this.ClientSize.Width - 100, panelLocation.X));
                        panelLocation.Y = Math.Max(25, Math.Min(this.ClientSize.Height - 100, panelLocation.Y));
                        floatingSidebarPanel.Location = panelLocation;
                    }
                    this.Update();
                }
            };
            toggleSidebarBtn.MouseUp += (s, e) =>
            {
                if (isDraggingBtn && e.Button == MouseButtons.Left)
                {
                    isDraggingBtn = false;
                    // 判断是否是单击松开还是拖动后
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
            
            // 初始化WebViews（延迟加载优化）
            UpdateWebViews();
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
            
            // 添加圆角的边框效果
            floatingSidebarPanel.Paint += (s, e) =>
            {
                var rect = floatingSidebarPanel.ClientRectangle;
                var g = e.Graphics;
                
                // 绘制简单阴影效果
                using (var shadowPen = new Pen(Color.FromArgb(30, 0, 0, 0), 4))
                {
                    g.DrawRectangle(shadowPen, new Rectangle(2, 2, rect.Width - 4, rect.Height - 4));
                }
                
                // 绘制边框
                using (var borderPen = new Pen(Color.FromArgb(24, 144, 255), 2))
                {
                    g.DrawRectangle(borderPen, new Rectangle(0, 0, rect.Width - 1, rect.Height - 1));
                }
            };
            
            // 添加标题栏（用于拖拽）- 使用双缓冲面板
            sidebarTitleBar = new DoubleBufferedPanel();
            sidebarTitleBar.Dock = DockStyle.Top;
            sidebarTitleBar.Height = 32;
            sidebarTitleBar.BackColor = Color.FromArgb(24, 144, 255);
            sidebarTitleBar.Cursor = Cursors.SizeAll;
            
            // 标题文字
            var titleLabel = new Label();
            titleLabel.Text = LanguageManager.Get("ToggleSidebar");
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = new Font("微软雅黑", 10, FontStyle.Bold);
            titleLabel.AutoSize = false;
            titleLabel.Size = new Size(200, 32);
            titleLabel.Location = new Point(10, 0);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Cursor = Cursors.SizeAll;
            
            // 添加标题栏拖拽事件
            Point btnOffset = new Point(0, 0);  // 图标相对面板的偏移
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
                    // 记录图标相对面板的位置
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
                    
                    floatingSidebarPanel.Location = newLocation;
                    
                    // 同时移动图标
                    var btnLocation = new Point(
                        newLocation.X + btnOffset.X,
                        newLocation.Y + btnOffset.Y
                    );
                    btnLocation.X = Math.Max(0, Math.Min(this.ClientSize.Width - toggleSidebarBtn.Width, btnLocation.X));
                    btnLocation.Y = Math.Max(25, Math.Min(this.ClientSize.Height - toggleSidebarBtn.Height, btnLocation.Y));
                    toggleSidebarBtn.Location = btnLocation;
                    this.Update();
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
            
            // 关闭按钮（放在最右边）
            var closeBtn = new Button();
            closeBtn.Size = new Size(28, 28);
            closeBtn.Location = new Point(SIDEBAR_WIDTH - 32, 2);
            closeBtn.Text = "×";
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
            
            // 添加调整大小手柄（右下角）
            resizeGrip = new Panel();
            resizeGrip.Size = new Size(16, 16);
            resizeGrip.BackColor = Color.Transparent;
            resizeGrip.Cursor = Cursors.SizeNWSE;
            resizeGrip.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            
            // 绘制调整大小图标
            resizeGrip.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using (var brush = new SolidBrush(Color.FromArgb(150, 120, 120, 120)))
                {
                    // 绘制斜线图标
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
                    
                    // 设置最小和最大尺寸
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
            
            // 内容面板（包含工具栏和搜索框） 使用双缓冲面板
            var contentPanel = new DoubleBufferedPanel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = Color.FromArgb(250, 251, 253);
            contentPanel.AutoScroll = true;
            contentPanel.Padding = new Padding(5);
            
            // 网络查询区域
            CreateNetworkSection();
            
            // 添加控件到内容面板
            contentPanel.Controls.Add(networkSectionPanel);
            
            // 添加所有控件到浮动面板
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
            
            // 区块标题
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
            
            // 区块内容
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
            var searchBtn = CreateCompactToolButton(LanguageManager.Get("ToolSearch"), "🔍", Color.FromArgb(82, 196, 26));
            searchBtn.Click += SearchButton_Click;
            
            // 复制按钮
            var copyBtn = CreateCompactToolButton(LanguageManager.Get("ToolCopy"), "📋", Color.FromArgb(22, 119, 255));
            copyBtn.Click += CopyButton_Click;

            // 翻译按钮
            var translateBtn = CreateCompactToolButton(LanguageManager.Get("ToolTranslate"), "文", Color.FromArgb(114, 46, 209));
            translateBtn.Click += TranslateButton_Click;
            
            // 清除按钮
            var clearBtn = CreateCompactToolButton(LanguageManager.Get("ToolClear"), "🗑️", Color.FromArgb(255, 77, 79));
            clearBtn.Click += ClearButton_Click;
            
            // 配置按钮
            var configBtn = CreateCompactToolButton(LanguageManager.Get("ToolConfig"), "⚙️", Color.FromArgb(24, 144, 255));
            configBtn.Click += ConfigButton_Click;
            
            // 历史按钮
            var historyBtn = CreateCompactToolButton(LanguageManager.Get("ToolHistory"), "🕒", Color.FromArgb(114, 46, 209));
            historyBtn.Click += HistoryButton_Click;
            
            // 恢复分栏按钮
            var restoreBtn = CreateCompactToolButton(LanguageManager.Get("ToolSplit"), "🪟", Color.FromArgb(250, 173, 20));
            restoreBtn.Click += (s, e) => { currentExpandedIndex = -1; UpdateWebViewLayout(); UpdateExpandToolButtons(); };
            
            networkToolStrip.Items.Add(searchBtn);
            networkToolStrip.Items.Add(copyBtn);
            networkToolStrip.Items.Add(translateBtn);
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
            // 调整侧边栏高度
            AdjustSidebarPanelHeight(heightDiff);
        }
        
        private void AdjustSidebarPanelHeight(int heightDiff)
        {
            if (floatingSidebarPanel == null) return;
            
            // 计算新高度
            int newHeight = floatingSidebarPanel.Height + heightDiff;
            // 设置最小和最大高度
            newHeight = Math.Max(150, Math.Min(800, newHeight));
            
            // 确保面板不会超出底部
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
                // 将面板放在图标旁边
                var panelX = toggleSidebarBtn.Location.X + toggleSidebarBtn.Width + 5;
                var panelY = toggleSidebarBtn.Location.Y;
                
                // 如果面板超出右边界，放到图标左侧
                if (panelX + floatingSidebarPanel.Width > this.ClientSize.Width)
                {
                    panelX = toggleSidebarBtn.Location.X - floatingSidebarPanel.Width - 5;
                }
                
                // 确保面板不会超出底部
                if (panelY + floatingSidebarPanel.Height > this.ClientSize.Height)
                {
                    panelY = this.ClientSize.Height - floatingSidebarPanel.Height - 10;
                }
                
                // 确保面板不会超出顶部
                panelY = Math.Max(25, panelY);
                panelX = Math.Max(0, panelX);
                
                floatingSidebarPanel.Location = new Point(panelX, panelY);
                floatingSidebarPanel.BringToFront();
            }
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
                string prefix = isExpanded ? "\u25C0" : "\u25B7";
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
                        string prefix = (i == currentExpandedIndex) ? "▼ " : "";
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

        /// <summary>
        /// 更新WebViews - 采用延迟加载策略优化内存和CPU使用
        /// 只创建WebView控件，但不立即初始化内核
        /// </summary>
        private void UpdateWebViews()
        {
            foreach (var wv in activeWebViews)
            {
                bottomPanel.Controls.Remove(wv);
                wv.Dispose();
            }
            activeWebViews.Clear();
            initializedWebViewIndices.Clear();

            var selectedSites = config.Sites.FindAll(s => s.IsSelected);
            if (selectedSites.Count == 0) return;

            // 只创建WebView控件，不初始化内核
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

            UpdateWebViewLayout();
            UpdateExpandToolButtons();
            
            // 延迟初始化第一个可见的WebView
            InitializeVisibleWebViewsAsync();
        }
        
        /// <summary>
        /// 异步初始化当前可见的WebView
        /// 只初始化可见的WebView，其他的在需要时再初始化
        /// </summary>
        private async void InitializeVisibleWebViewsAsync()
        {
            var selectedSites = config.Sites.FindAll(s => s.IsSelected);
            if (selectedSites.Count == 0) return;
            
            // 如果有展开的视图，只初始化那个
            if (currentExpandedIndex >= 0 && currentExpandedIndex < selectedSites.Count)
            {
                await InitializeWebViewIfNeeded(currentExpandedIndex, selectedSites[currentExpandedIndex]);
            }
            else
            {
                // 分栏模式：逐个初始化，减少同时加载的压力
                for (int i = 0; i < selectedSites.Count; i++)
                {
                    await InitializeWebViewIfNeeded(i, selectedSites[i]);
                    // 给UI一些时间响应
                    await Task.Delay(100);
                }
            }
        }
        
        /// <summary>
        /// 按需初始化单个WebView
        /// </summary>
        private async Task InitializeWebViewIfNeeded(int index, WebSiteConfig site)
        {
            if (index < 0 || index >= activeWebViews.Count) return;
            if (initializedWebViewIndices.Contains(index)) return; // 已初始化
            
            var wv = activeWebViews[index];
            try
            {
                await wv.EnsureCoreWebView2Async(null);
                wv.CoreWebView2.Navigate(site.Url);
                initializedWebViewIndices.Add(index);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化 {site.Name} 时出错: {ex.Message}");
            }
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

        private void CopyButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(searchTextBox.Text))
            {
                Clipboard.SetText(searchTextBox.Text);
            }
        }

        private void TranslateButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(searchTextBox.Text))
            {
                searchTextBox.Text = "翻译以下内容为中文:\r\n" + searchTextBox.Text;
                SearchButton_Click(sender, e);
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
            
            // 确保所有WebView都已初始化
            var selectedSites = config.Sites.FindAll(s => s.IsSelected);
            for (int i = 0; i < selectedSites.Count; i++)
            {
                await InitializeWebViewIfNeeded(i, selectedSites[i]);
            }
            
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

        private async void ToggleExpand(int index)
        {
            if (currentExpandedIndex == index)
            {
                currentExpandedIndex = -1;
            }
            else
            {
                currentExpandedIndex = index;
                // 确保切换到的WebView已初始化
                var selectedSites = config.Sites.FindAll(s => s.IsSelected);
                if (index >= 0 && index < selectedSites.Count)
                {
                    await InitializeWebViewIfNeeded(index, selectedSites[index]);
                }
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
                        var inputElements = document.querySelectorAll('textarea[placeholder*=""输入""], textarea[aria-label*=""输入""], textarea, div[contenteditable=""true""]');
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
        
        /// <summary>
        /// 创建圆形区域
        /// </summary>
        private System.Drawing.Region CreateRoundedRegion(int width, int height, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, width, height);
            return new System.Drawing.Region(path);
        }
    }
}
