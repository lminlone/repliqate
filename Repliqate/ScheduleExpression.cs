using System.Globalization;
using Quartz;

namespace Repliqate;

public enum ScheduleExprTypeTokens { Daily, Weekly, Monthly }
public enum ScheduleExprDayTokens { Sun, Mon, Tue, Wed, Thu, Fri, Sat } // Ordered like this to make Cron string conversion easier

public class ScheduleExpression : IEquatable<ScheduleExpression>
{
    public bool Valid { get; set; } = false;
    
    private CronExpression? _cron = null;

    public ScheduleExpression() {}

    public static ScheduleExpression FromQuartzCronString(string cronString)
    {
        return new ScheduleExpression
        {
            _cron = new CronExpression(cronString)
        };
    }

    public static ScheduleExpression? FromString(string s)
    {
        ScheduleExpression scheduleExpression = new();

        if (s.StartsWith('@'))
        {
            var parts = s.Trim('@').Trim('"').Split(' ');
            if (parts.Length < 2)
                return null;

            if (!Enum.TryParse<ScheduleExprTypeTokens>(parts[0], true, out var scheduleType))
            {
                return null;
            }

            if (!TryParseTimeOnly(parts[1], out var time))
            {
                return null;
            }

            int dayOfMonth = 0;
            ScheduleExprDayTokens day = ScheduleExprDayTokens.Sun;
            if (parts.Length > 2)
            {
                if (scheduleType == ScheduleExprTypeTokens.Monthly && !int.TryParse(parts[2], out dayOfMonth))
                {
                    return null;
                }
                
                if (scheduleType == ScheduleExprTypeTokens.Weekly && !Enum.TryParse(parts[2], true, out day))
                {
                    return null;
                }
            }

            // Convert to Quartz cron string
            string cronStr = string.Empty;
            switch(scheduleType)
            {
                case ScheduleExprTypeTokens.Daily:
                    cronStr = $"0 {time.Minute} {time.Hour} 1/1 * ? *";
                    break;
                case ScheduleExprTypeTokens.Weekly:
                    cronStr = $"0 {time.Minute} {time.Hour} ? * {day.ToString().ToUpper()} *";
                    break;
                case ScheduleExprTypeTokens.Monthly:
                    cronStr = $"0 {time.Minute} {time.Hour} {dayOfMonth} 1/1 ? *";
                    break;
            };
            
            scheduleExpression._cron = new CronExpression(cronStr);
        }
        else
        {
            scheduleExpression._cron = new CronExpression(s);
        }
        
        return scheduleExpression;
    }

    public static bool TryParseTimeOnly(ReadOnlySpan<char> input, out TimeOnly value)
    {
        string[] formats = [
            "h:mm tt",
            "hh:mm tt",
            "h tt",
            "h:mmtt",
            "hh:mmtt",
            "htt",
            
            "HH:mm" // 24-hour time format
        ];
        
        if (TimeOnly.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return true;
        }

        return false;
    }

    public string ToCronString()
    {
        if (_cron is null)
            return string.Empty;
        
        return _cron.CronExpressionString;
    }

    public string GetSummary()
    {
        return _cron?.GetExpressionSummary() ?? string.Empty;
    }

    public DateTimeOffset? NextValidTime(DateTimeOffset t)
    {
        return _cron?.GetNextValidTimeAfter(t);
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not ScheduleExpression other) return false;
        return Equals(other);
    }

    public bool Equals(ScheduleExpression? other)
    {
        if (other is null) return false;
        return Valid == other.Valid &&
               _cron == other._cron;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Valid, _cron.CronExpressionString);
    }
    
    public static bool operator ==(ScheduleExpression? left, ScheduleExpression? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(ScheduleExpression? left, ScheduleExpression? right)
    {
        return !(left == right);
    }
}