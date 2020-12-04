using marraia.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Rendering;
using System.CommandLine.Rendering.Views;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace marraia
{
    class Program
    {
        private static InvocationContext invocationContext;
        private static ConsoleRenderer consoleRenderer;

        static void Main(InvocationContext invocationContext, string[] args = null)
        {
            Program.invocationContext = invocationContext;
            consoleRenderer = new ConsoleRenderer(
              invocationContext.Console,
              mode: invocationContext.BindingContext.OutputMode(),
              resetAfterRender: true
            );

            var repositories = GetProjectsForGitHub().Result;

            var cmd = new RootCommand();
            cmd.AddCommand(NewProject(repositories));

            cmd.InvokeAsync(args).Wait();
        }

        private static Command NewProject(List<Projects> repositories)
        {
            var cmd = new Command("new", "Comando para criar um novo projeto");

            foreach (var command in repositories)
            {
                var repositoryCommad = new Command(command.Description, $"Comando para criar um projeto à partir do template {command.Description}");
                repositoryCommad.AddOption(new Option(new[] { "--name", "-n" }, "Nome da solução")
                {
                    Argument = new Argument<string>
                    {
                        Arity = ArgumentArity.ExactlyOne
                    }
                });

                repositoryCommad.Handler = CommandHandler.Create<string>((name) => {
                    if (string.IsNullOrEmpty(name))
                    {
                        Console.WriteLine("Usage: new [template] [options]");
                        Console.WriteLine("\n");
                        Console.WriteLine("Options:");
                        Console.WriteLine("-n, --name <NomeDoSeuProjeto>");
                        return;
                    }

                    CreateProject(name, command.Url);
                });

                cmd.Add(repositoryCommad);
            }

            cmd.Handler = CommandHandler.Create(() =>
            {
                var table = new TableView<Projects>
                {
                    Items = repositories
                };

                Console.WriteLine("\n");
                Console.WriteLine("Usage: new [template]");
                Console.WriteLine("\n");

                table.AddColumn(template => template.Description, "Template");

                var screen = new ScreenView(consoleRenderer, invocationContext.Console) { Child = table };
                screen.Render();

                Console.WriteLine("-----");
                Console.WriteLine("\n");
                Console.WriteLine("Exemples:");
                Console.WriteLine($"  ConsoleApp1 new { repositories[0].Description } --name NomeDoSeuProjeto");
            });

            return cmd;
        }

        private static void CreateProject(string project, string url)
        {
            using (var powershell = PowerShell.Create())
            {
                powershell.AddScript(@"cd C:\_test");
                powershell.AddScript($"mkdir {project}");
                powershell.AddScript($"cd {project}");
                powershell.AddScript($"git clone {url}");
                var results = powershell.Invoke();
            }

            Console.WriteLine($"Projeto {project} criado com sucesso!");

            ChangeContentFileNameForProject($"c:\\_test\\{project}", project);
        }

        private static async Task<List<Projects>> GetProjectsForGitHub()
        {
            var nameRepositories = new List<Projects>();
            var client = new HttpClient();
                       
            ProductHeaderValue header = new ProductHeaderValue("marraia", "marraia");
            ProductInfoHeaderValue userAgent = new ProductInfoHeaderValue(header);
            client.DefaultRequestHeaders.UserAgent.Add(userAgent);

            var response = await client
                                    .GetAsync("https://api.github.com/users/marraia/repos");

            if (response.IsSuccessStatusCode)
            {
                var projects = await response
                                        .Content
                                        .ReadAsStringAsync()
                                        .ConfigureAwait(false);

                dynamic repositories = JsonConvert.DeserializeObject(projects);

                foreach (var repository in repositories)
                {
                    nameRepositories.Add(new Projects()
                    {
                        Description = repository.name.ToString(),
                        Url = repository.git_url.ToString()
                    });
                }
            }

            return nameRepositories;
        }

        private static void ChangeFileNameForProject(string path, string project)
        {
            var files = Directory.GetFiles(path, "AcademiaDemo");
            foreach (var item in files)
            {
                var fileInfo = new FileInfo(item);
                fileInfo.MoveTo($"{path}{project}");
            }
        }

        private static void ChangeContentFileNameForProject(string path, string project)
        {
            var files = Directory.EnumerateFiles(path, "*.cs*", SearchOption.AllDirectories);

            foreach (var item in files)
            {
                Console.WriteLine(item);
                var contentFile = File.ReadAllText(item);
                contentFile = contentFile.Replace("AcademiaDemo", project);
                File.WriteAllText(item, contentFile);
            }
        }
    }
}
