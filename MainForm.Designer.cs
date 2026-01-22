#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace KomicViewer
{
    partial class MainForm
    {
        private IContainer? components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new Container();
            SuspendLayout();
            // 
            // MainForm
            // 
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1200, 800);
            this.Name = "MainForm";
            this.Text = "Komic Viewer";
            
            // Set application icon
            try
            {
                using var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("KomicViewer.Resources.app-icon.ico");
                if (iconStream != null)
                {
                    this.Icon = new Icon(iconStream);
                }
            }
            catch
            {
                // Ignore icon loading errors
            }
            
            ResumeLayout(false);
        }

        #endregion
    }
}
