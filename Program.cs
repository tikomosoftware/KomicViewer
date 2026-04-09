using System.Runtime.InteropServices;
using System.Reflection;
using LibHeifSharp;

namespace KomicViewer;

static class Program
{
    private static bool _isHeifAvailable = false;

    [STAThread]
    static void Main()
    {
        // Setup global exception handling for better diagnostics
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        
        Application.ThreadException += (s, e) => {
            LogFatalError(e.Exception, "ThreadException");
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            if (e.ExceptionObject is Exception ex)
                LogFatalError(ex, "AppDomain.UnhandledException");
            else
                LogFatalError(new Exception(e.ExceptionObject?.ToString() ?? "Unknown"), "AppDomain.UnhandledException");
        };

        // Setup Native DLL Resolver for LibHeifSharp
        NativeLibrary.SetDllImportResolver(typeof(HeifContext).Assembly, (libraryName, assembly, searchPath) =>
        {
            if (libraryName == "libheif")
            {
                // Try several common locations for the native DLL
                string[] paths = {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libheif.dll"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", "win-x64", "native", "libheif.dll"),
                    "libheif.dll"
                };

                foreach (var path in paths)
                {
                    if (NativeLibrary.TryLoad(path, out var handle))
                        return handle;
                }
            }
            return IntPtr.Zero;
        });

        try
        {
            ApplicationConfiguration.Initialize();
            
            // Pre-check for LibHeif availability to avoid crash later
            CheckHeifAvailability();

            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            LogFatalError(ex, "Main.Catch");
        }
    }

    private static void CheckHeifAvailability()
    {
        try
        {
            var version = LibHeifSharp.LibHeifInfo.Version;
            _isHeifAvailable = true;
        }
        catch (Exception ex)
        {
            _isHeifAvailable = false;
            var msg = "【警告】AVIFデコードライブラリ(libheif)の読み込みに失敗しました。\n" +
                      "AVIFファイルの表示はできませんが、アプリは起動します。\n\n" +
                      "原因: " + ex.Message;
            MessageBox.Show(msg, "Library Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public static bool IsHeifAvailable => _isHeifAvailable;

    private static void LogFatalError(Exception ex, string source)
    {
        var message = $"【致命的エラー ({source})】\n{ex.Message}\n\n【詳細】\n{ex}";
        Console.Error.WriteLine(message);
        MessageBox.Show(message, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}