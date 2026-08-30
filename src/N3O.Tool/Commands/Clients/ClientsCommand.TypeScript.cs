using Microsoft.Extensions.Logging;
using N3O.Tool.Utilities;
using NJsonSchema.CodeGeneration.TypeScript;
using NSwag.CodeGeneration;
using NSwag.CodeGeneration.TypeScript;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace N3O.Tool.Commands.Clients;

public partial class ClientsCommand {
    private async Task GenerateTypeScriptClientAsync(bool connectApi) {
        var openApiDocument = await GetOpenApiDocumentAsync("typescript");

        var srcFolder = System.IO.Path.Combine(OutputPath, "src");
        Directory.CreateDirectory(srcFolder);

        var settings = new TypeScriptClientGeneratorSettings();

        settings.ImportRequiredTypes = true;
        
        settings.TypeScriptGeneratorSettings.ExcludedTypeNames = ExcludeModels?.Split('|') ?? [];
        settings.TypeScriptGeneratorSettings.ExportTypes = true;
        settings.TypeScriptGeneratorSettings.TypeStyle = TypeScriptTypeStyle.Interface;
        
        if (connectApi) {
            settings.ClientBaseClass = "ConnectApiBase";
            settings.ConfigurationClass = "IApiHeaders";
            settings.UseTransformOptionsMethod = true;
            
            settings.TypeScriptGeneratorSettings.ExtensionCode = EmbeddedResource.Text("ConnectApiBase.ts");
        } else {
            settings.ConfigurationClass = "IApiConfiguration";
            settings.UseTransformOptionsMethod = false;
        }

        var generator = new TypeScriptClientGenerator(openApiDocument, settings);

        var tsMain = generator.GenerateFile(ClientGeneratorOutputType.Full);
        
        await File.WriteAllTextAsync(System.IO.Path.Combine(srcFolder, "index.ts"), tsMain);

        GenerateNpmPackage();
    }

    public void GenerateNpmPackage() {
        GeneratePackageJson();
        GenerateTsConfig();
        File.WriteAllText(System.IO.Path.Combine(OutputPath, "README.md"), PackageDescription);

        RunNpm("install");
        RunNpm("run build");

        var nodeModules = System.IO.Path.Combine(OutputPath, "node_modules");

        if (Directory.Exists(nodeModules)) {
            Directory.Delete(nodeModules, true);
        }
    }

    private void GeneratePackageJson() {
        var packageJson = Json.Deserialize<PackageJson>(EmbeddedResource.Text("package.json"));

        packageJson.Name = PackageName;
        packageJson.Description = PackageDescription;

        var outputFile = System.IO.Path.Combine(OutputPath, "package.json");
        var outputContent = Json.Serialize(packageJson);

        _logger.LogDebug($"Wrote the following to {outputFile}");
        _logger.LogDebug(outputContent);

        File.WriteAllText(outputFile, outputContent);
    }

    private void GenerateTsConfig() {
        var outputFile = System.IO.Path.Combine(OutputPath, "tsconfig.json");
        var outputContent = EmbeddedResource.Text("tsconfig.json");

        _logger.LogDebug($"Wrote the following to {outputFile}");
        _logger.LogDebug(outputContent);

        File.WriteAllText(outputFile, outputContent);
    }

    private void RunNpm(string args) {
        var npm = Host.IsWindows ? "npm.cmd" : "npm";
        var output = new List<string>();

        var process = _shell.Run(npm, args, x => output.Add(x), workingDirectory: OutputPath);

        process.WaitForExit();

        if (process.ExitCode != 0) {
            var detail = string.Join(Environment.NewLine, output.Where(x => !string.IsNullOrWhiteSpace(x)));

            throw new Exception($"{npm} {args} exited with code {process.ExitCode}{Environment.NewLine}{detail}");
        }
    }
}