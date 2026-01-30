namespace KomicViewer.Services;

public enum Language
{
    Japanese,
    English
}

public static class LanguageManager
{
    private static Language _currentLanguage = Language.Japanese;

    public static Language CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            _currentLanguage = value;
            OnLanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? OnLanguageChanged;

    public static string GetString(string key)
    {
        return _currentLanguage switch
        {
            Language.English => EnglishStrings.GetValueOrDefault(key, key),
            _ => JapaneseStrings.GetValueOrDefault(key, key)
        };
    }

    private static readonly Dictionary<string, string> JapaneseStrings = new()
    {
        ["AppTitle"] = "Komic Viewer",
        ["MenuFile"] = "ファイル",
        ["MenuOpen"] = "開く...",
        ["MenuClose"] = "閉じる",
        ["MenuExit"] = "終了",
        ["ViewSingle"] = "単ページ",
        ["ViewDual"] = "見開き",
        ["Fullscreen"] = "フルスクリーン",
        ["NormalSize"] = "元のサイズ",
        ["RightToLeft"] = "右開き",
        ["LeftToRight"] = "左開き",
        ["Help"] = "使い方",
        ["ThemeDark"] = "ダークテーマ",
        ["ThemeLight"] = "ライトテーマ",
        ["TopMost"] = "最前面 (ON)",
        ["NormalWindow"] = "最前面 (OFF)",
        ["Next"] = "次へ",
        ["Prev"] = "前へ",
        ["OpenFileDialogTitle"] = "コミックファイルを開く",
        ["ArchiveFilter"] = "アーカイブファイル|*.zip;*.rar;*.cbz;*.cbr|ZIPファイル|*.zip;*.cbz|RARファイル|*.rar;*.cbr|すべてのファイル|*.*",
        ["NoImagesFound"] = "画像が見つかりませんでした。",
        ["Error"] = "エラー",
        ["CouldNotOpenFile"] = "ファイルを開けませんでした:",
        ["TooltipToggleView"] = "単ページ / 見開き を切り替え",
        ["TooltipDirection"] = "表示方向: 右開き / 左開き を切り替え",
        ["TooltipFullScreen"] = "フルスクリーン表示",
        ["TooltipExitFullScreen"] = "フルスクリーンを終了",
        ["TooltipHelp"] = "使い方を表示",
        ["TooltipTheme"] = "ライト/ダークテーマ切り替え",
        ["TooltipTopMost"] = "ウィンドウを最前面に表示",
        ["AdjustForward"] = "1ページ送る（見開き調整用）",
        ["AdjustBackward"] = "1ページ戻す（見開き調整用）",
        ["HelpTitle"] = "使い方",
        ["Close"] = "閉じる",
        ["Language"] = "言語 (Language)",
        ["Japanese"] = "日本語 (Japanese)",
        ["English"] = "英語 (English)"
    };

    private static readonly Dictionary<string, string> EnglishStrings = new()
    {
        ["AppTitle"] = "Komic Viewer",
        ["MenuFile"] = "File",
        ["MenuOpen"] = "Open...",
        ["MenuClose"] = "Close",
        ["MenuExit"] = "Exit",
        ["ViewSingle"] = "Single Page",
        ["ViewDual"] = "Dual Page",
        ["Fullscreen"] = "Fullscreen",
        ["NormalSize"] = "Restore Size",
        ["RightToLeft"] = "R to L",
        ["LeftToRight"] = "L to R",
        ["Help"] = "Help",
        ["ThemeDark"] = "Dark Theme",
        ["ThemeLight"] = "Light Theme",
        ["TopMost"] = "Top Most (ON)",
        ["NormalWindow"] = "Top Most (OFF)",
        ["Next"] = "Next",
        ["Prev"] = "Prev",
        ["OpenFileDialogTitle"] = "Open Comic File",
        ["ArchiveFilter"] = "Archive|*.zip;*.rar;*.cbz;*.cbr|ZIP|*.zip;*.cbz|RAR|*.rar;*.cbr|All Files|*.*",
        ["NoImagesFound"] = "No images found.",
        ["Error"] = "Error",
        ["CouldNotOpenFile"] = "Could not open file:",
        ["TooltipToggleView"] = "Toggle Single/Dual Page View",
        ["TooltipDirection"] = "Toggle Reading Direction",
        ["TooltipFullScreen"] = "Fullscreen View",
        ["TooltipExitFullScreen"] = "Exit Fullscreen",
        ["TooltipHelp"] = "Show Help and Help",
        ["TooltipTheme"] = "Toggle Theme",
        ["TooltipTopMost"] = "Always on Top",
        ["AdjustForward"] = "Forward 1 Page (Adjustment)",
        ["AdjustBackward"] = "Backward 1 Page (Adjustment)",
        ["HelpTitle"] = "Help",
        ["Close"] = "Close",
        ["Language"] = "Language",
        ["Japanese"] = "Japanese",
        ["English"] = "English"
    };

    public static string GetHelpText(string version)
    {
        if (_currentLanguage == Language.English)
        {
            return @$"Komic Viewer v{version}
Lightweight and easy-to-use comic/manga viewer

[File Operations]
- Open file: Ctrl + O
- Close file: Ctrl + W
- Exit application: Alt + F4
- Drag & Drop to open files

[Page Navigation]
- Next page: Right Arrow, Down Arrow, Space, PageDown
- Previous page: Left Arrow, Up Arrow, PageUp
- First page: Home
- Last page: End
- Mouse click: Click screen edges (100px width) to turn pages
- Mouse wheel: Up for previous, Down for next
- Toolbar slider: Jump to any page using the slider in the top toolbar

[View Mode]
- Single page mode: '1' key
- Dual page mode: '2' key
- Also switchable via toolbar buttons

[Fullscreen]
- Toggle fullscreen: F11
- Exit fullscreen: Esc or Restore Size button
- When in fullscreen: Move mouse to top of screen to show toolbar

[Reading Direction] (Dual page mode only)
- Change R to L / L to R: Toolbar button
- Right to Left: Current page on right, next page on left (Japanese style)
- Left to Right: Current page on left, next page on right (Western style)

[Dual Page Adjustment] (Dual page mode only)
- 1-page adjustment: [ < ] [ > ] buttons
- Keyboard shortcut: Shift + Left/Right Arrow

[Theme]
- Light/Dark theme: Toolbar theme button
- Settings are saved automatically

[Window Settings]
- Always on Top: Toolbar button to stay above other windows
- Settings are saved automatically

[Supported Formats]
- ZIP (.zip, .cbz), RAR (.rar, .cbr)
- Images: JPEG, PNG, GIF, BMP, WebP

---
(C) 2026 tikomo software";
        }
        else
        {
            return @$"Komic Viewer v{version}
軽量で使いやすいコミック・マンガビューアー

【ファイル操作】
・ファイルを開く: Ctrl + O
・ファイルを閉じる: Ctrl + W
・アプリケーション終了: Alt + F4
・ドラッグ&ドロップでファイルを開く

【ページナビゲーション】
・次のページ: → ↓ Space PageDown
・前のページ: ← ↑ PageUp
・最初のページ: Home
・最後のページ: End
・マウスクリック: 画面の左右端（100px幅）をクリックでページ送り
・マウスホイール: 上で前のページ、下で次のページ
・ツールバースライダー: 上部ツールバーのナビゲーションで任意のページにジャンプ

【表示モード】
・単ページモード: 1キー
・見開きモード: 2キー
・ツールバーのボタンでも切り替え可能

【フルスクリーン】
・フルスクリーン切り替え: F11
・フルスクリーン終了: Esc または 元のサイズボタン
・フルスクリーン時: マウスを画面上部に移動でツールバー表示

【読み方向】（見開きモード時のみ）
・右開き/左開きの切り替え: ツールバーのボタン
・右開き: 右側に現在ページ、左側に次ページを表示
・左開き: 左側に現在ページ、右側に次ページを表示

【見開き調整】（見開きモード時のみ）
・ページ調整ボタン: [ < ] [ > ] で1ページずつ調整
・キーボードショートカット: Shift + ← →

【テーマ】
・ライト/ダークテーマ切り替え: ツールバーのテーマボタン
・設定は自動保存されます

【ウィンドウ表示】
・最前面表示: ツールバーのボタンで他のウィンドウより前に表示
・設定は自動保存されます

【対応ファイル形式】
・ZIP (.zip, .cbz)
・RAR (.rar, .cbr)
・対応画像: JPEG, PNG, GIF, BMP, WebP

---
(C) 2026 tikomo software";
        }
    }
}
