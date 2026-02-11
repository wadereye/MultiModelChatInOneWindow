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
        private Label networkSectionHeader;
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
        
        // 图标动画相关
        private System.Windows.Forms.Timer? animationTimer;
        private int animationFrame = 0;
        private bool isAnimating = false;
        private Random random = new Random();
        private int nextAnimationTime = 0;
        private int animationCounter = 0;
        
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
        private string historyDirPath = Path.Combine(Application.StartupPath, "history");
        
        // 常量 - 默认长方形对话框尺寸
        private const int SIDEBAR_WIDTH = 480;
        private const int SIDEBAR_HEIGHT = 320;

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

            // 自定义绘制浮动图标 - 智慧老人头像
            toggleSidebarBtn.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                
                // 计算动画偏移量（跳动效果）
                int offsetY = 0;
                if (isAnimating && animationFrame > 0)
                {
                    // 使用正弦波模拟跳动：向上弹起然后落下
                    double phase = animationFrame / 10.0; // 一帧10个时间单位
                    offsetY = (int)(-3 * Math.Sin(phase * Math.PI)); // 最大向上偏移3像素
                }
                
                // 绘制柔和的渐变背景 - 暖色调智慧感
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    toggleSidebarBtn.ClientRectangle,
                    Color.FromArgb(255, 200, 120),
                    Color.FromArgb(230, 150, 80),
                    135f))
                {
                    g.FillEllipse(brush, 1, 1 + offsetY, 46, 46);
                }
                
                // 绘制金色光环效果
                using (var pen = new Pen(Color.FromArgb(255, 220, 150), 2))
                {
                    g.DrawEllipse(pen, 2, 2 + offsetY, 44, 44);
                }
                
                // 绘制智慧老人头像（带动画偏移）
                var avatarRect = toggleSidebarBtn.ClientRectangle;
                avatarRect.Offset(0, offsetY);
                DrawWiseOldManAvatar(g, avatarRect, offsetY);
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
            
            // 初始化图标动画Timer
            InitializeAnimationTimer();
        }
        
        /// <summary>
        /// 初始化图标动画定时器
        /// </summary>
        private void InitializeAnimationTimer()
        {
            // 设置下一次动画的随机时间（3-8秒之间）
            nextAnimationTime = random.Next(300, 800);
            
            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 50; // 50ms刷新一次
            animationTimer.Tick += (s, e) =>
            {
                animationCounter++;
                
                // 检查是否需要开始新的动画
                if (!isAnimating && animationCounter >= nextAnimationTime)
                {
                    isAnimating = true;
                    animationFrame = 0;
                }
                
                // 执行动画
                if (isAnimating)
                {
                    animationFrame++;
                    toggleSidebarBtn?.Invalidate(); // 重绘按钮
                    
                    // 动画结束（10帧完成一次跳动）
                    if (animationFrame >= 10)
                    {
                        isAnimating = false;
                        animationFrame = 0;
                        animationCounter = 0;
                        // 设置下一次动画的随机时间
                        nextAnimationTime = random.Next(300, 800); // 15-40秒（300*50ms 到 800*50ms）
                    }
                }
            };
            animationTimer.Start();
        }

        private void CreateFloatingSidebar()
        {
            // 使用双缓冲面板减少闪烁 - 现代问答对话框样式
            floatingSidebarPanel = new DoubleBufferedPanel();
            floatingSidebarPanel.Size = new Size(SIDEBAR_WIDTH, SIDEBAR_HEIGHT);
            floatingSidebarPanel.Location = new Point(60, 40);
            floatingSidebarPanel.BackColor = Color.White;
            floatingSidebarPanel.BorderStyle = BorderStyle.None;
            floatingSidebarPanel.Visible = false;
            floatingSidebarPanel.Padding = new Padding(0);
            
            // 创建圆角区域
            var roundPath = new System.Drawing.Drawing2D.GraphicsPath();
            int radius = 16;
            roundPath.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            roundPath.AddArc(floatingSidebarPanel.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            roundPath.AddArc(floatingSidebarPanel.Width - radius * 2, floatingSidebarPanel.Height - radius * 2, radius * 2, radius * 2, 0, 90);
            roundPath.AddArc(0, floatingSidebarPanel.Height - radius * 2, radius * 2, radius * 2, 90, 90);
            roundPath.CloseFigure();
            floatingSidebarPanel.Region = new Region(roundPath);
            
            // 现代化圆角对话框样式绘制
            floatingSidebarPanel.Paint += (s, e) =>
            {
                var rect = floatingSidebarPanel.ClientRectangle;
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                
                // 绘制多层阴影效果 - 外发光
                for (int i = 8; i > 0; i--)
                {
                    using (var shadowPen = new Pen(Color.FromArgb(8 - i, 100, 100, 120), i))
                    {
                        var shadowRect = new Rectangle(i, i, rect.Width - i * 2, rect.Height - i * 2);
                        using (var shadowPath = new System.Drawing.Drawing2D.GraphicsPath())
                        {
                            shadowPath.AddArc(shadowRect.X, shadowRect.Y, radius, radius, 180, 90);
                            shadowPath.AddArc(shadowRect.Right - radius, shadowRect.Y, radius, radius, 270, 90);
                            shadowPath.AddArc(shadowRect.Right - radius, shadowRect.Bottom - radius, radius, radius, 0, 90);
                            shadowPath.AddArc(shadowRect.X, shadowRect.Bottom - radius, radius, radius, 90, 90);
                            shadowPath.CloseFigure();
                            g.DrawPath(shadowPen, shadowPath);
                        }
                    }
                }
                
                // 绘制边框 - 柔和的蓝色渐变边框
                using (var borderBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    rect, Color.FromArgb(100, 180, 255), Color.FromArgb(80, 160, 240), 45f))
                using (var borderPen = new Pen(borderBrush, 1.5f))
                {
                    using (var borderPath = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        borderPath.AddArc(0, 0, radius, radius, 180, 90);
                        borderPath.AddArc(rect.Width - radius, 0, radius, radius, 270, 90);
                        borderPath.AddArc(rect.Width - radius, rect.Height - radius, radius, radius, 0, 90);
                        borderPath.AddArc(0, rect.Height - radius, radius, radius, 90, 90);
                        borderPath.CloseFigure();
                        g.DrawPath(borderPen, borderPath);
                    }
                }
            };
            
            // 更新圆角区域当大小改变时
            floatingSidebarPanel.Resize += (s, e) =>
            {
                var newPath = new System.Drawing.Drawing2D.GraphicsPath();
                newPath.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
                newPath.AddArc(floatingSidebarPanel.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
                newPath.AddArc(floatingSidebarPanel.Width - radius * 2, floatingSidebarPanel.Height - radius * 2, radius * 2, radius * 2, 0, 90);
                newPath.AddArc(0, floatingSidebarPanel.Height - radius * 2, radius * 2, radius * 2, 90, 90);
                newPath.CloseFigure();
                floatingSidebarPanel.Region = new Region(newPath);
            };
            
            // 添加标题栏（用于拖拽）- 现代化渐变样式
            sidebarTitleBar = new DoubleBufferedPanel();
            sidebarTitleBar.Dock = DockStyle.Top;
            sidebarTitleBar.Height = 42;
            sidebarTitleBar.Cursor = Cursors.SizeAll;
            
            // 自定义绘制渐变标题栏
            sidebarTitleBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                
                // 渐变背景 - 现代蓝紫渐变
                using (var gradientBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    sidebarTitleBar.ClientRectangle,
                    Color.FromArgb(86, 119, 252),
                    Color.FromArgb(95, 85, 215),
                    135f))
                {
                    g.FillRectangle(gradientBrush, sidebarTitleBar.ClientRectangle);
                }
                
                // 顶部高光
                using (var highlightPen = new Pen(Color.FromArgb(120, 160, 255), 1))
                {
                    g.DrawLine(highlightPen, 0, 0, sidebarTitleBar.Width, 0);
                }
            };
            
            // 标题文字 - 更现代的风格
            var titleLabel = new Label();
            titleLabel.Text = "✨ " + LanguageManager.Get("ToggleSidebar");
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = new Font("微软雅黑", 11, FontStyle.Bold);
            titleLabel.AutoSize = false;
            titleLabel.Size = new Size(200, 42);
            titleLabel.Location = new Point(12, 0);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Cursor = Cursors.SizeAll;
            titleLabel.BackColor = Color.Transparent;
            
            // 添加标题栏拖拽事件
            Point btnOffset = new Point(0, 0);
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
                    newLocation.X = Math.Max(0, Math.Min(this.ClientSize.Width - 100, newLocation.X));
                    newLocation.Y = Math.Max(25, Math.Min(this.ClientSize.Height - 100, newLocation.Y));
                    
                    floatingSidebarPanel.Location = newLocation;
                    
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
            
            // 关闭按钮 - 现代化圆形按钮
            var closeBtn = new Button();
            closeBtn.Size = new Size(28, 28);
            closeBtn.Location = new Point(SIDEBAR_WIDTH - 36, 7);
            closeBtn.Text = "✕";
            closeBtn.TextAlign = ContentAlignment.MiddleCenter;
            closeBtn.FlatStyle = FlatStyle.Flat;
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.BackColor = Color.FromArgb(60, 255, 255, 255);
            closeBtn.ForeColor = Color.White;
            closeBtn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            closeBtn.Cursor = Cursors.Hand;
            // 创建圆形区域
            var closePath = new System.Drawing.Drawing2D.GraphicsPath();
            closePath.AddEllipse(0, 0, 28, 28);
            closeBtn.Region = new System.Drawing.Region(closePath);
            
            closeBtn.MouseEnter += (s, e) => closeBtn.BackColor = Color.FromArgb(255, 100, 100);
            closeBtn.MouseLeave += (s, e) => closeBtn.BackColor = Color.FromArgb(60, 255, 255, 255);
            closeBtn.Click += (s, e) => { floatingSidebarPanel.Visible = false; };
            
            sidebarTitleBar.Controls.Add(titleLabel);
            sidebarTitleBar.Controls.Add(closeBtn);
            closeBtn.BringToFront();
            
            // 添加调整大小手柄（右下角）- 现代化样式
            resizeGrip = new Panel();
            resizeGrip.Size = new Size(20, 20);
            resizeGrip.BackColor = Color.Transparent;
            resizeGrip.Cursor = Cursors.SizeNWSE;
            resizeGrip.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            
            // 绘制现代化的调整大小图标
            resizeGrip.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                
                using (var brush = new SolidBrush(Color.FromArgb(120, 140, 180)))
                {
                    // 绘制三个小圆点组成调整手柄
                    g.FillEllipse(brush, 12, 4, 4, 4);
                    g.FillEllipse(brush, 8, 8, 4, 4);
                    g.FillEllipse(brush, 12, 8, 4, 4);
                    g.FillEllipse(brush, 4, 12, 4, 4);
                    g.FillEllipse(brush, 8, 12, 4, 4);
                    g.FillEllipse(brush, 12, 12, 4, 4);
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
                    
                    newWidth = Math.Max(320, Math.Min(650, newWidth));
                    newHeight = Math.Max(380, Math.Min(850, newHeight));
                    
                    floatingSidebarPanel.Size = new Size(newWidth, newHeight);
                    closeBtn.Location = new Point(floatingSidebarPanel.Width - 36, 7);
                }
            };
            resizeGrip.MouseUp += (s, e) =>
            {
                isResizingPanel = false;
            };
            
            // 内容面板 - 现代化白色卡片样式
            var contentPanel = new DoubleBufferedPanel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = Color.White;
            contentPanel.AutoScroll = true;
            contentPanel.Padding = new Padding(12);
            
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
                resizeGrip.Location = new Point(floatingSidebarPanel.Width - 20, floatingSidebarPanel.Height - 20);
            };
            resizeGrip.Location = new Point(floatingSidebarPanel.Width - 20, floatingSidebarPanel.Height - 20);
            resizeGrip.BringToFront();
        }

        private void CreateNetworkSection()
        {
            networkSectionPanel = new Panel();
            networkSectionPanel.Dock = DockStyle.Fill;
            networkSectionPanel.BackColor = Color.White;
            networkSectionPanel.Padding = new Padding(0);
            
            // 现代化简洁标题栏 - 移除折叠功能，保持简洁
            networkSectionHeader = new Label();
            networkSectionHeader.Dock = DockStyle.Top;
            networkSectionHeader.Height = 32;
            networkSectionHeader.Text = LanguageManager.Get("SectionNetwork");
            networkSectionHeader.TextAlign = ContentAlignment.MiddleLeft;
            networkSectionHeader.Padding = new Padding(4, 0, 0, 4);
            networkSectionHeader.BackColor = Color.White;
            networkSectionHeader.ForeColor = Color.FromArgb(86, 119, 252);
            networkSectionHeader.Font = new Font("微软雅黑", 11, FontStyle.Bold);
            
            // 区块内容 - 现代化卡片样式
            networkSectionContent = new Panel();
            networkSectionContent.Dock = DockStyle.Fill;
            networkSectionContent.Padding = new Padding(0);
            networkSectionContent.BackColor = Color.White;
            
            // 工具栏
            CreateNetworkToolStrip();
            
            // 搜索输入框 - 现代化圆角输入框
            var inputContainer = new Panel();
            inputContainer.Dock = DockStyle.Fill;
            inputContainer.BackColor = Color.FromArgb(248, 249, 252);
            inputContainer.Padding = new Padding(12);
            
            searchTextBox = new TextBox();
            searchTextBox.Dock = DockStyle.Fill;
            searchTextBox.Font = new Font("微软雅黑", 12);
            searchTextBox.PlaceholderText = LanguageManager.Get("InputPlaceholder");
            searchTextBox.Multiline = true;
            searchTextBox.ScrollBars = ScrollBars.Vertical;
            searchTextBox.AcceptsReturn = false;
            searchTextBox.BorderStyle = BorderStyle.None;
            searchTextBox.BackColor = Color.FromArgb(248, 249, 252);
            searchTextBox.ForeColor = Color.FromArgb(60, 60, 70);
            searchTextBox.KeyDown += (sender, e) => {
                if ((e.Control && e.KeyCode == Keys.Enter) || (e.KeyCode == Keys.Enter && !e.Shift))
                {
                    SearchButton_Click(sender, e);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            
            inputContainer.Controls.Add(searchTextBox);
            networkSectionContent.Controls.Add(inputContainer);
            networkSectionContent.Controls.Add(networkToolStrip);
            
            networkSectionPanel.Controls.Add(networkSectionContent);
            networkSectionPanel.Controls.Add(networkSectionHeader);
        }

        private void CreateNetworkToolStrip()
        {
            networkToolStrip = new ToolStrip();
            networkToolStrip.Dock = DockStyle.Top;
            networkToolStrip.ImageScalingSize = new Size(18, 18);
            networkToolStrip.Font = new Font("微软雅黑", 9);
            networkToolStrip.BackColor = Color.White;
            networkToolStrip.GripStyle = ToolStripGripStyle.Hidden;
            networkToolStrip.Padding = new Padding(8, 6, 8, 6);
            networkToolStrip.RenderMode = ToolStripRenderMode.System;
            networkToolStrip.LayoutStyle = ToolStripLayoutStyle.Flow;
            networkToolStrip.Height = 42;
            
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
            btn.Margin = new Padding(2, 2, 2, 2);
            btn.Padding = new Padding(6, 4, 6, 4);
            btn.AutoSize = true;
            btn.BackColor = Color.FromArgb(248, 249, 252);
            btn.ForeColor = Color.FromArgb(80, 80, 90);
            btn.Font = new Font("微软雅黑", 9);
            
            using (var bmp = new Bitmap(18, 18))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(iconColor))
                {
                    g.DrawString(icon, new Font("Segoe UI Symbol", 10, FontStyle.Bold), brush, 0, 0);
                }
                btn.Image = (Image)bmp.Clone();
            }
            
            return btn;
        }

        /// <summary>
        /// 绘制智慧老人头像
        /// </summary>
        /// <param name="offsetY">垂直偏移量，用于动画效果</param>
        private void DrawWiseOldManAvatar(Graphics g, Rectangle rect, int offsetY = 0)
        {
            int centerX = rect.Width / 2;
            int centerY = rect.Height / 2 + offsetY;
            
            // 脸部 - 暖肤色
            using (var faceBrush = new SolidBrush(Color.FromArgb(255, 220, 177)))
            {
                g.FillEllipse(faceBrush, centerX - 14, centerY - 10, 28, 26);
            }
            
            // 白发/胡须 - 银白色
            using (var hairBrush = new SolidBrush(Color.FromArgb(240, 240, 245)))
            {
                // 头顶白发
                g.FillEllipse(hairBrush, centerX - 16, centerY - 16, 32, 16);
                // 两侧白发
                g.FillEllipse(hairBrush, centerX - 18, centerY - 8, 10, 18);
                g.FillEllipse(hairBrush, centerX + 8, centerY - 8, 10, 18);
            }
            
            // 眉毛 - 白色长眉
            using (var browPen = new Pen(Color.FromArgb(220, 220, 230), 2))
            {
                browPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                browPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                // 左眉
                g.DrawCurve(browPen, new Point[] {
                    new Point(centerX - 10, centerY - 4),
                    new Point(centerX - 6, centerY - 6),
                    new Point(centerX - 2, centerY - 4)
                });
                // 右眉
                g.DrawCurve(browPen, new Point[] {
                    new Point(centerX + 2, centerY - 4),
                    new Point(centerX + 6, centerY - 6),
                    new Point(centerX + 10, centerY - 4)
                });
            }
            
            // 眼睛 - 智慧的黑色眼睛，带高光
            using (var eyeBrush = new SolidBrush(Color.FromArgb(50, 40, 35)))
            {
                g.FillEllipse(eyeBrush, centerX - 8, centerY - 2, 5, 5);
                g.FillEllipse(eyeBrush, centerX + 3, centerY - 2, 5, 5);
            }
            // 眼睛高光
            using (var highlightBrush = new SolidBrush(Color.White))
            {
                g.FillEllipse(highlightBrush, centerX - 7, centerY - 1, 2, 2);
                g.FillEllipse(highlightBrush, centerX + 4, centerY - 1, 2, 2);
            }
            
            // 鼻子
            using (var nosePen = new Pen(Color.FromArgb(200, 160, 130), 1))
            {
                g.DrawCurve(nosePen, new Point[] {
                    new Point(centerX, centerY + 1),
                    new Point(centerX - 1, centerY + 5),
                    new Point(centerX + 2, centerY + 5)
                });
            }
            
            // 嘴巴 - 慈祥的微笑
            using (var mouthPen = new Pen(Color.FromArgb(180, 100, 90), 1.5f))
            {
                mouthPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                mouthPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawArc(mouthPen, centerX - 6, centerY + 4, 12, 8, 20, 140);
            }
            
            // 长胡须 - 飘逸的白胡须
            using (var beardBrush = new SolidBrush(Color.FromArgb(235, 235, 240)))
            {
                // 主胡须
                g.FillEllipse(beardBrush, centerX - 10, centerY + 8, 20, 14);
                // 胡须细节
                using (var beardPen = new Pen(Color.FromArgb(220, 220, 225), 1))
                {
                    g.DrawCurve(beardPen, new Point[] {
                        new Point(centerX - 6, centerY + 12),
                        new Point(centerX - 8, centerY + 16),
                        new Point(centerX - 5, centerY + 18)
                    });
                    g.DrawCurve(beardPen, new Point[] {
                        new Point(centerX + 2, centerY + 14),
                        new Point(centerX + 3, centerY + 18),
                        new Point(centerX + 6, centerY + 17)
                    });
                }
            }
            
            // 眼镜 - 智慧的金边眼镜
            using (var glassesPen = new Pen(Color.FromArgb(180, 160, 100), 1.5f))
            {
                // 左镜片框
                g.DrawEllipse(glassesPen, centerX - 10, centerY - 4, 8, 7);
                // 右镜片框
                g.DrawEllipse(glassesPen, centerX + 2, centerY - 4, 8, 7);
                // 鼻梁架
                g.DrawLine(glassesPen, centerX - 2, centerY - 1, centerX + 2, centerY - 1);
                // 镜腿
                g.DrawLine(glassesPen, centerX - 10, centerY - 1, centerX - 14, centerY - 3);
                g.DrawLine(glassesPen, centerX + 10, centerY - 1, centerX + 14, centerY - 3);
            }
            
            // 腮红 - 健康的红晕
            using (var cheekBrush = new SolidBrush(Color.FromArgb(40, 255, 180, 160)))
            {
                g.FillEllipse(cheekBrush, centerX - 11, centerY + 2, 5, 4);
                g.FillEllipse(cheekBrush, centerX + 6, centerY + 2, 5, 4);
            }
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
                // 确保历史目录存在
                if (!Directory.Exists(historyDirPath))
                {
                    Directory.CreateDirectory(historyDirPath);
                }
                
                // 按日期生成文件名
                string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
                string historyFilePath = Path.Combine(historyDirPath, $"history_{dateStr}.txt");
                
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
            // 确保历史目录存在
            if (!Directory.Exists(historyDirPath))
            {
                Directory.CreateDirectory(historyDirPath);
            }
            
            var historyForm = new HistoryForm(historyDirPath);
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
                await Task.Delay(150);
                
                // 将问题文本转义，防止JS注入问题
                string escapedQuestion = question
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r\n", "\\n")
                    .Replace("\r", "\\n")
                    .Replace("\n", "\\n")
                    .Replace("'", "\\'")
                    .Replace("`", "\\`");
                
                // 第一步：通过JS找到输入框并注入文本
                string injectScript = @"
                    (function() {
                        var question = """ + escapedQuestion + @""";
                        
                        // 查找输入元素（textarea 或 contenteditable div）
                        var selectors = [
                            'textarea[placeholder*=""输入""]',
                            'textarea[aria-label*=""输入""]',
                            'textarea[placeholder*=""Ask""]',
                            'textarea[placeholder*=""Send""]',
                            'textarea[placeholder*=""Message""]',
                            'textarea[placeholder*=""Type""]',
                            'div[contenteditable=""true""][role=""textbox""]',
                            'div[contenteditable=""true""]',
                            'textarea'
                        ];
                        
                        var inputElement = null;
                        for (var s = 0; s < selectors.length; s++) {
                            var elements = document.querySelectorAll(selectors[s]);
                            for (var i = 0; i < elements.length; i++) {
                                var el = elements[i];
                                if (el.offsetHeight > 0 && el.offsetWidth > 0) {
                                    inputElement = el;
                                    break;
                                }
                            }
                            if (inputElement) break;
                        }
                        
                        if (!inputElement) {
                            // 兜底：尝试点击底部区域来触发输入框
                            var x = window.innerWidth / 2;
                            var y = window.innerHeight - 100;
                            var fallbackEl = document.elementFromPoint(x, y);
                            if (fallbackEl) {
                                fallbackEl.click();
                            }
                            return JSON.stringify({success: false, method: 'none', reason: 'no_input_found'});
                        }
                        
                        // 聚焦元素
                        inputElement.focus();
                        inputElement.click();
                        
                        // 根据元素类型注入文本
                        if (inputElement.tagName.toLowerCase() === 'textarea' || inputElement.tagName.toLowerCase() === 'input') {
                            // 对于 textarea/input，使用 value 设置 + 触发事件
                            var nativeInputValueSetter = Object.getOwnPropertyDescriptor(
                                window.HTMLTextAreaElement.prototype, 'value'
                            ) || Object.getOwnPropertyDescriptor(
                                window.HTMLInputElement.prototype, 'value'
                            );
                            
                            if (nativeInputValueSetter && nativeInputValueSetter.set) {
                                nativeInputValueSetter.set.call(inputElement, question);
                            } else {
                                inputElement.value = question;
                            }
                            
                            // 触发 React/Vue 等框架所需的事件
                            inputElement.dispatchEvent(new Event('input', { bubbles: true }));
                            inputElement.dispatchEvent(new Event('change', { bubbles: true }));
                            inputElement.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true }));
                            inputElement.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true }));
                            
                            return JSON.stringify({success: true, method: 'value_setter', value: inputElement.value.substring(0, 50)});
                        } else if (inputElement.getAttribute('contenteditable') === 'true') {
                            // 对于 contenteditable div
                            inputElement.innerHTML = '';
                            inputElement.focus();
                            
                            // 使用 execCommand 插入文本（兼容性最好）
                            document.execCommand('selectAll', false, null);
                            document.execCommand('insertText', false, question);
                            
                            // 触发事件
                            inputElement.dispatchEvent(new Event('input', { bubbles: true }));
                            inputElement.dispatchEvent(new Event('change', { bubbles: true }));
                            
                            return JSON.stringify({success: true, method: 'contenteditable', value: inputElement.textContent.substring(0, 50)});
                        }
                        
                        return JSON.stringify({success: false, method: 'unknown_type', reason: inputElement.tagName});
                    })();
                ";
                
                string result = await wv.ExecuteScriptAsync(injectScript);
                await Task.Delay(200);
                
                // 第二步：验证文本是否已注入成功
                bool injected = false;
                if (result != null && result.Contains("\"success\":true"))
                {
                    injected = true;
                }
                
                // 第三步：如果JS注入失败，回退到剪贴板方式（带重试）
                if (!injected)
                {
                    injected = await FallbackClipboardPaste(wv, question);
                }
                
                // 第四步：二次验证 — 检查输入框中是否确实有内容
                if (injected)
                {
                    string verifyScript = @"
                        (function() {
                            var selectors = ['textarea', 'div[contenteditable=""true""]'];
                            for (var s = 0; s < selectors.length; s++) {
                                var elements = document.querySelectorAll(selectors[s]);
                                for (var i = 0; i < elements.length; i++) {
                                    var el = elements[i];
                                    if (el.offsetHeight > 0 && el.offsetWidth > 0) {
                                        var content = el.value || el.textContent || el.innerText || '';
                                        if (content.length > 0) {
                                            return JSON.stringify({hasContent: true, length: content.length});
                                        }
                                    }
                                }
                            }
                            return JSON.stringify({hasContent: false, length: 0});
                        })();
                    ";
                    
                    string verifyResult = await wv.ExecuteScriptAsync(verifyScript);
                    
                    // 如果验证发现输入框为空，再次尝试剪贴板方式
                    if (verifyResult != null && verifyResult.Contains("\"hasContent\":false"))
                    {
                        await FallbackClipboardPaste(wv, question);
                    }
                }
                
                await Task.Delay(150);
                
                // 第五步：发送回车提交
                string enterScript = @"
                    (function() {
                        var selectors = ['textarea', 'div[contenteditable=""true""]'];
                        for (var s = 0; s < selectors.length; s++) {
                            var elements = document.querySelectorAll(selectors[s]);
                            for (var i = 0; i < elements.length; i++) {
                                var el = elements[i];
                                if (el.offsetHeight > 0 && el.offsetWidth > 0) {
                                    el.focus();
                                    // 模拟按下 Enter 键
                                    var enterEvent = new KeyboardEvent('keydown', {
                                        key: 'Enter',
                                        code: 'Enter',
                                        keyCode: 13,
                                        which: 13,
                                        bubbles: true
                                    });
                                    el.dispatchEvent(enterEvent);
                                    return 'enter_sent';
                                }
                            }
                        }
                        
                        // 兜底：尝试点击发送按钮
                        var sendBtns = document.querySelectorAll('button[aria-label*=""发送""], button[aria-label*=""Send""], button[type=""submit""]');
                        for (var i = 0; i < sendBtns.length; i++) {
                            if (sendBtns[i].offsetHeight > 0) {
                                sendBtns[i].click();
                                return 'button_clicked';
                            }
                        }
                        return 'no_action';
                    })();
                ";
                
                string enterResult = await wv.ExecuteScriptAsync(enterScript);
                
                // 如果JS模拟Enter无效，回退到SendKeys
                if (enterResult != null && enterResult.Contains("no_action"))
                {
                    wv.Focus();
                    await Task.Delay(100);
                    SendKeys.SendWait("{ENTER}");
                }
                
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"模拟输入到 {wv.Tag} 时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 回退方案：使用剪贴板粘贴，带重试机制
        /// </summary>
        private async Task<bool> FallbackClipboardPaste(Microsoft.Web.WebView2.WinForms.WebView2 wv, string question)
        {
            const int maxRetries = 3;
            
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    wv.Focus();
                    await Task.Delay(150);
                    
                    // 先通过JS确保输入框获得焦点
                    string focusScript = @"
                        (function() {
                            var selectors = ['textarea', 'div[contenteditable=""true""]'];
                            for (var s = 0; s < selectors.length; s++) {
                                var elements = document.querySelectorAll(selectors[s]);
                                for (var i = 0; i < elements.length; i++) {
                                    var el = elements[i];
                                    if (el.offsetHeight > 0 && el.offsetWidth > 0) {
                                        el.focus();
                                        el.click();
                                        // 清空现有内容
                                        if (el.tagName.toLowerCase() === 'textarea') {
                                            el.select();
                                        } else {
                                            document.execCommand('selectAll', false, null);
                                        }
                                        return 'focused';
                                    }
                                }
                            }
                            return 'not_found';
                        })();
                    ";
                    
                    await wv.ExecuteScriptAsync(focusScript);
                    await Task.Delay(100);
                    
                    // 设置剪贴板并粘贴
                    Clipboard.SetText(question);
                    await Task.Delay(50);
                    
                    // 使用 SendKeys 粘贴
                    SendKeys.SendWait("^v");
                    await Task.Delay(300);
                    
                    // 验证是否粘贴成功
                    string verifyScript = @"
                        (function() {
                            var selectors = ['textarea', 'div[contenteditable=""true""]'];
                            for (var s = 0; s < selectors.length; s++) {
                                var elements = document.querySelectorAll(selectors[s]);
                                for (var i = 0; i < elements.length; i++) {
                                    var el = elements[i];
                                    if (el.offsetHeight > 0 && el.offsetWidth > 0) {
                                        var content = el.value || el.textContent || el.innerText || '';
                                        return JSON.stringify({length: content.length});
                                    }
                                }
                            }
                            return JSON.stringify({length: 0});
                        })();
                    ";
                    
                    string verifyResult = await wv.ExecuteScriptAsync(verifyScript);
                    
                    // 检查是否有内容
                    if (verifyResult != null && !verifyResult.Contains("\"length\":0"))
                    {
                        return true; // 粘贴成功
                    }
                    
                    // 粘贴失败，等待后重试
                    Console.WriteLine($"粘贴到 {wv.Tag} 失败，第 {retry + 1} 次重试...");
                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"剪贴板粘贴到 {wv.Tag} 出错: {ex.Message}");
                }
            }
            
            return false; // 所有重试都失败
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
