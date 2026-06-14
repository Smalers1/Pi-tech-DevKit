using System.Runtime.CompilerServices;

// InternalsVisibleTo keeps the fixture export / dependency-declaration plumbing OUT of the package's
// public API surface (Proof B baseline) while letting the EditMode net (Proof A/C fixture tests) reach
// FixtureDependencies + the FixtureCorpus parametrization source. Mirrors the ContentDelivery /
// AgentSubstrate pattern (their AssemblyInfo.cs).
[assembly: InternalsVisibleTo("Pitech.XR.Scenario.Editor.Tests")]
