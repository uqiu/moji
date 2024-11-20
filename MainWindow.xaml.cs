﻿using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;  // 添加这个引用
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;  // 添加这行
using System.Windows.Interop;
using Microsoft.Extensions.Configuration;
using System.Windows.Forms; // Windows Forms 组件
using System.Drawing; // 系统图标
using MessageBox = System.Windows.MessageBox;  // 添加在文件开头的 using 部分
using WinFormsApplication = System.Windows.Forms.Application;
using WpfApplication = System.Windows.Application;
using System.Collections.Generic;  // 添加 Dictionary 支持
using Serilog;

namespace moji
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9000;
        private HwndSource? _source;  // 添加字段保存 source 引用
        private readonly IConfiguration _configuration;
        public NotifyIcon? notifyIcon;  // 改为 public
        private readonly ILogger _logger;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                InitializeNotifyIcon(); // 添加初始化托盘图标
                this.Deactivated += MainWindow_Deactivated; // 添加失去焦点事件处理
                this.Hide(); // 启动时隐藏主窗口

                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                _configuration = builder.Build();

                // 配置日志
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File("logs/moji_.log", 
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();
                    
                _logger = Log.Logger;
                _logger.Information("应用程序启动");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初始化时发生错误: {ex.Message}");
                System.Windows.Application.Current.Shutdown();
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.ShowInTaskbar = false; // 在任务栏不显示图标
            
            // 确保先注销已存在的热键
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            
            RegisterHotKey(); // 在这里注册热键，此时窗口句柄已经创建
        }

        private void InitializeNotifyIcon()
        {
            notifyIcon = new NotifyIcon();
            try 
            {
                // 尝试从资源加载图标
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                notifyIcon.Icon = SystemIcons.Application;
            }
            
            notifyIcon.Visible = true;
            notifyIcon.Text = "Moji Dict"; // 鼠标悬停时显示的提示文本

            // 创建右键菜单
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            var showItem = new System.Windows.Forms.ToolStripMenuItem("显示");
            var exitItem = new System.Windows.Forms.ToolStripMenuItem("退出");

            showItem.Click += (s, e) => 
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            };
            exitItem.Click += (s, e) => 
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                WpfApplication.Current.Shutdown();
            };

            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(exitItem);
            notifyIcon.ContextMenuStrip = contextMenu;

            // 双击托盘图标显���窗口
            notifyIcon.MouseDoubleClick += (s, e) => 
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            };
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;  // 取消关闭操作
            this.Hide();      // 仅隐藏窗口
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            this.Hide(); // 窗口失去焦点时隐藏
        }

        private void RegisterHotKey()
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                _source = HwndSource.FromHwnd(helper.Handle);
                _source?.AddHook(HwndHook);

                const uint MOD_ALT = 0x0001;
                const uint VK_L = 0x4C;

                // 尝试注册热键
                bool success = RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_ALT, VK_L);
                
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    MessageBox.Show($"注册热键失败: 错误代码 {error}\n请确保没有其他程序占用了 ALT+L 快捷键");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"注册热键时发生错误: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 确保在关闭时正确清理
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source = null;
            }

            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            
            _logger.Information("应用程序关闭");
            Log.CloseAndFlush();
            base.OnClosed(e);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                const int WM_HOTKEY = 0x0312;
                if (msg == WM_HOTKEY)
                {
                    int id = wParam.ToInt32();
                    if (id == HOTKEY_ID)
                    {
                        WpfApplication.Current.Dispatcher.Invoke(() =>
                        {
                            ShowClipboardContent();
                        });
                        handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                WpfApplication.Current.Dispatcher.Invoke(() =>
                {
                    ResultTextBox.Document = new FlowDocument(new Paragraph(
                        new Run($"发生错误: {ex.Message}")
                    ));
                    Show();
                    Activate();
                });
            }
            return IntPtr.Zero;
        }

        private async void ShowClipboardContent()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    
                    ResultTextBox.Document = new FlowDocument(new Paragraph(
                        new Run("正在查询...")
                    ));
                    var clipboardText = System.Windows.Clipboard.GetText();
                    await SendHttpRequest(clipboardText);
                }
                else
                {
                    ResultTextBox.Document = new FlowDocument(new Paragraph(
                        new Run("剪切板没有文本内容")
                    ));
                    Show();
                    Activate();
                }
                Topmost = true;
            }
            catch (Exception ex)
            {
                ResultTextBox.Document = new FlowDocument(new Paragraph(
                    new Run($"处理剪贴板内容时发生错误: {ex.Message}")
                ));
                Show();
                Activate();
            }
        }

        private async Task SendHttpRequest(string clipboardText)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    const string url = "https://api.mojidict.com/parse/functions/search-all";

                    // 设置所有请求头
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("accept", "application/json, text/plain, */*");
                    client.DefaultRequestHeaders.Add("accept-language", "zh-CN,zh;q=0.9,en;q=0.8,zh-TW;q=0.7,ja;q=0.6");
                    client.DefaultRequestHeaders.Add("origin", "chrome-extension://edoiodnmpjehmemkkfmnefmkboeaahlf");
                    client.DefaultRequestHeaders.Add("priority", "u=1, i");
                    client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
                    client.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
                    client.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
                    client.DefaultRequestHeaders.Add("sec-fetch-site", "none");
                    client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

                    // 构建请求体
                    var requestBody = new
                    {
                        g_os = "webExtension",
                        g_ver = "v4.7.6.20240313",
                        _InstallationId = "7d959a18-48c4-243c-7486-632147466544",
                        _ClientVersion = "js3.4.1",
                        _ApplicationId = "E62VyFVLMiW7kvbtVq3p",
                        types = new[] { 102 },
                        text = clipboardText
                    };

                    // 序列化请求体
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
                    var requestContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    requestContent.Headers.ContentType.CharSet = "UTF-8";



                    // 记录 CURL 命令
                    _logger.Information("发送请求\nCURL Command:\n{CurlCommand}", 
                        GenerateCurlCommand(url, client.DefaultRequestHeaders, jsonContent));

                    var response = await client.PostAsync(url, requestContent);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // 记录响应详情
                    _logger.Information("收到响应\n状态码: {StatusCode}\n响应内容: {Response}", 
                        response.StatusCode, responseContent);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = System.Text.Json.JsonDocument.Parse(responseContent);
                        var searchResults = jsonResponse.RootElement
                            .GetProperty("result")
                            .GetProperty("result")
                            .GetProperty("word")
                            .GetProperty("searchResult");

                        var flowDoc = new FlowDocument();
                        
                        // 添加查询内容
                        var queryPara = new Paragraph(new Run($"查询内容: {clipboardText}") { FontWeight = FontWeights.Bold });
                        queryPara.Margin = new Thickness(0, 0, 0, 10);
                        flowDoc.Blocks.Add(queryPara);

                        foreach (var result in searchResults.EnumerateArray())
                        {
                            var title = result.GetProperty("title").GetString();
                            var excerpt = result.GetProperty("excerpt").GetString();

                            var para = new Paragraph();
                            para.Inlines.Add(new Run(title) { FontWeight = FontWeights.Bold });
                            para.Inlines.Add(new LineBreak());
                            para.Inlines.Add(new Run(excerpt));
                            para.Margin = new Thickness(0, 0, 0, 10);
                            
                            flowDoc.Blocks.Add(para);
                        }

                        ResultTextBox.Document = flowDoc;
                    }
                    else 
                    {
                        ResultTextBox.Document = new FlowDocument(new Paragraph(
                            new Run($"请求失败: {response.StatusCode}\n{responseContent}")
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "发送请求时发生错误");
                ResultTextBox.Document = new FlowDocument(new Paragraph(
                    new Run($"发送请求时发生错误: {ex.Message}")
                ));
            }
        }

        private string GenerateCurlCommand(string url, HttpHeaders headers, string jsonContent)
        {
            var curlCommand = $"curl '{url}' \\\n";
            foreach (var header in headers)
            {
                curlCommand += $"  -H '{header.Key}: {string.Join(", ", header.Value)}' \\\n";
            }
            curlCommand += $"  -H 'content-type: application/json;charset=UTF-8' \\\n";
            curlCommand += $"  --data-raw '{jsonContent}'";
            return curlCommand;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        // 添加 UnregisterHotKey 导入
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}