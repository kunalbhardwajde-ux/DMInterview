using LmsTracker.Api.Infrastructure;

namespace LmsTracker.Api.Tests;

public sealed class LocalCourseCatalogTests
{
    [Fact]
    public void BuildSeedCourses_ReturnsExpectedUdemyCatalog()
    {
        var courses = LocalCourseCatalog.BuildSeedCourses();

        Assert.NotNull(courses);
        Assert.True(courses.Count >= 55);
        Assert.All(courses, c =>
        {
            Assert.Equal("Udemy", c.Provider);
            Assert.False(string.IsNullOrWhiteSpace(c.Title));
            Assert.False(string.IsNullOrWhiteSpace(c.ExternalCourseId));
            Assert.False(string.IsNullOrWhiteSpace(c.LaunchUrl));
            Assert.NotEqual(Guid.Empty, c.Id);
        });

        var duplicateExternalIds = courses
            .GroupBy(c => c.ExternalCourseId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(duplicateExternalIds);
    }
}
