namespace TSS.Visitors
{
    public interface IPosition
    {
        int Index { get; }

        bool IsLast { get; }
    }

    public class Position : IPosition
    {
        public static readonly Position Single = new Position(0, true);

        public Position(int index, bool isLast)
        {
            Index = index;
            IsLast = isLast;
        }

        public int Index { get; }

        public bool IsLast { get; }
    }

    public static class PositionExtensions
    {
        public static bool Matches(this IPosition position, string expression)
        {
            if (position == null || string.IsNullOrEmpty(expression) || !expression.StartsWith("$"))
            {
                return false;
            }

            var pos = expression.Substring(1);
            if (pos.Length == 0)
            {
                return false;
            }

            switch (pos)
            {
                case "first":
                    return position.Index == 0;
                case "last":
                    return position.IsLast;
                case "even":
                    return position.Index % 2 == 1;
                case "odd":
                    return position.Index % 2 == 0;
                default:
                    try
                    {
                        if (pos.StartsWith("<>"))
                        {
                            return position.Index + 1 != NumberRangeHelpers.GetNumberFromExpression(pos.Substring(2));
                        }
                        if (pos.StartsWith("<="))
                        {
                            return position.Index + 1 <= NumberRangeHelpers.GetNumberFromExpression(pos.Substring(2));
                        }
                        if (pos.StartsWith("<"))
                        {
                            return position.Index + 1 < NumberRangeHelpers.GetNumberFromExpression(pos.Substring(1));
                        }
                        if (pos.StartsWith(">="))
                        {
                            return position.Index + 1 >= NumberRangeHelpers.GetNumberFromExpression(pos.Substring(2));
                        }
                        if (pos.StartsWith(">"))
                        {
                            return position.Index + 1 > NumberRangeHelpers.GetNumberFromExpression(pos.Substring(1));
                        }
                        if (pos.StartsWith("="))
                        {
                            return position.Index + 1 == NumberRangeHelpers.GetNumberFromExpression(pos.Substring(1));
                        }

                        return position.Index + 1 == NumberRangeHelpers.GetNumberFromExpression(pos);
                    }
                    catch
                    {
                        return false;
                    }
            }
        }
    }
}
