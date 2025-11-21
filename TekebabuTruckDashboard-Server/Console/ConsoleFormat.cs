using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public class ConsoleFormat
{
    public static int log_level = 1; // ログレベルの初期値

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    public enum LogLevel
    {
        DEBUG = 0,
        INFO = 1,
        WARNING = 2,
        ERROR = 3
    }

    [STAThread]
    public void Init()
    {
        AllocConsole();
        Info("Console Initialized.", nameof(ConsoleFormat));
    }

    [STAThread]
    public void SetLevel(LogLevel level)
    {
        if (level < LogLevel.DEBUG || level > LogLevel.ERROR)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Invalid log level specified.");
        }
        log_level = (int)level; // ログレベルを設定
    }

    [STAThread]
    public void Debug(string message, string class_name)
    {
        if (log_level > (int)LogLevel.DEBUG)
        {
            return; // ログレベルがDEBUGより高い場合は何もしない
        }

        if (string.IsNullOrEmpty(class_name))
        {
            class_name = "Unknown"; // クラス名が指定されていない場合のデフォルト値
        }

        // デバッグメッセージを出力（必要に応じて実装）
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[{class_name}][DEBUG] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
    }

    [STAThread]
    public void Info(string message, string class_name)
    {
        if (log_level > (int)LogLevel.INFO)
        {
            return; // ログレベルがINFOより高い場合は何もしない
        }

        if (string.IsNullOrEmpty(class_name))
        {
            class_name = "Unknown"; // クラス名が指定されていない場合のデフォルト値
        }

        // コンソールにメッセージを出力
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"[{class_name}][INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
    }

    [STAThread]
    public void Warning(string message, string class_name)
    {
        if (log_level > (int)LogLevel.WARNING)
        {
            return; // ログレベルがWARNINGより高い場合は何もしない
        }

        if (string.IsNullOrEmpty(class_name))
        {
            class_name = "Unknown"; // クラス名が指定されていない場合のデフォルト値
        }

        // コンソールに警告メッセージを出力
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{class_name}][WARNING] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
    }

    [STAThread]
    public void Error(string message, string class_name)
    {
        if (log_level > (int)LogLevel.ERROR)
        {
            return; // ログレベルがERRORより高い場合は何もしない
        }

        if (string.IsNullOrEmpty(class_name))
        {
            class_name = "Unknown"; // クラス名が指定されていない場合のデフォルト値
        }

        // コンソールにエラーメッセージを出力
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[{class_name}][ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
    }

    public void ResetColor()
    {
        // コンソールの色をリセット
        Console.ResetColor();
    }

    [STAThread]
    public void Close()
    {
        FreeConsole(); // コンソールを解放
    }
}
