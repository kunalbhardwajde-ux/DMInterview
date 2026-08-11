using LmsTracker.Api.Domain;

namespace LmsTracker.Api.Infrastructure;

public static class LocalCourseCatalog
{
    public static IReadOnlyList<Course> BuildSeedCourses()
    {
        var items = new (string ExternalId, string Title, string Url)[]
        {
            ("local-udemy-001", "C# for Beginners: Learn C# Fundamentals", "https://www.udemy.com/course/csharp-tutorial-for-beginners/"),
            ("local-udemy-002", "ASP.NET Core Web API Bootcamp", "https://www.udemy.com/course/aspnet-core-webapi/"),
            ("local-udemy-003", "ASP.NET Core MVC From Scratch", "https://www.udemy.com/course/asp-net-core-mvc-updated/"),
            ("local-udemy-004", "Entity Framework Core Deep Dive", "https://www.udemy.com/course/entity-framework-core-a-full-tour/"),
            ("local-udemy-005", "LINQ in C# - Master Querying", "https://www.udemy.com/course/csharp-advanced/"),
            ("local-udemy-006", "Clean Architecture in .NET", "https://www.udemy.com/course/clean-architecture-aspnet-core/"),
            ("local-udemy-007", "Domain-Driven Design in Practice", "https://www.udemy.com/course/domain-driven-design-in-practice/"),
            ("local-udemy-008", "Microservices with .NET", "https://www.udemy.com/course/microservices-with-aspnet-core/"),
            ("local-udemy-009", "Docker for .NET Developers", "https://www.udemy.com/course/docker-kubernetes-the-practical-guide/"),
            ("local-udemy-010", "Kubernetes Hands-On for Beginners", "https://www.udemy.com/course/learn-kubernetes/"),
            ("local-udemy-011", "SQL Server for Developers", "https://www.udemy.com/course/microsoft-sql-server-for-beginners/"),
            ("local-udemy-012", "T-SQL Querying Essentials", "https://www.udemy.com/course/t-sql-for-data-analysis/"),
            ("local-udemy-013", "Azure Fundamentals for Engineers", "https://www.udemy.com/course/az-900-microsoft-azure-fundamentals-exam-prep/"),
            ("local-udemy-014", "Azure DevOps Pipelines End to End", "https://www.udemy.com/course/azure-devops-pipelines/"),
            ("local-udemy-015", "Git and GitHub Complete Guide", "https://www.udemy.com/course/git-and-github-bootcamp/"),
            ("local-udemy-016", "REST API Design Best Practices", "https://www.udemy.com/course/rest-api-design-the-complete-guide/"),
            ("local-udemy-017", "gRPC in .NET", "https://www.udemy.com/course/grpc-dotnet/"),
            ("local-udemy-018", "Message Queues with RabbitMQ", "https://www.udemy.com/course/rabbitmq-complete-guide/"),
            ("local-udemy-019", "Redis Caching for High Performance APIs", "https://www.udemy.com/course/redis-the-complete-developers-guide-p/"),
            ("local-udemy-020", "JWT Authentication and Authorization", "https://www.udemy.com/course/jwt-authentication-and-authorization/"),
            ("local-udemy-021", "OAuth 2.0 and OpenID Connect", "https://www.udemy.com/course/oauth-2-simplified/"),
            ("local-udemy-022", "Unit Testing in .NET with xUnit", "https://www.udemy.com/course/unit-testing-in-csharp/"),
            ("local-udemy-023", "Integration Testing for ASP.NET Core", "https://www.udemy.com/course/aspnet-core-integration-testing/"),
            ("local-udemy-024", "Test Driven Development in C#", "https://www.udemy.com/course/tdd-csharp/"),
            ("local-udemy-025", "Design Patterns in C#", "https://www.udemy.com/course/design-patterns-csharp-dotnet/"),
            ("local-udemy-026", "SOLID Principles in Real Projects", "https://www.udemy.com/course/solid-principles/"),
            ("local-udemy-027", "System Design Interview Fundamentals", "https://www.udemy.com/course/system-design-interview-guide/"),
            ("local-udemy-028", "Scalable Backend Design", "https://www.udemy.com/course/scalable-web-application-design/"),
            ("local-udemy-029", "React - The Complete Guide", "https://www.udemy.com/course/react-the-complete-guide-incl-redux/"),
            ("local-udemy-030", "Advanced React Patterns", "https://www.udemy.com/course/advanced-react-and-redux/"),
            ("local-udemy-031", "TypeScript for Professionals", "https://www.udemy.com/course/understanding-typescript/"),
            ("local-udemy-032", "JavaScript: From Zero to Expert", "https://www.udemy.com/course/the-complete-javascript-course/"),
            ("local-udemy-033", "Node.js API Development Masterclass", "https://www.udemy.com/course/nodejs-express-mongodb-bootcamp/"),
            ("local-udemy-034", "Express.js in Depth", "https://www.udemy.com/course/express-js/"),
            ("local-udemy-035", "MongoDB for Developers", "https://www.udemy.com/course/mongodb-the-complete-developers-guide/"),
            ("local-udemy-036", "PostgreSQL Database Essentials", "https://www.udemy.com/course/sql-and-postgresql/"),
            ("local-udemy-037", "Python for Data Engineering", "https://www.udemy.com/course/python-for-data-science-and-machine-learning-bootcamp/"),
            ("local-udemy-038", "Pandas and NumPy Practical", "https://www.udemy.com/course/data-analysis-with-pandas/"),
            ("local-udemy-039", "Machine Learning A-Z", "https://www.udemy.com/course/machinelearning/"),
            ("local-udemy-040", "Deep Learning with TensorFlow", "https://www.udemy.com/course/tensorflow-developer-certificate-machine-learning-zero-to-mastery/"),
            ("local-udemy-041", "Prompt Engineering for Developers", "https://www.udemy.com/course/prompt-engineering/"),
            ("local-udemy-042", "LLM Application Development", "https://www.udemy.com/course/llm-engineering/"),
            ("local-udemy-043", "CI/CD Pipelines Practical", "https://www.udemy.com/course/learn-devops-ci-cd-with-jenkins/"),
            ("local-udemy-044", "Terraform for Cloud Infrastructure", "https://www.udemy.com/course/terraform-beginner-to-advanced/"),
            ("local-udemy-045", "Linux Command Line for Engineers", "https://www.udemy.com/course/linux-mastery/"),
            ("local-udemy-046", "Bash Scripting Automation", "https://www.udemy.com/course/bash-scripting/"),
            ("local-udemy-047", "Nginx and Reverse Proxy Essentials", "https://www.udemy.com/course/nginx-fundamentals/"),
            ("local-udemy-048", "Observability with Prometheus and Grafana", "https://www.udemy.com/course/prometheus/"),
            ("local-udemy-049", "Security Fundamentals for Web Apps", "https://www.udemy.com/course/web-security-fundamentals/"),
            ("local-udemy-050", "OWASP Top 10 for Developers", "https://www.udemy.com/course/owasp-top-10/"),
            ("local-udemy-051", "Product Management for Engineers", "https://www.udemy.com/course/become-a-product-manager-learn-the-skills-get-a-job/"),
            ("local-udemy-052", "Agile and Scrum for Software Teams", "https://www.udemy.com/course/scrum-certification/"),
            ("local-udemy-053", "Communication Skills for Tech Professionals", "https://www.udemy.com/course/communication-skills-for-professionals/"),
            ("local-udemy-054", "Technical Writing for Developers", "https://www.udemy.com/course/technical-writing-how-to-write-software-documentation/"),
            ("local-udemy-055", "Leadership Essentials for New Managers", "https://www.udemy.com/course/new-manager-training/"),
        };

        return items.Select(item => new Course
        {
            Id = Guid.NewGuid(),
            ExternalCourseId = item.ExternalId,
            Title = item.Title,
            Provider = "Udemy",
            LaunchUrl = item.Url
        }).ToList();
    }
}
