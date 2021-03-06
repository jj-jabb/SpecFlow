﻿using System.CodeDom;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using NUnit.Framework;
using TechTalk.SpecFlow.Generator;
using TechTalk.SpecFlow.Generator.Configuration;
using TechTalk.SpecFlow.Generator.UnitTestProvider;
using TechTalk.SpecFlow.Parser;
using TechTalk.SpecFlow.Parser.SyntaxElements;
using TechTalk.SpecFlow.Utils;

namespace GeneratorTests
{
    /// <summary>
    /// A test for testing cusomterized test generator provider
    /// </summary>
    [TestFixture]
    public class CustomTestGeneratorProviderTest
    {
        private const string SampleFeatureFile = @"
            Feature: Sample feature file for a custom generator provider
            
            Scenario: Simple scenario
				Given there is something
				When I do something
				Then something should happen

            @mytag
			Scenario Outline: Simple Scenario Outline
				Given there is something
                    """"""
                      long string
                    """"""
				When I do <what>
                    | foo | bar |
                    | 1   | 2   |
				Then something should happen
			Examples: 
				| what           |
				| something      |
				| somethign else |
";

        public static SpecFlowUnitTestConverter CreateUnitTestConverter(IUnitTestGeneratorProvider testGeneratorProvider)
        {
            var codeDomHelper = new CodeDomHelper(CodeDomProviderLanguage.CSharp);
            return new SpecFlowUnitTestConverter(testGeneratorProvider, codeDomHelper,
                                                 new GeneratorConfiguration { AllowRowTests = true, AllowDebugGeneratedFiles = true });
        }

        /// <summary>
        /// Generates the scenario example tests.
        /// </summary>
        [Test]
        public void GenerateScenarioExampleTests()
        {
            SpecFlowLangParser parser = new SpecFlowLangParser(new CultureInfo("en-US"));
            using (var reader = new StringReader(SampleFeatureFile))
            {
                Feature feature = parser.Parse(reader, null);                    
                Assert.IsNotNull(feature);

                var sampleTestGeneratorProvider = new SimpleTestGeneratorProvider();
                var converter = CreateUnitTestConverter(sampleTestGeneratorProvider);
                CodeNamespace code = converter.GenerateUnitTestFixture(feature, "TestClassName", "Target.Namespace");

                Assert.IsNotNull(code);
                  
                // make sure name space is changed
                Assert.AreEqual(code.Name, SimpleTestGeneratorProvider.DefaultNameSpace);

                // make sure all method titles are changed correctly
                List<string> methodTitles = new List<string>();
                for (int i = 0; i < code.Types[0].Members.Count; i++)
                {
                    methodTitles.Add(code.Types[0].Members[i].Name);
                }

                foreach (var title in sampleTestGeneratorProvider.newTitles)
                {
                    Assert.IsTrue(methodTitles.Contains(title));
                }
            }
        }

        /// <summary>
        /// This class will change the default name space of a geneated test code, and the method name for tests corresponds to a scenario example
        /// </summary>
        class SimpleTestGeneratorProvider : MsTestGeneratorProvider
        {
            public static string DefaultNameSpace
            {
                get
                {
                    return "SampleTestGeneratorProvider";
                }
            }

            public override void FinalizeTestClass(TestClassGenerationContext generationContext)
            {
                base.FinalizeTestClass(generationContext);
                // change namespace 
                generationContext.Namespace.Name = DefaultNameSpace;
            }

            public override void SetTestMethodAsRow(TestClassGenerationContext generationContext, CodeMemberMethod testMethod, string scenarioTitle, string exampleSetName, string variantName, IEnumerable<KeyValuePair<string, string>> arguments)
            {
                base.SetTestMethodAsRow(generationContext, testMethod, scenarioTitle, exampleSetName, variantName, arguments);

                // change memberMethodName
                testMethod.Name = GetMethodName(scenarioTitle, exampleSetName, arguments);
                newTitles.Add(testMethod.Name);
            }

            public List<string> newTitles = new List<string>();

            /// <summary>
            /// Gets the name of the method by concat the name and value of arugment together
            /// </summary>
            /// <param name="title">The title.</param>
            /// <param name="exampleSetTitle">The example set title.</param>
            /// <param name="arguments">The arguments.</param>
            /// <returns></returns>
            private static string GetMethodName(string title, string exampleSetTitle, IEnumerable<KeyValuePair<string, string>> arguments)
            {
                string testMethodName = string.IsNullOrEmpty(exampleSetTitle)
               ? string.Format("{0}", title)
               : string.Format("{0}_{1}", title, exampleSetTitle);

                testMethodName += string.Concat(arguments.Select(kp => "_" + kp.Key + "_" + kp.Value).ToArray());
                IEnumerable<char> chars = from ch in testMethodName
                                          where (char.IsLetter(ch) || char.IsNumber(ch) || ch == '_') && ch != ' '
                                          select ch;
                testMethodName = new string(chars.ToArray());
                return testMethodName;
            }
        }
    }
}
