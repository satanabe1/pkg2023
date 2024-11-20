using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityEditor;
using UnityEngine;

public class AssemblyDependencyVisualizerWindow : EditorWindow
{
    [MenuItem("Tools/Assembly Dependency Visualizer")]
    public static void Open() => GetWindow<AssemblyDependencyVisualizerWindow>();

    [SerializeField] private string _inputRegex = ".*";
    [SerializeField] private string _inputExcludeRegex = "";
    [SerializeField] private string _outputRegex = ".*";
    [SerializeField] private string _outputExcludeRegex = "";

    private void OnGUI()
    {
        _inputRegex = EditorGUILayout.TextField("Input Regex", _inputRegex);
        _inputExcludeRegex = EditorGUILayout.TextField("Input Exclude Regex", _inputExcludeRegex);
        _outputRegex = EditorGUILayout.TextField("Output Regex", _outputRegex);
        _outputExcludeRegex = EditorGUILayout.TextField("Output Exclude Regex", _outputExcludeRegex);
        if (GUILayout.Button("Do"))
        {
            var sb = new StringBuilder();
            sb.AppendLine("```mermaid");
            sb.AppendLine(ToMermaidFlowChart());
            sb.AppendLine("```");
            var result = sb.ToString();
            Debug.Log(result);
            GUIUtility.systemCopyBuffer = result;
        }
    }

    private string ToMermaidFlowChart()
    {
        var inputRegex = string.IsNullOrWhiteSpace(_inputRegex) ? null : new Regex(_inputRegex);
        var inputExcludeRegex = string.IsNullOrWhiteSpace(_inputExcludeRegex) ? null : new Regex(_inputExcludeRegex);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(x => inputRegex == null || inputRegex.IsMatch(x.GetName().Name))
            .Where(x => inputExcludeRegex == null || !inputExcludeRegex.IsMatch(x.GetName().Name));
        var modules = ListModuleSymbols(assemblies)
            .SelectMany(x => x.ReferencedAssemblySymbols.SelectMany(y => y.Modules));

        var sb = new StringBuilder();
        sb.AppendLine("graph LR;");
        var outputRegex = string.IsNullOrWhiteSpace(_outputRegex) ? null : new Regex(_outputRegex);
        var outputExcludeRegex = string.IsNullOrWhiteSpace(_outputExcludeRegex) ? null : new Regex(_outputExcludeRegex);
        foreach (var module in modules)
        {
            if (outputRegex != null && !outputRegex.IsMatch(module.Name)) continue;
            if (outputExcludeRegex != null && outputExcludeRegex.IsMatch(module.Name)) continue;
            var moduleName = GetModuleName(module);
            foreach (var referencedModule in ListReferencedModules(module))
            {
                var referencedModuleName = GetModuleName(referencedModule);
                if (referencedModuleName == "<Missing Module>") continue;
                sb.AppendLine($"  {moduleName} --> {referencedModuleName};");
            }
        }

        return sb.ToString();
    }

    private static string GetModuleName(IModuleSymbol module)
    {
        var n = module.Name;
        if (n.EndsWith(".dll")) n = n[..^4];
        return n;
    }

    private static IEnumerable<IModuleSymbol> ListReferencedModules(IModuleSymbol module)
    {
        return module.ReferencedAssemblySymbols.SelectMany(x => x.Modules);
    }

    private static IEnumerable<IModuleSymbol> ListModuleSymbols(IEnumerable<Assembly> assemblies)
    {
        var refs = assemblies
            .Where(x => !x.IsDynamic && !string.IsNullOrEmpty(x.Location))
            .Select(x => (asm: x, metadata: MetadataReference.CreateFromFile(x.Location)))
            .ToDictionary(x => x.asm.GetName().Name, x => x);
        var compilation = CSharpCompilation
            .Create("tmp-compilation", references: refs.Values.Select(x => x.metadata),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.Assembly.Modules;
    }
}