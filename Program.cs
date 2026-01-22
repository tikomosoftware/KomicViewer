namespace KomicViewer;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"アプリケーション起動エラー:\n{ex.Message}\n\nスタックトレース:\n{ex.StackTrace}", 
                "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}