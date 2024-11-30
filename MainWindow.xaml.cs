using System;
using System.IO;
using System.Linq;  // 添加这行
using System.Net.Http;
using System.Net.Http.Headers;  // 添加这个引用
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;  // 添加这行
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;  // 添加这行
using System.Windows.Interop;
using Microsoft.Extensions.Configuration;
using System.Windows.Forms; // Windows Forms 组件
using System.Drawing; // 用于 Icon
using DrawingColor = System.Drawing.Color; // 为 System.Drawing.Color 添加别名
using MediaColor = System.Windows.Media.Color; // 为 System.Windows.Media.Color 添加别名
using System.Windows.Media; // 用于 SolidColorBrush
using System.ComponentModel; // 添加 TypeConverter 的引用
using MessageBox = System.Windows.MessageBox;  // 添加在文件开头的 using 部分
using WinFormsApplication = System.Windows.Forms.Application;
using WpfApplication = System.Windows.Application;
using System.Collections.Generic;  // 添加 Dictionary 支持
using Serilog;
using DotNetEnv; // 添加这行
using MediaColorConverter = System.Windows.Media.ColorConverter;  // 添加在 using 区域
using WpfRichTextBox = System.Windows.Controls.RichTextBox;  // 添加这行
using WpfTabControl = System.Windows.Controls.TabControl;    // 添加这行
using WinFormsRichTextBox = System.Windows.Forms.RichTextBox; // 添加这行
using WinFormsDataFormats = System.Windows.Forms.DataFormats;  // 添加这行
using WpfDataFormats = System.Windows.DataFormats;  // 添加这行
using System.Globalization;
using System.Windows.Data;

namespace moji
{
    public class SubtractValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double screenWidth && double.TryParse(parameter?.ToString(), out double subtractValue))
            {
                return screenWidth - subtractValue;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const int HOTKEY_ID = 9000;
        private HwndSource? _source;  // 添加字段保存 source 引用
        private readonly IConfiguration _configuration;
        public NotifyIcon? notifyIcon;  // 改为 public
        private readonly ILogger _logger;

        private SolidColorBrush _borderBrush;
        private MediaColor _shadowColor;  // 修改这里

        public SolidColorBrush BorderBrush
        {
            get => _borderBrush;
            set
            {
                _borderBrush = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderBrush)));
            }
        }

        public MediaColor ShadowColor  // 修改这里
        {
            get => _shadowColor;
            set
            {
                _shadowColor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShadowColor)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // 添加取消令牌源
        private CancellationTokenSource? _currentRequestCts;
        private string _lastSearchText = string.Empty;

        // 添加缓存字段
        private string _cachedSearchText;
        private FlowDocument _cachedVocabularyDoc;
        private FlowDocument _cachedExamplesDoc;

        // 在class MainWindow开头添加DEEPL API相关常量
        private const string DEEPL_API_URL = "https://api-free.deepl.com/v2/translate";
        private string DEEPL_API_KEY => Env.GetString("DEEPL_API_KEY");

        public MainWindow()
        {
            _cachedSearchText = string.Empty;
            _cachedVocabularyDoc = null;
            _cachedExamplesDoc = null;

            try
            {
                InitializeComponent();
                Env.Load(); // 加载.env文件
                this.DataContext = this;
                
                // 设置颜色 - 移除背景色设置
                string borderColor = Env.GetString("UI_BORDER_COLOR") ?? "#E0E0E0";
                string shadowColor = Env.GetString("UI_SHADOW_COLOR") ?? "#DDDDDD";

                BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(borderColor));
                ShadowColor = (MediaColor)MediaColorConverter.ConvertFromString(shadowColor);

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

                // 添加标签页切换事件处理
                TabControl.SelectionChanged += TabControl_SelectionChanged;
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

            // 双击托盘图标显示窗口
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
                const uint MOD_CONTROL = 0x0002;
                const uint VK_F20 = 0x83;  // F20的虚拟键码

                // 尝试注册热键 (MOD_CONTROL | MOD_ALT 组合CTRL+ALT)
                bool success = RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_F20);
                
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    MessageBox.Show($"注册热键失败: 错误代码 {error}\n请确保没有其他程序占用了 CTRL+ALT+F20 快捷键");
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
                    var errorDoc = new FlowDocument(new Paragraph(
                        new Run($"发生错误: {ex.Message}")
                    ));
                    UpdateResultTextBox(errorDoc, VocabularyTextBox);
                    UpdateResultTextBox(errorDoc, ExamplesTextBox);
                    Show();
                    Activate();
                });
            }
            return IntPtr.Zero;
        }

        // 添加新方法用于检查文本是否包含汉字或日文字符
        private bool ContainsJapaneseOrChinese(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            // 日文平假名范围：3040-309F
            // 日文片假名范围：30A0-30FF
            // 汉字基本范围：4E00-9FAF
            return text.Any(c => 
                (c >= '\u3040' && c <= '\u309F') ||  // 平假名
                (c >= '\u30A0' && c <= '\u30FF') ||  // 片假名
                (c >= '\u4E00' && c <= '\u9FAF'));   // 汉字
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
                    
                    var clipboardText = System.Windows.Clipboard.GetText();

                    // 修改这部分判断逻辑
                    if (!ContainsJapaneseOrChinese(clipboardText))
                    {
                        // 显示翻译中提示
                        var loadingDoc = new FlowDocument(new Paragraph(
                            new Run("正在翻译...") { FontWeight = FontWeights.Bold }
                        ));
                        VocabularyTextBox.Document = loadingDoc;
                        ExamplesTextBox.Document = CloneFlowDocument(loadingDoc);

                        try 
                        {
                            // 调用DEEPL API翻译
                            string translatedText = await TranslateWithDeepL(clipboardText);
                            
                            var translationDoc = new FlowDocument();
                            
                            // 添加原文
                            var originalPara = new Paragraph(new Run("原文: ") { FontWeight = FontWeights.Bold });
                            originalPara.Inlines.Add(new Run(clipboardText));
                            originalPara.Margin = new Thickness(0, 0, 0, 10);
                            translationDoc.Blocks.Add(originalPara);
                            
                            // 添加译文
                            var translatedPara = new Paragraph(new Run("译文: ") { FontWeight = FontWeights.Bold });
                            translatedPara.Inlines.Add(new Run(translatedText));
                            translatedPara.Margin = new Thickness(0, 0, 0, 10);
                            translationDoc.Blocks.Add(translatedPara);

                            VocabularyTextBox.Document = translationDoc;
                            ExamplesTextBox.Document = CloneFlowDocument(translationDoc);
                            return;
                        }
                        catch (Exception ex)
                        {
                            var errorDoc = new FlowDocument(new Paragraph(
                                new Run($"翻译失败: {ex.Message}") { FontWeight = FontWeights.Bold }
                            ));
                            VocabularyTextBox.Document = errorDoc;
                            ExamplesTextBox.Document = CloneFlowDocument(errorDoc);
                            return;
                        }
                    }

                    // 如果内容没变且缓存存在，创建新的 FlowDocument 副本
                    if (clipboardText == _cachedSearchText && 
                        _cachedVocabularyDoc != null && 
                        _cachedExamplesDoc != null)
                    {
                        VocabularyTextBox.Document = CloneFlowDocument(_cachedVocabularyDoc);
                        ExamplesTextBox.Document = CloneFlowDocument(_cachedExamplesDoc);
                        return;
                    }

                    _lastSearchText = clipboardText;

                    // 取消之前的请求
                    _currentRequestCts?.Cancel();
                    _currentRequestCts = new CancellationTokenSource();

                    // 显示加载提示 - 为每个文本框创建独立的 Document
                    VocabularyTextBox.Document = new FlowDocument(new Paragraph(new Run("正在查询...")));
                    ExamplesTextBox.Document = new FlowDocument(new Paragraph(new Run("正在查询...")));
                    
                    // 同时发送两个请求
                    await Task.WhenAll(
                        SendHttpRequest(clipboardText, VocabularyTextBox, 102, _currentRequestCts.Token),
                        SendHttpRequest(clipboardText, ExamplesTextBox, 103, _currentRequestCts.Token)
                    );

                    // 缓存新的搜索结果
                    _cachedSearchText = clipboardText;
                    _cachedVocabularyDoc = CloneFlowDocument(VocabularyTextBox.Document);
                    _cachedExamplesDoc = CloneFlowDocument(ExamplesTextBox.Document);
                }
                else
                {
                    // 为每个文本框创建新的实例
                    var noContentDoc1 = new FlowDocument(new Paragraph(new Run("剪切板没有文本内容")));
                    var noContentDoc2 = new FlowDocument(new Paragraph(new Run("剪切板没有文本内容")));
                    VocabularyTextBox.Document = noContentDoc1;
                    ExamplesTextBox.Document = noContentDoc2;
                    Show();
                    Activate();
                }
                Topmost = true;
            }
            catch (Exception ex)
            {
                // 为每个文本框创建新的实例
                var errorDoc1 = new FlowDocument(new Paragraph(new Run($"处理剪贴板内容时发生错误: {ex.Message}")));
                var errorDoc2 = new FlowDocument(new Paragraph(new Run($"处理剪贴板内容时发生错误: {ex.Message}")));
                VocabularyTextBox.Document = errorDoc1;
                ExamplesTextBox.Document = errorDoc2;
                Show();
                Activate();
            }
        }

        // 添加DEEPL翻译方法
        private async Task<string> TranslateWithDeepL(string text)
        {
            if (string.IsNullOrEmpty(DEEPL_API_KEY))
            {
                throw new Exception("未设置DEEPL_API_KEY环境变量");
            }

            using (var client = new HttpClient())
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", DEEPL_API_KEY),
                    new KeyValuePair<string, string>("text", text),
                    new KeyValuePair<string, string>("target_lang", "ZH"),
                });

                var response = await client.PostAsync(DEEPL_API_URL, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"DEEPL API请求失败: {response.StatusCode}\n{responseContent}");
                }

                var jsonResponse = System.Text.Json.JsonDocument.Parse(responseContent);
                return jsonResponse.RootElement
                    .GetProperty("translations")[0]
                    .GetProperty("text")
                    .GetString();
            }
        }

        private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastSearchText)) return;

            var selectedTab = TabControl.SelectedItem as TabItem;
            if (selectedTab == null) return;

            var targetTextBox = selectedTab.Header.ToString() == "词汇" ? VocabularyTextBox : ExamplesTextBox;
            var cachedDoc = selectedTab.Header.ToString() == "词汇" ? _cachedVocabularyDoc : _cachedExamplesDoc;

            // 如果有缓存，创建新的 Document 副本
            if (cachedDoc != null)
            {
                targetTextBox.Document = CloneFlowDocument(cachedDoc);
                return;
            }

            // 如果没有缓存，创建新的提示文档
            targetTextBox.Document = new FlowDocument(new Paragraph(new Run("请重新查询获取内容")));
        }

        private TabItem? CurrentTab => ((WpfTabControl)FindName("TabControl"))?.SelectedItem as TabItem;
        private WpfRichTextBox? CurrentTextBox => CurrentTab?.Header.ToString() == "词汇" ? VocabularyTextBox : ExamplesTextBox;

        private async Task SendHttpRequest(string clipboardText, WpfRichTextBox targetTextBox, int type, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    const string url = "https://api.mojidict.com/parse/functions/search-all";

                    // 从环境变量读取请求头
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("accept", Env.GetString("HTTP_ACCEPT"));
                    client.DefaultRequestHeaders.Add("accept-language", Env.GetString("HTTP_ACCEPT_LANGUAGE"));
                    client.DefaultRequestHeaders.Add("origin", Env.GetString("HTTP_ORIGIN"));
                    client.DefaultRequestHeaders.Add("priority", Env.GetString("HTTP_PRIORITY"));
                    client.DefaultRequestHeaders.Add("sec-ch-ua", Env.GetString("HTTP_SEC_CH_UA"));
                    client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", Env.GetString("HTTP_SEC_CH_UA_MOBILE"));
                    client.DefaultRequestHeaders.Add("sec-ch-ua-platform", Env.GetString("HTTP_SEC_CH_UA_PLATFORM"));
                    client.DefaultRequestHeaders.Add("sec-fetch-dest", Env.GetString("HTTP_SEC_FETCH_DEST"));
                    client.DefaultRequestHeaders.Add("sec-fetch-mode", Env.GetString("HTTP_SEC_FETCH_MODE"));
                    client.DefaultRequestHeaders.Add("sec-fetch-site", Env.GetString("HTTP_SEC_FETCH_SITE"));
                    client.DefaultRequestHeaders.Add("user-agent", Env.GetString("HTTP_USER_AGENT"));

                    // 构建请求体
                    var requestBody = new
                    {
                        g_os = Env.GetString("REQUEST_G_OS"),
                        g_ver = Env.GetString("REQUEST_G_VER"),
                        _InstallationId = Env.GetString("REQUEST_INSTALLATION_ID"),
                        _ClientVersion = Env.GetString("REQUEST_CLIENT_VERSION"),
                        _ApplicationId = Env.GetString("REQUEST_APPLICATION_ID"),
                        types = new[] { type },
                        text = clipboardText
                    };

                    // 序列化请求体
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
                    var requestContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    requestContent.Headers.ContentType.CharSet = "UTF-8";



                    // 记录 CURL 命令
                    _logger.Information("发送请求\nCURL Command:\n{CurlCommand}", 
                        GenerateCurlCommand(url, client.DefaultRequestHeaders, jsonContent));

                    var response = await client.PostAsync(url, requestContent, cancellationToken);
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    // 记录响应详情
                    _logger.Information("收到响应\n状态码: {StatusCode}\n响应内容: {Response}", 
                        response.StatusCode, responseContent);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = System.Text.Json.JsonDocument.Parse(responseContent);
                        var searchResults = jsonResponse.RootElement
                            .GetProperty("result")
                            .GetProperty("result")
                            .GetProperty(type == 102 ? "word" : "example")
                            .GetProperty("searchResult");

                        var flowDoc = new FlowDocument();
                        
                        // 添加查询内容
                        var queryPara = new Paragraph(new Run($"Word: {clipboardText}") { FontWeight = FontWeights.Bold });
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

                        UpdateResultTextBox(flowDoc, targetTextBox);
                    }
                    else 
                    {
                        var errorDoc = new FlowDocument(new Paragraph(
                            new Run($"请求失败: {response.StatusCode}\n{responseContent}")
                        ));
                        UpdateResultTextBox(errorDoc, targetTextBox);
                    }
                }
            }
            // 添加取消异常处理
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;  // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                var errorDoc = new FlowDocument(new Paragraph(
                    new Run($"发送请求时发生错误: {ex.Message}")
                ));
                UpdateResultTextBox(errorDoc, targetTextBox);
                _logger.Error(ex, "发送请求时发生错误");
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

        private void UpdateResultTextBox(FlowDocument flowDoc, WpfRichTextBox textBox)
        {
            // 设置文档级别的样式
            flowDoc.PagePadding = new Thickness(0);
            flowDoc.TextAlignment = TextAlignment.Left;
            
            // 更新文档内容
            textBox.Document = flowDoc;
            
            // 获取环境变量中的颜色设置
            string defaultTextColor = Env.GetString("UI_DEFAULT_TEXT_COLOR") ?? "#CCCCCC";
            string titleColor = Env.GetString("UI_TITLE_COLOR") ?? "#2196F3";
            string contentColor = Env.GetString("UI_CONTENT_COLOR") ?? "#999999";
            string secondaryColor = Env.GetString("UI_SECONDARY_COLOR") ?? "#999999";
            
            foreach (var block in flowDoc.Blocks)
            {
                if (block is Paragraph para)
                {
                    // 设置段落样式
                    para.Margin = new Thickness(0, 0, 0, 10);
                    para.LineHeight = 1.5;
                    
                    // 设置文本样式
                    foreach (var inline in para.Inlines)
                    {
                        if (inline is Run run)
                        {
                            if (run.FontWeight == FontWeights.Bold)
                            {
                                // 标题文本
                                run.Foreground = new SolidColorBrush(
                                    (MediaColor)MediaColorConverter.ConvertFromString(titleColor));
                                run.FontSize = 16;
                            }
                            else
                            {
                                // 普通文本
                                run.Foreground = new SolidColorBrush(
                                    (MediaColor)MediaColorConverter.ConvertFromString(contentColor));
                                run.FontSize = 14;
                            }
                        }
                    }
                }
            }

            // 强制重新渲染
            textBox.InvalidateVisual();
        }

        private void DragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        // 添加 UnregisterHotKey 导入
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // 添加 FlowDocument 克隆方法
        private FlowDocument CloneFlowDocument(FlowDocument source)
        {
            var memoryStream = new MemoryStream();
            var range = new TextRange(source.ContentStart, source.ContentEnd);
            range.Save(memoryStream, WpfDataFormats.Xaml);
            
            var newDocument = new FlowDocument();
            var newRange = new TextRange(newDocument.ContentStart, newDocument.ContentEnd);
            memoryStream.Seek(0, SeekOrigin.Begin);
            newRange.Load(memoryStream, WpfDataFormats.Xaml);
            
            return newDocument;
        }
    }
}