using DrawingRectangle = System.Drawing.Rectangle;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StoryTranslator.App;

public partial class RegionSelectorWindow : Window
{
    private System.Windows.Point _dragStart;
    private bool _isDragging;

    public RegionSelectorWindow()
    {
        InitializeComponent();
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
    }

    public DrawingRectangle? SelectedRegion { get; private set; }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = true;
        CaptureMouse();
        UpdateSelection(e.GetPosition(this));
        e.Handled = true;
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            UpdateSelection(e.GetPosition(this));
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        UpdateSelection(e.GetPosition(this));
        _isDragging = false;
        ReleaseMouseCapture();

        var dpi = VisualTreeHelper.GetDpi(this);
        var left = (int)Math.Round(Canvas.GetLeft(SelectionOutline) * dpi.DpiScaleX);
        var top = (int)Math.Round(Canvas.GetTop(SelectionOutline) * dpi.DpiScaleY);
        var width = (int)Math.Round(SelectionOutline.Width * dpi.DpiScaleX);
        var height = (int)Math.Round(SelectionOutline.Height * dpi.DpiScaleY);
        if (width > 0 && height > 0)
        {
            SelectedRegion = new DrawingRectangle(left, top, width, height);
            DialogResult = true;
        }
        else
        {
            DialogResult = false;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }

    private void UpdateSelection(System.Windows.Point current)
    {
        var left = Math.Min(_dragStart.X, current.X);
        var top = Math.Min(_dragStart.Y, current.Y);
        Canvas.SetLeft(SelectionOutline, left);
        Canvas.SetTop(SelectionOutline, top);
        SelectionOutline.Width = Math.Abs(current.X - _dragStart.X);
        SelectionOutline.Height = Math.Abs(current.Y - _dragStart.Y);
        SelectionOutline.Visibility = Visibility.Visible;
    }
}



