---
title: Scheduling
weight: 3
tags:
- Docs
- Guide
cascade:
  type: docs
---

Repliqate provides flexible scheduling options using a (half-custom) syntax while maintaining compatibility with "standard" cron expressions.

## Shorthand Syntax
### Frequency Options
- `@daily <time>` - Run once per day.
- `@weekly <time> <day of the week>` - Run once per week on this specific day.
- `@monthly <time> <day of the month>` - Run once per month on this specific date.

### Time Formats
Supports both 12-hour and 24-hour time formats:
- 12-hour: `3:00 PM`, `3PM`, `3:00pm`
- 24-hour: `15:00`

### Examples
- `@monthly 9am 15`: Run on the 15th of every month at 9am.
- `@weekly 4am Mon`: Run weekly on Mondays at 4am.
- `@daily 23:59`: Run every day at 11:59pm.

## Advanced Scheduling
For more complex scheduling needs, Repliqate also accepts [Quartz cron expressions](http://www.cronmaker.com)

### Examples
- `0 0 19 1/1 * ? *`: Run every hour (not recommended) starting at 7pm.
- `0 0 2 ? * MON-FRI *`: Run every weekday at 2am.