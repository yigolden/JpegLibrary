using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace JpegDebugDump
{
    internal static class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new CommandLineBuilder();

            SetupDebugDumpCommand(builder.Command);

            builder.UseDefaults();

            Parser parser = builder.Build();
            await parser.InvokeAsync(args);
        }

        static void SetupDebugDumpCommand(Command command)
        {
            command.Description = "Dump decoded JPEG image components.";

            command.AddOption(Output());

            command.AddArgument(new Argument<FileInfo>()
            {
                Name = "source",
                Description = "The JPEG file to dump.",
                Arity = ArgumentArity.ExactlyOne
            }.ExistingOnly());


            command.Handler = CommandHandler.Create<FileInfo, string, CancellationToken>(DebugDumpAction.DebugDump);

            static Option Output() =>
                new Option<string>(new[] { "--output", "--out", "-o" }, "Output file base path.")
                {
                    Name = "output",
                    Arity = ArgumentArity.ZeroOrOne
                };

        }
    }

}
