#nullable enable
using System;
using System.Drawing;
using System.Windows.Forms;

namespace KomicViewer.Forms;

public partial class HelpForm : Form
{
    private readonly bool _isDarkMode;

    public HelpForm(bool isDarkMode = true)
    {
        _isDarkMode = isDarkMode;
        InitializeComponent();
        SetupContent();
    }

    private void SetupContent()
    {
        // Theme colors
        var bgColor = _isDarkMode ? Color.FromArgb(25, 25, 25) : Color.FromArgb(250, 250, 250);
        var textColor = _isDarkMode ? Color.White : Color.Black;
        var panelColor = _isDarkMode ? Color.FromArgb(35, 35, 35) : Color.FromArgb(240, 240, 240);
        var buttonColor = _isDarkMode ? Color.FromArgb(60, 60, 60) : Color.FromArgb(220, 220, 220);

        BackColor = bgColor;
        ForeColor = textColor;

        Text = $"使い方 - Komic Viewer v{GetApplicationVersion()}";
        Size = new Size(650, 550); // サイズを少し大きく
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20),
            BackColor = bgColor
        };

        var content = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = panelColor,
            ForeColor = textColor,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            Font = new Font("Yu Gothic UI", 10F),
            Text = GetHelpText()
        };

        panel.Controls.Add(content);
        Controls.Add(panel);

        // Close button
        var closeButton = new Button
        {
            Text = "閉じる",
            Size = new Size(80, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(Width - 100, Height - 60),
            BackColor = buttonColor,
            ForeColor = textColor,
            FlatStyle = FlatStyle.Flat
        };
        closeButton.Click += (_, _) => Close();
        Controls.Add(closeButton);
    }

    private static string GetApplicationVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.2";
    }

    private static string GetHelpText()
    {
        var version = GetApplicationVersion();
        return $@"Komic Viewer v{version}
軽量で使いやすいコミック・マンガビューアー

■ ファイル操作
・ファイルを開く: Ctrl + O
・ファイルを閉じる: Ctrl + W
・アプリケーション終了: Alt + F4
・ドラッグ&ドロップでファイルを開く

■ ページナビゲーション
・次のページ: → ↓ Space PageDown
・前のページ: ← ↑ PageUp
・最初のページ: Home
・最後のページ: End
・マウスクリック: 画面の左右端（100px幅）をクリックでページ送り
・マウスホイール: 上で前のページ、下で次のページ
・ツールバースクロールバー: 上部ツールバーのスクロールバーで任意のページにジャンプ

■ 表示モード
・単ページモード: 1キー
・見開きモード: 2キー
・ツールバーのボタンでも切り替え可能

■ フルスクリーン
・フルスクリーン切り替え: F11
・フルスクリーン終了: Esc または フルスクリーン終了ボタン
・フルスクリーン時: マウスを画面上部に移動でツールバー表示
・ツールバースクロールバー: フルスクリーン時もツールバーのスクロールバーでページナビゲーション可能
・フルスクリーン終了ボタン: ツールバー右端の「元のサイズ」ボタン

■ 読み方向（見開きモード時のみ）
・右開き/左開きの切り替え: ツールバーのボタン
・右開き: 右側に現在ページ、左側に次ページを表示
・左開き: 左側に現在ページ、右側に次ページを表示
・マウスクリック動作も読み方向に対応

■ 見開き調整（見開きモード時のみ）
・ページ調整ボタン: ◁ ▷ で1ページずつ調整
・右開き時: ◁=次ページ、▷=前ページ
・左開き時: ◁=前ページ、▷=次ページ
・キーボードショートカット: Shift + ← →

■ テーマ
・ライト/ダークテーマ切り替え: ツールバーのテーマボタン
・タイトルバーの色も連動して変更
・設定は自動保存されます

■ ウィンドウ表示
・最前面表示: ツールバーの最前面ボタンで他のウィンドウより前に表示
・設定は自動保存されます

■ 対応ファイル形式
・ZIP (.zip, .cbz)
・RAR (.rar, .cbr)
・対応画像: JPEG, PNG, GIF, BMP, WebP

■ その他の機能
・横長画像の自動単ページ表示
・高品質な画像拡大縮小
・画面中央での起動
・クリック領域の制限（中央部分はクリック無効）

■ 更新履歴 (v1.0.2)
・ツールバースクロールバーを追加（任意のページに素早くジャンプ）
・下部スライダーを廃止してUIをシンプル化
・最前面表示時のダイアログ表示問題を解決
・描画崩れ問題を根本的に解決

---
© 2026 tikomo software";
    }
}