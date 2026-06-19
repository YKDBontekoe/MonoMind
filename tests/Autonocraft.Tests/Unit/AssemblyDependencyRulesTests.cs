using System.Reflection;

namespace Autonocraft.Tests.Unit;

/// <summary>
/// Encodes the target assembly dependency graph from the multi-assembly codemap refactor.
/// Update <see cref="AllowedReferences"/> when adding or splitting projects.
/// </summary>
public sealed class AssemblyDependencyRulesTests
{
    private static readonly Dictionary<string, string[]> AllowedReferences = new(StringComparer.Ordinal)
    {
        ["Autonocraft.Domain"] = [],
        ["Autonocraft.Diagnostics"] = ["Autonocraft.Domain"],
        ["Autonocraft.Items"] = ["Autonocraft.Domain"],
        ["Autonocraft.World"] = ["Autonocraft.Domain", "Autonocraft.Diagnostics", "Autonocraft.Items"],
        ["Autonocraft.Crafting"] = ["Autonocraft.Domain", "Autonocraft.Items", "Autonocraft.World"],
        ["Autonocraft.Entities"] = ["Autonocraft.Domain", "Autonocraft.World"],
        ["Autonocraft.Village"] = ["Autonocraft.Domain", "Autonocraft.Items", "Autonocraft.World", "Autonocraft.Entities", "Autonocraft.Crafting"],
        ["Autonocraft.Ai"] = ["Autonocraft.Domain"],
        ["Autonocraft.Engine"] = ["Autonocraft.Domain", "Autonocraft.Diagnostics", "Autonocraft.Items", "Autonocraft.World", "Autonocraft.Entities", "Autonocraft.Village"],
        ["Autonocraft.Core"] = ["Autonocraft.Domain", "Autonocraft.Diagnostics", "Autonocraft.Items", "Autonocraft.World", "Autonocraft.Entities", "Autonocraft.Village", "Autonocraft.Crafting", "Autonocraft.Ai", "Autonocraft.Engine"],
        ["Autonocraft"] = ["Autonocraft.Domain", "Autonocraft.Diagnostics", "Autonocraft.Items", "Autonocraft.World", "Autonocraft.Entities", "Autonocraft.Village", "Autonocraft.Crafting", "Autonocraft.Ai", "Autonocraft.Core", "Autonocraft.Engine"],
        ["Autonocraft.Tests"] = ["Autonocraft.Domain", "Autonocraft.Diagnostics", "Autonocraft.Items", "Autonocraft.World", "Autonocraft.Entities", "Autonocraft.Village", "Autonocraft.Crafting", "Autonocraft.Ai", "Autonocraft.Core", "Autonocraft.Engine", "Autonocraft"],
    };

    [Fact]
    public void GameAssemblies_OnlyReferenceAllowedDependencies()
    {
        var gameAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Where(a =>
            {
                var name = a.GetName().Name;
                return name != null && (name.StartsWith("Autonocraft", StringComparison.Ordinal) || name == "Autonocraft.Tests");
            })
            .ToDictionary(a => a.GetName().Name!, a => a, StringComparer.Ordinal);

        foreach (var (assemblyName, assembly) in gameAssemblies)
        {
            if (!AllowedReferences.TryGetValue(assemblyName, out var allowed))
            {
                continue;
            }

            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            var referenced = assembly.GetReferencedAssemblies()
                .Select(r => r.Name)
                .Where(n => n != null && n.StartsWith("Autonocraft", StringComparison.Ordinal))
                .Select(n => n!)
                .ToList();

            var violations = referenced.Where(r => !allowedSet.Contains(r)).ToList();
            Assert.True(
                violations.Count == 0,
                $"{assemblyName} has disallowed references: {string.Join(", ", violations)}. Allowed: {string.Join(", ", allowed)}");
        }
    }

    [Fact]
    public void Domain_HasNoGameAssemblyReferences()
    {
        var domain = typeof(Autonocraft.Domain.World.BlockType).Assembly;
        var gameRefs = domain.GetReferencedAssemblies()
            .Select(r => r.Name)
            .Where(n => n != null && n.StartsWith("Autonocraft", StringComparison.Ordinal) && n != "Autonocraft.Domain")
            .ToList();

        Assert.Empty(gameRefs);
    }
}
