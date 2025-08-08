namespace GatewayPluginContract;

public static class Visualisation
{
    // Marker interface for vis data response models
    // Only for data, vis metadata is handled separately
    public interface ICardVisualisation
    {
    }

    /// <summary>
    /// Represents the data for a pie chart
    /// Each key in the Segments dictionary represents a segment label,
    /// Each value represents the size of the segment (as a decimal value > 0 and <= 1).
    /// </summary>
    public class PieChartModel : ICardVisualisation
    {
        public required Dictionary<string, double> Segments { get; init; }
    }
    
    /// <summary>
    /// Represents the data for a bar chart
    /// Each key in the Data dictionary represents a bar label,
    /// Each value represents the height of the bar.
    /// </summary>
    public class BarChartModel : ICardVisualisation
    {
        public required Dictionary<string, double> Data { get; init; }
        public required string XAxisLabel { get; init; }
        public required string YAxisLabel { get; init; }
    }
    
    /// <summary>
    /// Represents the data for a line chart
    /// Each key in the outer Data dictionary represents a line series label,
    /// Each value is another dictionary where the key is the x-axis label
    /// Each inner dictionary's value represents the y-axis value for that series at that x-axis label.
    /// </summary>
    public class LineChartModel : ICardVisualisation
    {
        public required Dictionary<string, Dictionary<string, double>> Data { get; init; }
        public required string XAxisLabel { get; init; }
        public required string YAxisLabel { get; init; }
    }
    
    public class TextModel : ICardVisualisation
    {
        public required string Text { get; init; }
    }
    
    public class BooleanModel : ICardVisualisation
    {
        public required bool Value { get; init; }
    }
    
    public class NumberModel : ICardVisualisation
    {
        public required double Value { get; init; }
    }
}