using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Hellosam.Net.TransparentPaint
{
    class MainViewModel : ViewModelBase
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(MainViewModel));

        double _height;
        public double Height
        {
            get { return _height; }
            set { Set(ref _height, value); }
        }

        double _width;
        public double Width
        {
            get { return _width; }
            set { Set(ref _width, value); }
        }

        double _ratioHeight;
        public double RatioHeight
        {
            get { return _ratioHeight; }
            set { Set(ref _ratioHeight, value); }
        }

        double _ratioWidth;
        public double RatioWidth
        {
            get { return _ratioWidth; }
            set { Set(ref _ratioWidth, value); }
        }

        double _penWidth;
        public double PenWidth
        {
            get { return _penWidth; }
            set { Set(ref _penWidth, value); OnPenWidthChanged(); }
        }
        
        int _port;
        public int Port
        {
            get { return _port; }
            set { Set(ref _port, value); }
        }

        bool _alwaysOnTop;
        public bool AlwaysOnTop
        {
            get { return _alwaysOnTop; }
            set { Set(ref _alwaysOnTop, value); }
        }
        
        bool _isServerActive;
        public bool IsServerActive
        {
            get { return _isServerActive; }
            private set { Set(ref _isServerActive, value); }
        }

        InkCanvas _canvas = null;
        public InkCanvas Canvas
        {
            get { return _canvas; }
            private set { Set(ref _canvas, value); }
        }

        bool _enableSnap;
        public bool EnableSnap
        {
            get { return _enableSnap; }
            set { Set(ref _enableSnap, value); OnEnableSnap(); }
        }

        bool _isSnapped;
        public bool IsSnapped
        {
            get { return _isSnapped; }
            private set { Set(ref _isSnapped, value); }
        }

        string _snapName;
        public string SnapName
        {
            get { return _snapName; }
            set { Set(ref _snapName, value); }
        }


        public ICommand RestartCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }
        public ICommand ClearInkCommand { get; private set; }
        public ICommand SetColorCommand { get; private set; }
        public ICommand UndoCommand { get; private set; }
        public ICommand CloneWindowCommand { get; private set; }
        public ICommand ToggleSnapCommand { get; private set; }

        MjpegStreamingServer _server = null;
        AsyncLock _serverStartLock = new AsyncLock();
        int _serverEra = 0;
        BufferBlock<UIElement> _renderQueue = new BufferBlock<UIElement>(new DataflowBlockOptions() { BoundedCapacity = 2 });
        CloneWindow _cloneWindow = null;

        CancellationTokenSource _snapCts = null;

        public MainViewModel()
        {
            CloseCommand = new RelayCommand(OnClose);
            RestartCommand = new RelayCommand(OnRestart);
            ClearInkCommand = new RelayCommand(OnClearInk);
            SetColorCommand = new RelayCommand<Color>(OnSetColor);
            UndoCommand = new RelayCommand(OnUndo);
            CloneWindowCommand = new RelayCommand(OnCloneWindow);
            ToggleSnapCommand = new RelayCommand(OnToggleSnap);

            Width = 512;
            Height = 512;

            if (!IsInDesignMode)
            {
                Application.Current.Dispatcher.InvokeAsync(RenderTask, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        public void LoadConfig(ConfigData config)
        {
            config.AppliesTo(this);
            OnRestart();
        }

        private void OnToggleSnap()
        {
            EnableSnap = !EnableSnap;
        }

        private void OnEnableSnap()
        {
            if (EnableSnap && _snapCts == null)
            {
                var cts = _snapCts = new CancellationTokenSource();
                Application.Current.Dispatcher.InvokeAsync(() => SnapTask(cts.Token), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            else if (!EnableSnap && _snapCts != null)
            {
                var snapCts = _snapCts;
                _snapCts = null;
                snapCts?.Cancel();
            }
        }

        private async Task SnapTask(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    IsSnapped = TryToSnap();
                    await Task.Delay(500);
                }
            }
            finally
            {
                IsSnapped = false;
            }
        }

        private bool TryToSnap()
        {
            if (string.IsNullOrEmpty(SnapName))
                return false;

            var w = Window.GetWindow(Canvas);
            if (w == null)
                return false;

            IntPtr matchingHWnd = IntPtr.Zero;
            User32.EnumDesktopWindows(IntPtr.Zero,
                (User32.EnumDelegate)delegate (IntPtr hWnd, int lParam)
                {
                    if (!User32.IsWindowVisible(hWnd))
                        return true;

                    StringBuilder strbTitle = new StringBuilder(255);
                    int nLength = User32.GetWindowText(hWnd, strbTitle, strbTitle.Capacity + 1);
                    string strTitle = strbTitle.ToString();
                    if (!string.IsNullOrEmpty(strTitle) && strTitle.IndexOf(SnapName) > 0)
                    {
                        matchingHWnd = hWnd;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);

            if (matchingHWnd == IntPtr.Zero)
                return false;

            User32.RECT rect;
            User32.POINT point = new User32.POINT { X = 0, Y = 0 };
            if (!User32.GetClientRect(matchingHWnd, out rect) || !User32.ClientToScreen(matchingHWnd, out point))
                return false;
            if (rect.Right - rect.Left <= 0 || rect.Bottom - rect.Top <= 0)
                return false;
            var dpiRatio = (double) User32.GetDpiRatio(matchingHWnd);

            var offset = Canvas.TranslatePoint(new Point(0, 0), w);

            Rect goal = new Rect(point.X, point.Y, rect.Right - rect.Left + 1, rect.Bottom - rect.Top + 1);
            if (RatioHeight > 0 && RatioWidth > 0)
            {
                var xRatio = RatioHeight / RatioWidth;
                if (goal.Height / goal.Width > xRatio)
                {
                    var newHeight = goal.Width * xRatio;
                    goal.Y = goal.Y + (goal.Height - newHeight) / 2;
                    goal.Height = newHeight;
                }
                else if (goal.Height / goal.Width < xRatio)
                {
                    var newWidth = goal.Height / xRatio;
                    goal.X = goal.X + (goal.Width - newWidth) / 2;
                    goal.Width = newWidth;
                }
            }

            w.Left = goal.X / dpiRatio - offset.X;
            Width = goal.Width / dpiRatio;
            w.Top = goal.Y / dpiRatio - offset.Y;
            Height = goal.Height / dpiRatio;

            return true;
        }

        internal void SetCanvas(InkCanvas canvas)
        {
            if (Canvas != null)
                throw new InvalidOperationException("Canvas has been set already");

            Canvas = canvas;
            canvas.StrokeCollected += Canvas_StrokeCollected;
            canvas.StrokeErased += Canvas_StrokeErased;
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;
        }
        
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.MouseDevice.Capture((UIElement)sender);
            Render(Canvas);
        }
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            e.MouseDevice.Capture(null);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.MouseDevice.Captured == (UIElement)sender)
            {
                Render(Canvas);
            }
        }

        private void Canvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            Render(Canvas);
        }

        private void Canvas_StrokeErased(object sender, RoutedEventArgs e)
        {
            Render(Canvas);
            Render(Canvas);
        }

        private async void OnRestart()
        {
            var localEra = ++_serverEra;
            using (await _serverStartLock.LockAsync())
            {
                if (localEra != _serverEra)
                    return;

                Logger.Debug("Request to restart HTTP server");
                IsServerActive = false;
                if (_server != null)
                {
                    Logger.Debug("Stopping the old server instance");
                    try
                    {
                        await _server.Stop();
                    }
                    catch (Exception)
                    {
                        // Exceptions would have been logged below
                    }
                    _server = null;
                    Logger.Debug("The old server instance is stopped");
                }
                if (Port != 0)
                {
                    _server = new MjpegStreamingServer();
                    IsServerActive = true;                    
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    _server.Start(Port).ContinueWith(async (task) =>
                    {
                        _server = null;
                        IsServerActive = false;
                        try
                        {
                            await task;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("HTTP server crashed", ex);
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
        }

        private void OnClearInk()
        {
            if (Canvas != null)
            {
                Canvas.Strokes.Clear();
                Render(Canvas);
                Render(Canvas);
            }
        }

        private void OnUndo()
        {
            if (Canvas != null && Canvas.Strokes.Count > 0)
            {
                Canvas.Strokes.RemoveAt(Canvas.Strokes.Count - 1);
                Render(Canvas);
                Render(Canvas);
            }
        }

        private void OnSetColor(Color color)
        {
            if (Canvas != null)
                Canvas.DefaultDrawingAttributes.Color = color;
        }
        private void OnPenWidthChanged()
        {
            if (Canvas != null && PenWidth > 0 && PenWidth < 1000)
            {
                Canvas.DefaultDrawingAttributes.Height = PenWidth;
                Canvas.DefaultDrawingAttributes.Width = PenWidth;
            }
        }

        public void Render(UIElement element)
        {
            _renderQueue.Post(element);
        }

        private async Task RenderTask()
        {
            while (true)
            {
                var element = await _renderQueue.ReceiveAsync();
                Rect rect = new Rect(element.RenderSize);
                RenderTargetBitmap rtb = new RenderTargetBitmap((int)rect.Right,
                  (int)rect.Bottom, 96d, 96d, System.Windows.Media.PixelFormats.Default);
                rtb.Render(element);

                //endcode as PNG
                BitmapEncoder pngEncoder = new PngBitmapEncoder();
                pngEncoder.Frames.Add(BitmapFrame.Create(rtb));

                using (var ms = new System.IO.MemoryStream())
                {
                    pngEncoder.Save(ms);
                    _server?.Publish(new BinaryPayload("image/png", ms.ToArray()));
                }
                await Task.Delay(50);
            }
        }

        public event EventHandler Close;
        protected virtual void OnClose()
        {
            EventHandler handler = Close;
            if (handler != null) handler(this, EventArgs.Empty);
            _server?.Stop();
            Config.SaveToSystem(new ConfigData(this));
        }

        private void OnCloneWindow()
        {
            if (_cloneWindow != null)
                _cloneWindow.Close();
            (_cloneWindow = new CloneWindow(this)).Show();
            _cloneWindow.WindowState = WindowState.Normal;
            User32.SendToBottom(_cloneWindow);
        }
    }
}
