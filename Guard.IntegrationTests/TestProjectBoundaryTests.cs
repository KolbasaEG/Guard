using System.Xml.Linq;

internal static class TestProjectBoundaryTests
{
  public static void Run(Action<bool, string> check)
  {
    // Like the other runners, execute from the repository root.
    var expected = new Dictionary<string, string[]> {
      ["Guard.Domain"] = [],
      ["Guard.Application"] = ["Guard.Domain"],
      ["Guard.Infrastructure"] = ["Guard.Domain", "Guard.Application"],
      ["Guard"] = ["Guard.Domain", "Guard.Application", "Guard.Infrastructure"],
      ["Guard.UnitTests"] = ["Guard.Domain", "Guard.Application"],
      ["Guard.IntegrationTests"] = ["Guard.Domain", "Guard.Application", "Guard.Infrastructure", "Guard"]
    };
    var solutionPaths = XDocument.Load("Guard.slnx").Descendants("Project")
      .Select(p => Path.GetFullPath((string)p.Attribute("Path")!)).ToArray();
    var expectedPaths = expected.Keys.Select(name => Path.GetFullPath($"{name}/{name}.csproj")).ToHashSet(StringComparer.OrdinalIgnoreCase);
    check(solutionPaths.Length == 6 && expectedPaths.SetEquals(solutionPaths),
      "solution contains exactly four production and two test projects");
    foreach (var (name, dependencies) in expected) {
      var path = Path.GetFullPath($"{name}/{name}.csproj");
      var project = XDocument.Load(path);
      var references = project.Descendants("ProjectReference")
        .Select(p => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, (string)p.Attribute("Include")!))).ToArray();
      var allowed = dependencies.Select(d => Path.GetFullPath($"{d}/{d}.csproj")).ToHashSet(StringComparer.OrdinalIgnoreCase);
      check(references.Length == dependencies.Length && allowed.SetEquals(references),
        $"{name} keeps its permitted project dependencies without test-to-test or production-to-test links");
      if (name is "Guard.Domain" or "Guard.Application" or "Guard.UnitTests")
        check(!project.Descendants("PackageReference").Any() && !project.Descendants("FrameworkReference").Any(),
          $"{name} has no external packages or framework references");
    }
  }
}
