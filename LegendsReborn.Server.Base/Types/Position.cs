using Chaos.Geometry.Abstractions.Definitions;

using Darkages.Enums;

namespace Darkages.Types;

[Serializable]
public class Position
{
    public Position(ushort x, ushort y)
    {
        X = x;
        Y = y;
    }

    public Position(int x, int y) : this((ushort)x, (ushort)y)
    {
    }

    public Position() : this(0, 0)
    {
    }

    public ushort X { get; set; }
    public ushort Y { get; set; }

    public static Position operator +(Position a, Direction b)
    {
        var location = new Position(a.X, a.Y);
        switch (b)
        {
            case Direction.Up:
                location.Y--;
                return location;

            case Direction.Right:
                location.X++;
                return location;

            case Direction.Down:
                location.Y++;
                return location;

            case Direction.Left:
                location.X--;
                return location;
            default:
                throw new ArgumentOutOfRangeException(nameof(b), b, null);
        }
    }

    public int DistanceFrom(ushort X, ushort Y)
    {
        double XDiff = Math.Abs(X - this.X);
        double YDiff = Math.Abs(Y - this.Y);

        return (int)(XDiff > YDiff ? XDiff : YDiff);
    }
    public int DistanceFrom(Position pos) => DistanceFrom(pos.X, pos.Y);

    public bool IsNearby(Position pos) => pos.DistanceFrom(X, Y) <= ServerSetup.Instance.Config.VeryNearByProximity;

    public bool IsNextTo(Position pos, int distance = 1)
    {
        if ((X == pos.X) && (Y + distance == pos.Y))
            return true;
        if ((X == pos.X) && (Y - distance == pos.Y))
            return true;
        if ((X == pos.X + distance) && (Y == pos.Y))
            return true;
        if ((X == pos.X - distance) && (Y == pos.Y))
            return true;

        return false;
    }

    public TileContentPosition[] SurroundingContent(Area map)
    {
        var list = new List<TileContentPosition>();

        if (X > 0)
            list.Add(new TileContentPosition(
                new Position(X - 1, Y),
                map.ObjectGrid[X - 1, Y].Sprites.Count == 0 ? !map.IsWall(X - 1, Y) ? TileContent.None : TileContent.Wall : TileContent.Wall));

        if (Y > 0)
            list.Add(new TileContentPosition(
                new Position(X, Y - 1),
                map.ObjectGrid[X, Y - 1].Sprites.Count == 0 ? !map.IsWall(X, Y - 1) ? TileContent.None : TileContent.Wall : TileContent.Wall));

        if (X < map.Height - 1)
            list.Add(new TileContentPosition(
                new Position(X + 1, Y),
                map.ObjectGrid[X + 1, Y].Sprites.Count == 0 ? !map.IsWall(X + 1, Y) ? TileContent.None : TileContent.Wall : TileContent.Wall));

        if (Y < map.Width - 1)
            list.Add(new TileContentPosition(
                new Position(X, Y + 1),
                map.ObjectGrid[X, Y + 1].Sprites.Count == 0 ? !map.IsWall(X, Y + 1) ? TileContent.None : TileContent.Wall : TileContent.Wall));

        return list.ToArray();
    }

    public class TileContentPosition
    {
        public TileContentPosition(Position pos, TileContent content)
        {
            Position = pos;
            Content = content;
        }

        public TileContent Content { get; set; }
        public Position Position { get; set; }
    }

    public static bool TryParse(string xvalue, string yvalue, out Position position)
    {
        position = null;

        if (!int.TryParse(xvalue, out var x) || !int.TryParse(yvalue, out var y))
            return false;

        position = new Position(x, y);
        return true;
    }
    public override bool Equals(object obj) => obj is Position position && Equals(position);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public bool Equals(Position other) => other is not null && X == other.X && Y == other.Y;
    public static bool operator ==(Position left, Position right) => (left is null && right is null) || (left is not null && left.Equals(right));
    public static bool operator !=(Position left, Position right) => !((left is null && right is null) || (left is not null && left.Equals(right)));

    public override string ToString()
    {
        return "(" + X.ToString() + "," + Y.ToString() + ")";
    }
}