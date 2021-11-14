using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;

namespace JpegDecode
{
    class Program
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
            command.Description = "Decode image from JPEG file.";

            command.AddOption(Output());

            command.AddArgument(new Argument<FileInfo>()
            {
                Name = "source",
                Description = "The JPEG file to decode.",
                Arity = ArgumentArity.ExactlyOne
            }.ExistingOnly());


            command.Handler = CommandHandler.Create<FileInfo, string>(DecodeAction.Decode);

            static Option Output() =>
                new Option<string>(new[] { "--output", "--out", "-o" }, "Output image file.")
                {
                    Name = "output",
                    Arity = ArgumentArity.ZeroOrOne
                };
        }
    }
}
