using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Test.Database;

namespace Rasa.Test.Compatibility
{
    /// <summary>
    /// gametools/check-mysql-migrations.ps1 and .sh list a context's migrations by the [Migration("...")] attributes in
    /// its src/Rasa.DBL/Migrations/MySql* folder, and compare them with the database's __EFMigrationsHistory.
    /// </summary>
    [TestClass]
    public class MySqlMigrationCheckTests
    {
        private static readonly Regex Attribute = new("\\[Migration\\(\"([^\"]+)\"\\)\\]");

        /// <summary>What the scripts read from a folder is what EF has for the context: no more, no fewer.</summary>
        [TestMethod]
        public void TheScriptsFindEachMySqlContextsMigrations()
        {
            var root = Root();
            foreach (var (folder, context) in new[]
                     {
                         ("MySqlAuth", typeof(Rasa.Context.Auth.MySqlAuthContext)),
                         ("MySqlChar", typeof(Rasa.Context.Char.MySqlCharContext)),
                         ("MySqlWorld", typeof(Rasa.Context.World.MySqlWorldContext))
                     })
            {
                var found = Directory.GetFiles(Path.Combine(root, "src", "Rasa.DBL", "Migrations", folder), "*.cs")
                    .SelectMany(file => Attribute.Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value))
                    .Distinct().OrderBy(id => id, System.StringComparer.Ordinal).ToList();
                using var database = PersistenceIntegrationTests.CreateContext(context, "unused");
                var known = database.Database.GetMigrations().OrderBy(id => id, System.StringComparer.Ordinal).ToList();
                CollectionAssert.AreEqual(known, found, folder);
            }
        }

        [TestMethod]
        public void BothScriptsAreThereAndGitKeepsTheShellOneAtLf()
        {
            var root = Root();
            var ps1 = File.ReadAllText(Path.Combine(root, "gametools", "check-mysql-migrations.ps1"));
            var sh = File.ReadAllText(Path.Combine(root, "gametools", "check-mysql-migrations.sh"));
            foreach (var script in new[] { ps1, sh })
            {
                StringAssert.Contains(script, "__EFMigrationsHistory");
                StringAssert.Contains(script, "Databases__");
            }
            // The working copy's line ends are left alone: a checkout that brought .gitattributes and the script in one
            // go (git am, a pull) wrote the script before the rule applied. The rule is what every later checkout keeps.
            StringAssert.StartsWith(sh, "#!/usr/bin/env bash");
            Assert.IsTrue(Regex.IsMatch(File.ReadAllText(Path.Combine(root, ".gitattributes")), @"(?m)^\*\.sh\s+text\s+eol=lf\s*$"),
                ".gitattributes keeps *.sh at LF");
            Assert.IsTrue(ps1.All(c => c < 128), "Windows PowerShell 5.1 reads a script without a BOM as ANSI: keep it ASCII.");
        }

        private static string Root()
        {
            var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Rasa.NET.sln")))
                directory = directory.Parent;
            Assert.IsNotNull(directory, "Could not locate the repository root.");
            return directory.FullName;
        }
    }
}
