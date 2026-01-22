using KomicViewer.Controls;
using KomicViewer.Services;
using KomicViewer.Forms;
using System.Runtime.InteropServices;

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
    private HScrollBar? _pageSlider;

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
            ShowMessageBoxSafely($"MainForm初期化エラー:\n{ex.Message}\n\nスタックトレース:\n{ex.StackTrace}", 
                "初期化エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(5, 2, 5, 2)
        };

        _toolbar.Items.Add(new ToolStripSeparator());

        // Open file button with context menu
        var openBtn = new ToolStripButton("📁 ファイル")
        {
            ForeColor = Color.White
        };
        
        // Create context menu for file operations
        _fileContextMenu = new ContextMenuStrip
        {
            BackColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240),
            ForeColor = _isDarkMode ? Color.White : Color.Black,
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Renderer = new DarkMenuRenderer(_isDarkMode)
        };

        // Open file item
        var openItem = new ToolStripMenuItem("開く...")
        {
            ForeColor = _isDarkMode ? Color.White : Color.Black,
            BackColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240),
            ShortcutKeyDisplayString = "Ctrl+O"
        };
        openItem.Click += (_, _) => OpenFile();
        _fileContextMenu.Items.Add(openItem);

        // Close file item
        var closeItem = new ToolStripMenuItem("閉じる")
        {
            ForeColor = _isDarkMode ? Color.White : Color.Black,
            BackColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240),
            ShortcutKeyDisplayString = "Ctrl+W"
        };
        closeItem.Click += (_, _) => CloseFile();
        _fileContextMenu.Items.Add(closeItem);

        _fileContextMenu.Items.Add(new ToolStripSeparator { BackColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240) });

        // Exit item
        var exitItem = new ToolStripMenuItem("終了")
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
            ForeColor = Color.LightGray
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
            Visible = false
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
            Visible = false
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
            ToolTipText = "単ページ / 見開き を切り替え"
        };

        // Initialize text clearly
        viewToggleBtn.Text = _viewerPanel.ViewMode == ViewMode.DualPage ? "見開き" : "単ページ";

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
            
            // 設定を保存
            SaveSettings();
        };

        _viewToggleBtn = viewToggleBtn;
        _toolbar.Items.Add(_viewToggleBtn);

        _toolbar.Items.Add(new ToolStripSeparator());

        // Fullscreen button
        _fullscreenBtn = new ToolStripButton("⛶ フルスクリーン")
        {
            ForeColor = Color.White
        };
        _fullscreenBtn.Click += (_, _) => ToggleFullScreen();
        _toolbar.Items.Add(_fullscreenBtn);
        
        _toolbar.Items.Add(new ToolStripSeparator());
        _directionBtn = new ToolStripButton()
        {
            ForeColor = Color.White,
            CheckOnClick = true,
            Checked = _isRightToLeft, // 設定から読み込んだ値を使用
            ToolTipText = "表示方向: 右開き / 左開き を切り替え"
        };
        
        // 初期テキストを設定から読み込んだ値に基づいて設定
        _directionBtn.Text = _isRightToLeft ? "右開き" : "左開き";
        
        _directionBtn.Click += (_, _) =>
        {
            _isRightToLeft = _directionBtn.Checked;
            _directionBtn.Text = _isRightToLeft ? "右開き" : "左開き";
            
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

        // Help button
        var helpBtn = new ToolStripButton("❓ 使い方")
        {
            ForeColor = Color.White,
            ToolTipText = "使い方を表示"
        };
        helpBtn.Click += (_, _) => ShowHelp();
        _toolbar.Items.Add(helpBtn);

        _toolbar.Items.Add(new ToolStripSeparator());

        // Theme toggle button
        var themeBtn = new ToolStripButton(_isDarkMode ? "🌙 ダーク" : "☀️ ライト")
        {
            ForeColor = Color.White,
            CheckOnClick = true,
            Checked = _isDarkMode,
            ToolTipText = "ライト/ダークテーマ切り替え"
        };
        themeBtn.Click += (_, _) => ToggleTheme(themeBtn);
        _toolbar.Items.Add(themeBtn);

        _toolbar.Items.Add(new ToolStripSeparator());

        // 最前面表示ボタン
        _topMostBtn = new ToolStripButton(_isTopMost ? "📌 最前面" : "📌 通常")
        {
            ForeColor = Color.White,
            CheckOnClick = true,
            Checked = _isTopMost,
            ToolTipText = "ウィンドウを最前面に表示"
        };
        _topMostBtn.Click += (_, _) => ToggleTopMost(_topMostBtn);
        _toolbar.Items.Add(_topMostBtn);

        // ページナビゲーション用スクロールバーをツールバーに追加
        _toolbar.Items.Add(new ToolStripSeparator());
        CreatePageScrollBar();

        // ツールバーを上部にドック（通常モード）
        _toolbar.Dock = DockStyle.Top;
        Controls.Add(_toolbar);
        
        // 初期状態でボタンの表示/非表示を設定
        UpdateAdjustmentButtons();
    }

    private void CreatePageScrollBar()
    {
        // 水平スクロールバーを作成
        _pageSlider = new HScrollBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 20,
            Width = 200, // 固定幅
            SmallChange = 1,
            LargeChange = 10
        };
        _pageSlider.Scroll += PageSlider_Scroll;

        // スクロールバーをToolStripControlHostでラップ
        var scrollBarHost = new ToolStripControlHost(_pageSlider)
        {
            AutoSize = false,
            Size = new Size(200, 25)
        };

        // ツールバーに追加（ラベルは削除）
        _toolbar?.Items.Add(scrollBarHost);
    }

    private void PageSlider_Scroll(object? sender, ScrollEventArgs e)
    {
        if (_pageSlider != null && _archiveReader.IsLoaded)
        {
            // スクロールバーの場合、実際の最大値は Maximum - LargeChange + 1
            var actualMaximum = _archiveReader.PageCount - 1;
            var targetPage = (int)Math.Round((double)_pageSlider.Value / actualMaximum * actualMaximum);
            
            // Adjust for dual page mode
            if (_viewerPanel.ViewMode == ViewMode.DualPage && targetPage > 0)
            {
                targetPage = (targetPage / 2) * 2; // 偶数に調整
            }
            
            targetPage = Math.Clamp(targetPage, 0, _archiveReader.PageCount - 1);
            
            // Update page if changed
            if (targetPage != _currentPage)
            {
                _currentPage = targetPage;
                UpdateDisplay();
            }
        }
    }

    private void UpdateSlider()
    {
        if (_pageSlider != null && _archiveReader.IsLoaded)
        {
            // スクロールバーの場合、Maximum = 実際の最大値 + LargeChange - 1
            _pageSlider.Maximum = _archiveReader.PageCount - 1 + _pageSlider.LargeChange - 1;
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
            Title = "コミックファイルを開く",
            Filter = "アーカイブファイル|*.zip;*.rar;*.cbz;*.cbr|ZIPファイル|*.zip;*.cbz|RARファイル|*.rar;*.cbr|すべてのファイル|*.*"
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
                ShowMessageBoxSafely("画像が見つかりませんでした。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowMessageBoxSafely($"ファイルを開けませんでした:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            _viewToggleBtn.Checked = _viewerPanel.ViewMode == ViewMode.DualPage;
            _viewToggleBtn.Text = _viewerPanel.ViewMode == ViewMode.DualPage ? "見開き" : "単ページ";
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
                _toolbar.Visible = true;
                _isToolbarVisible = true;
            }
            // フルスクリーンボタンのテキストを元に戻す
            if (_fullscreenBtn != null)
            {
                _fullscreenBtn.Text = "⛶ フルスクリーン";
                _fullscreenBtn.ToolTipText = "フルスクリーン表示";
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
                _fullscreenBtn.Text = "🗗 元のサイズ";
                _fullscreenBtn.ToolTipText = "フルスクリーンを終了";
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
            _prevBtn.Text = "前 ▶";
            _nextBtn.Text = "◀ 次";
        }
        else
        {
            // 左開き（欧米式）: 左から右に読むので、前は左向き、次は右向き
            _prevBtn.Text = "◀ 前";
            _nextBtn.Text = "次 ▶";
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
            if (_toolbar.Items[i] == _prevBtn || _toolbar.Items[i] == _nextBtn || _toolbar.Items[i] == _pageLabel)
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

        // 読み方向に応じた順序で再追加
        if (_isRightToLeft)
        {
            // 右開き: 左側に次、右側に前
            _toolbar.Items.Insert(separatorIndex, _nextBtn);     // 左側に「◀ 次」
            _toolbar.Items.Insert(separatorIndex + 1, _pageLabel);
            _toolbar.Items.Insert(separatorIndex + 2, _prevBtn); // 右側に「前 ▶」
        }
        else
        {
            // 左開き: 左側に前、右側に次
            _toolbar.Items.Insert(separatorIndex, _prevBtn);     // 左側に「◀ 前」
            _toolbar.Items.Insert(separatorIndex + 1, _pageLabel);
            _toolbar.Items.Insert(separatorIndex + 2, _nextBtn); // 右側に「次 ▶」
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
                _adjustPrevBtn.ToolTipText = "◁ 1ページ送る（見開き調整用）";
                _adjustNextBtn.ToolTipText = "▷ 1ページ戻す（見開き調整用）";
            }
            else
            {
                // 左開き: ◁が前ページ、▷が次ページ
                _adjustPrevBtn.ToolTipText = "◁ 1ページ戻す（見開き調整用）";
                _adjustNextBtn.ToolTipText = "▷ 1ページ送る（見開き調整用）";
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
        themeBtn.Text = _isDarkMode ? "🌙 ダーク" : "☀️ ライト";
        SaveSettings(); // テーマ設定を保存
    }

    private void ToggleTopMost(ToolStripButton topMostBtn)
    {
        _isTopMost = topMostBtn.Checked;
        TopMost = _isTopMost;
        topMostBtn.Text = _isTopMost ? "📌 最前面" : "📌 通常";
        SaveSettings(); // 最前面設定を保存
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
                ["windowY"] = Location.Y
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
                _toolbar.BackColor = Color.FromArgb(50, 50, 50);
                _toolbar.ForeColor = Color.White;
                
                // 全てのツールバーアイテムの色を更新
                foreach (ToolStripItem item in _toolbar.Items)
                {
                    item.ForeColor = Color.White;
                    if (item is ToolStripButton btn)
                    {
                        btn.BackColor = Color.FromArgb(50, 50, 50);
                    }
                }
                
                // 最前面表示ボタンの色も更新
                if (_topMostBtn != null)
                {
                    _topMostBtn.ForeColor = Color.White;
                    _topMostBtn.BackColor = Color.FromArgb(50, 50, 50);
                }
            }
            
            // ファイルメニューの色を更新
            if (_fileContextMenu != null)
            {
                _fileContextMenu.BackColor = Color.FromArgb(50, 50, 50);
                _fileContextMenu.ForeColor = Color.White;
                _fileContextMenu.Renderer = new DarkMenuRenderer(true);
                
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
                _toolbar.BackColor = Color.FromArgb(230, 230, 230);
                _toolbar.ForeColor = Color.Black;
                
                // 全てのツールバーアイテムの色を更新
                foreach (ToolStripItem item in _toolbar.Items)
                {
                    item.ForeColor = Color.Black;
                    if (item is ToolStripButton btn)
                    {
                        btn.BackColor = Color.FromArgb(230, 230, 230);
                    }
                }
                
                // 最前面表示ボタンの色も更新
                if (_topMostBtn != null)
                {
                    _topMostBtn.ForeColor = Color.Black;
                    _topMostBtn.BackColor = Color.FromArgb(230, 230, 230);
                }
            }
            
            // ファイルメニューの色を更新
            if (_fileContextMenu != null)
            {
                _fileContextMenu.BackColor = Color.FromArgb(240, 240, 240);
                _fileContextMenu.ForeColor = Color.Black;
                _fileContextMenu.Renderer = new DarkMenuRenderer(false);
                
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

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(value);
        
        // Apply title bar theme when window becomes visible
        if (value && IsHandleCreated)
        {
            ApplyTitleBarTheme(_isDarkMode);
        }
    }
}

/// <summary>
/// Custom renderer for dark theme context menus
/// </summary>
public class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly bool _isDarkMode;

    public DarkMenuRenderer(bool isDarkMode = true) : base(new DarkColorTable(isDarkMode)) 
    {
        _isDarkMode = isDarkMode;
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var bgColor = _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240);
        var hoverColor = _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(220, 220, 220);

        if (e.Item.Selected)
        {
            // Highlight color when item is hovered
            using var brush = new SolidBrush(hoverColor);
            e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
        }
        else
        {
            // Normal background
            using var brush = new SolidBrush(bgColor);
            e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var separatorColor = _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(200, 200, 200);
        using var pen = new Pen(separatorColor);
        var rect = e.Item.ContentRectangle;
        e.Graphics.DrawLine(pen, rect.Left + 2, rect.Height / 2, rect.Right - 2, rect.Height / 2);
    }
}

/// <summary>
/// Dark color table for menu renderer
/// </summary>
public class DarkColorTable : ProfessionalColorTable
{
    private readonly bool _isDarkMode;

    public DarkColorTable(bool isDarkMode = true)
    {
        _isDarkMode = isDarkMode;
    }

    public override Color MenuBorder => _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(200, 200, 200);
    public override Color MenuItemBorder => _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(200, 200, 200);
    public override Color MenuItemSelected => _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(220, 220, 220);
    public override Color MenuStripGradientBegin => _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240);
    public override Color MenuStripGradientEnd => _isDarkMode ? Color.FromArgb(50, 50, 50) : Color.FromArgb(240, 240, 240);
    public override Color MenuItemSelectedGradientBegin => _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(220, 220, 220);
    public override Color MenuItemSelectedGradientEnd => _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(220, 220, 220);
    public override Color MenuItemPressedGradientBegin => _isDarkMode ? Color.FromArgb(60, 60, 60) : Color.FromArgb(200, 200, 200);
    public override Color MenuItemPressedGradientEnd => _isDarkMode ? Color.FromArgb(60, 60, 60) : Color.FromArgb(200, 200, 200);
}
