using HexFund.UI.Services.Http;

namespace HexFund.UI.Tests;

public class ProblemDetailsParserTests
{
    [Fact]
    public void Parses_a_validation_problem_with_field_errors_and_correlation_id()
    {
        const string body = """
            {"title":"One or more validation errors occurred.","status":400,
             "errors":{"Body":["Body is too short."],"Type":["Invalid type.","Second"]},
             "correlationId":"b1c7aaaa-1111"}
            """;

        var problem = ProblemDetailsParser.Parse(body);

        Assert.NotNull(problem);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Equal(400, problem.Status);
        Assert.Equal("b1c7aaaa-1111", problem.CorrelationId);
        Assert.Equal("Body is too short.", problem.FirstError("body"));
        Assert.Equal(new[] { "Invalid type.", "Second" }, problem.Errors["Type"]);
    }

    [Fact]
    public void FirstError_returns_null_for_an_unknown_field()
    {
        var problem = ProblemDetailsParser.Parse("""{"errors":{"Body":["x"]}}""");

        Assert.Null(problem!.FirstError("missing"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_body_gives_null(string? body) =>
        Assert.Null(ProblemDetailsParser.Parse(body));

    [Theory]
    [InlineData("<html>Bad gateway</html>")]
    [InlineData("{not json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    [InlineData("42")]
    public void Malformed_or_non_object_body_gives_null_and_never_throws(string body) =>
        Assert.Null(ProblemDetailsParser.Parse(body));

    [Fact]
    public void Wrong_value_types_are_ignored()
    {
        var problem = ProblemDetailsParser.Parse(
            """{"title":5,"status":"400","errors":{"Body":"single message","Other":[1,2]},"correlationId":null}""");

        Assert.NotNull(problem);
        Assert.Null(problem.Title);
        Assert.Null(problem.Status);
        Assert.Null(problem.CorrelationId);
        Assert.Equal("single message", problem.FirstError("Body"));
        Assert.Empty(problem.Errors["Other"]);
    }

    [Fact]
    public void Errors_that_is_not_an_object_gives_no_errors()
    {
        var problem = ProblemDetailsParser.Parse("""{"errors":["a"]}""");

        Assert.NotNull(problem);
        Assert.Empty(problem.Errors);
    }
}
