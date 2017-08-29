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
    public partial class CloneWindow : Window
    {
        private MainViewModel _vm;
        
        internal CloneWindow(MainViewModel vm):this()
        {
            DataContext = _vm = vm;
            _vm.Close += _vm_Close;
        }

        public CloneWindow()
        {
            InitializeComponent();
        }

        private void _vm_Close(object sender, EventArgs e)
        {
            Close();
        }
    }
}
