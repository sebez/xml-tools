using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Sebez.XmlTools.TrxToPlaylist
{
    public class Transformer
    {
        private static readonly Regex FullClassNamePattern = new Regex(@"(.*)\.Test\.(.*)\.(.*)");
        private Config _config;
        private XDocument _doc;
        private TestSuite _suite;

        public void Start(Config config)
        {
            _config = config;

            /* Load TRX */
            LoadTrx();

            /* Extract Data */
            ExtractData();

            /* Generate playlist */
            GeneratePlayList();
        }

        private void LoadTrx()
        {
            if (!File.Exists(_config.TrxPath))
            {
                throw new FileNotFoundException(_config.TrxPath);
            }

            Console.WriteLine($"Chargement de {_config.TrxPath}");

            _doc = XDocument.Load(_config.TrxPath);
        }



        private void ExtractData()
        {
            var xnm = new XmlNamespaceManager(new NameTable());
            xnm.AddNamespace("x", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010");

            /* Extrait les résultats. */
            Console.WriteLine($"Résultats...");
            var results = _doc.XPathSelectElements("/x:TestRun/x:Results/x:UnitTestResult", xnm);
            ResultMap map = new ResultMap();
            foreach (var result in results)
            {
                Console.WriteLine($"{result.Attribute("testName").Value} : {result.Attribute("outcome").Value}");

                map[result.Attribute("testId").Value] = result.Attribute("outcome").Value;
            }

            /* Extrait les tests. */
            Console.WriteLine($"Définitions...");

            var definitions = _doc.XPathSelectElements("/x:TestRun/x:TestDefinitions/x:UnitTest", xnm);

            var raw = definitions.Select(x =>
            {
                XElement testMethod = x.Descendants().Skip(1).First();
                XElement execution = x.Descendants().First();
                string fullClassName = testMethod.Attribute("className").Value;

                var match = FullClassNamePattern.Match(fullClassName);
                var projectName = $"{match.Groups[1].Value}.Test";
                var namespaceName = $"{match.Groups[1].Value}.Test.{match.Groups[2].Value}";
                var simpleClassName = match.Groups[3].Value;

                return new
                {
                    TestId = x.Attribute("id").Value,
                    ProjectName = projectName,
                    Namespace = namespaceName,
                    Classname = simpleClassName,
                    MethodName = x.Attribute("name").Value,
                };
            }).ToList();

            /* Construction de la suite KO */
            Console.WriteLine("Suite KO : ");
            _suite = new TestSuite();
            foreach (var projetRaw in raw.GroupBy(x => x.ProjectName))
            {
                TestProject testProject = new TestProject
                {
                    Name = projetRaw.Key,
                };

                foreach (var namespaceRaw in projetRaw.GroupBy(n => n.Namespace))
                {
                    var testNamespace = new TestNamespace
                    {
                        Name = namespaceRaw.Key
                    };

                    foreach (var classRaw in namespaceRaw.GroupBy(x => x.Classname))
                    {
                        var testClass = new TestClass
                        {
                            Name = classRaw.Key
                        };

                        foreach (var methodRaw in classRaw)
                        {
                            var testMethod = new TestMethod
                            {
                                Name = methodRaw.MethodName,
                                FullName = $"{methodRaw.Namespace}.{methodRaw.Classname}.{methodRaw.MethodName}"
                            };

                            if (map.ContainsKey(methodRaw.TestId) && map[methodRaw.TestId] == "Failed")
                            {
                                Console.WriteLine($"Test {map[methodRaw.TestId]} {testMethod.Name}");
                                testClass.Methods.Add(testMethod);
                            }
                        }

                        if (testClass.Methods.Any())
                        {
                            testNamespace.Classes.Add(testClass);
                        }
                    }

                    if (testNamespace.Classes.Any())
                    {
                        testProject.Namespaces.Add(testNamespace);
                    }
                }

                if (testProject.Namespaces.Any())
                {
                    _suite.Projets.Add(testProject);
                }
            }
        }

        private void GeneratePlayList()
        {
            Console.WriteLine($"Construction de la playlist {_config.PlaylistPath}");
            XDocument playlist = new XDocument(
                new XElement("Playlist",
                    new XAttribute("Version", "2.0"),
                    new XElement("Rule",
                        new XAttribute("Name", "Includes"),
                        new XAttribute("Match", "Any"),
                        new XElement("Rule",
                            new XAttribute("Match", "All"),
                            new XElement("Property",
                                new XAttribute("Name", "Solution")),
                            new XElement("Rule",
                                new XAttribute("Match", "Any"),
                                /* Projets */
                                _suite.Projets.Select(p =>
                                    new XElement("Rule",
                                        new XAttribute("Match", "All"),
                                        new XElement("Property",
                                            new XAttribute("Name", "Project"),
                                            new XAttribute("Value", p.Name)
                                        ),
                                        new XElement("Rule",
                                            new XAttribute("Match", "Any"),
                                            /* Namespaces */
                                            p.Namespaces.Select(n =>
                                                new XElement("Rule",
                                                    new XAttribute("Match", "All"),
                                                        new XElement("Property",
                                                        new XAttribute("Name", "Namespace"),
                                                        new XAttribute("Value", n.Name)
                                                    ),
                                                    new XElement("Rule",
                                                        new XAttribute("Match", "Any"),
                                                        /* Classes */
                                                        n.Classes.Select(c =>
                                                            new XElement("Rule",
                                                                new XAttribute("Match", "All"),
                                                                new XElement("Property",
                                                                    new XAttribute("Name", "Class"),
                                                                    new XAttribute("Value", c.Name)
                                                                ),
                                                                new XElement("Rule",
                                                                    new XAttribute("Match", "Any"),
                                                                    c.Methods.Select(m =>
                                                                        new XElement("Rule",
                                                                            new XAttribute("Match", "All"),
                                                                            new XElement("Property",
                                                                                new XAttribute("Name", "TestWithNormalizedFullyQualifiedName"),
                                                                                new XAttribute("Value", m.FullName)
                                                                            ),
                                                                            new XElement("Rule",
                                                                                new XAttribute("Match", "Any"),
                                                                                new XElement("Property",
                                                                                    new XAttribute("Name", "DisplayName"),
                                                                                    new XAttribute("Value", m.Name)
                                                                                )
                                                                            )
                                                                        )
                                                                    )
                                                                )
                                                            )
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            );
            playlist.Save(_config.PlaylistPath);
        }

        private class TestSuite
        {
            public List<TestProject> Projets { get; set; } = new List<TestProject>();
        }

        private class TestProject
        {
            public string Name { get; set; }
            public List<TestNamespace> Namespaces { get; set; } = new List<TestNamespace>();
        }

        private class TestNamespace
        {
            public string Name { get; set; }
            public List<TestClass> Classes { get; set; } = new List<TestClass>();
        }

        private class TestClass
        {
            public string Name { get; set; }

            public List<TestMethod> Methods { get; set; } = new List<TestMethod>();
        }

        private class TestMethod
        {
            public string Name { get; set; }

            public string FullName { get; set; }
        }

        /// <summary>
        /// Test ID to Result MAP.
        /// </summary>
        private class ResultMap : Dictionary<string, string>
        {
        }
    }
}
