using Repliqate;

namespace Tests;

public class TestScheduleParsing
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    [TestCase("@daily 12am")]
    [TestCase("@daily 6am")]
    [TestCase("@daily 3pm")]
    [TestCase("@daily 11:59pm")]
    [TestCase("@daily 23:59")]
    [TestCase("@weekly 4am Mon")]
    [TestCase("@weekly 12pm Wed")]
    [TestCase("@weekly 11pm Fri")]
    [TestCase("@weekly 7am Sun")]
    [TestCase("@monthly 1am 1")]
    [TestCase("@monthly 9am 15")]
    [TestCase("@monthly 6pm 28")]
    [TestCase("@monthly 11pm 31")]
    public void ValidateSchedule_ShouldAcceptValidInputs(string scheduleString)
    {
        ScheduleExpression? schedule = ScheduleExpression.FromString(scheduleString);
        Assert.That(schedule, Is.Not.Null);
    }
    
    [Test]
    [TestCase("")]
    [TestCase("@hello_world")]
    [TestCase("@daily www Monday")]
    [TestCase("yearly 4am Monday")]
    [TestCase("@yearly 4am Anotherday")]
    [TestCase("daily 100pm Randomday")]
    public void ValidateSchedule_ShouldRejectInvalidInputs(string scheduleString)
    {
        try
        {
            ScheduleExpression? schedule = ScheduleExpression.FromString(scheduleString);
            Assert.That(schedule, Is.Null);
        }
        catch (System.FormatException e)
        {
            Assert.Pass();
        }
    }

    [Test]
    [TestCase("@daily 12am", ExpectedResult = "0 0 0 1/1 * ? *")]
    [TestCase("@daily 11:59pm", ExpectedResult = "0 59 23 1/1 * ? *")]
    [TestCase("@weekly 11pm Fri", ExpectedResult = "0 0 23 ? * FRI *")]
    [TestCase("@weekly 7am Sun", ExpectedResult = "0 0 7 ? * SUN *")]
    [TestCase("@monthly 9am 15", ExpectedResult = "0 0 9 15 1/1 ? *")]
    [TestCase("@monthly 11pm 31", ExpectedResult = "0 0 23 31 1/1 ? *")]
    public string ValidateSchedule_ShouldValidateCron(string scheduleString)
    {
        ScheduleExpression? schedule = ScheduleExpression.FromString(scheduleString);
        Assert.That(schedule, Is.Not.Null);
    
        return schedule.ToCronString();
    }
}