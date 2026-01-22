#nullable enable
using System.ComponentModel;

namespace KomicViewer.Forms
{
    partial class HelpForm
    {
        private IContainer? components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            // 
            // HelpForm
            // 
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(600, 500);
            Name = "HelpForm";
            Text = "使い方";
            ResumeLayout(false);
        }
    }
}