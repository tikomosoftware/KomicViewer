using System.ComponentModel;

namespace KomicViewer.Controls;

/// <summary>
/// Custom panel for displaying comic pages with single/dual page support
/// </summary>
public sealed class ComicViewerPanel : Panel
{
    private Image? _leftPage;
    private Image? _rightPage;
    private ViewMode _viewMode = ViewMode.SinglePage;
    private FitMode _fitMode = FitMode.FitToWindow;
    private int _clickZoneWidth = 100; // クリック領域の幅（ピクセル）
    private bool _isRightToLeft = true; // 読み方向（true: 右開き, false: 左開き）
    private bool _showLeftArrow = false; // 左矢印表示フラグ
    private bool _showRightArrow = false; // 右矢印表示フラグ
    private System.Windows.Forms.Timer? _arrowHideTimer; // 矢印非表示タイマー
    private System.Windows.Forms.Timer? _arrowFadeTimer; // 矢印フェードタイマー
    private float _arrowOpacity = 0.0f; // 矢印の透明度（0.0-1.0）
    private bool _arrowFadingIn = false; // フェードイン中かどうか
    private bool _arrowFadingOut = false; // フェードアウト中かどうか

    // When true (default) and in DualPage mode, if either page image is landscape
    // (width > height) the panel will render a single page instead of a spread.
    [Browsable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(true)]
    public bool PreferSingleOnLandscape { get; set; } = true;

    public event EventHandler? ClickLeft;
    public event EventHandler? ClickRight;
    public event EventHandler? PageNext;
    public event EventHandler? PagePrevious;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ViewMode ViewMode
    {
        get => _viewMode;
        set
        {
            if (_viewMode != value)
            {
                _viewMode = value;
                Invalidate();
            }
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FitMode FitMode
    {
        get => _fitMode;
        set
        {
            if (_fitMode != value)
            {
                _fitMode = value;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// クリック領域の幅（ピクセル）。画面の左右端からこの幅の領域でのみページ送りが有効
    /// </summary>
    [Browsable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(100)]
    public int ClickZoneWidth
    {
        get => _clickZoneWidth;
        set => _clickZoneWidth = Math.Max(10, Math.Min(value, 300)); // 10-300ピクセルの範囲で制限
    }

    /// <summary>
    /// デバッグ用：クリック領域を視覚的に表示するかどうか
    /// </summary>
    [Browsable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(false)]
    public bool ShowClickZones { get; set; } = false;

    /// <summary>
    /// 読み方向（true: 右開き/日本式, false: 左開き/欧米式）
    /// </summary>
    [Browsable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(true)]
    public bool IsRightToLeft
    {
        get => _isRightToLeft;
        set
        {
            if (_isRightToLeft != value)
            {
                _isRightToLeft = value;
                Invalidate(); // 読み方向が変わったら再描画
            }
        }
    }

    public ComicViewerPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | 
                 ControlStyles.UserPaint | 
                 ControlStyles.OptimizedDoubleBuffer, true);
        // Make panel focusable so it can receive mouse wheel events when hovered
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.FromArgb(30, 30, 30);
        
        // 矢印非表示タイマーの初期化
        _arrowHideTimer = new System.Windows.Forms.Timer
        {
            Interval = 2000 // 2秒
        };
        _arrowHideTimer.Tick += ArrowHideTimer_Tick;
        
        // 矢印フェードタイマーの初期化
        _arrowFadeTimer = new System.Windows.Forms.Timer
        {
            Interval = 16 // 約60FPS
        };
        _arrowFadeTimer.Tick += ArrowFadeTimer_Tick;
    }

    private readonly object _pageLock = new object();
    
    public void SetPages(Image? currentPage, Image? nextPage = null)
    {
        lock (_pageLock)
        {
            // 古い画像を保持
            var oldLeftPage = _leftPage;
            var oldRightPage = _rightPage;
            
            // 新しい画像を設定
            _leftPage = currentPage;
            _rightPage = nextPage;
            
            // 再描画を要求
            Invalidate();
            
            // 画像が変更された場合のみ古い画像を破棄
            if (oldLeftPage != null && oldLeftPage != currentPage && oldLeftPage != nextPage)
            {
                oldLeftPage.Dispose();
            }
            if (oldRightPage != null && oldRightPage != currentPage && oldRightPage != nextPage)
            {
                oldRightPage.Dispose();
            }
        }
    }

    public void ClearPages()
    {
        SetPages(null, null);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        var g = e.Graphics;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // スレッドセーフにページ画像を取得
        Image? leftPage, rightPage;
        lock (_pageLock)
        {
            leftPage = _leftPage;
            rightPage = _rightPage;
        }

        if (_viewMode == ViewMode.SinglePage)
        {
            DrawSinglePage(g, leftPage); // leftPageは実際には現在のページ
        }
        else
        {
            DrawDualPage(g, leftPage, rightPage); // 現在のページ、次のページ
        }

        // デバッグ用：クリック領域を表示
        if (ShowClickZones)
        {
            DrawClickZones(g);
        }
        
        // 矢印を表示
        DrawNavigationArrows(g);
    }

    private void ArrowHideTimer_Tick(object? sender, EventArgs e)
    {
        _arrowHideTimer?.Stop();
        StartArrowFadeOut();
    }

    private void ArrowFadeTimer_Tick(object? sender, EventArgs e)
    {
        const float fadeSpeed = 0.08f; // フェード速度
        
        if (_arrowFadingIn)
        {
            _arrowOpacity += fadeSpeed;
            if (_arrowOpacity >= 1.0f)
            {
                _arrowOpacity = 1.0f;
                _arrowFadingIn = false;
                _arrowFadeTimer?.Stop();
            }
        }
        else if (_arrowFadingOut)
        {
            _arrowOpacity -= fadeSpeed;
            if (_arrowOpacity <= 0.0f)
            {
                _arrowOpacity = 0.0f;
                _arrowFadingOut = false;
                _showLeftArrow = false;
                _showRightArrow = false;
                _arrowFadeTimer?.Stop();
            }
        }
        
        Invalidate();
    }

    private void StartArrowFadeIn()
    {
        _arrowFadingIn = true;
        _arrowFadingOut = false;
        _arrowFadeTimer?.Start();
    }

    private void StartArrowFadeOut()
    {
        _arrowFadingIn = false;
        _arrowFadingOut = true;
        _arrowFadeTimer?.Start();
    }

    private void DrawNavigationArrows(Graphics g)
    {
        if ((!_showLeftArrow && !_showRightArrow) || _arrowOpacity <= 0.0f) return;

        var clientWidth = ClientRectangle.Width;
        var clientHeight = ClientRectangle.Height;
        
        // 矢印のサイズと位置（小さくした）
        var arrowSize = 40; // 60から40に変更
        var arrowY = (clientHeight - arrowSize) / 2;
        
        // ブルー系の半透明色（透明度をアニメーション）
        var alpha = (int)(120 * _arrowOpacity); // 最大透明度120
        var fillAlpha = (int)(100 * _arrowOpacity); // 塗りつぶしは少し薄く
        
        using var arrowBrush = new SolidBrush(Color.FromArgb(fillAlpha, 64, 128, 255)); // ブルー系
        using var arrowPen = new Pen(Color.FromArgb(alpha, 32, 96, 255), 2); // 輪郭線も少し細く
        
        // 左矢印
        if (_showLeftArrow)
        {
            var leftArrowX = _clickZoneWidth / 2 - arrowSize / 2;
            DrawArrow(g, arrowBrush, arrowPen, leftArrowX, arrowY, arrowSize, true);
        }
        
        // 右矢印
        if (_showRightArrow)
        {
            var rightArrowX = clientWidth - _clickZoneWidth / 2 - arrowSize / 2;
            DrawArrow(g, arrowBrush, arrowPen, rightArrowX, arrowY, arrowSize, false);
        }
    }

    private void DrawArrow(Graphics g, Brush brush, Pen pen, int x, int y, int size, bool pointsLeft)
    {
        // 矢印の形状を作成
        var points = new Point[3];
        
        if (pointsLeft)
        {
            // 左向き矢印 ◀
            points[0] = new Point(x, y + size / 2);           // 左の尖った部分
            points[1] = new Point(x + size, y);               // 右上
            points[2] = new Point(x + size, y + size);        // 右下
        }
        else
        {
            // 右向き矢印 ▶
            points[0] = new Point(x + size, y + size / 2);    // 右の尖った部分
            points[1] = new Point(x, y);                      // 左上
            points[2] = new Point(x, y + size);               // 左下
        }
        
        // 矢印を描画
        g.FillPolygon(brush, points);
        g.DrawPolygon(pen, points);
    }

    private void DrawClickZones(Graphics g)
    {
        var clientWidth = ClientRectangle.Width;
        var clientHeight = ClientRectangle.Height;
        
        // 半透明の色でクリック領域を表示
        using var leftBrush = new SolidBrush(Color.FromArgb(50, 255, 0, 0)); // 赤色半透明
        using var rightBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 255)); // 青色半透明
        
        var leftZone = new Rectangle(0, 0, _clickZoneWidth, clientHeight);
        var rightZone = new Rectangle(clientWidth - _clickZoneWidth, 0, _clickZoneWidth, clientHeight);
        
        g.FillRectangle(leftBrush, leftZone);
        g.FillRectangle(rightBrush, rightZone);
        
        // 境界線を描画
        using var pen = new Pen(Color.White, 1);
        g.DrawRectangle(pen, leftZone);
        g.DrawRectangle(pen, rightZone);
    }

    private void DrawSinglePage(Graphics g, Image? image)
    {
        if (image == null) return;

        var rect = CalculateFitRect(image.Size, ClientRectangle);
        g.DrawImage(image, rect);
    }

    private void DrawDualPage(Graphics g, Image? currentPage, Image? nextPage)
    {
        // デバッグ用ログ
        Console.WriteLine($"DrawDualPage: 読み方向={(_isRightToLeft ? "右開き" : "左開き")}, 現在ページ={currentPage != null}, 次ページ={nextPage != null}");
        
        // 読み方向に応じてページの配置を決定
        // currentPage = 現在のページ, nextPage = 次のページ
        
        if (currentPage != null && nextPage != null)
        {
            var client = ClientRectangle;
            var currentScaled = ScaleToHeight(currentPage.Size, client.Height);
            var nextScaled = ScaleToHeight(nextPage.Size, client.Height);

            var totalWidth = currentScaled.Width + nextScaled.Width;

            // If they don't fit horizontally, scale both down proportionally
            if (totalWidth > client.Width)
            {
                var scale = (double)client.Width / totalWidth;
                currentScaled = ScaleSize(currentScaled, scale);
                nextScaled = ScaleSize(nextScaled, scale);
                totalWidth = currentScaled.Width + nextScaled.Width;
            }

            // Center the combined pair
            var startX = (client.Width - totalWidth) / 2 + client.X;
            
            Rectangle currentRect, nextRect;
            
            if (_isRightToLeft)
            {
                // 右開き: 右側に現在ページ、左側に次ページ
                currentRect = new Rectangle(startX + nextScaled.Width, (client.Height - currentScaled.Height) / 2 + client.Y, currentScaled.Width, currentScaled.Height);
                nextRect = new Rectangle(startX, (client.Height - nextScaled.Height) / 2 + client.Y, nextScaled.Width, nextScaled.Height);
                Console.WriteLine("右開き: 現在ページを右側、次ページを左側に配置");
            }
            else
            {
                // 左開き: 左側に現在ページ、右側に次ページ
                currentRect = new Rectangle(startX, (client.Height - currentScaled.Height) / 2 + client.Y, currentScaled.Width, currentScaled.Height);
                nextRect = new Rectangle(startX + currentScaled.Width, (client.Height - nextScaled.Height) / 2 + client.Y, nextScaled.Width, nextScaled.Height);
                Console.WriteLine("左開き: 現在ページを左側、次ページを右側に配置");
            }

            g.DrawImage(currentPage, currentRect);
            g.DrawImage(nextPage, nextRect);
            return;
        }

        // 片方のページのみの場合の処理
        if (currentPage != null)
        {
            Rectangle area;
            if (_isRightToLeft)
            {
                // 右開き: 現在ページを右側に表示
                area = new Rectangle(ClientRectangle.Width / 2, 0, ClientRectangle.Width / 2, ClientRectangle.Height);
            }
            else
            {
                // 左開き: 現在ページを左側に表示
                area = new Rectangle(0, 0, ClientRectangle.Width / 2, ClientRectangle.Height);
            }
            var rect = CalculateFitRect(currentPage.Size, area);
            g.DrawImage(currentPage, rect);
        }
        
        if (nextPage != null)
        {
            Rectangle area;
            if (_isRightToLeft)
            {
                // 右開き: 次ページを左側に表示
                area = new Rectangle(0, 0, ClientRectangle.Width / 2, ClientRectangle.Height);
            }
            else
            {
                // 左開き: 次ページを右側に表示
                area = new Rectangle(ClientRectangle.Width / 2, 0, ClientRectangle.Width / 2, ClientRectangle.Height);
            }
            var rect = CalculateFitRect(nextPage.Size, area);
            g.DrawImage(nextPage, rect);
        }
    }

    private Rectangle CalculateFitRect(Size imageSize, Rectangle container)
    {
        float scale;
        
        if (_fitMode == FitMode.FitToWidth)
        {
            scale = (float)container.Width / imageSize.Width;
        }
        else if (_fitMode == FitMode.FitToHeight)
        {
            scale = (float)container.Height / imageSize.Height;
        }
        else // FitToWindow
        {
            var scaleX = (float)container.Width / imageSize.Width;
            var scaleY = (float)container.Height / imageSize.Height;
            scale = Math.Min(scaleX, scaleY);
        }

        var newWidth = (int)(imageSize.Width * scale);
        var newHeight = (int)(imageSize.Height * scale);
        var x = container.X + (container.Width - newWidth) / 2;
        var y = container.Y + (container.Height - newHeight) / 2;

        return new Rectangle(x, y, newWidth, newHeight);
    }

    private static Size ScaleToHeight(Size original, int targetHeight)
    {
        if (original.Height == 0) return new Size(0, 0);
        var scale = (double)targetHeight / original.Height;
        return new Size(Math.Max(1, (int)(original.Width * scale)), Math.Max(1, (int)(original.Height * scale)));
    }

    private static Size ScaleSize(Size original, double factor)
    {
        return new Size(Math.Max(1, (int)(original.Width * factor)), Math.Max(1, (int)(original.Height * factor)));
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        var clientWidth = ClientRectangle.Width;
        var clientHeight = ClientRectangle.Height;
        
        // クリック領域を定義
        var leftZone = new Rectangle(0, 0, _clickZoneWidth, clientHeight);
        var rightZone = new Rectangle(clientWidth - _clickZoneWidth, 0, _clickZoneWidth, clientHeight);
        
        // 左側のクリック領域
        if (leftZone.Contains(e.Location))
        {
            ClickLeft?.Invoke(this, EventArgs.Empty);
        }
        // 右側のクリック領域
        else if (rightZone.Contains(e.Location))
        {
            ClickRight?.Invoke(this, EventArgs.Empty);
        }
        // 中央部分はクリックしても何もしない（画像の詳細を見たい場合など）
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var clientWidth = ClientRectangle.Width;
        var clientHeight = ClientRectangle.Height;
        
        // クリック領域を定義
        var leftZone = new Rectangle(0, 0, _clickZoneWidth, clientHeight);
        var rightZone = new Rectangle(clientWidth - _clickZoneWidth, 0, _clickZoneWidth, clientHeight);
        
        var wasShowingArrows = _showLeftArrow || _showRightArrow;
        var newShowLeftArrow = leftZone.Contains(e.Location);
        var newShowRightArrow = rightZone.Contains(e.Location);
        
        // 矢印表示状態が変わった場合
        if (newShowLeftArrow != _showLeftArrow || newShowRightArrow != _showRightArrow)
        {
            _showLeftArrow = newShowLeftArrow;
            _showRightArrow = newShowRightArrow;
            
            if (_showLeftArrow || _showRightArrow)
            {
                // 矢印を表示する場合はフェードイン
                if (_arrowOpacity < 1.0f)
                {
                    StartArrowFadeIn();
                }
                _arrowHideTimer?.Stop();
                _arrowHideTimer?.Start();
            }
            else
            {
                // 矢印を非表示にする場合はフェードアウト
                _arrowHideTimer?.Stop();
                StartArrowFadeOut();
            }
        }
        else if (_showLeftArrow || _showRightArrow)
        {
            // 同じ領域内でマウスが動いている場合はタイマーをリスタート
            _arrowHideTimer?.Stop();
            _arrowHideTimer?.Start();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        
        // マウスがコントロールから離れたら矢印をフェードアウト
        if (_showLeftArrow || _showRightArrow)
        {
            _arrowHideTimer?.Stop();
            StartArrowFadeOut();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        // Ensure we have focus to receive mouse wheel events when cursor is over the panel
        try { Focus(); } catch { }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (e.Delta < 0)
            PageNext?.Invoke(this, EventArgs.Empty);
        else if (e.Delta > 0)
            PagePrevious?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _arrowHideTimer?.Stop();
            _arrowHideTimer?.Dispose();
            _arrowFadeTimer?.Stop();
            _arrowFadeTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

public enum ViewMode
{
    SinglePage,
    DualPage
}

public enum FitMode
{
    FitToWindow,
    FitToWidth,
    FitToHeight
}

public enum SliderDisplayMode
{
    Hidden,        // 非表示
    AlwaysVisible, // 常時表示
    AutoHide       // 自動非表示（マウスホバー時のみ表示）
}

public class SliderSettings
{
    public SliderDisplayMode DisplayMode { get; set; } = SliderDisplayMode.AutoHide;
    public bool ShowInNormalMode { get; set; } = true;
    public bool ShowInFullScreenMode { get; set; } = true;
    public int AutoHideDelay { get; set; } = 2000; // ミリ秒
}
