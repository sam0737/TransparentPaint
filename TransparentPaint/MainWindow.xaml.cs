using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Hellosam.Net.TransparentPaint
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm = new MainViewModel();
            _vm.LoadConfig(Config.CreateFromSystemOrDefault());
            _vm.Close += _vm_Close;

            _vm.SetCanvas(Canvas);
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _vm.Close -= _vm_Close;
            _vm.CloseCommand.Execute(null);
        }

        private void _vm_Close(object sender, EventArgs e)
        {
            Close();
        }

        private void Canvas_StrokeCollected(object sender, System.Windows.Controls.InkCanvasStrokeCollectedEventArgs e)
        {
            _vm.Render((UIElement)sender);
            _vm.Render((UIElement)sender);
        }

        private void Canvas_StrokeErased(object sender, RoutedEventArgs e)
        {
            _vm.Render((UIElement)sender);
            _vm.Render((UIElement)sender);
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_vm.IsSnapped)
                this.DragMove();
        }
    }
}
