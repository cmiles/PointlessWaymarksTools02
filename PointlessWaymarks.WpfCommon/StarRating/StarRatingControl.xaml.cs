using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PointlessWaymarks.WpfCommon.StarRating;

public partial class StarRatingControl : UserControl
{
    public static readonly DependencyProperty RatingProperty =
        DependencyProperty.Register(nameof(Rating), typeof(int), typeof(StarRatingControl),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRatingChanged));

    private static readonly Brush FilledBrush = CreateFrozenBrush(Color.FromRgb(255, 185, 0));
    private static readonly Brush EmptyBrush = CreateFrozenBrush(Color.FromRgb(160, 160, 160));

    public StarRatingControl()
    {
        InitializeComponent();
        UpdateStars();
    }

    public int Rating
    {
        get => (int)GetValue(RatingProperty);
        set => SetValue(RatingProperty, value);
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static void OnRatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((StarRatingControl)d).UpdateStars();
    }

    private void Star_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Tag?.ToString(), out var starValue))
            Rating = Rating == starValue ? 0 : starValue;
    }

    private void UpdateStars()
    {
        var rating = Rating;
        Button[] stars = [Star1, Star2, Star3, Star4, Star5];
        for (var i = 0; i < 5; i++)
        {
            stars[i].Content = i < rating ? "★" : "☆";
            stars[i].Foreground = i < rating ? FilledBrush : EmptyBrush;
        }
    }
}
