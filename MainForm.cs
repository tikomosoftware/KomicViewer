using KomicViewer.Controls;
using KomicViewer.Services;
using KomicViewer.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace KomicViewer;

public partial class MainForm : Form
{
    // Win32 API declarations for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int attrValue, int attrSize);

    private const uint DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private readonly ArchiveReader _archiveReader = new();
    private readonly ComicViewerPanel _viewerPanel = new();
    private int _currentPage;
    private bool _isRightToLeft = true; // true = right-to-left reading (default)
    private bool _isFullScreen;
    private FormWindowState _previousWindowState;
    private FormBorderStyle _previousBorderStyle;
    private System.Windows.Forms.Timer? _toolbarHideTimer;
    private bool _isToolbarVisible = true;
    private bool _isDarkMode = true; // ダークモード設定
    private bool _isTopMost = false; // 最前面表示設定
    private DateTime _lastTimerStart = DateTime.MinValue; // タイマー再スタート制御用
    private DateTime _lastMouseMoveCheck = DateTime.MinValue; // マウス移動チェック頻度制限用

    private ToolStripButton? _viewToggleBtn;
    private ViewMode? _desiredViewMode;
    
    // ページスライダー関連
    private ModernProgressSlider? _pageSlider;

    public MainForm()
    {
        try
        {
            InitializeComponent();
            
            // Apply dark title bar
            ApplyDarkTitleBar();
            
            // Dark theme
            BackColor = Color.FromArgb(25, 25, 25);
            ForeColor = Color.White;

            // Load user settings
            LoadSettings();

            // Viewer panel setup
            _viewerPanel.Dock = DockStyle.Fill;
            _viewerPanel.ClickLeft += Viewer_ClickLeft;
            _viewerPanel.ClickRight += Viewer_ClickRight;
            _viewerPanel.PageNext += Viewer_PageNext;
            _viewerPanel.PagePrevious += Viewer_PagePrevious;
            _viewerPanel.MouseMove += ViewerPanel_MouseMove;
            _viewerPanel.IsRightToLeft = _isRightToLeft; // 読み方向を設定
            Controls.Add(_viewerPanel);

            // Toolbar - ApplyTheme()より前に作成
            CreateToolbar();

            // Apply initial theme - ツールバー作成後に実行
            ApplyTheme();

            // Apply initial TopMost setting
            TopMost = _isTopMost;
            
            // Apply initial reading direction
            if (_viewerPanel != null)
            {
                _viewerPanel.IsRightToLeft = _isRightToLeft;
            }
            
            // Apply initial view mode if specified
            if (_desiredViewMode.HasValue && _viewerPanel != null)
            {
                _viewerPanel.ViewMode = _desiredViewMode.Value;
                UpdateAdjustmentButtons();
                UpdateViewToggleButton();
            }

            // Keyboard
            KeyPreview = true;
            KeyDown += MainForm_KeyDown;

            // Drag & Drop
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;

            // Mouse move for toolbar auto-hide
            MouseMove += MainForm_MouseMove;

            // Form load event for centering
            Load += MainForm_Load;
            
            // Window size and position change events
            ResizeEnd += (_, _) => SaveSettings();
            LocationChanged += (_, _) => SaveSettings();
            Resize += MainForm_Resize;

            // Window settings
            Text = "Komic Viewer";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;

            // 確実に画面中央に表示するための追加設定
            CenterToScreen();

            // Setup toolbar auto-hide timer
            _toolbarHideTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000 // 1 second (素早く非表示)
            };
            _toolbarHideTimer.Tick += ToolbarHideTimer_Tick;

            UpdateTitle();
        }
        catch (Exception ex)
        {
            ShowMessageBoxSafely($"{LanguageManager.GetString("Error")}:\n{ex.Message}\n\n{ex.StackTrace}", 
                LanguageManager.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }

    private ToolStrip? _toolbar;
    private ToolStripLabel? _pageLabel;
    private ToolStripButton? _prevBtn;
    private ToolStripButton? _nextBtn;
    private ToolStripButton? _adjustPrevBtn; // 見開き用1ページ戻し
    private ToolStripButton? _adjustNextBtn; // 見開き用1ページ送り
    private ToolStripButton? _directionBtn;  // 右開き・左開き切り替えボタン
    private ContextMenuStrip? _fileContextMenu; // ファイルメニュー
    private ToolStripButton? _fullscreenBtn; // フルスクリーンボタン
    private ToolStripButton? _topMostBtn; // 最前面表示ボタン

    private void CreateToolbar()
    {
        _toolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(10, 5, 10, 5),
            AutoSize = true,
            RenderMode = ToolStripRenderMode.Professional,
            Renderer = new ModernToolStripRenderer(_isDarkMode)
        };

        _toolbar.Items.Add(new ToolStripSeparator());

        // Open file button with context menu
        var openBtn = new ToolStripButton(LanguageManager.GetString("MenuFile"))
        {
            ForeColor = Color.White,
            Image = IconService.GetIcon(IconService.IconType.File, 20, Color.White),
            Margin = new Padding(5, 0, 5, 0)
        };
        
        // Create context menu for file operations
        _fileContextMenu = new ContextMenuStrip
        {
            BackColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240),
            ForeColor = _isDarkMode ? Color.White : Color.Black,
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Renderer = new ModernToolStripRenderer(_isDarkMode)
        };

        // Open file item
        var openItem = new ToolStripMenuItem(LanguageManager.GetString("MenuOpen"))
        {
            ForeColor = _isDarkMode ? Color.White : Color.Black,
            BackColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240),
            ShortcutKeyDisplayString = "Ctrl+O"
        };
        openItem.Click += (_, _) => OpenFile();
        _fileContextMenu.Items.Add(openItem);

        // Close file item
        var closeItem = new ToolStripMenuItem(LanguageManager.GetString("MenuClose"))
        {
            ForeColor = _isDarkMode ? Color.White : Color.Black,
            BackColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240),
            ShortcutKeyDisplayString = "Ctrl+W"
        };
        closeItem.Click += (_, _) => CloseFile();
        _fileContextMenu.Items.Add(closeItem);

        _fileContextMenu.Items.Add(new ToolStripSeparator { BackColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240) });

        // Exit item
        var exitItem = new ToolStripMenuItem(LanguageManager.GetString("MenuExit"))
        {
            ForeColor = _isDarkMode ? Color.White : Color.Black,
            BackColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240),
            ShortcutKeyDisplayString = "Alt+F4"
        };
        exitItem.Click += (_, _) => Close();
        _fileContextMenu.Items.Add(exitItem);

        // Show context menu when button is clicked
        openBtn.Click += (sender, e) =>
        {
            if (sender is ToolStripButton button && _toolbar != null)
            {
                var buttonBounds = button.Bounds;
                var menuLocation = _toolbar.PointToScreen(new Point(buttonBounds.Left, buttonBounds.Bottom));
                _fileContextMenu.Show(menuLocation);
            }
        };

        _toolbar.Items.Add(openBtn);

        _toolbar.Items.Add(new ToolStripSeparator());

        // Navigation buttons - 初期配置（後で動的に変更）
        _prevBtn = new ToolStripButton()
        {
            ForeColor = Color.White
        };
        _prevBtn.Click += (_, _) => PreviousPage();

        _pageLabel = new ToolStripLabel("0 / 0")
        {
            ForeColor = Color.LightGray,
            Margin = new Padding(5, 0, 5, 0)
        };

        _nextBtn = new ToolStripButton()
        {
            ForeColor = Color.White
        };
        _nextBtn.Click += (_, _) => NextPage();

        // 読み方向に応じた順序で追加
        AddNavigationButtonsInOrder();

        // 見開き調整ボタン（初期状態では非表示）
        _adjustPrevBtn = new ToolStripButton("◁")
        {
            ForeColor = Color.White,
            ToolTipText = "1ページ戻す（見開き調整用）",
            Visible = false,
            Margin = new Padding(3, 0, 3, 0)
        };
        _adjustPrevBtn.Click += (_, _) => 
        {
            // 読み方向に応じて動作を変更
            if (_isRightToLeft)
                AdjustPageForward();  // 右開きでは◁が次ページ
            else
                AdjustPageBackward(); // 左開きでは◁が前ページ
        };
        _toolbar.Items.Add(_adjustPrevBtn);

        _adjustNextBtn = new ToolStripButton("▷")
        {
            ForeColor = Color.White,
            ToolTipText = "1ページ送る（見開き調整用）",
            Visible = false,
            Margin = new Padding(3, 0, 3, 0)
        };
        _adjustNextBtn.Click += (_, _) => 
        {
            // 読み方向に応じて動作を変更
            if (_isRightToLeft)
                AdjustPageBackward(); // 右開きでは▷が前ページ
            else
                AdjustPageForward();  // 左開きでは▷が次ページ
        };
        _toolbar.Items.Add(_adjustNextBtn);

        // 初期状態のボタンテキストを設定
        UpdateNavigationButtons();

        _toolbar.Items.Add(new ToolStripSeparator());

        // View mode button (single <-> dual) as a single toggle button
        var viewToggleBtn = new ToolStripButton()
        {
            ForeColor = Color.White,
            CheckOnClick = true,
            Checked = _viewerPanel.ViewMode == ViewMode.DualPage,
            ToolTipText = LanguageManager.GetString("TooltipToggleView"),
            Margin = new Padding(5, 0, 5, 0)
        };

        // 初期テキストとアイコンを設定
        viewToggleBtn.Text = _viewerPanel.ViewMode == ViewMode.DualPage ? LanguageManager.GetString("ViewDual") : LanguageManager.GetString("ViewSingle");
        viewToggleBtn.Image = IconService.GetIcon(_viewerPanel.ViewMode == ViewMode.DualPage ? IconService.IconType.ViewDual : IconService.IconType.ViewSingle, 20, Color.White);

        viewToggleBtn.Click += (_, _) =>
        {
            if (_viewerPanel.ViewMode == ViewMode.SinglePage)
            {
                _desiredViewMode = ViewMode.DualPage;
                SetViewMode(ViewMode.DualPage);
            }
            else
            {
                _desiredViewMode = ViewMode.SinglePage;
                SetViewMode(ViewMode.SinglePage);
            }
            viewToggleBtn.Text = _viewerPanel.ViewMode == ViewMode.DualPage ? LanguageManager.GetString("ViewDual") : LanguageManager.GetString("ViewSingle");

            // 設定を保存
            SaveSettings();
        };

        _viewToggleBtn = viewToggleBtn;
        _toolbar.Items.Add(_viewToggleBtn);

        _toolbar.Items.Add(new ToolStripSeparator());

        // Fullscreen button
        _fullscreenBtn = new ToolStripButton(LanguageManager.GetString("Fullscreen"))
        {
            ForeColor = Color.White,
            ToolTipText = LanguageManager.GetString("TooltipFullScreen"),
            Image = IconService.GetIcon(IconService.IconType.Fullscreen, 20, Color.White),
            Margin = new Padding(5, 0, 5, 0)
        };
        _fullscreenBtn.Click += (_, _) => ToggleFullScreen();
        _toolbar.Items.Add(_fullscreenBtn);
        
        _toolbar.Items.Add(new ToolStripSeparator());
        _directionBtn = new ToolStripButton()
        {
            ForeColor = Color.White,
            CheckOnClick = true,
            Checked = _isRightToLeft, // 設定から読み込んだ値を使用
            ToolTipText = LanguageManager.GetString("TooltipDirection"),
            Margin = new Padding(5, 0, 5, 0)
        };
        
        // 初期テキストを設定から読み込んだ値に基づいて設定
        _directionBtn.Text = _isRightToLeft ? LanguageManager.GetString("RightToLeft") : LanguageManager.GetString("LeftToRight");
        
        _directionBtn.Click += (_, _) =>
        {
            _isRightToLeft = _directionBtn.Checked;
            _directionBtn.Text = _isRightToLeft ? LanguageManager.GetString("RightToLeft") : LanguageManager.GetString("LeftToRight");
            
            // ViewerPanelに読み方向を通知
            _viewerPanel.IsRightToLeft = _isRightToLeft;
            
            // ナビゲーションボタンのテキストを更新
            UpdateNavigationButtons();
            
            // 現在表示中の場合は再描画
            if (_archiveReader.IsLoaded)
            {
                UpdateDisplay();
            }
            
            // 設定を保存
            SaveSettings();
            
            // デバッグ用（リリース時は削除可能）
            Console.WriteLine($"読み方向変更: {(_isRightToLeft ? "右開き" : "左開き")}");
        };
        _toolbar.Items.Add(_directionBtn);


        _toolbar.Items.Add(new ToolStripSeparator());

        // ページナビゲーション用スクロールバーをツールバーに追加
        CreatePageScrollBar();

        _toolbar.Items.Add(new ToolStripSeparator());

        // Right-aligned items (Add in reverse order because Alignment.Right stacks from right to left)
        // Final desired visual order: [Slider] ... [Theme] | [TopMost] | [Language] | [Help]

        // 1. Help button (Rightmost)
        var helpBtn = new ToolStripButton(LanguageManager.GetString("Help"))
        {
            Alignment = ToolStripItemAlignment.Right,
            ForeColor = Color.White,
            ToolTipText = LanguageManager.GetString("TooltipHelp"),
            Image = IconService.GetIcon(IconService.IconType.Help, 20, Color.White),
            Margin = new Padding(10, 0, 10, 0)
        };
        helpBtn.Click += (_, _) => ShowHelp();
        _toolbar.Items.Add(helpBtn);

        // 2. Separator
        _toolbar.Items.Add(new ToolStripSeparator { Alignment = ToolStripItemAlignment.Right });

        // 3. Language button
        var langBtn = new ToolStripButton(LanguageManager.GetString("Language"))
        {
            Alignment = ToolStripItemAlignment.Right,
            ForeColor = Color.White,
            ToolTipText = "Switch Language (Japanese / English)",
            Image = IconService.GetIcon(IconService.IconType.Language, 20, Color.White),
            Margin = new Padding(5, 0, 5, 0)
        };
        var langMenu = new ContextMenuStrip
        {
            BackColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240),
            ForeColor = _isDarkMode ? Color.White : Color.Black,
            ShowImageMargin = false,
            Renderer = new ModernToolStripRenderer(_isDarkMode)
        };
        var jaItem = new ToolStripMenuItem(LanguageManager.GetString("Japanese")) { Tag = Language.Japanese };
        var enItem = new ToolStripMenuItem(LanguageManager.GetString("English")) { Tag = Language.English };
        jaItem.Click += (s, e) => ChangeLanguage(Language.Japanese);
        enItem.Click += (s, e) => ChangeLanguage(Language.English);
        langMenu.Items.Add(jaItem);
        langMenu.Items.Add(enItem);
        langBtn.Click += (s, e) => langMenu.Show(Cursor.Position);
        _toolbar.Items.Add(langBtn);

        // 4. Separator
        _toolbar.Items.Add(new ToolStripSeparator { Alignment = ToolStripItemAlignment.Right });

        // 5. TopMost button
        _topMostBtn = new ToolStripButton(LanguageManager.GetString(_isTopMost ? "TopMost" : "NormalWindow"))
        {
            Alignment = ToolStripItemAlignment.Right,
            ForeColor = Color.White,
            CheckOnClick = true,
            Checked = _isTopMost,
            ToolTipText = LanguageManager.GetString("TooltipTopMost"),
            Image = IconService.GetIcon(_isTopMost ? IconService.IconType.TopMostOn : IconService.IconType.TopMostOff, 20, Color.White),
            Margin = new Padding(5, 0, 5, 0)
        };
        _topMostBtn.Click += (_, _) => 
        {
            _isTopMost = _topMostBtn.Checked;
            TopMost = _isTopMost;
            _topMostBtn.Text = LanguageManager.GetString(_isTopMost ? "TopMost" : "NormalWindow");
            _topMostBtn.Image?.Dispose();
            _topMostBtn.Image = IconService.GetIcon(_isTopMost ? IconService.IconType.TopMostOn : IconService.IconType.TopMostOff, 20, _isDarkMode ? Color.White : Color.Black);
            SaveSettings();
        };
        _toolbar.Items.Add(_topMostBtn);

        // 6. Separator
        _toolbar.Items.Add(new ToolStripSeparator { Alignment = ToolStripItemAlignment.Right });

        // 7. Theme toggle button
        var themeBtn = new ToolStripButton(LanguageManager.GetString(_isDarkMode ? "ThemeDark" : "ThemeLight"))
        {
            Alignment = ToolStripItemAlignment.Right,
            ForeColor = Color.White,
            CheckOnClick = true,
            Checked = _isDarkMode,
            ToolTipText = LanguageManager.GetString("TooltipTheme"),
            Image = IconService.GetIcon(_isDarkMode ? IconService.IconType.ThemeDark : IconService.IconType.ThemeLight, 20, Color.White),
            Margin = new Padding(5, 0, 5, 0)
        };
        themeBtn.Click += (_, _) => ToggleTheme(themeBtn);
        _toolbar.Items.Add(themeBtn);

        // ツールバーを上部にドック（通常モード）
        _toolbar.Dock = DockStyle.Top;
        Controls.Add(_toolbar);
        
        // 初期状態でボタンの表示/非表示を設定
        UpdateAdjustmentButtons();
    }

    private void CreatePageScrollBar()
    {
        // カスタムスライダーを作成
        _pageSlider = new ModernProgressSlider
        {
            Minimum = 0,
            Maximum = _archiveReader.IsLoaded ? _archiveReader.PageCount - 1 : 100,
            Value = 0,
            Height = 24,
            Width = 200, // 固定幅
            IsDarkMode = _isDarkMode
        };
        _pageSlider.ValueChanged += (s, e) => {
            if (_archiveReader.IsLoaded)
            {
                int targetPage = _pageSlider.Value;
                targetPage = Math.Clamp(targetPage, 0, _archiveReader.PageCount - 1);
                
                if (targetPage != _currentPage)
                {
                    _currentPage = targetPage;
                    UpdateDisplay();
                }
            }
        };

        // スライダーをToolStripControlHostでラップ
        var scrollBarHost = new ToolStripControlHost(_pageSlider)
        {
            AutoSize = false,
            Size = new Size(200, 25),
            Margin = new Padding(10, 0, 10, 0)
        };

        _toolbar?.Items.Add(scrollBarHost);
    }


    private void UpdateSlider()
    {
        if (_pageSlider != null && _archiveReader.IsLoaded)
        {
            _pageSlider.Maximum = _archiveReader.PageCount - 1;
            _pageSlider.Value = Math.Clamp(_currentPage, 0, _archiveReader.PageCount - 1);
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case System.Windows.Forms.Keys.Right:
            case System.Windows.Forms.Keys.Space:
            case System.Windows.Forms.Keys.PageDown:
            case System.Windows.Forms.Keys.Down: // allow Down arrow for next page
                NextPage();
                e.Handled = true;
                break;
            case System.Windows.Forms.Keys.Left:
            case System.Windows.Forms.Keys.PageUp:
            case System.Windows.Forms.Keys.Up: // allow Up arrow for previous page
                PreviousPage();
                e.Handled = true;
                break;
            case System.Windows.Forms.Keys.Home:
                GoToPage(0);
                e.Handled = true;
                break;
            case System.Windows.Forms.Keys.End:
                GoToPage(_archiveReader.PageCount - 1);
                e.Handled = true;
                break;
            case System.Windows.Forms.Keys.D1:
                _desiredViewMode = ViewMode.SinglePage;
                SetViewMode(ViewMode.SinglePage);
                SaveSettings();
                e.Handled = true;
                break;
            case System.Windows.Forms.Keys.D2:
                _desiredViewMode = ViewMode.DualPage;
                SetViewMode(ViewMode.DualPage);
                SaveSettings();
                e.Handled = true;
                break;
            case System.Windows.Forms.Keys.F11:
                ToggleFullScreen();
                e.Handled = true;
                break;
            case System.Windows.Forms.Keys.Escape:
                if (_isFullScreen) ToggleFullScreen();
                e.Handled = true;
                break;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Handle navigation keys at a low level so they work regardless of focused control
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Right:
            case Keys.Space:
            case Keys.PageDown:
            case Keys.Down: // Down arrow for next page
                NextPage();
                return true;
            case Keys.Left:
            case Keys.PageUp:
            case Keys.Up: // Up arrow for previous page
                PreviousPage();
                return true;
            case Keys.Home:
                GoToPage(0);
                return true;
            case Keys.End:
                GoToPage(_archiveReader.PageCount - 1);
                return true;
            case Keys.D1:
                _desiredViewMode = ViewMode.SinglePage;
                SetViewMode(ViewMode.SinglePage);
                SaveSettings();
                return true;
            case Keys.D2:
                _desiredViewMode = ViewMode.DualPage;
                SetViewMode(ViewMode.DualPage);
                SaveSettings();
                return true;
            case Keys.F11:
                ToggleFullScreen();
                return true;
            case Keys.Escape:
                if (_isFullScreen) ToggleFullScreen();
                return true;
        }

        // Handle Ctrl+O for opening files
        if (keyData == (Keys.Control | Keys.O))
        {
            OpenFile();
            return true;
        }

        // Handle Ctrl+W for closing files
        if (keyData == (Keys.Control | Keys.W))
        {
            CloseFile();
            return true;
        }

        // Handle Shift+Arrow keys for single page adjustment in dual page mode
        if (_viewerPanel.ViewMode == ViewMode.DualPage)
        {
            if (keyData == (Keys.Shift | Keys.Right))
            {
                // 右矢印: 右開きでは前ページ、左開きでは次ページ
                if (_isRightToLeft)
                    AdjustPageBackward();
                else
                    AdjustPageForward();
                return true;
            }
            if (keyData == (Keys.Shift | Keys.Left))
            {
                // 左矢印: 右開きでは次ページ、左開きでは前ページ
                if (_isRightToLeft)
                    AdjustPageForward();
                else
                    AdjustPageBackward();
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
            if (files?.Length > 0 && IsArchiveFile(files[0]))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }
        }
        e.Effect = DragDropEffects.None;
    }

    private async void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            await LoadArchiveAsync(files[0]);
        }
    }

    private async void OpenFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = LanguageManager.GetString("OpenFileDialogTitle"),
            Filter = LanguageManager.GetString("ArchiveFilter")
        };

        if (ShowCommonDialogSafely(dialog) == DialogResult.OK)
        {
            await LoadArchiveAsync(dialog.FileName);
        }
    }

    private async Task LoadArchiveAsync(string filePath)
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            
            if (await _archiveReader.LoadAsync(filePath))
            {
                _currentPage = 0;
                UpdateDisplay();
                Text = $"Komic Viewer - {Path.GetFileName(filePath)}";
            }
            else
            {
                ShowMessageBoxSafely(LanguageManager.GetString("NoImagesFound"), LanguageManager.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowMessageBoxSafely($"{LanguageManager.GetString("CouldNotOpenFile")}\n{ex.Message}", LanguageManager.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void UpdateDisplay()
    {
        if (!_archiveReader.IsLoaded)
        {
            _viewerPanel.ClearPages();
            UpdateTitle();
            return;
        }

        // Respect user's desired view mode but fall back if PreferSingleOnLandscape prevents dual spread
        var targetMode = _desiredViewMode ?? _viewerPanel.ViewMode;
        if (targetMode == ViewMode.DualPage && _viewerPanel.PreferSingleOnLandscape)
        {
            var leftPage = _archiveReader.GetPage(_currentPage);
            var rightPage = _archiveReader.GetPage(_currentPage + 1);
            try
            {
                if (leftPage == null || rightPage == null || leftPage.Width > leftPage.Height || rightPage.Width > rightPage.Height)
                {
                    targetMode = ViewMode.SinglePage;
                }
            }
            finally
            {
                leftPage?.Dispose();
                rightPage?.Dispose();
            }
        }

        // Apply targetMode to viewer panel
        if (_viewerPanel.ViewMode != targetMode)
            _viewerPanel.ViewMode = targetMode;

        if (_viewerPanel.ViewMode == ViewMode.SinglePage)
        {
            var page = _archiveReader.GetPage(_currentPage);
            _viewerPanel.SetPages(page);
        }
        else
        {
            // Dual page mode
            var currentPage = _archiveReader.GetPage(_currentPage);
            var nextPage = _archiveReader.GetPage(_currentPage + 1);
            _viewerPanel.SetPages(currentPage, nextPage);
        }

        UpdateTitle();
        UpdateSlider(); // スライダーも更新
    }

    private void UpdateTitle()
    {
        if (_pageLabel != null)
        {
            var pageDisplay = _viewerPanel.ViewMode == ViewMode.DualPage && _currentPage + 1 < _archiveReader.PageCount
                ? $"{_currentPage + 1}-{_currentPage + 2} / {_archiveReader.PageCount}"
                : $"{_currentPage + 1} / {_archiveReader.PageCount}";
            
            _pageLabel.Text = _archiveReader.IsLoaded ? pageDisplay : "0 / 0";
        }
    }

    private void NextPage()
    {
        if (!_archiveReader.IsLoaded) return;

        var increment = _viewerPanel.ViewMode == ViewMode.DualPage ? 2 : 1;
        var newPage = _currentPage + increment;
        
        if (newPage < _archiveReader.PageCount)
        {
            _currentPage = newPage;
            UpdateDisplay();
        }
    }

    private void PreviousPage()
    {
        if (!_archiveReader.IsLoaded) return;

        var decrement = _viewerPanel.ViewMode == ViewMode.DualPage ? 2 : 1;
        var newPage = _currentPage - decrement;
        
        // 負の値になる場合は0ページ目に移動
        if (newPage < 0)
        {
            newPage = 0;
        }
        
        // ページが変わる場合のみ更新
        if (newPage != _currentPage)
        {
            _currentPage = newPage;
            UpdateDisplay();
        }
    }

    private void GoToPage(int page)
    {
        if (!_archiveReader.IsLoaded) return;
        
        _currentPage = Math.Clamp(page, 0, _archiveReader.PageCount - 1);
        UpdateDisplay();
    }

    private void SetViewMode(ViewMode mode)
    {
        if (_viewerPanel.ViewMode == mode) return;

        // 見開きへ切替え要求時に「横長時単ページ」設定が有効なら、
        // 現在ページと次ページを確認して、どちらかが横長または片側欠けなら単ページのままにする
        if (mode == ViewMode.DualPage && _viewerPanel.PreferSingleOnLandscape && _archiveReader.IsLoaded)
        {
            Image? left = null;
            Image? right = null;
            try
            {
                left = _archiveReader.GetPage(_currentPage);
                right = _archiveReader.GetPage(_currentPage + 1);

                var leftMissing = left == null;
                var rightMissing = right == null;
                var leftLandscape = left != null && left.Width > left.Height;
                var rightLandscape = right != null && right.Width > right.Height;

                if (leftMissing || rightMissing || leftLandscape || rightLandscape)
                {
                    // 単ページのままにして表示崩れを防ぐ
                    _viewerPanel.ViewMode = ViewMode.SinglePage;
                    UpdateDisplay();
                    
                    // 調整ボタンの表示を更新
                    UpdateAdjustmentButtons();
                    
                    // ツールバーボタンの状態を更新
                    UpdateViewToggleButton();
                    return;
                }
            }
            finally
            {
                left?.Dispose();
                right?.Dispose();
            }
        }

        _viewerPanel.ViewMode = mode;
        UpdateDisplay();
        
        // 調整ボタンの表示を更新
        UpdateAdjustmentButtons();
        
        // ツールバーボタンの状態を更新
        UpdateViewToggleButton();
    }

    private void UpdateViewToggleButton()
    {
        if (_viewToggleBtn != null)
        {
            var isDual = _viewerPanel.ViewMode == ViewMode.DualPage;
            _viewToggleBtn.Checked = isDual;
            _viewToggleBtn.Text = isDual ? LanguageManager.GetString("ViewDual") : LanguageManager.GetString("ViewSingle");
            
            _viewToggleBtn.Image?.Dispose();
            _viewToggleBtn.Image = IconService.GetIcon(isDual ? IconService.IconType.ViewDual : IconService.IconType.ViewSingle, 20, _isDarkMode ? Color.White : Color.Black);
        }
    }

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            // Exit fullscreen
            FormBorderStyle = _previousBorderStyle;
            WindowState = _previousWindowState;
            if (_toolbar != null) 
            {
                // 通常モードではDockを使用
                _toolbar.Dock = DockStyle.Top;
                _toolbar.SendToBack(); // ドッキング順序を正しくするために背面に移動（背面に送ると最初にドッキングされる）
                _toolbar.Visible = true;
                _isToolbarVisible = true;
            }
            // フルスクリーンボタンのテキストを元に戻す
            if (_fullscreenBtn != null)
            {
                _fullscreenBtn.Text = LanguageManager.GetString("Fullscreen");
                _fullscreenBtn.ToolTipText = LanguageManager.GetString("TooltipFullScreen");
                _fullscreenBtn.Image = IconService.GetIcon(IconService.IconType.Fullscreen, 20, _isDarkMode ? Color.White : Color.Black);
            }
            _toolbarHideTimer?.Stop();
        }
        else
        {
            // Enter fullscreen
            _previousBorderStyle = FormBorderStyle;
            _previousWindowState = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            
            // フルスクリーン時は手動で位置制御
            if (_toolbar != null) 
            {
                _toolbar.Dock = DockStyle.None; // 手動制御のためDockを解除
                _toolbar.Location = new Point(0, 2); // 見切れ防止のため2px下に配置
                _toolbar.Width = this.Width;
                _toolbar.BringToFront();
                _toolbar.Visible = false;
                _isToolbarVisible = false;
            }
            // フルスクリーンボタンのテキストを変更
            if (_fullscreenBtn != null)
            {
                _fullscreenBtn.Text = LanguageManager.GetString("NormalSize");
                _fullscreenBtn.ToolTipText = LanguageManager.GetString("TooltipExitFullScreen");
                _fullscreenBtn.Image = IconService.GetIcon(IconService.IconType.Restore, 20, _isDarkMode ? Color.White : Color.Black);
            }
        }
        _isFullScreen = !_isFullScreen;
    }

    private static bool IsArchiveFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".zip" or ".rar" or ".cbz" or ".cbr";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        SaveSettings(); // 設定を保存
        _toolbarHideTimer?.Stop();
        _toolbarHideTimer?.Dispose();
        _archiveReader.Dispose();
    }

    private void Viewer_ClickLeft(object? sender, EventArgs e)
    {
        Console.WriteLine($"左クリック - 読み方向: {(_isRightToLeft ? "右開き" : "左開き")}");
        if (_isRightToLeft)
            NextPage();
        else
            PreviousPage();
    }

    private void Viewer_ClickRight(object? sender, EventArgs e)
    {
        Console.WriteLine($"右クリック - 読み方向: {(_isRightToLeft ? "右開き" : "左開き")}");
        if (_isRightToLeft)
            PreviousPage();
        else
            NextPage();
    }

    private void Viewer_PageNext(object? sender, EventArgs e)
    {
        // Mouse wheel down -> next page, direction unaffected (next means viewer's logical next)
        NextPage();
    }

    private void Viewer_PagePrevious(object? sender, EventArgs e)
    {
        PreviousPage();
    }

    private void ViewerPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        // マウス移動チェックの頻度を制限（100ms間隔）
        var now = DateTime.Now;
        if ((now - _lastMouseMoveCheck).TotalMilliseconds < 100)
            return;
        
        _lastMouseMoveCheck = now;
        
        // Convert viewer panel coordinates to form coordinates
        if (sender is Control control)
        {
            var formPoint = PointToClient(control.PointToScreen(e.Location));
            var formArgs = new MouseEventArgs(e.Button, e.Clicks, formPoint.X, formPoint.Y, e.Delta);
            MainForm_MouseMove(this, formArgs);
        }
    }

    private void ShowHelp()
    {
        using var helpForm = new HelpForm(_isDarkMode);
        ShowDialogSafely(helpForm);
    }

    /// <summary>
    /// TopMost設定を考慮してダイアログを安全に表示するヘルパーメソッド
    /// </summary>
    private DialogResult ShowDialogSafely(Form dialog)
    {
        var wasTopMost = TopMost;
        if (wasTopMost)
        {
            TopMost = false;
        }

        try
        {
            dialog.TopMost = wasTopMost;
            return dialog.ShowDialog(this);
        }
        finally
        {
            if (wasTopMost)
            {
                TopMost = true;
            }
        }
    }

    /// <summary>
    /// TopMost設定を考慮してCommonDialogを安全に表示するヘルパーメソッド
    /// </summary>
    private DialogResult ShowCommonDialogSafely(CommonDialog dialog)
    {
        var wasTopMost = TopMost;
        if (wasTopMost)
        {
            TopMost = false;
        }

        try
        {
            return dialog.ShowDialog(this);
        }
        finally
        {
            if (wasTopMost)
            {
                TopMost = true;
            }
        }
    }

    /// <summary>
    /// TopMost設定を考慮してMessageBoxを安全に表示するヘルパーメソッド
    /// </summary>
    private DialogResult ShowMessageBoxSafely(string text, string caption = "", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
    {
        var wasTopMost = TopMost;
        if (wasTopMost)
        {
            TopMost = false;
        }

        try
        {
            return MessageBox.Show(this, text, caption, buttons, icon);
        }
        finally
        {
            if (wasTopMost)
            {
                TopMost = true;
            }
        }
    }

    private void CloseFile()
    {
        _archiveReader.Close();
        _currentPage = 0;
        _viewerPanel.ClearPages();
        Text = "Komic Viewer";
        UpdateTitle();
    }

    private void MainForm_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isFullScreen) return;

        // Show toolbar when mouse is near the top of the screen
        if (e.Y <= 52) // Top 52 pixels (ツールバー位置に合わせて調整)
        {
            if (!_isToolbarVisible)
            {
                ShowToolbarInFullScreen();
            }
            // ツールバーが表示されている間はタイマーを停止
            _toolbarHideTimer?.Stop();
            _lastTimerStart = DateTime.MinValue; // タイマーリセット
        }
        else
        {
            // マウスが上部から離れた場合、タイマーを開始（ツールバーが表示されている場合のみ）
            if (_isToolbarVisible)
            {
                // タイマーが既に動いていない場合のみ開始（頻繁な再スタートを防ぐ）
                if (_lastTimerStart == DateTime.MinValue || _toolbarHideTimer?.Enabled != true)
                {
                    _toolbarHideTimer?.Stop();
                    _toolbarHideTimer?.Start();
                    _lastTimerStart = DateTime.Now;
                    System.Diagnostics.Debug.WriteLine($"Hide timer started at {DateTime.Now:HH:mm:ss.fff} - will hide in 1 second");
                }
            }
        }
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        // ツールバーの幅をフォームに合わせて調整
        if (_toolbar != null && !_toolbar.IsDisposed)
        {
            _toolbar.Width = this.Width;
        }
    }

    private void ToolbarHideTimer_Tick(object? sender, EventArgs e)
    {
        _toolbarHideTimer?.Stop();
        _lastTimerStart = DateTime.MinValue; // タイマー状態をリセット
        
        System.Diagnostics.Debug.WriteLine($"Timer tick at {DateTime.Now:HH:mm:ss.fff} - checking mouse position");
        
        if (_isFullScreen && _isToolbarVisible)
        {
            // Check current mouse position
            var mousePos = PointToClient(Cursor.Position);
            
            // マウスが上部52px領域にない場合、ツールバーを非表示
            if (mousePos.Y > 52)
            {
                HideToolbarInFullScreen();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Mouse still in top area, keeping toolbar visible");
            }
        }
    }

    private void ShowToolbarInFullScreen()
    {
        if (_isFullScreen && _toolbar != null && !_isToolbarVisible)
        {
            // ツールバーを最前面に配置してから表示
            _toolbar.BringToFront();
            _toolbar.Location = new Point(0, 2); // 見切れ防止のため2px下に配置
            _toolbar.Visible = true;
            _isToolbarVisible = true;
            _toolbarHideTimer?.Stop();
            
            // デバッグ用ログ
            System.Diagnostics.Debug.WriteLine($"Toolbar shown at {DateTime.Now:HH:mm:ss.fff}, Location: {_toolbar.Location}");
        }
    }

    private void HideToolbarInFullScreen()
    {
        if (_isFullScreen && _toolbar != null && _isToolbarVisible)
        {
            _toolbar.Visible = false;
            _isToolbarVisible = false;
            
            // デバッグ用ログ
            System.Diagnostics.Debug.WriteLine($"Toolbar hidden at {DateTime.Now:HH:mm:ss.fff}");
        }
    }

    private void UpdateNavigationButtons()
    {
        if (_prevBtn == null || _nextBtn == null) return;

        if (_isRightToLeft)
        {
            // 右開き（日本式）: 右から左に読むので、次は左向き、前は右向き
            _prevBtn.Text = $"{LanguageManager.GetString("Prev")} ▶";
            _nextBtn.Text = $"◀ {LanguageManager.GetString("Next")}";
        }
        else
        {
            // 左開き（欧米式）: 左から右に読むので、前は左向き、次は右向き
            _prevBtn.Text = $"◀ {LanguageManager.GetString("Prev")}";
            _nextBtn.Text = $"{LanguageManager.GetString("Next")} ▶";
        }
        
        // ボタンの順序を更新
        UpdateNavigationButtonOrder();
        
        // 見開き調整ボタンの表示/非表示を制御
        UpdateAdjustmentButtons();
    }

    private void AddNavigationButtonsInOrder()
    {
        if (_toolbar == null || _prevBtn == null || _nextBtn == null || _pageLabel == null) return;

        if (_isRightToLeft)
        {
            // 右開き: 左側に次、右側に前
            _toolbar.Items.Add(_nextBtn);  // 左側に「◀ 次」
            _toolbar.Items.Add(_pageLabel);
            _toolbar.Items.Add(_prevBtn);  // 右側に「前 ▶」
        }
        else
        {
            // 左開き: 左側に前、右側に次
            _toolbar.Items.Add(_prevBtn);  // 左側に「◀ 前」
            _toolbar.Items.Add(_pageLabel);
            _toolbar.Items.Add(_nextBtn);  // 右側に「次 ▶」
        }
    }

    private void UpdateNavigationButtonOrder()
    {
        if (_toolbar == null || _prevBtn == null || _nextBtn == null || _pageLabel == null) return;

        // 現在のナビゲーションボタンを一時的に削除
        var separatorIndex = -1;
        for (int i = 0; i < _toolbar.Items.Count; i++)
        {
            if (_toolbar.Items[i] == _prevBtn || _toolbar.Items[i] == _nextBtn || _toolbar.Items[i] == _pageLabel || _toolbar.Items[i] == _adjustPrevBtn || _toolbar.Items[i] == _adjustNextBtn)
            {
                if (separatorIndex == -1)
                {
                    separatorIndex = i; // 最初のナビゲーション要素の位置を記録
                }
            }
        }

        // ナビゲーション要素を削除
        _toolbar.Items.Remove(_prevBtn);
        _toolbar.Items.Remove(_nextBtn);
        _toolbar.Items.Remove(_pageLabel);
        _toolbar.Items.Remove(_adjustPrevBtn);
        _toolbar.Items.Remove(_adjustNextBtn);

        if (separatorIndex == -1) separatorIndex = 0;

        // 読み方向に応じた順序で再追加
        if (_isRightToLeft)
        {
            // 右開き: [次] [ラベル] [前] [◁] [▷]
            _toolbar.Items.Insert(separatorIndex, _nextBtn);
            _toolbar.Items.Insert(separatorIndex + 1, _pageLabel);
            _toolbar.Items.Insert(separatorIndex + 2, _prevBtn);
            _toolbar.Items.Insert(separatorIndex + 3, _adjustPrevBtn);
            _toolbar.Items.Insert(separatorIndex + 4, _adjustNextBtn);
        }
        else
        {
            // 左開き: [◁] [▷] [前] [ラベル] [次]
            _toolbar.Items.Insert(separatorIndex, _adjustPrevBtn);
            _toolbar.Items.Insert(separatorIndex + 1, _adjustNextBtn);
            _toolbar.Items.Insert(separatorIndex + 2, _prevBtn);
            _toolbar.Items.Insert(separatorIndex + 3, _pageLabel);
            _toolbar.Items.Insert(separatorIndex + 4, _nextBtn);
        }
    }

    private void UpdateAdjustmentButtons()
    {
        if (_adjustPrevBtn == null || _adjustNextBtn == null || _directionBtn == null) return;
        
        var isDualPage = _viewerPanel.ViewMode == ViewMode.DualPage;
        
        // 見開き調整ボタンの表示/非表示
        _adjustPrevBtn.Visible = isDualPage;
        _adjustNextBtn.Visible = isDualPage;
        
        // 右開き・左開き切り替えボタンの表示/非表示（見開きの場合のみ表示）
        _directionBtn.Visible = isDualPage;
        
        if (isDualPage)
        {
            // 読み方向に応じてツールチップを更新
            if (_isRightToLeft)
            {
                // 右開き: ◁が次ページ、▷が前ページ
                _adjustPrevBtn.ToolTipText = $"◁ {LanguageManager.GetString("AdjustForward")}";
                _adjustNextBtn.ToolTipText = $"▷ {LanguageManager.GetString("AdjustBackward")}";
            }
            else
            {
                // 左開き: ◁が前ページ、▷が次ページ
                _adjustPrevBtn.ToolTipText = $"◁ {LanguageManager.GetString("AdjustBackward")}";
                _adjustNextBtn.ToolTipText = $"▷ {LanguageManager.GetString("AdjustForward")}";
            }
        }
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        // フォーム読み込み時に確実に画面中央に配置
        CenterToScreen();
        
        // マルチモニター環境でプライマリモニターの中央に配置
        var screen = Screen.PrimaryScreen;
        if (screen != null)
        {
            var screenBounds = screen.WorkingArea;
            Location = new Point(
                screenBounds.X + (screenBounds.Width - Width) / 2,
                screenBounds.Y + (screenBounds.Height - Height) / 2
            );
        }
        
        // 最小サイズを設定（ウィンドウが小さくなりすぎないように）
        MinimumSize = new Size(800, 600);
        
        // 初期状態でフォーカスを設定
        Focus();
    }

    private void ToggleTheme(ToolStripButton themeBtn)
    {
        _isDarkMode = themeBtn.Checked;
        ApplyTheme();
        themeBtn.Text = _isDarkMode ? LanguageManager.GetString("ThemeDark") : LanguageManager.GetString("ThemeLight");
        SaveSettings(); // テーマ設定を保存
    }

    private void ToggleTopMost(ToolStripButton topMostBtn)
    {
        _isTopMost = topMostBtn.Checked;
        TopMost = _isTopMost;
        topMostBtn.Text = _isTopMost ? LanguageManager.GetString("TopMost") : LanguageManager.GetString("NormalWindow");
        SaveSettings(); // 最前面設定を保存
    }

    private void ChangeLanguage(Language lang)
    {
        if (LanguageManager.CurrentLanguage == lang) return;
        
        LanguageManager.CurrentLanguage = lang;
        
        // ツールバーを再作成するのが一番確実
        Controls.Remove(_toolbar);
        CreateToolbar();
        ApplyTheme();
        
        // ナビゲーションボタンの状態を更新
        UpdateNavigationButtons();
        
        // 表示を更新
        UpdateDisplay();
        
        // 設定を保存
        SaveSettings();
    }

    private void LoadSettings()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsDir = Path.Combine(appDataPath, "KomicViewer");
            var settingsFile = Path.Combine(settingsDir, "settings.json");
            
            if (File.Exists(settingsFile))
            {
                var json = File.ReadAllText(settingsFile);
                if (!string.IsNullOrEmpty(json))
                {
                    var settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (settings != null)
                    {
                        // ダークモード設定
                        if (settings.TryGetValue("isDarkMode", out var darkModeObj) && darkModeObj is System.Text.Json.JsonElement darkModeElement)
                        {
                            _isDarkMode = darkModeElement.GetBoolean();
                        }
                        
                        // 最前面表示設定
                        if (settings.TryGetValue("isTopMost", out var topMostObj) && topMostObj is System.Text.Json.JsonElement topMostElement)
                        {
                            _isTopMost = topMostElement.GetBoolean();
                        }
                        
                        // 読み方向設定
                        if (settings.TryGetValue("isRightToLeft", out var rightToLeftObj) && rightToLeftObj is System.Text.Json.JsonElement rightToLeftElement)
                        {
                            _isRightToLeft = rightToLeftElement.GetBoolean();
                        }
                        
                        // 表示モード設定
                        if (settings.TryGetValue("viewMode", out var viewModeObj) && viewModeObj is System.Text.Json.JsonElement viewModeElement)
                        {
                            var viewModeStr = viewModeElement.GetString();
                            if (Enum.TryParse<ViewMode>(viewModeStr, out var viewMode))
                            {
                                _desiredViewMode = viewMode;
                            }
                        }
                        
                        // ウィンドウサイズ設定
                        if (settings.TryGetValue("windowWidth", out var widthObj) && widthObj is System.Text.Json.JsonElement widthElement &&
                            settings.TryGetValue("windowHeight", out var heightObj) && heightObj is System.Text.Json.JsonElement heightElement)
                        {
                            var width = widthElement.GetInt32();
                            var height = heightElement.GetInt32();
                            if (width >= 800 && height >= 600) // 最小サイズチェック
                            {
                                Size = new Size(width, height);
                            }
                        }
                        
                        // ウィンドウ位置設定
                        if (settings.TryGetValue("windowX", out var xObj) && xObj is System.Text.Json.JsonElement xElement &&
                            settings.TryGetValue("windowY", out var yObj) && yObj is System.Text.Json.JsonElement yElement)
                        {
                            var x = xElement.GetInt32();
                            var y = yElement.GetInt32();
                            
                            // 画面内に収まるかチェック
                            var screen = Screen.FromPoint(new Point(x, y));
                            if (screen.WorkingArea.Contains(x, y))
                            {
                                StartPosition = FormStartPosition.Manual;
                                Location = new Point(x, y);
                            }
                        }
                        // 言語設定
                        if (settings.TryGetValue("language", out var languageObj) && languageObj is System.Text.Json.JsonElement languageElement)
                        {
                            var langStr = languageElement.GetString();
                            if (Enum.TryParse<Language>(langStr, out var lang))
                            {
                                LanguageManager.CurrentLanguage = lang;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // 設定読み込みに失敗した場合はデフォルト値を使用
            _isDarkMode = true;
            _isTopMost = false;
            _isRightToLeft = true;
            LanguageManager.CurrentLanguage = Language.Japanese;
        }
    }

    private void SaveSettings()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsDir = Path.Combine(appDataPath, "KomicViewer");
            
            if (!Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }

            var settingsFile = Path.Combine(settingsDir, "settings.json");
            var settings = new Dictionary<string, object>
            {
                ["isDarkMode"] = _isDarkMode,
                ["isTopMost"] = _isTopMost,
                ["isRightToLeft"] = _isRightToLeft,
                ["viewMode"] = _viewerPanel?.ViewMode.ToString() ?? ViewMode.SinglePage.ToString(),
                ["windowWidth"] = Width,
                ["windowHeight"] = Height,
                ["windowX"] = Location.X,
                ["windowY"] = Location.Y,
                ["language"] = LanguageManager.CurrentLanguage.ToString()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            File.WriteAllText(settingsFile, json);
        }
        catch
        {
            // 設定保存に失敗しても続行
        }
    }

    private void ApplyTheme()
    {
        // タイトルバーのテーマを更新
        if (IsHandleCreated)
        {
            ApplyTitleBarTheme(_isDarkMode);
        }

        if (_isDarkMode)
        {
            // ダークテーマ
            BackColor = Color.FromArgb(25, 25, 25);
            ForeColor = Color.White;
            if (_viewerPanel != null)
                _viewerPanel.BackColor = Color.FromArgb(30, 30, 30);
            
            if (_toolbar != null)
            {
                _toolbar.Renderer = new ModernToolStripRenderer(true);
                
                // 全てのツールバーアイテムのアイコンのみ更新（背景などはレンダラーが担当）
                foreach (ToolStripItem item in _toolbar.Items)
                {
                    item.ForeColor = Color.White;
                    if (item is ToolStripButton btn)
                    {
                        RefreshButtonIcon(btn, Color.White);
                    }
                }
            }
                
                // 最前面表示ボタンの色も更新
                if (_topMostBtn != null)
                {
                    _topMostBtn.ForeColor = Color.White;
                    _topMostBtn.BackColor = Color.FromArgb(50, 50, 50);
                }

                // スライダーのテーマも更新
                if (_pageSlider != null) _pageSlider.IsDarkMode = true;
            
            // ファイルメニューの色を更新
            if (_fileContextMenu != null)
            {
                _fileContextMenu.BackColor = Color.FromArgb(50, 50, 50);
                _fileContextMenu.ForeColor = Color.White;
                _fileContextMenu.Renderer = new ModernToolStripRenderer(true);
                
                foreach (ToolStripItem item in _fileContextMenu.Items)
                {
                    item.ForeColor = Color.White;
                    item.BackColor = Color.FromArgb(50, 50, 50);
                }
            }
        }
        else
        {
            // ライトテーマ
            BackColor = Color.FromArgb(240, 240, 240);
            ForeColor = Color.Black;
            if (_viewerPanel != null)
                _viewerPanel.BackColor = Color.FromArgb(250, 250, 250);

            if (_toolbar != null)
            {
                _toolbar.Renderer = new ModernToolStripRenderer(false);

                // 全てのツールバーアイテムのアイコンを更新
                foreach (ToolStripItem item in _toolbar.Items)
                {
                    item.ForeColor = Color.Black;
                    if (item is ToolStripButton btn)
                    {
                        RefreshButtonIcon(btn, Color.Black);
                    }
                }
            }

            // ファイルメニューの色を更新
            if (_fileContextMenu != null)
            {
                _fileContextMenu.BackColor = Color.FromArgb(240, 240, 240);
                _fileContextMenu.ForeColor = Color.Black;
                _fileContextMenu.Renderer = new ModernToolStripRenderer(false);

                foreach (ToolStripItem item in _fileContextMenu.Items)
                {
                    item.ForeColor = Color.Black;
                    item.BackColor = Color.FromArgb(240, 240, 240);
                }
            }
        }

        // 再描画
        Invalidate(true);
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(value);

        // Apply title bar theme when window becomes visible
        if (value && IsHandleCreated)
        {
            ApplyTitleBarTheme(_isDarkMode);
        }
    }

    private void AdjustPageForward()
    {
        if (!_archiveReader.IsLoaded) return;
        
        // 1ページだけ進む
        var newPage = _currentPage + 1;
        if (newPage < _archiveReader.PageCount)
        {
            _currentPage = newPage;
            UpdateDisplay();
        }
    }

    private void AdjustPageBackward()
    {
        if (!_archiveReader.IsLoaded) return;
        
        // 1ページだけ戻る
        var newPage = _currentPage - 1;
        if (newPage >= 0)
        {
            _currentPage = newPage;
            UpdateDisplay();
        }
    }

    private void ApplyDarkTitleBar()
    {
        ApplyTitleBarTheme(_isDarkMode);
    }

    private void ApplyTitleBarTheme(bool isDark)
    {
        if (Environment.OSVersion.Version.Major >= 10)
        {
            var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
            
            // Windows 10 version 2004 and later use a different attribute
            if (Environment.OSVersion.Version.Build >= 19041)
            {
                attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
            }

            int useImmersiveDarkMode = isDark ? 1 : 0; // ダークモードかどうかで切り替え
            DwmSetWindowAttribute(Handle, attribute, ref useImmersiveDarkMode, sizeof(int));
        }
    }

    private void RefreshButtonIcon(ToolStripButton btn, Color color)
    {
        if (btn == null) return;
        IconService.IconType? type = null;
        
        string text = btn.Text;
        if (text == LanguageManager.GetString("MenuFile")) type = IconService.IconType.File;
        else if (text == LanguageManager.GetString("Help")) type = IconService.IconType.Help;
        else if (text == LanguageManager.GetString("Language")) type = IconService.IconType.Language;
        else if (text == LanguageManager.GetString("Fullscreen")) type = IconService.IconType.Fullscreen;
        else if (text == LanguageManager.GetString("NormalSize")) type = IconService.IconType.Restore;
        else if (text == LanguageManager.GetString("ViewDual")) type = IconService.IconType.ViewDual;
        else if (text == LanguageManager.GetString("ViewSingle")) type = IconService.IconType.ViewSingle;
        else if (text == LanguageManager.GetString("ThemeDark") || text == LanguageManager.GetString("ThemeLight"))
            type = _isDarkMode ? IconService.IconType.ThemeDark : IconService.IconType.ThemeLight;
        else if (text == LanguageManager.GetString("TopMost") || text == LanguageManager.GetString("NormalWindow"))
            type = _isTopMost ? IconService.IconType.TopMostOn : IconService.IconType.TopMostOff;

        if (type.HasValue)
        {
            btn.Image?.Dispose();
            btn.Image = IconService.GetIcon(type.Value, 20, color);
        }
    }
}

/// <summary>
/// Modern renderer for a professional, flat UI look
/// </summary>
public class ModernToolStripRenderer : ToolStripProfessionalRenderer
{
    private readonly bool _isDarkMode;

    public ModernToolStripRenderer(bool isDarkMode) : base(new ModernColorTable(isDarkMode))
    {
        _isDarkMode = isDarkMode;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        var bgColor = _isDarkMode ? Color.FromArgb(35, 35, 35) : Color.FromArgb(245, 245, 245);
        using var brush = new SolidBrush(bgColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);

        // Bottom border (subtle accent)
        var borderColor = _isDarkMode ? Color.FromArgb(60, 60, 60) : Color.FromArgb(210, 210, 210);
        using var pen = new Pen(borderColor);
        e.Graphics.DrawLine(pen, e.AffectedBounds.Left, e.AffectedBounds.Bottom - 1, e.AffectedBounds.Right, e.AffectedBounds.Bottom - 1);
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected && !e.Item.Pressed && !(e.Item is ToolStripButton btn && btn.Checked)) return;

        var rect = new Rectangle(Point.Empty, e.Item.Size);
        rect.Inflate(-2, -2); // Padding for rounded look

        Color backColor;
        if (e.Item.Pressed)
            backColor = _isDarkMode ? Color.FromArgb(80, 80, 80) : Color.FromArgb(180, 180, 180);
        else if (e.Item.Selected)
            backColor = _isDarkMode ? Color.FromArgb(65, 65, 65) : Color.FromArgb(220, 220, 220);
        else 
            backColor = _isDarkMode ? Color.FromArgb(55, 55, 55) : Color.FromArgb(200, 200, 200);

        using var path = GetRoundedRectanglePath(rect, 4);
        using var brush = new SolidBrush(backColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected) return;

        var rect = new Rectangle(Point.Empty, e.Item.Size);
        var backColor = _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(220, 220, 220);

        using var brush = new SolidBrush(backColor);
        e.Graphics.FillRectangle(brush, rect);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var color = _isDarkMode ? Color.FromArgb(60, 60, 60) : Color.FromArgb(200, 200, 200);
        using var pen = new Pen(color);
        int mid = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, e.Item.Width / 2, 4, e.Item.Width / 2, e.Item.Height - 4);
    }

    private GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2f;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseAllFigures();
        return path;
    }
}

public class ModernColorTable : ProfessionalColorTable
{
    private readonly bool _isDarkMode;

    public ModernColorTable(bool isDarkMode)
    {
        _isDarkMode = isDarkMode;
    }

    public override Color ToolStripBorder => Color.Transparent; // Remove border
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(220, 220, 220);
    public override Color MenuBorder => _isDarkMode ? Color.FromArgb(60, 60, 60) : Color.FromArgb(200, 200, 200);
}

/// <summary>
/// A modern, custom-drawn slider for page navigation
/// </summary>
public class ModernProgressSlider : Control
{
    private int _value = 0;
    private int _minimum = 0;
    private int _maximum = 100;
    private bool _isDarkMode = true;
    private bool _isHovering = false;
    private bool _isDragging = false;

    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set {
            var newValue = Math.Clamp(value, _minimum, _maximum);
            if (_value != newValue) {
                _value = newValue;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(_minimum, value); Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set { _isDarkMode = value; Invalidate(); }
    }

    public ModernProgressSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        float trackHeight = 4f;
        float trackY = (rect.Height - trackHeight) / 2f;
        float padding = 8f;
        float availableWidth = rect.Width - padding * 2;

        // Draw track
        var trackColor = _isDarkMode ? Color.FromArgb(60, 60, 60) : Color.FromArgb(210, 210, 210);
        using (var trackBrush = new SolidBrush(trackColor))
        using (var path = GetRoundedRect(new RectangleF(padding, trackY, availableWidth, trackHeight), trackHeight / 2))
        {
            e.Graphics.FillPath(trackBrush, path);
        }

        // Draw progress
        float progressWidth = _maximum > _minimum ? (float)(_value - _minimum) / (_maximum - _minimum) * availableWidth : 0;
        if (progressWidth > 0)
        {
            var progressColor = _isDarkMode ? Color.FromArgb(100, 100, 100) : Color.FromArgb(160, 160, 160);
            using (var progressBrush = new SolidBrush(progressColor))
            using (var path = GetRoundedRect(new RectangleF(padding, trackY, progressWidth, trackHeight), trackHeight / 2))
            {
                e.Graphics.FillPath(progressBrush, path);
            }
        }

        // Draw thumb
        float thumbSize = _isHovering || _isDragging ? 12f : 10f;
        float thumbX = padding + progressWidth - thumbSize / 2f;
        float thumbY = (rect.Height - thumbSize) / 2f;
        
        var thumbColor = _isDarkMode ? Color.White : Color.FromArgb(40, 40, 40);
        if (_isDragging) thumbColor = _isDarkMode ? Color.LightGray : Color.Black;

        using (var thumbBrush = new SolidBrush(thumbColor))
        {
            e.Graphics.FillEllipse(thumbBrush, thumbX, thumbY, thumbSize, thumbSize);
        }
    }

    private GraphicsPath GetRoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            UpdateValueFromMouse(e.X);
            Capture = true;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool wasHovering = _isHovering;
        _isHovering = ClientRectangle.Contains(e.Location);
        if (wasHovering != _isHovering) Invalidate();

        if (_isDragging)
        {
            UpdateValueFromMouse(e.X);
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _isDragging = false;
        Capture = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    private void UpdateValueFromMouse(int mouseX)
    {
        float padding = 8f;
        float availableWidth = Width - padding * 2;
        float relativeX = Math.Clamp(mouseX - padding, 0, availableWidth);
        Value = (int)Math.Round(relativeX / availableWidth * (_maximum - _minimum) + _minimum);
    }
}
