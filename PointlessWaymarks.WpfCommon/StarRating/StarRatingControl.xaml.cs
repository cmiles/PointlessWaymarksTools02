using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PointlessWaymarks.WpfCommon.StarRating;

public partial class StarRatingControl : UserControl
{
    private static readonly Brush FilledBrush = CreateFrozenBrush(Color.FromRgb(255, 185, 0));
    private static readonly Brush EmptyBrush = CreateFrozenBrush(Color.FromRgb(160, 160, 160));

    private INotifyPropertyChanged? _subscribedContext;

    public StarRatingControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        UpdateStars();
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedContext != null)
            _subscribedContext.PropertyChanged -= OnContextPropertyChanged;

        _subscribedContext = DataContext as INotifyPropertyChanged;

        if (_subscribedContext != null)
            _subscribedContext.PropertyChanged += OnContextPropertyChanged;

        UpdateStars();
    }

    private void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StarRatingContext.UserValue))
            Dispatcher.Invoke(UpdateStars);
    }

    private void Star_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Tag?.ToString(), out var starValue)
            && DataContext is StarRatingContext context)
            context.UserValue = context.UserValue == starValue ? 0 : starValue;
    }

    private void UpdateStars()
    {
        var rating = (DataContext as StarRatingContext)?.UserValue ?? 0;
        Button[] stars = [Star1, Star2, Star3, Star4, Star5];
        for (var i = 0; i < 5; i++)
        {
            stars[i].Content = i < rating ? "★" : "☆";
            stars[i].Foreground = i < rating ? FilledBrush : EmptyBrush;
        }
    }
}
