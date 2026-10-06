using System;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public struct Point2D : IEquatable<Point2D>
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double DistanceTo(Point2D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static Point2D operator +(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
        public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
        public static Point2D operator *(Point2D a, double scale) => new(a.X * scale, a.Y * scale);
        public static Point2D operator /(Point2D a, double scale) => new(a.X / scale, a.Y / scale);

        public bool Equals(Point2D other) => Math.Abs(X - other.X) < 0.001 && Math.Abs(Y - other.Y) < 0.001;

        public override bool Equals(object? obj) => obj is Point2D other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X:F1}, {Y:F1})";
    }
}
