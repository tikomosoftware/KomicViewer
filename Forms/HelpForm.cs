#nullable enable
using System;
using System.Drawing;
using System.Windows.Forms;
using KomicViewer.Services;

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

        Text = $"{LanguageManager.GetString("HelpTitle")} - Komic Viewer v{GetApplicationVersion()}";
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
            Text = LanguageManager.GetString("Close"),
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
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.1";
    }

    private static string GetHelpText()
    {
        var version = GetApplicationVersion();
        return LanguageManager.GetHelpText(version);
    }
}
